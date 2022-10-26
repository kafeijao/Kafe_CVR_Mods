using ABI_RC.Core;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using Rug.Osc;

namespace OSC;

[HarmonyPatch]
internal class HarmonyPatches {

    private static bool _performanceMode;

    static HarmonyPatches() {
        // Handle performance mod changes
        _performanceMode = OSC.Instance.meOSCPerformanceMode.Value;
        OSC.Instance.meOSCPerformanceMode.OnValueChanged += (_, enabled) => _performanceMode = enabled;
    }

    // Avatar
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AvatarDetails_t), "Recycle")]
    internal static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance) {
        Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateAnimatorManager))]
    internal static void AfterUpdateAnimatorManager(CVRAnimatorManager manager) {
        Events.Avatar.OnAnimatorManagerUpdate(manager);
    }

    // Parameters
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterFloat))]
    internal static void AfterSetAnimatorParameterFloat(string name, float value, CVRAnimatorManager __instance, bool ____parametersChanged) {
        if (!____parametersChanged || _performanceMode) return;
        Events.Avatar.OnParameterChangedFloat(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterInt))]
    internal static void AfterSetAnimatorParameterInt(string name, int value, CVRAnimatorManager __instance, bool ____parametersChanged) {
        if (!____parametersChanged || _performanceMode) return;
        Events.Avatar.OnParameterChangedInt(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterBool))]
    internal static void AfterSetAnimatorParameterBool(string name, bool value, CVRAnimatorManager __instance, bool ____parametersChanged) {
        if (!____parametersChanged || _performanceMode) return;
        Events.Avatar.OnParameterChangedBool(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterTrigger))]
    internal static void AfterSetAnimatorParameterTrigger(string name, CVRAnimatorManager __instance, bool ____parametersChanged) {
        if (!____parametersChanged || _performanceMode) return;
        Events.Avatar.OnParameterChangedTrigger(__instance, name);
    }

    // Spawnables
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateMultiPurposeFloat), typeof(CVRSpawnableValue), typeof(float), typeof(int))]
    internal static void AfterUpdateMultiPurposeFloat(CVRSpawnableValue spawnableValue, CVRSpawnable __instance) {
        if (_performanceMode) return;
        Events.Spawnable.OnSpawnableParameterChanged(__instance, spawnableValue);
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateFromNetwork), typeof(CVRSyncHelper.PropData))]
    internal static void AfterSpawnableUpdateFromNetwork(CVRSyncHelper.PropData propData, CVRSpawnable __instance) {
        if (_performanceMode) return;
        Events.Spawnable.OnSpawnableUpdateFromNetwork(propData);
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn), typeof(CVRSyncHelper.PropData))]
    internal static void AfterApplyPropValuesSpawn(CVRSyncHelper.PropData propData) {
        Events.Spawnable.OnSpawnableCreated(propData);
    }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
    internal static void BeforeSpawnableDestroy(CVRSpawnable __instance) {
        Events.Spawnable.OnSpawnableDestroyed(__instance);
    }

    // Trackers
    [HarmonyPostfix]
    [HarmonyPatch(typeof(VRTrackerManager), nameof(VRTrackerManager.Update))]
    internal static void AfterVRTrackerManagerUpdate(VRTrackerManager __instance) {
        if (_performanceMode) return;
        Events.Tracking.OnTrackingDataDeviceUpdated(__instance);
    }

    // Scene
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSetup), "Start")]
    internal static void AfterPlayerSetup() {
        Events.Scene.OnPlayerSetup();
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSetup), "LateUpdate")]
    internal static void AfterPlayerSetupLateUpdate() {
        Events.Scene.OnPlayerSetupLateUpdate();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRInputManager), "Start")]
    private static void AfterInputManagerCreated() {
        Events.Scene.OnInputManagerCreated();
    }

    // OSC Lib Actually following the spec ;_;
    // Let's nuke the address validation so we can get the juicy #ParamName working in the address
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OscAddress), nameof(OscAddress.IsValidAddressPattern))]
    private static bool BeforeOscAddressIsValid(ref bool __result) {
        __result = true;
        return false;
    }
}
