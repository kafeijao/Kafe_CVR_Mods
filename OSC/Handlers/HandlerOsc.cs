using System.Collections;
using System.Net;
using MelonLoader;
using OSC.Handlers.OscModules;
using Rug.Osc;
using OSC.Utils;

namespace OSC.Handlers;

internal class HandlerOsc {

    private static HandlerOsc Instance;

    private OscReceiver _receiver;
    private OscSender _sender;

    private readonly Avatar AvatarHandler;
    private readonly Input InputHandler;
    private readonly Spawnable SpawnableHandler;
    private readonly Tracking TrackingHandler;
    private readonly Config ConfigHandler;

    private static bool _debugMode;
    private static bool _compatibilityVRCFaceTracking;

    private static readonly Queue<OscPacket> _frameOscPacketsBuffer = new();
    private static bool _useOscBundles;

    private const int MessageBufferSize = 600;
    private const int MaxPacketSize = 2*1048;
    private const int MaxMessagesPerBundle = 10;

    static HandlerOsc() {

        // Handle config debug value and changes
        _debugMode = OSC.Instance.meOSCDebug.Value;
        OSC.Instance.meOSCDebug.OnValueChanged += (_, newValue) => _debugMode = newValue;

        // Setup the late update tick to send the current osc packets buffer as a bundle
        if (OSC.Instance.meOSCServerOutUseBundles.Value) {
            Events.Scene.PlayerSetupLateUpdateTicked += LateUpdateTick;
            _useOscBundles = true;
        }
        OSC.Instance.meOSCServerOutUseBundles.OnValueChanged += (oldUseBundles, newUseBundles) => {
            if (oldUseBundles == newUseBundles) return;
            if (newUseBundles) Events.Scene.PlayerSetupLateUpdateTicked += LateUpdateTick;
            else Events.Scene.PlayerSetupLateUpdateTicked -= LateUpdateTick;
            _useOscBundles = newUseBundles;
        };
    }

    public HandlerOsc() {

        try {
            // Start the osc msg receiver in a Coroutine
            _receiver = new OscReceiver(OSC.Instance.meOSCServerInPort.Value, MessageBufferSize, MaxPacketSize);
            _receiver.Connect();
            MelonCoroutines.Start(OscReceiverHandler());

            MelonLogger.Msg($"[Server] OSC Server started listening on the port {OSC.Instance.meOSCServerInPort.Value}.");
        }
        catch (Exception e) {
            MelonLogger.Error($"Failed initializing OSC receiver Coroutine!.");
            MelonLogger.Error(e);
            throw;
        }

        // Handle config listener port changes
        OSC.Instance.meOSCServerInPort.OnValueChanged += (oldPort, newPort) => {
            if (oldPort == newPort) return;
            MelonLogger.Msg("[Server] OSC server port config has changed. Restarting server...");
            try {
                _receiver?.Close();
                _receiver = new OscReceiver(newPort);
                _receiver.Connect();
                MelonLogger.Msg($"[Server] OSC Server started listening on the port {newPort}.");
            }
            catch (Exception e) {
                MelonLogger.Error(e);
                throw;
            }
        };

        // Start sender
        MelonLogger.Msg($"[Server] OSC Server started sending to {OSC.Instance.meOSCServerOutIp.Value}:{OSC.Instance.meOSCServerOutPort.Value}.");
        ConnectSender(OSC.Instance.meOSCServerOutIp.Value, OSC.Instance.meOSCServerOutPort.Value);

        // Handle config sender ip/port changes
        OSC.Instance.meOSCServerOutIp.OnValueChanged += (_, newIp) => {
            if (!ConnectSender(newIp, OSC.Instance.meOSCServerOutPort.Value)) return;
            MelonLogger.Msg($"[Server] OSC out IP has changed, new messages will be sent to: {OSC.Instance.meOSCServerOutIp.Value}:{OSC.Instance.meOSCServerOutPort.Value}.");
        };
        OSC.Instance.meOSCServerOutPort.OnValueChanged += (_, newPort) => {
            if (!ConnectSender(OSC.Instance.meOSCServerOutIp.Value, newPort)) return;
            MelonLogger.Msg($"[Server] OSC out Port has changed, new messages will be sent to: {OSC.Instance.meOSCServerOutIp.Value}:{OSC.Instance.meOSCServerOutPort.Value}.");
        };

        // Handle VRCFaceTracking compatibility
        _compatibilityVRCFaceTracking = OSC.Instance.meOSCCompatibilityVRCFaceTracking.Value;
        OSC.Instance.meOSCCompatibilityVRCFaceTracking.OnValueChanged += (_, enabled) => _compatibilityVRCFaceTracking = enabled;

        // Create instances of the handler modules
        AvatarHandler = new Avatar();
        InputHandler = new Input();
        SpawnableHandler = new Spawnable();
        TrackingHandler = new Tracking();
        ConfigHandler = new Config();

        Instance = this;
    }

    private bool ConnectSender(string ip, int port) {
        var parsedIp = IPAddress.Parse(ip);
        if (_sender != null && _sender.Port == port && Equals(_sender.RemoteAddress, parsedIp)) return false;
        var oldSender = _sender;
        _sender = new OscSender(parsedIp, port, MessageBufferSize, MaxPacketSize);
        _sender.Connect();
        oldSender?.Close();
        return true;
    }

    public static void SendMessage(string address, params object[] data) {

        // VRCFaceTracking explodes when sending arrays for some reason (this will break a lot of features of this mod
        if (_compatibilityVRCFaceTracking) {
            if (data.Length == 0) return;
            var compMsg = new OscMessage(address, data[0]);
            if (_useOscBundles) lock (_frameOscPacketsBuffer) _frameOscPacketsBuffer.Enqueue(compMsg);
            else Instance._sender.Send(compMsg);
            return;
        }

        var msg = new OscMessage(address, data);
        if (_useOscBundles) lock (_frameOscPacketsBuffer) _frameOscPacketsBuffer.Enqueue(msg);
        else Instance._sender.Send(msg);
    }

    private static void LateUpdateTick() {
        lock (_frameOscPacketsBuffer) {
            if (_frameOscPacketsBuffer.Count == 0) return;

            var current = DateTime.UtcNow;

            do {
                // Bundle a max of 10 messages together (so we don't hit the 2048 limit)
                var bundlePart = _frameOscPacketsBuffer.DequeueChunk(MaxMessagesPerBundle).ToArray();
                try {
                    Instance._sender.Send(new OscBundle(current, bundlePart));
                }
                catch (NotSupportedException e) {
                    MelonLogger.Error(e);
                    MelonLogger.Error($"This error most likely means that we're trying to send a bundle of OSC " +
                                      $"messages, but it exceeds the maximum packet size.");
                    throw;
                }
            } while (_frameOscPacketsBuffer.Count > 0);
        }
    }

    private void OscPacketHandler(OscPacket packet) {
        switch (packet) {
            case OscMessage oscMessage:
                OscMessageHandler(oscMessage);
                break;
            case OscBundle oscBundle:
                OscBundleHandler(oscBundle);
                break;
        }
    }

    private void OscBundleHandler(OscBundle bundle) {
        foreach (var packet in bundle) {
            OscPacketHandler(packet);
        }
    }

    private void OscMessageHandler(OscMessage oscMessage) {

        if (_debugMode) {
            var debugMsg = $"[Debug] Received OSC Message -> Address: {oscMessage.Address}, Args:";
            debugMsg = oscMessage.Aggregate(debugMsg,
                (current, arg) => current + $"\n\t\t\t{arg} [{arg?.GetType()}]");
            MelonLogger.Msg(debugMsg);
        }

        try {
            var addressLower = oscMessage.Address.ToLower();
            switch (addressLower) {
                case not null when addressLower.StartsWith(Avatar.AddressPrefixAvatar):
                    AvatarHandler.ReceiveMessageHandler(oscMessage);
                    break;
                case not null when addressLower.StartsWith(Input.AddressPrefixInput):
                    InputHandler.ReceiveMessageHandler(oscMessage);
                    break;
                case not null when addressLower.StartsWith(Spawnable.AddressPrefixSpawnable):
                    SpawnableHandler.ReceiveMessageHandler(oscMessage);
                    break;
                case not null when addressLower.StartsWith(Tracking.AddressPrefixTracking):
                    TrackingHandler.ReceiveMessageHandler(oscMessage);
                    break;
                case not null when addressLower.StartsWith(Config.AddressPrefixConfig):
                    ConfigHandler.ReceiveMessageHandler(oscMessage);
                    break;
            }
        }
        catch (Exception e) {
            var debugMsg = $"Failed executing the ReceiveMessageHandler from OSC." +
                           $"[Error] Received OSC Message -> Address: {oscMessage.Address}, Args:";
            debugMsg = oscMessage.Aggregate(debugMsg,
                (current, arg) => current + $"\n\t\t\t{arg} [{arg?.GetType()}]");
            MelonLogger.Error(debugMsg);
            MelonLogger.Error(e);
        }
    }

    private IEnumerator OscReceiverHandler() {

        while (_receiver.State != OscSocketState.Closed) {
            if (_receiver.State != OscSocketState.Connected) yield return null;;

            try {
                // Execute while has messages
                while (_receiver.TryReceive(out var packet)) {
                    OscPacketHandler(packet);
                }
            }
            catch (Exception e) {
                if (_receiver.State == OscSocketState.Connected) {
                    MelonLogger.Error($"Failed executing the ReceiveMessageHandler from OSC.");
                    MelonLogger.Error(e);
                }
            }

            // Has no messages -> Wait for next frame
            yield return null;
        }
    }

    public void Close() {
        _receiver.Close();
        _sender.Close();
    }
}
