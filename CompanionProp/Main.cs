using System.Collections;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.CompanionProp;

public class CompanionProp : MelonMod {

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        CheckGuid(ModConfig.MePropGuid.Value, false);

        object spawning = null;

        CVRGameEventSystem.Instance.OnConnected.AddListener(instanceID => {

            if (!ShouldSpawnProp) return;

            // Handle can't spawn notification
            if (!CVRWorld.Instance.allowSpawnables) {
                if (ModConfig.MeSendHudSpawnNotAllowedNotification.Value)
                    ViewManager.Instance.NotifyUser("(Local) Client", "CompanionProp - This world doesn't allow props :c", 2f);
                return;
            }

            // Cancel previous
            if (spawning != null)
                MelonCoroutines.Stop(spawning);

            // Start the spawning coroutine
            spawning = MelonCoroutines.Start(DelaySpawnProp(instanceID));
        });
    }

    private IEnumerator DelaySpawnProp(string instanceIdToSpawn) {

        int delaySeconds = ModConfig.MeSpawnDelay.Value;

        MelonLogger.Msg($"Connected to instance: {instanceIdToSpawn}, spawning {ModConfig.MePropGuid.Value} in {delaySeconds} seconds...");

        yield return new WaitForSeconds(delaySeconds);

        // Double-check our settings
        if (!ShouldSpawnProp) yield break;

        // Ensure we're still on the same instance
        if (instanceIdToSpawn != MetaPort.Instance.CurrentInstanceId) {
            MelonLogger.Warning($"Prop started spawning when we were on the instance {instanceIdToSpawn}, but now we're on {MetaPort.Instance.CurrentInstanceId}. Ignoring...");
            yield break;
        }

        // Handle spawning notification
        if (ModConfig.MeSendHudSpawnNotification.Value)
            ViewManager.Instance.NotifyUser("(Local) Client", "CompanionProp - Spawning our prop c:", 2f);

        PlayerSetup.Instance.DropProp(ModConfig.MePropGuid.Value);

        MelonLogger.Msg($"Spawned our precious {ModConfig.MePropGuid.Value} prop!");
    }

    public static void CheckGuid(string guidValue, bool forceCheck) {
        if (!ModConfig.SpawnPropOnJoin.Value && !forceCheck) return;
        if (Guid.TryParse(guidValue, out _)) {
            MelonLogger.Msg($"Using the prop's guid to {guidValue}");
        }
        else {
            MelonLogger.Warning($"The current prop's guid \"{guidValue}\" is not valid. Ensure to input a valid guid on the configuration.");
        }
    }

    private bool ShouldSpawnProp => ModConfig.MeEnabled.Value && ModConfig.SpawnPropOnJoin.Value && Guid.TryParse(ModConfig.MePropGuid.Value, out _);


    [HarmonyPatch]
    internal class HarmonyPatches {

        private static bool MatchesOurProp(CVRSyncHelper.PropData prop) {
            return prop.SpawnedBy == MetaPort.Instance.ownerId && prop.ObjectId == ModConfig.MePropGuid.Value;
        }

        private static bool MatchesOurPropInstanceID(string propInstanceId) {
            return propInstanceId.StartsWith("p+" + ModConfig.MePropGuid.Value, StringComparison.OrdinalIgnoreCase);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.DeleteMyProps))]
        static void Before_CVRSyncHelper_DeleteMyProps(out (IEnumerable<CVRSyncHelper.PropData>, IEnumerable<string>) __state) {
            __state = default;

            // Ignore and execute the normal method like usual
            if (!ModConfig.MeEnabled.Value || !ModConfig.MePreventRemoveAllMyProps.Value) {
                return;
            }

            try {
                __state = (CVRSyncHelper.Props.Where(MatchesOurProp).ToArray(), CVRSyncHelper.MySpawnedPropInstanceIds.Where(MatchesOurPropInstanceID).ToArray());
                CVRSyncHelper.Props.RemoveAll(MatchesOurProp);
                CVRSyncHelper.MySpawnedPropInstanceIds.RemoveWhere(MatchesOurPropInstanceID);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error executing {nameof(Before_CVRSyncHelper_DeleteMyProps)} Patch");
                MelonLogger.Error(e);
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.DeleteMyProps))]
        static void After_CVRSyncHelper_DeleteMyProps((IEnumerable<CVRSyncHelper.PropData>, IEnumerable<string>) __state) {

            // Ignore and execute the normal method like usual
            if (!ModConfig.MeEnabled.Value || !ModConfig.MePreventRemoveAllMyProps.Value)
                return;

            try {
                CVRSyncHelper.Props.AddRange(__state.Item1);
                CVRSyncHelper.MySpawnedPropInstanceIds.UnionWith(__state.Item2);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error executing {nameof(After_CVRSyncHelper_DeleteMyProps)} Patch");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.DeleteAllProps))]
        static void Before_CVRSyncHelper_DeleteAllProps(out (IEnumerable<CVRSyncHelper.PropData>, IEnumerable<string>) __state) {
            __state = default;

            // Ignore and execute the normal method like usual
            if (!ModConfig.MeEnabled.Value || !ModConfig.MePreventRemoveAllProps.Value) {
                return;
            }

            try {
                __state = (CVRSyncHelper.Props.Where(MatchesOurProp).ToArray(), CVRSyncHelper.MySpawnedPropInstanceIds.Where(MatchesOurPropInstanceID).ToArray());
                CVRSyncHelper.Props.RemoveAll(MatchesOurProp);
                CVRSyncHelper.MySpawnedPropInstanceIds.RemoveWhere(MatchesOurPropInstanceID);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error executing {nameof(Before_CVRSyncHelper_DeleteAllProps)} Patch");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.DeleteAllProps))]
        static void After_CVRSyncHelper_DeleteAllProps((IEnumerable<CVRSyncHelper.PropData>, IEnumerable<string>) __state) {

            // Ignore and execute the normal method like usual
            if (!ModConfig.MeEnabled.Value || !ModConfig.MePreventRemoveAllProps.Value)
                return;

            try {
                CVRSyncHelper.Props.AddRange(__state.Item1);
                CVRSyncHelper.MySpawnedPropInstanceIds.UnionWith(__state.Item2);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error executing {nameof(After_CVRSyncHelper_DeleteAllProps)} Patch");
                MelonLogger.Error(e);
            }
        }
    }
}
