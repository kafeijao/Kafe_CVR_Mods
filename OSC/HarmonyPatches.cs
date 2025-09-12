using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util;
using ABI_RC.Core.Util.AnimatorManager;
using ABI_RC.Systems.IK.TrackingModules;
using ABI_RC.Systems.OSC;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;

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
    internal static void Before_AvatarDetails_t_Recycle(AvatarDetails_t __instance)
    {
        try
        {
            Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(Before_AvatarDetails_t_Recycle)} patch", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AvatarAnimatorManager), nameof(AvatarAnimatorManager.Setup))]
    static void After_AvatarAnimatorManager_Setup(AvatarAnimatorManager __instance)
    {
        try
        {
            if (__instance != PlayerSetup.Instance.AnimatorManager) return;
            Events.Avatar.OnAnimatorManagerUpdate(__instance);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_AvatarAnimatorManager_Setup)} patch", e);
        }
    }

    // Spawnables
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateMultiPurposeFloat), typeof(CVRSpawnableValue),
        typeof(float), typeof(int))]
    internal static void After_CVRSpawnable_UpdateMultiPurposeFloat(CVRSpawnableValue spawnableValue, CVRSpawnable __instance)
    {
        try
        {
            if (_performanceMode) return;
            Events.Spawnable.OnSpawnableParameterChanged(__instance, spawnableValue);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_CVRSpawnable_UpdateMultiPurposeFloat)} patch", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateFromNetwork), typeof(CVRSyncHelper.PropData))]
    internal static void After_CVRSpawnable_UpdateFromNetwork(CVRSyncHelper.PropData propData, CVRSpawnable __instance)
    {
        try
        {
            if (_performanceMode) return;
            Events.Spawnable.OnSpawnableUpdateFromNetwork(propData);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_CVRSpawnable_UpdateFromNetwork)} patch", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn), typeof(CVRSyncHelper.PropData))]
    internal static void After_CVRSyncHelper_ApplyPropValuesSpawn(CVRSyncHelper.PropData propData)
    {
        try
        {
            Events.Spawnable.OnSpawnableCreated(propData);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_CVRSyncHelper_ApplyPropValuesSpawn)} patch", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
    internal static void Before_Spawnable_OnDestroy(CVRSpawnable __instance)
    {
        try
        {
            Events.Spawnable.OnSpawnableDestroyed(__instance);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(Before_Spawnable_OnDestroy)} patch", e);
        }
    }

    // Trackers
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SteamVRTrackingModule), nameof(SteamVRTrackingModule.ModuleUpdate))]
    internal static void After_SteamVRTrackingModule_ModuleUpdate(SteamVRTrackingModule __instance)
    {
        try
        {
            if (_performanceMode) return;
            Events.Tracking.OnTrackingDataDeviceUpdated(__instance);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_SteamVRTrackingModule_ModuleUpdate)} patch", e);
        }
    }

    // OSCServer
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OSCServer), nameof(OSCServer.StartServer))]
    internal static void After_OSCServer_StartServer(OSCServer __instance)
    {
        try
        {
            Events.OSCServerEvents.OnOSCServerStateUpdate(__instance.IsServerRunning);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_OSCServer_StartServer)} patch", e);
        }
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(OSCServer), nameof(OSCServer.StopServer))]
    internal static void After_OSCServer_StopServer(OSCServer __instance)
    {
        try
        {
            Events.OSCServerEvents.OnOSCServerStateUpdate(__instance.IsServerRunning);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_OSCServer_StopServer)} patch", e);
        }
    }
}
