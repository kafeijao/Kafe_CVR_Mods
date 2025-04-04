﻿using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util;
using ABI_RC.Core.Util.AnimatorManager;
using ABI_RC.Systems.IK.TrackingModules;
using ABI_RC.Systems.InputManagement;
using ABI.CCK.Components;
using HarmonyLib;
using Rug.Osc.Core;
using UnityEngine;

namespace Kafe.OSC;

[HarmonyPatch]
internal class HarmonyPatches {

    private static bool _performanceMode;

    static HarmonyPatches() {
        // Handle performance mod changes
        _performanceMode = OSC.Instance.meOSCPerformanceMode.Value;
        OSC.Instance.meOSCPerformanceMode.OnEntryValueChanged.Subscribe((_, enabled) => _performanceMode = enabled);
    }

    // Avatar
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.Recycle))]
    internal static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance) {
        Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AvatarAnimatorManager), nameof(AvatarAnimatorManager.Setup))]
    static void AfterUpdateAnimatorManager(AvatarAnimatorManager __instance) {
        if (__instance != PlayerSetup.Instance.animatorManager) return;
        Events.Avatar.OnAnimatorManagerUpdate(__instance);
    }

    // Parameters
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetParameter_Internal), typeof(CVRAnimatorManager.ParamDef), typeof(float))]
    internal static void AfterSetAnimatorParameterFloat(CVRAnimatorManager.ParamDef param, float value, CVRAnimatorManager __instance) {
        if (__instance is not AvatarAnimatorManager avatarAnimatorManager) return;
        if (!avatarAnimatorManager.AASParameterChangedSinceLastSync || _performanceMode) return;
        Events.Avatar.OnParameterChangedFloat(avatarAnimatorManager, param.name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetParameter_Internal), typeof(CVRAnimatorManager.ParamDef), typeof(int))]
    internal static void AfterSetAnimatorParameterInt(CVRAnimatorManager.ParamDef param, int value, CVRAnimatorManager __instance) {
        if (__instance is not AvatarAnimatorManager avatarAnimatorManager) return;
        if (!avatarAnimatorManager.AASParameterChangedSinceLastSync || _performanceMode) return;
        Events.Avatar.OnParameterChangedInt(avatarAnimatorManager, param.name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetParameter_Internal), typeof(CVRAnimatorManager.ParamDef), typeof(bool))]
    internal static void AfterSetAnimatorParameterBool(CVRAnimatorManager.ParamDef param, bool value, CVRAnimatorManager __instance) {
        if (__instance is not AvatarAnimatorManager avatarAnimatorManager) return;
        if (!avatarAnimatorManager.AASParameterChangedSinceLastSync || _performanceMode) return;

        if (param.type == AnimatorControllerParameterType.Bool)
            Events.Avatar.OnParameterChangedBool(avatarAnimatorManager, param.name, value);
        else if (param.type == AnimatorControllerParameterType.Trigger && value)
            Events.Avatar.OnParameterChangedTrigger(avatarAnimatorManager, param.name);
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
    [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.ModuleUpdate))]
    internal static void AfterVRTrackerManagerUpdate(SteamVRTrackingModule __instance) {
        if (_performanceMode) return;
        Events.Tracking.OnTrackingDataDeviceUpdated(__instance);
    }

    // Scene
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
    internal static void AfterPlayerSetup() {
        Events.Scene.OnPlayerSetup();
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.LateUpdate))]
    internal static void AfterPlayerSetupLateUpdate() {
        Events.Scene.OnPlayerSetupLateUpdate();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.Start))]
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
