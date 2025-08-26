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

    private readonly Action<bool, TrackingDataSource, int, string> _trackingDeviceConnected;
    private readonly Action<TrackingDataSource, int, string, Vector3, Vector3, float> _trackingDataDeviceUpdated;
    private readonly Action<Vector3, Vector3> _trackingDataPlaySpaceUpdated;

    public OSCTrackingModule() : base(ModulePrefix)
    {
        // Send tracking device stats events
        _trackingDeviceConnected = (connected, source, id, deviceName) =>
        {
            const string address =
                $"{ModulePrefix}/{nameof(TrackingEntity.device)}/{nameof(TrackingOperations.status)}";
            Server.DispatchMessage(new OscMessage(address, connected, Enum.GetName(typeof(TrackingDataSource), source),
                id, deviceName));
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
            const string address =
                $"{ModulePrefix}/{nameof(TrackingEntity.play_space)}/{nameof(TrackingOperations.data)}";
            Server.DispatchMessage(new OscMessage(address, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z));
        };

        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCTrackingModule.Value) Enable();
        OSC.Instance.meOSCTrackingModule.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        });
    }

    private void Enable()
    {
        Tracking.TrackingDeviceConnected += _trackingDeviceConnected;
        Tracking.TrackingDataDeviceUpdated += _trackingDataDeviceUpdated;
        Tracking.TrackingDataPlaySpaceUpdated += _trackingDataPlaySpaceUpdated;
    }

    private void Disable()
    {
        Tracking.TrackingDeviceConnected -= _trackingDeviceConnected;
        Tracking.TrackingDataDeviceUpdated -= _trackingDataDeviceUpdated;
        Tracking.TrackingDataPlaySpaceUpdated -= _trackingDataPlaySpaceUpdated;
    }

    #region Module Overrides

    public override bool HandleIncoming(OscMessage packet) => false;

    #endregion Module Overrides

    // Todo: Add OSCQuery?
}
