using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.PropManagement;
using ABI_RC.Core.Util;
using ABI_RC.Core.Util.AnimatorManager;
using ABI_RC.Systems.IK.TrackingModules;
using ABI_RC.Systems.OSC;
using ABI_RC.Systems.SyncedObjects;
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Avatar_t), MethodType.Constructor)]
    static void After_Avatar_t_Constructor(Avatar_t __instance) {
        try
        {
            Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_Avatar_t_Constructor)} patch", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.PopulateFromApiResponse))]
    static void After_AvatarDetails_t_PopulateFromApiResponse(AvatarDetails_t __instance) {
        try
        {
            Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_AvatarDetails_t_PopulateFromApiResponse)} patch", e);
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
    [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.UpdateMultiPurposeFloat), typeof(CVRSpawnableValue), typeof(float))]
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
    [HarmonyPatch(typeof(SyncedObjectManager), nameof(SyncedObjectManager.OnUpdateBroadcast))]
    internal static void After_SyncedObjectManager_OnUpdateBroadcast()
    {
        try
        {
            if (_performanceMode) return;
            Events.Spawnable.OnSyncStateReceived();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_SyncedObjectManager_OnUpdateBroadcast)} patch", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SyncedObjectManager), nameof(SyncedObjectManager.OnFullSync))]
    internal static void After_SyncedObjectManager_OnFullSync()
    {
        try
        {
            if (_performanceMode) return;
            Events.Spawnable.OnSyncStateReceived();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(After_SyncedObjectManager_OnFullSync)} patch", e);
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
