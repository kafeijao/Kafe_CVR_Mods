using ABI_RC.Core.Player;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.IK.TrackingModules;
using Kafe.OSC.Components;
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
    // private static bool _sendTrackingData;
    // private static bool _receiveTrackingData;
    private static float _updateRate;
    private static float _nextUpdate;

    // private static Transform _playerSpaceTransform;
    // private static Transform _playerHmdTransform;

    private static readonly Dictionary<SteamVRTrackingModuleWrapper.TrackerInfo, bool> TrackerLastState = new();

    public static event Action<bool, TrackingDataSource, int, string> TrackingDeviceConnected;
    public static event Action<TrackingDataSource, int, string, Vector3, Vector3, float> TrackingDataDeviceUpdated;
    public static event Action<Vector3, Vector3> TrackingDataPlaySpaceUpdated;

    static Tracking() {

        // Set whether the module is enabled and handle the config changes
        _enabled = OSC.Instance.meOSCTrackingModule.Value;
        // _sendTrackingData = OSC.Instance.meOSCTrackingModuleSendData.Value;
        // _receiveTrackingData = OSC.Instance.meOSCTrackingModuleReceiveData.Value;
        OSC.Instance.meOSCTrackingModule.OnEntryValueChanged.Subscribe((_, newValue) => _enabled = newValue);
        // OSC.Instance.meOSCTrackingModuleSendData.OnEntryValueChanged.Subscribe((_, newValue) => _sendTrackingData = newValue);
        // OSC.Instance.meOSCTrackingModuleReceiveData.OnEntryValueChanged.Subscribe((_, newValue) => _receiveTrackingData = newValue);

        // Set the update rate and handle the config changes
        _updateRate = OSC.Instance.meOSCTrackingModuleUpdateInterval.Value;
        _nextUpdate = _updateRate + Time.time;
        OSC.Instance.meOSCTrackingModuleUpdateInterval.OnEntryValueChanged.Subscribe((_, newValue) => {
            _updateRate = newValue;
            _nextUpdate = Time.time + newValue;
        });

        // Set the play space transform when it loads
        // Scene.PlayerSetup += () => _playerSpaceTransform = PlayerSetup.Instance.transform;
        // Scene.PlayerSetup += () => _playerHmdTransform = PlayerSetup.Instance.vrCamera.transform;
    }

    public static void OnTrackingDataDeviceUpdated(SteamVRTrackingModule steamVRTrackingModule) {

        // Ignore if the module is disabled
        if (!_enabled /*|| !_sendTrackingData*/) return;

        if (Time.time >= _nextUpdate) {
            _nextUpdate = Time.time + _updateRate;

            // Handle Play Space
            var playSpace = IKSystem.Instance._vrPlaySpace;
            TrackingDataPlaySpaceUpdated?.Invoke(playSpace.position, playSpace.rotation.eulerAngles);

            // var print = Input.GetKeyDown(KeyCode.P);

            // Handle trackers
            foreach (var steamVRTracker in SteamVRTrackingModuleWrapper.TrackersInfo.Values) {

                var isActive = steamVRTracker.IsActive && steamVRTracker.IsValid;

                // Manage Connected/Disconnected trackers
                if ((!TrackerLastState.ContainsKey(steamVRTracker) && isActive)
                    || (TrackerLastState.ContainsKey(steamVRTracker) && TrackerLastState[steamVRTracker] != isActive)) {
                    // MelonLogger.Msg($"[Tracker] Idx: {steamVRTracker.Index}, IsActive: {steamVRTracker.IsActive}, IsValid: {steamVRTracker.IsValid}, Name: {steamVRTracker.Name}, Class: {steamVRTracker.Class}, Role: {steamVRTracker.Role.ToString()}, DataSource: {steamVRTracker.DataSource.ToString()}, BatteryLevel: {steamVRTracker.BatteryLevel}, Position: {steamVRTracker.Position.ToString("F2")}, Rotation: {steamVRTracker.Rotation.eulerAngles.ToString("F2")}");
                    TrackingDeviceConnected?.Invoke(isActive, steamVRTracker.DataSource, steamVRTracker.Index, steamVRTracker.Name);
                    TrackerLastState[steamVRTracker] = isActive;
                }

                // if (print && steamVRTracker.IsActive) {
                //     MelonLogger.Msg($"[Tracker] Idx: {steamVRTracker.Index}, IsActive: {steamVRTracker.IsActive}, Name: {steamVRTracker.Name}, Class: {steamVRTracker.Class}, Role: {steamVRTracker.Role.ToString()}, DataSource: {steamVRTracker.DataSource.ToString()}, IsValid: {steamVRTracker.IsValid}, BatteryLevel: {steamVRTracker.BatteryLevel}, Position: {steamVRTracker.Position.ToString("F2")}, Rotation: {steamVRTracker.Rotation.eulerAngles.ToString("F2")}");
                // }

                // Ignore inactive trackers
                if (!isActive) continue;

                TrackingDataDeviceUpdated?.Invoke(steamVRTracker.DataSource, steamVRTracker.Index, steamVRTracker.Name, playSpace.TransformPoint(steamVRTracker.Position), (playSpace.rotation * steamVRTracker.Rotation).eulerAngles, steamVRTracker.BatteryLevel);
            }

            // // Handle HMD
            // if (MetaPort.Instance.isUsingVr && _playerHmdTransform != null) {
            //     TrackingDataDeviceUpdated?.Invoke(TrackingDataSource.hmd, 0, "hmd", _playerHmdTransform.position, _playerHmdTransform.rotation.eulerAngles, 0);
            // }

            // Tell props to send their location as well
            Spawnable.OnTrackingTick();
        }
    }

    internal static void Reset() {
        // Clear the cache to force an update to the connected devices
        TrackerLastState.Clear();
    }
}
