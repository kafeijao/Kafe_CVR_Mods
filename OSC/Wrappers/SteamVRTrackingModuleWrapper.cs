using ABI_RC.Systems.IK.TrackingModules;
using HarmonyLib;
using Kafe.OSC.Events;
using MelonLoader;
using UnityEngine;
using Valve.VR;

namespace Kafe.OSC.Components;

internal static class SteamVRTrackingModuleWrapper {

    private static bool _performanceMode;

    private static SteamVR _steamVRInstance;
    private static CVRSystem _openVRSystem;

    static SteamVRTrackingModuleWrapper() {
        // Handle performance mod changes
        _performanceMode = OSC.Instance.meOSCPerformanceMode.Value;
        OSC.Instance.meOSCPerformanceMode.OnEntryValueChanged.Subscribe((_, enabled) => _performanceMode = enabled);
    }

    internal class TrackerInfo {

        internal readonly int Index;
        internal readonly string Name;

        internal ETrackedDeviceClass Class { get; private set; }
        internal TrackingDataSource DataSource { get; private set; }
        internal ETrackedControllerRole Role { get; private set; }

        internal bool IsActive;
        internal bool IsValid;

        internal Vector3 Position;
        internal Quaternion Rotation;

        internal float BatteryLevel;

        public TrackerInfo(int index, string name, ETrackedDeviceClass deviceClass) {
            Index = index;
            Name = name;
            Class = deviceClass;
        }

        internal void UpdateDataSource() {
            Role = _openVRSystem.GetControllerRoleForTrackedDeviceIndex((uint)Index);
            DataSource = Class switch {
                ETrackedDeviceClass.HMD => TrackingDataSource.hmd,
                ETrackedDeviceClass.TrackingReference => TrackingDataSource.base_station,
                ETrackedDeviceClass.Controller when Role == ETrackedControllerRole.LeftHand => TrackingDataSource.left_controller,
                ETrackedDeviceClass.Controller when Role == ETrackedControllerRole.RightHand => TrackingDataSource.right_controller,
                ETrackedDeviceClass.GenericTracker => TrackingDataSource.tracker,
                _ => TrackingDataSource.unknown
            };
        }
    }

    internal static readonly Dictionary<int, TrackerInfo> TrackersInfo = new ();

    private static void TrackedDeviceRoleChanged(VREvent_t e) {
        if (TrackersInfo.TryGetValue((int) e.trackedDeviceIndex, out var trackerInfo)) {
            trackerInfo.UpdateDataSource();
        }
    }

    private static void OnNewPoses(TrackedDevicePose_t[] poses) {
        for (var index = 0; index < poses.Length; index++) {
            var pose = poses[index];

            // Create tracker info if it doesn't exist
            if (!TrackersInfo.TryGetValue(index, out var trackerInfo)) {
                var stringProperty = _steamVRInstance.GetStringProperty(ETrackedDeviceProperty.Prop_ControllerType_String, out var error1, (uint) index);
                var deviceName = error1 != ETrackedPropertyError.TrackedProp_Success ? "" : stringProperty;
                var deviceClass = _openVRSystem.GetTrackedDeviceClass((uint)index);
                trackerInfo = new TrackerInfo(index, deviceName, deviceClass);
                trackerInfo.UpdateDataSource();
                TrackersInfo[index] = trackerInfo;
            }

            // Grab the updated info
            trackerInfo.IsActive = pose.bDeviceIsConnected;
            trackerInfo.IsValid = pose.bPoseIsValid;

            var rigidTransform = new SteamVR_Utils.RigidTransform(pose.mDeviceToAbsoluteTracking);
            trackerInfo.Position = rigidTransform.pos;
            trackerInfo.Rotation = rigidTransform.rot;

            var error2 = ETrackedPropertyError.TrackedProp_Success;
            var prop = _steamVRInstance.hmd.GetFloatTrackedDeviceProperty((uint) index, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error2);
            trackerInfo.BatteryLevel = error2 != ETrackedPropertyError.TrackedProp_Success ? 0.0f : prop;
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static bool _running;
        private static bool _errored;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.ModuleStart))]
        internal static void After_SteamVRTrackingModule_ModuleStart() {
            if (_errored || SteamVR_Render.instance == null) return;
            if (SteamVR.instance == null || SteamVR.instance.hmd == null || OpenVR.System == null) return;

            _steamVRInstance = SteamVR.instance;
            _openVRSystem = OpenVR.System;

            try {
                TrackersInfo.Clear();
                SteamVR_Events.System(EVREventType.VREvent_TrackedDeviceRoleChanged).AddListener(TrackedDeviceRoleChanged);
                _running = true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Critical Error while reading the SteamVR tracker info. Report the error bellow to the Mod Author.");
                MelonLogger.Error(e);
                _errored = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.ModuleDestroy))]
        internal static void After_SteamVRTrackingModule_ModuleDestroy() {
            if (_errored || !_running) return;
            try {
                SteamVR_Events.System(EVREventType.VREvent_TrackedDeviceRoleChanged).RemoveListener(TrackedDeviceRoleChanged);
                TrackersInfo.Clear();
            }
            catch (Exception e) {
                MelonLogger.Error($"Critical Error while reading the SteamVR tracker info. Report the error bellow to the Mod Author.");
                MelonLogger.Error(e);
                _errored = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.OnNewPoses))]
        internal static void After_SteamVRTrackingModule_OnNewPoses(TrackedDevicePose_t[] poses) {
            if (_errored || _performanceMode || !_running) return;

            try {
                OnNewPoses(poses);
            }
            catch (Exception e) {
                MelonLogger.Error($"Critical Error while reading the SteamVR tracker info. Report the error bellow to the Mod Author.");
                MelonLogger.Error(e);
                _errored = true;
            }
        }
    }
}
