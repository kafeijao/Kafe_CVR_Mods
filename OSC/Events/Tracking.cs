using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using UnityEngine;

namespace Kafe.OSC.Events;

public enum TrackingDataSource {
    hmd,
    base_station,
    right_controller,
    left_controller,
    tracker,
    unknown,
}

public static class Tracking {

    private static bool _enabled;
    private static float _updateRate;
    private static float _nextUpdate;

    private static Transform _playerSpaceTransform;
    private static Transform _playerHmdTransform;

    private static readonly Dictionary<TrackingPoint, bool> TrackerLastState = new();

    public static event Action<bool, TrackingDataSource, int, string> TrackingDeviceConnected;
    public static event Action<TrackingDataSource, int, string, Vector3, Vector3, float> TrackingDataDeviceUpdated;
    public static event Action<Vector3, Vector3> TrackingDataPlaySpaceUpdated;

    static Tracking() {

        // Set whether the module is enabled and handle the config changes
        _enabled = OSC.Instance.meOSCTrackingModule.Value;
        OSC.Instance.meOSCTrackingModule.OnEntryValueChanged.Subscribe((_, newValue) => _enabled = newValue);

        // Set the update rate and handle the config changes
        _updateRate = OSC.Instance.meOSCTrackingModuleUpdateInterval.Value;
        _nextUpdate = _updateRate + Time.time;
        OSC.Instance.meOSCTrackingModuleUpdateInterval.OnEntryValueChanged.Subscribe((_, newValue) => {
            _updateRate = newValue;
            _nextUpdate = Time.time + newValue;
        });

        // Set the play space transform when it loads
        Scene.PlayerSetup += () => _playerSpaceTransform = PlayerSetup.Instance.transform;
        Scene.PlayerSetup += () => _playerHmdTransform = PlayerSetup.Instance.vrCamera.transform;
    }

    public static void OnTrackingDataDeviceUpdated(IKSystem ikSystem) {

        // Ignore if the module is disabled
        if (!_enabled) return;

        if (Time.time >= _nextUpdate) {
            _nextUpdate = Time.time + _updateRate;

            // Handle trackers
            for (var index = 0; index < ikSystem.AllTrackingPoints.Count; index++) {
                var trackingPoint = ikSystem.AllTrackingPoints[index];
                // Todo: Check what isActive means (there is also inUse and isValid, and some more?)
                var isActive = trackingPoint.isActive;

                // Manage Connected/Disconnected trackers
                if ((!TrackerLastState.ContainsKey(trackingPoint) && isActive) ||
                    (TrackerLastState.ContainsKey(trackingPoint) &&
                     TrackerLastState[trackingPoint] != isActive)) {
                    TrackingDeviceConnected?.Invoke(isActive, GetSource(trackingPoint),
                        index, trackingPoint.deviceName);
                    TrackerLastState[trackingPoint] = isActive;
                }

                // Ignore inactive trackers
                if (!isActive) continue;

                var transform = trackingPoint.referenceTransform;
                TrackingDataDeviceUpdated?.Invoke(GetSource(trackingPoint), index,
                    trackingPoint.deviceName, transform.position, transform.rotation.eulerAngles,
                    trackingPoint.batteryPercentage);
            }

            // Handle HMD
            if (MetaPort.Instance.isUsingVr && _playerHmdTransform != null) {
                TrackingDataDeviceUpdated?.Invoke(TrackingDataSource.hmd, 0, "hmd", _playerHmdTransform.position, _playerHmdTransform.rotation.eulerAngles, 0);
            }

            // Handle Play Space
            if (_playerSpaceTransform != null) {
                TrackingDataPlaySpaceUpdated?.Invoke(_playerSpaceTransform.position, _playerSpaceTransform.rotation.eulerAngles);
            }

            // Tell props to send their location as well
            Spawnable.OnTrackingTick();
        }
    }

    internal static void Reset() {
        // Clear the cache to force an update to the connected devices
        TrackerLastState.Clear();
    }

    // private static TrackingDataSource GetSource(TrackingPoint trackingPoint) {
    //     return trackingPoint.assignedRole switch {
    //         ETrackedControllerRole.Invalid => trackingPoint.deviceName == ""
    //             ? TrackingDataSource.base_station
    //             : TrackingDataSource.unknown,
    //         ETrackedControllerRole.LeftHand => TrackingDataSource.left_controller,
    //         ETrackedControllerRole.RightHand => TrackingDataSource.right_controller,
    //         ETrackedControllerRole.OptOut => TrackingDataSource.tracker,
    //         _ => TrackingDataSource.unknown
    //     };
    // }
    private static TrackingDataSource GetSource(TrackingPoint trackingPoint) {
        // Todo: Fix this
        return trackingPoint.assignedRole switch {
            // TrackingPoint.TrackingRole.Invalid => trackingPoint.deviceName == ""
            //     ? TrackingDataSource.base_station
            //     : TrackingDataSource.unknown,
            // TrackingPoint.TrackingRole.Generic => expr,
            // TrackingPoint.TrackingRole.LeftFoot => expr,
            // TrackingPoint.TrackingRole.RightFoot => expr,
            // TrackingPoint.TrackingRole.LeftKnee => expr,
            // TrackingPoint.TrackingRole.RightKnee => expr,
            // TrackingPoint.TrackingRole.Hips => expr,
            // TrackingPoint.TrackingRole.Chest => expr,
            // TrackingPoint.TrackingRole.LeftElbow => expr,
            // TrackingPoint.TrackingRole.RightElbow => expr,
            // TrackingPoint.TrackingRole.LeftHand => expr,
            // TrackingPoint.TrackingRole.RightHand => expr,
            // TrackingPoint.TrackingRole.Head => expr,
            _ => TrackingDataSource.unknown,
        };
    }
}
