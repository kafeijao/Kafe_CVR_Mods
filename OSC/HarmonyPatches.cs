using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util;
using ABI_RC.Core.Util.AnimatorManager;
using ABI_RC.Systems.IK.TrackingModules;
using ABI.CCK.Components;
using HarmonyLib;

namespace Kafe.OSC;

[HarmonyPatch]
internal class HarmonyPatches
{
    private static bool _performanceMode;

    static HarmonyPatches()
    {
        // Handle performance mod changes
        _performanceMode = OSC.Instance.meOSCPerformanceMode.Value;
        OSC.Instance.meOSCPerformanceMode.OnEntryValueChanged.Subscribe((_, enabled) => _performanceMode = enabled);
    }

    // Avatar
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.Recycle))]
    internal static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance)
    {
        Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AvatarAnimatorManager), nameof(AvatarAnimatorManager.Setup))]
    static void AfterUpdateAnimatorManager(AvatarAnimatorManager __instance)
    {
        if (__instance != PlayerSetup.Instance.AnimatorManager) return;
        Events.Avatar.OnAnimatorManagerUpdate(__instance);
    }

    // Spawnables
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateMultiPurposeFloat), typeof(CVRSpawnableValue),
        typeof(float), typeof(int))]
    internal static void AfterUpdateMultiPurposeFloat(CVRSpawnableValue spawnableValue, CVRSpawnable __instance)
    {
        if (_performanceMode) return;
        Events.Spawnable.OnSpawnableParameterChanged(__instance, spawnableValue);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateFromNetwork), typeof(CVRSyncHelper.PropData))]
    internal static void AfterSpawnableUpdateFromNetwork(CVRSyncHelper.PropData propData, CVRSpawnable __instance)
    {
        if (_performanceMode) return;
        Events.Spawnable.OnSpawnableUpdateFromNetwork(propData);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn), typeof(CVRSyncHelper.PropData))]
    internal static void AfterApplyPropValuesSpawn(CVRSyncHelper.PropData propData)
    {
        Events.Spawnable.OnSpawnableCreated(propData);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
    internal static void BeforeSpawnableDestroy(CVRSpawnable __instance)
    {
        Events.Spawnable.OnSpawnableDestroyed(__instance);
    }

    // Trackers
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.ModuleUpdate))]
    internal static void AfterVRTrackerManagerUpdate(SteamVRTrackingModule __instance)
    {
        if (_performanceMode) return;
        Events.Tracking.OnTrackingDataDeviceUpdated(__instance);
    }
}
