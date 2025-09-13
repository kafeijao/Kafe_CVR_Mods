using ABI_RC.Systems.OSC;
using Kafe.OSC.Events;
using LucHeart.CoreOSC;
using UnityEngine;

namespace Kafe.OSC.Modules;

internal enum TrackingEntity
{
    device,
    play_space,
}

internal enum TrackingOperations
{
    status,
    data,
}

public class OSCTrackingModule : OSCModule
{
    public const string ModulePrefix = "/tracking";

    private bool _moduleEnabled;
    private bool _oscServerRunning;

    private bool _listeningTrackingEvents;

    private readonly Action<bool, TrackingDataSource, int, string> _trackingDeviceConnected;
    private readonly Action<TrackingDataSource, int, string, Vector3, Vector3, float> _trackingDataDeviceUpdated;
    private readonly Action<Vector3, Vector3> _trackingDataPlaySpaceUpdated;

    public OSCTrackingModule() : base(ModulePrefix)
    {
        // Send tracking device stats events
        _trackingDeviceConnected = (connected, source, id, deviceName) =>
        {
            const string address = $"{ModulePrefix}/{nameof(TrackingEntity.device)}/{nameof(TrackingOperations.status)}";
            Server.DispatchMessage(new OscMessage(address, connected, Enum.GetName(typeof(TrackingDataSource), source), id, deviceName));
        };

        // Send tracking device data update events
        _trackingDataDeviceUpdated = (source, id, deviceName, pos, rot, battery) =>
        {
            const string address = $"{ModulePrefix}/{nameof(TrackingEntity.device)}/{nameof(TrackingOperations.data)}";
            Server.DispatchMessage(new OscMessage(address, Enum.GetName(typeof(TrackingDataSource), source), id,
                deviceName, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, battery));
        };

        // Send tracking play space data update events
        _trackingDataPlaySpaceUpdated = (pos, rot) =>
        {
            const string address = $"{ModulePrefix}/{nameof(TrackingEntity.play_space)}/{nameof(TrackingOperations.data)}";
            Server.DispatchMessage(new OscMessage(address, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z));
        };

        // Enable according to the config and set up the config listeners
        _moduleEnabled = OSC.Instance.meOSCTrackingModule.Value;
        OSC.Instance.meOSCTrackingModule.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            _moduleEnabled = newValue;
            UpdateState();
        });

        _oscServerRunning = OSCServer.IsRunning;
        OSCServerEvents.OSCServerStateUpdated += isRunning =>
        {
            _oscServerRunning = isRunning;
            UpdateState();
        };

        UpdateState();
    }

    private void UpdateState()
    {
        bool shouldListen = _oscServerRunning && _moduleEnabled;
        // Already on the correct listening state
        if (shouldListen == _listeningTrackingEvents) return;

        if (shouldListen) Enable();
        else Disable();

        _listeningTrackingEvents = shouldListen;
    }

    private void Enable()
    {
        _listeningTrackingEvents = true;
        Tracking.TrackingDeviceConnected += _trackingDeviceConnected;
        Tracking.TrackingDataDeviceUpdated += _trackingDataDeviceUpdated;
        Tracking.TrackingDataPlaySpaceUpdated += _trackingDataPlaySpaceUpdated;
    }

    private void Disable()
    {
        _listeningTrackingEvents = false;
        Tracking.TrackingDeviceConnected -= _trackingDeviceConnected;
        Tracking.TrackingDataDeviceUpdated -= _trackingDataDeviceUpdated;
        Tracking.TrackingDataPlaySpaceUpdated -= _trackingDataPlaySpaceUpdated;
    }

    #region Module Overrides

    public override bool HandleIncoming(OscMessage packet) => false;

    #endregion Module Overrides

    // Todo: Add OSCQuery?
}
