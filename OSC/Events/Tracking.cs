using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.IK.TrackingModules;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Valve.VR;

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

    public static void OnTrackingDataDeviceUpdated(SteamVRTrackingModule steamVRTrackingModule) {

        // Ignore if the module is disabled
        if (!_enabled) return;

        if (Time.time >= _nextUpdate) {
            _nextUpdate = Time.time + _updateRate;

            // Handle trackers
            foreach (var vrTracker in IKSystem.Instance.AllTrackingPoints) {

                // Manage Connected/Disconnected trackers
                if ((!TrackerLastState.ContainsKey(vrTracker) && vrTracker.isActive) || (TrackerLastState.ContainsKey(vrTracker) && TrackerLastState[vrTracker] != vrTracker.isActive)) {
                    MelonLogger.Msg($"[Tracker] {vrTracker.isActive}, {vrTracker.identifier}, {vrTracker.name}, {vrTracker.assignedRole.ToString()}, {vrTracker.deviceName}, {vrTracker.inUse}, {vrTracker.isValid}, {vrTracker.suggestedRole.ToString()}");
                    TrackingDeviceConnected?.Invoke(vrTracker.isActive, GetSource(vrTracker), GetIndex(vrTracker), vrTracker.deviceName);
                    TrackerLastState[vrTracker] = vrTracker.isActive;
                }

                // Ignore inactive trackers
                if (!vrTracker.isActive) continue;

                TrackingDataDeviceUpdated?.Invoke(GetSource(vrTracker), GetIndex(vrTracker), vrTracker.deviceName, vrTracker.position, vrTracker.rotation.eulerAngles, vrTracker.batteryPercentage);
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

    private static int GetIndex(TrackingPoint vrTracker) {
        return vrTracker.displayObject.GetHashCode();
        // return (int)Traverse.Create(vrTracker).Field<SteamVR_TrackedObject>("_trackedObject").Value.index;
    }

    private static TrackingDataSource GetSource(TrackingPoint vrTracker) {
        return TrackingDataSource.unknown;
        // Todo: Figure it out
        // return vrTracker.assignedRole switch {
        //     ETrackedControllerRole.Invalid => vrTracker.deviceName == ""
        //         ? TrackingDataSource.base_station
        //         : TrackingDataSource.unknown,
        //     ETrackedControllerRole.LeftHand => TrackingDataSource.left_controller,
        //     ETrackedControllerRole.RightHand => TrackingDataSource.right_controller,
        //     ETrackedControllerRole.OptOut => TrackingDataSource.tracker,
        //     _ => TrackingDataSource.unknown
        // };
    }
}
