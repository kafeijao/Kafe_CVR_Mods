using System.Collections;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.NavMeshFollower.CCK;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshFollower;

public class NavMeshFollower : MelonMod {

    internal static readonly Dictionary<string, Vector3> PlayerViewpoints = new();

    internal static bool TestMode;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        ModConfig.InitializeBTKUI();

        FollowerController.Initialize();

        // Add our CCK script because duh
        SharedFilter._spawnableWhitelist.Add(typeof(FollowerInfo));

        // Check if it is in debug mode
        // Keeping it hard-ish to enable so people don't abuse it
        foreach (var commandLineArg in Environment.GetCommandLineArgs()) {
            if (!commandLineArg.StartsWith("--cck-debugger-test=")) continue;
            var input = commandLineArg.Split(new[] { "=" }, StringSplitOptions.None)[1];
            using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            var sb = new System.Text.StringBuilder();
            foreach (var t in hashBytes) sb.Append(t.ToString("X2"));
            TestMode = sb.ToString().Equals("738A9A4AD5E2F8AB10E702D44C189FA8");
            if (TestMode) MelonLogger.Msg("Test Mode is ENABLED!");
        }

        // #if DEBUG
        // CVRGameEventSystem.Instance.OnConnected.AddListener(instanceID => {
        //     if (!CVRWorld.Instance.allowSpawnables || AuthManager.username != "Kafeijao") return;
        //     MelonLogger.Msg($"Connected to instance: {instanceID} Spawning in one seconds...");
        //     IEnumerator DelaySpawnProp() {
        //         yield return new WaitForSeconds(3f);
        //         PlayerSetup.Instance.DropProp("13cbe183-1fd5-4c7e-ad5d-04c102a79f74");
        //     }
        //     MelonCoroutines.Start(DelaySpawnProp());
        // });
        // #endif
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.LateUpdate))]
        public static void After_PlayerSetup_LateUpdate(PlayerSetup __instance) {
            // Save view point positions. This late update runs after VRIK, so all viewpoints should be gucci
            try {
                // Save player's viewpoints
                if (PlayerViewpoints.Count != CVRPlayerManager.Instance.NetworkPlayers.Count + 1) PlayerViewpoints.Clear();
                PlayerViewpoints[MetaPort.Instance.ownerId] = PlayerSetup.Instance._viewPoint.GetPointPosition();
                foreach (var player in CVRPlayerManager.Instance.NetworkPlayers) {
                    PlayerViewpoints[player.Uuid] = player.PuppetMaster._viewPoint.GetPointPosition();
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_LateUpdate)}.");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.FixedUpdate))]
        public static void Before_CVRSpawnable_FixedUpdate(CVRSpawnable __instance) {
            // Update the synced values driven by UpdateBy so it's independent from our controls
            try {

                // Ignore props that are not grabbed by me (when followers grab it's as if I was grabbing)
                if (__instance.pickup == null || !__instance.pickup.IsGrabbedByMe()) return;

                foreach (var availableSpawnablePickup in Pickups.AvailableSpawnablePickups) {

                    // Ignore if spawnable doesn't include this pickup, or doesn't have updated by owner sync values
                    if (availableSpawnablePickup.Spawnable != __instance || !availableSpawnablePickup.HasUpdatedByOwnerSyncValues) continue;

                    // Ignore if this pickup is not grabbed by a follower
                    if (!availableSpawnablePickup.GrabbedByFollower(out var controller)) continue;

                    var index = -1;
                    foreach (var syncValue in __instance.syncValues) {
                        ++index;

                        // Ignore non-owner updated by
                        if (!Pickups.SpawnablePickupWrapper.OwnerUpdatedBy.Contains(syncValue.updatedBy)) continue;

                        // Set the value as 0 if it's not defined, otherwise use the overriden value
                        var num = !controller.UpdatedByValues.TryGetValue(syncValue.updatedBy, out var overrideValue) ? 0f : overrideValue;

                        // Process the updated method
                        switch (syncValue.updateMethod) {
                            case CVRSpawnableValue.UpdateMethod.AddToDefault:
                                num = syncValue.startValue + num;
                                break;
                            case CVRSpawnableValue.UpdateMethod.AddToCurrent:
                                num = syncValue.currentValue + num;
                                break;
                            case CVRSpawnableValue.UpdateMethod.SubtractFromDefault:
                                num = syncValue.startValue - num;
                                break;
                            case CVRSpawnableValue.UpdateMethod.SubtractFromCurrent:
                                num = syncValue.currentValue - num;
                                break;
                            case CVRSpawnableValue.UpdateMethod.MultiplyWithDefault:
                                num = syncValue.startValue * num;
                                break;
                            case CVRSpawnableValue.UpdateMethod.DefaultDividedByCurrent:
                                // Prevent division by 0
                                if (num == 0.0) continue;
                                num = syncValue.startValue / num;
                                break;
                        }

                        // Update the value on the animator if it's not equal to the current one
                        if (!Mathf.Approximately(num, syncValue.currentValue)) {
                            __instance.needsUpdate = true;
                            __instance.UpdateMultiPurposeFloat(syncValue, num, index);
                        }
                    }
                }

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_CVRSpawnable_FixedUpdate)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
