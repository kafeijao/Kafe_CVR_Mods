using OSC.Events;
using UnityEngine;

namespace OSC.Handlers.OscModules;

public class Tracking : OscHandler {

    internal const string AddressPrefixTracking = "/tracking/";

    private readonly Action<TrackingDataSource, int, string, Vector3, Vector3, float> _trackingDataDeviceUpdated;
    private readonly Action<Vector3, Vector3> _trackingDataPlaySpaceUpdated;

    public Tracking() {

        // Send tracking data device update events
        _trackingDataDeviceUpdated = (source, id, deviceName, pos, rot, battery) => {
            var address = $"{AddressPrefixTracking}device/{Enum.GetName(typeof(Events.TrackingDataSource), source)}/{id}";
            HandlerOsc.SendMessage(address, deviceName, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, battery);
        };

        // Send tracking data play space update events
        _trackingDataPlaySpaceUpdated = (pos, rot) => {
            var address = $"{AddressPrefixTracking}play_space";
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
        Events.Tracking.TrackingDataDeviceUpdated += _trackingDataDeviceUpdated;
        Events.Tracking.TrackingDataPlaySpaceUpdated += _trackingDataPlaySpaceUpdated;
    }

    internal sealed override void Disable() {
        Events.Tracking.TrackingDataDeviceUpdated -= _trackingDataDeviceUpdated;
        Events.Tracking.TrackingDataPlaySpaceUpdated -= _trackingDataPlaySpaceUpdated;
    }
}
