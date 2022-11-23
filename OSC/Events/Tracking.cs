using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using UnityEngine;
using Valve.VR;

namespace OSC.Events;

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

    private static readonly Dictionary<VRTracker, bool> TrackerLastState = new();

    public static event Action<bool, TrackingDataSource, int, string> TrackingDeviceConnected;
    public static event Action<TrackingDataSource, int, string, Vector3, Vector3, float> TrackingDataDeviceUpdated;
    public static event Action<Vector3, Vector3> TrackingDataPlaySpaceUpdated;

    static Tracking() {

        // Set whether the module is enabled and handle the config changes
        _enabled = OSC.Instance.meOSCTrackingModule.Value;
        OSC.Instance.meOSCTrackingModule.OnValueChanged += (_, newValue) => _enabled = newValue;

        // Set the update rate and handle the config changes
        _updateRate = OSC.Instance.meOSCTrackingModuleUpdateInterval.Value;
        _nextUpdate = _updateRate + Time.time;
        OSC.Instance.meOSCTrackingModuleUpdateInterval.OnValueChanged += (_, newValue) => {
            _updateRate = newValue;
            _nextUpdate = Time.time + newValue;
        };

        // Set the play space transform when it loads
        Scene.PlayerSetup += () => _playerSpaceTransform = PlayerSetup.Instance.transform;
        Scene.PlayerSetup += () => _playerHmdTransform = PlayerSetup.Instance.vrCamera.transform;
    }

    public static void OnTrackingDataDeviceUpdated(VRTrackerManager trackerManager) {

        // Ignore if the module is disabled
        if (!_enabled) return;

        if (Time.time >= _nextUpdate) {
            _nextUpdate = Time.time + _updateRate;

            // Handle trackers
            foreach (var vrTracker in trackerManager.trackers) {

                // Manage Connected/Disconnected trackers
                if ((!TrackerLastState.ContainsKey(vrTracker) && vrTracker.active) || (TrackerLastState.ContainsKey(vrTracker) && TrackerLastState[vrTracker] != vrTracker.active)) {
                    TrackingDeviceConnected?.Invoke(vrTracker.active, GetSource(vrTracker), GetIndex(vrTracker), vrTracker.deviceName);
                    TrackerLastState[vrTracker] = vrTracker.active;
                }

                // Ignore inactive trackers
                if (!vrTracker.active) continue;

                var transform = vrTracker.transform;
                TrackingDataDeviceUpdated?.Invoke(GetSource(vrTracker), GetIndex(vrTracker), vrTracker.deviceName, transform.position, transform.rotation.eulerAngles, vrTracker.batteryStatus);
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

    private static int GetIndex(VRTracker vrTracker) {
        return (int)Traverse.Create(vrTracker).Field<SteamVR_TrackedObject>("_trackedObject").Value.index;
    }

    private static TrackingDataSource GetSource(VRTracker vrTracker) {
        return vrTracker.role switch {
            ETrackedControllerRole.Invalid => vrTracker.deviceName == ""
                ? TrackingDataSource.base_station
                : TrackingDataSource.unknown,
            ETrackedControllerRole.LeftHand => TrackingDataSource.left_controller,
            ETrackedControllerRole.RightHand => TrackingDataSource.right_controller,
            ETrackedControllerRole.OptOut => TrackingDataSource.tracker,
            _ => TrackingDataSource.unknown
        };
    }
}
