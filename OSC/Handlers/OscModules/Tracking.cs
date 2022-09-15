using OSC.Events;
using UnityEngine;

namespace OSC.Handlers.OscModules;

enum TrackingEntity {
    device,
    play_space,
}

enum TrackingOperations {
    status,
    data,
}

public class Tracking : OscHandler {

    internal const string AddressPrefixTracking = "/tracking/";

    private readonly Action<bool, TrackingDataSource, int, string> _trackingDeviceConnected;
    private readonly Action<TrackingDataSource, int, string, Vector3, Vector3, float> _trackingDataDeviceUpdated;
    private readonly Action<Vector3, Vector3> _trackingDataPlaySpaceUpdated;

    public Tracking() {

        // Send tracking device stats events
        _trackingDeviceConnected = (connected, source, id, deviceName) => {
            const string address = $"{AddressPrefixTracking}{nameof(TrackingEntity.device)}/{nameof(TrackingOperations.status)}";
            HandlerOsc.SendMessage(address, connected, Enum.GetName(typeof(TrackingDataSource), source), id, deviceName);
        };

        // Send tracking device data update events
        _trackingDataDeviceUpdated = (source, id, deviceName, pos, rot, battery) => {
            const string address = $"{AddressPrefixTracking}{nameof(TrackingEntity.device)}/{nameof(TrackingOperations.data)}";
            HandlerOsc.SendMessage(address, Enum.GetName(typeof(TrackingDataSource), source), id, deviceName,
                pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, battery);
        };

        // Send tracking play space data update events
        _trackingDataPlaySpaceUpdated = (pos, rot) => {
            const string address = $"{AddressPrefixTracking}{nameof(TrackingEntity.play_space)}/{nameof(TrackingOperations.data)}";
            HandlerOsc.SendMessage(address, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
        };

        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCTrackingModule.Value) Enable();
        OSC.Instance.meOSCTrackingModule.OnValueChanged += (oldValue, newValue) => {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        };
    }

    internal sealed override void Enable() {
        Events.Tracking.TrackingDeviceConnected += _trackingDeviceConnected;
        Events.Tracking.TrackingDataDeviceUpdated += _trackingDataDeviceUpdated;
        Events.Tracking.TrackingDataPlaySpaceUpdated += _trackingDataPlaySpaceUpdated;
    }

    internal sealed override void Disable() {
        Events.Tracking.TrackingDeviceConnected -= _trackingDeviceConnected;
        Events.Tracking.TrackingDataDeviceUpdated -= _trackingDataDeviceUpdated;
        Events.Tracking.TrackingDataPlaySpaceUpdated -= _trackingDataPlaySpaceUpdated;
    }
}
