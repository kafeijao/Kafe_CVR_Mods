using ABI_RC.Core.Player;
using HarmonyLib;
using UnityEngine;
using Valve.VR;

namespace OSC.Events;

public enum TrackingDataSource {
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
        Events.Scene.PlayerSetup += () => _playerSpaceTransform = PlayerSetup.Instance.transform;
    }

    public static void OnTrackingDataDeviceUpdated(VRTrackerManager trackerManager) {

        // Ignore if the module is disabled
        if (!_enabled) return;

        if (Time.time >= _nextUpdate) {
            _nextUpdate = Time.time + _updateRate;

            //MelonLogger.Msg($"---------------------------------- Tracker Debug ----------------------------------");

            // Handle trackers
            foreach (var vrTracker in trackerManager.trackers) {

                // Ignore inactive trackers
                if (!vrTracker.active) continue;

                var index = (int) Traverse.Create(vrTracker).Field<SteamVR_TrackedObject>("_trackedObject").Value.index;
                var transform = vrTracker.transform;

                var source = vrTracker.role switch {
                    ETrackedControllerRole.Invalid => vrTracker.deviceName == "" ? TrackingDataSource.base_station : TrackingDataSource.unknown,
                    ETrackedControllerRole.LeftHand => TrackingDataSource.left_controller,
                    ETrackedControllerRole.RightHand => TrackingDataSource.right_controller,
                    ETrackedControllerRole.OptOut => TrackingDataSource.tracker,
                    _ => TrackingDataSource.unknown
                };

                // MelonLogger.Msg($"Tracker: " +
                //                 $"Active: {vrTracker.active}, " +
                //                 $"name: {deviceName}, " +
                //                 $"Role: {Enum.GetName(typeof(ETrackedControllerRole), vrTracker.role)}, " +
                //                 $"battery: {vrTracker.batteryStatus}, " +
                //                 $"local: {vrTracker.transform.localPosition.ToString("F3")}");

                TrackingDataDeviceUpdated?.Invoke(source, index, vrTracker.deviceName, transform.position, transform.rotation.eulerAngles, vrTracker.batteryStatus);
            }

            // Handle Play Space
            if (_playerSpaceTransform != null) {
                TrackingDataPlaySpaceUpdated?.Invoke(_playerSpaceTransform.position, _playerSpaceTransform.rotation.eulerAngles);
            }
        }
    }
}
