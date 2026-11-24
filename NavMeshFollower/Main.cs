using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.SyncedObjects;
using ABI.CCK.Components;
using GameCommon.Models.Sync;
using HarmonyLib;
using Kafe.NavMeshFollower.CCK;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower;

public class NavMeshFollower : MelonMod
{
    internal static readonly Dictionary<string, Vector3> PlayerViewpoints = new();

    internal static bool TestMode;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();

        ModConfig.SetupBTKUI(MelonAssembly.Assembly);

        FollowerController.Initialize();

        // Runs after every LateUpdate, so the IK already posed the players and the followers
        CVRFrameDriver.OnPostLateUpdate += SavePlayerViewpoints;
        CVRFrameDriver.OnPostLateUpdate += FollowerController.UpdateCarriedPickups;

        // Calling on melon initializing was causing issues sometimes
        CVRGameEventSystem.Initialization.OnPlayerSetupStart.AddListener(() =>
        {
            MelonLogger.Msg($"Adding {nameof(NavMeshFollower)} components to the whitelist");

            // Add our CCK script because duh
            SharedFilter.SpawnableWhitelist.Add(typeof(FollowerInfo));
        });

        // Check if it is in debug mode
        // Keeping it hard-ish to enable so people don't abuse it
        foreach (var commandLineArg in Environment.GetCommandLineArgs())
        {
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
        //     if (!CVRWorld.Instance.allowSpawnables || AuthManager.Username != "Kafeijao") return;
        //     MelonLogger.Msg($"Connected to instance: {instanceID} Spawning in one seconds...");
        //     IEnumerator DelaySpawnProp() {
        //         yield return new WaitForSeconds(3f);
        //         PlayerSetup.Instance.DropProp("13cbe183-1fd5-4c7e-ad5d-04c102a79f74");
        //     }
        //     MelonCoroutines.Start(DelaySpawnProp());
        // });
        // #endif
    }

    /// <summary>
    /// Saves where every player's view point is this frame
    /// </summary>
    private static void SavePlayerViewpoints()
    {
        try
        {
            // Save player's viewpoints
            if (PlayerViewpoints.Count != CVRPlayerManager.Instance.NetworkPlayers.Count + 1)
                PlayerViewpoints.Clear();
            PlayerViewpoints[MetaPort.Instance.ownerId] = PlayerSetup.Instance.ViewPoint.GetPointPosition();
            foreach (var player in CVRPlayerManager.Instance.NetworkPlayers)
            {
                if (!player.PuppetMaster.IsAvatarLoaded)
                    continue; // GetPointPosition can have the fallback transform null on first frame
                PlayerViewpoints[player.Uuid] = player.PuppetMaster.ViewPoint.GetPointPosition();
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(SavePlayerViewpoints)}.", e);
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SyncedObjectManager), nameof(SyncedObjectManager.ClaimFor))]
        public static void After_SyncedObjectManager_ClaimFor(SyncedObjectEntry entry, int slotIndex, ushort us, ref byte __result)
        {
            try
            {
                // Impulse is the strongest claim nobody chose, so only a deliberate hold can take a carried pickup off us
                if (__result >= (byte)SyncType.Impulse) return;

                var kind = entry.Layout.Slots[slotIndex].Kind;
                if (kind != SyncSlotKind.RootTransform && kind != SyncSlotKind.SubTransform) return;
                if (!FollowerController.CarriedTransforms.Contains(entry.Bindings[slotIndex].Transform)) return;
                if (SyncedObjectManager.IsHeldByThem(entry, slotIndex, us)) return;

                __result = (byte)SyncType.Impulse;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_SyncedObjectManager_ClaimFor)}.");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.FixedUpdate))]
        public static void Before_CVRSpawnable_FixedUpdate(CVRSpawnable __instance)
        {
            // Update the synced values driven by UpdateBy so it's independent from our controls
            try
            {
                // Followers can only carry props that have a pickup
                if (__instance.pickup == null) return;

                foreach (var availableSpawnablePickup in Pickups.AvailableSpawnablePickups)
                {
                    // Ignore if spawnable doesn't include this pickup, or doesn't have updated by owner sync values
                    if (availableSpawnablePickup.Spawnable != __instance ||
                        !availableSpawnablePickup.HasUpdatedByOwnerSyncValues) continue;

                    // Ignore if this pickup is not grabbed by a follower
                    if (!availableSpawnablePickup.GrabbedByFollower(out var controller)) continue;

                    var index = -1;
                    foreach (var syncValue in __instance.syncValues)
                    {
                        ++index;

                        // Ignore non-owner updated by
                        if (!Pickups.SpawnablePickupWrapper.OwnerUpdatedBy.Contains(syncValue.updatedBy)) continue;

                        // Set the value as 0 if it's not defined, otherwise use the overriden value
                        var num = !controller.UpdatedByValues.TryGetValue(syncValue.updatedBy, out var overrideValue)
                            ? 0f
                            : overrideValue;

                        // Process the updated method
                        switch (syncValue.updateMethod)
                        {
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
                        if (!Mathf.Approximately(num, syncValue.currentValue))
                        {
                            // The game wrote its own value this step, drop it so it doesn't get synced as a quick flicker
                            syncValue.ClearHeld();
                            __instance.SetValue(index, num);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_CVRSpawnable_FixedUpdate)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
