using MelonLoader;
using SharpOSC;
using OSC.Handlers.OscModules;

namespace OSC.Handlers;

internal class HandlerOsc {

    private static HandlerOsc Instance;

    private UDPListener _listener;
    private UDPSender _sender;

    private Avatar AvatarHandler;
    private Input InputHandler;
    private Spawnable SpawnableHandler;
    private Tracking TrackingHandler;

    public HandlerOsc() {

        // Start listener
        _listener = new UDPListener(OSC.Instance.meOSCServerInPort.Value, ReceiveMessageHandler);
        MelonLogger.Msg($"[Server] OSC Server started listening on the port {OSC.Instance.meOSCServerInPort.Value}.");

        // Handle config listener port changes
        OSC.Instance.meOSCServerInPort.OnValueChanged += (oldPort, newPort) => {
            if (oldPort == newPort) return;
            MelonLogger.Msg("[Server] OSC server port config has changed. Restarting server...");
            try {
                _listener?.Close();
                _listener = new UDPListener(newPort, ReceiveMessageHandler);
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

        // Create instances of the handler modules
        AvatarHandler = new Avatar();
        InputHandler = new Input();
        SpawnableHandler = new Spawnable();
        TrackingHandler = new Tracking();

        Instance = this;
    }

    private bool ConnectSender(string ip, int port) {
        if (_sender != null && _sender.Port == port && _sender.Address == ip) return false;
        var oldSender = _sender;
        _sender = new UDPSender(ip, port);
        oldSender?.Close();
        return true;
    }

    public static void SendMessage(string address, params object[] data) {
        Instance._sender.Send(new OscMessage(address, data));
    }

    private static void ReceiveMessageHandler(OscPacket packet) {

        // Ignore packets that had errors
        if (packet == null) return;

        var oscMessage = (OscMessage) packet;

        try {

            var address = oscMessage.Address;

            var addressLower = oscMessage.Address.ToLower();

            switch (addressLower) {
                case not null when addressLower.StartsWith(Avatar.AddressPrefixAvatar):
                    Instance.AvatarHandler.ReceiveMessageHandler(address, oscMessage.Arguments);
                    break;
                case not null when addressLower.StartsWith(Input.AddressPrefixInput):
                    Instance.InputHandler.ReceiveMessageHandler(address, oscMessage.Arguments);
                    break;
                case not null when addressLower.StartsWith(Spawnable.AddressPrefixSpawnable):
                    Instance.SpawnableHandler.ReceiveMessageHandler(address, oscMessage.Arguments);
                    break;
                case not null when addressLower.StartsWith(Tracking.AddressPrefixTracking):
                    Instance.TrackingHandler.ReceiveMessageHandler(address, oscMessage.Arguments);
                    break;
            }
        }
        catch (Exception e) {
            MelonLogger.Error($"Failed executing the ReceiveMessageHandler from OSC. Contact the mod creator. " +
                              $"Address: {oscMessage.Address} Args: {oscMessage.Arguments} Type: {oscMessage.Arguments.GetType()}");
            MelonLogger.Error(e);
            throw;
        }
    }
}
