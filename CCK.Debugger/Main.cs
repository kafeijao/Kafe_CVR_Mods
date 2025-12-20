using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Player.EyeMovement;
using ABI_RC.Core.Player.EyeMovement.Targets;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using ABI_RC.Systems.RuntimeDebug;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.CCK.Debugger.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger;

public class CCKDebugger : MelonMod
{
    internal static bool TestMode;

    public override void OnInitializeMelon()
    {
        // Melon Config
        ModConfig.InitializeMelonPrefs();

        // Load assembly resources
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

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

        Integrations.UILib.InitializeUILib();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    public override void OnUpdate()
    {
        if (CVRInputManager.Instance != null && CVRInputManager.Instance.reload)
            Events.DebuggerMenuCohtml.OnCohtmlMenuReload();
    }

    [HarmonyPatch]
    internal class HarmonyPatches
    {
        // Avatar Destroyed
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.OnDestroy))]
        private static void After_CVAvatar_OnDestroy(CVRAvatar __instance)
        {
            try
            {
                Events.Avatar.OnCVRAvatarDestroyed(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVAvatar_OnDestroy)} Postfix...", e);
            }
        }

        // Avatar Started
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.Start))]
        private static void After_CVAvatar_Start(CVRAvatar __instance)
        {
            try
            {
                Events.Avatar.OnCVRAvatarStarted(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVAvatar_Start)} Postfix...", e);
            }
        }

        // Spawnable Destroyed
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
        private static void After_CVRSpawnable_OnDestroy(CVRSpawnable __instance)
        {
            try
            {
                Events.Spawnable.OnCVRSpawnableDestroyed(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVRSpawnable_OnDestroy)} Postfix...", e);
            }
        }

        // Spawnable Started
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.Start))]
        private static void After_CVRSpawnable_Start(CVRSpawnable __instance)
        {
            try
            {
                Events.Spawnable.OnCVRSpawnableStarted(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVRSpawnable_Start)} Postfix...", e);
            }
        }

        // Spawnables
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Spawnable_t), MethodType.Constructor)]
        private static void After_Spawnable_t_Constructor(Spawnable_t __instance)
        {
            try
            {
                Events.Spawnable.SpawnableNamesCache[__instance.SpawnableId] = __instance.SpawnableName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_Spawnable_t_Constructor)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnableDetail_t), nameof(SpawnableDetail_t.PopulateFromApiResponse))]
        private static void After_SpawnableDetail_t_PopulateFromApiResponse(SpawnableDetail_t __instance)
        {
            try
            {
                Events.Spawnable.SpawnableNamesCache[__instance.SpawnableId] = __instance.SpawnableName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_SpawnableDetail_t_PopulateFromApiResponse)} patch", e);
            }
        }

        // Spawnable Trigger Task
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.ExecuteTrigger))]
        private static void AfterSpawnableTriggerExecuted(CVRSpawnableTriggerTask __instance)
        {
            try
            {
                Events.Spawnable.OnSpawnableTriggerExecuted(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterSpawnableTriggerExecuted)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new Type[] { })]
        private static void AfterSpawnableTriggerTriggeredByHelper(CVRSpawnableTriggerTask __instance)
        {
            try
            {
                Events.Spawnable.OnSpawnableTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterSpawnableTriggerTriggeredByHelper)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new[] { typeof(CVRPointer), typeof(bool), typeof(float), typeof(bool) })]
        private static void AfterSpawnableTriggerTriggered(CVRSpawnableTriggerTask __instance, bool exit)
        {
            try
            {
                // Ignore the exit events on the enterTasks
                if (exit) return;
                Events.Spawnable.OnSpawnableTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterSpawnableTriggerTriggered)} patch", e);
            }
        }

        // Spawnable Trigger Task Stay
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTaskStay), nameof(CVRSpawnableTriggerTaskStay.trigger))]
        private static void AfterSpawnableStayTriggerTriggered(CVRSpawnableTriggerTaskStay __instance)
        {
            try
            {
                Events.Spawnable.OnSpawnableStayTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterSpawnableStayTriggerTriggered)} patch", e);
            }
        }

        // Avatar
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Avatar_t), MethodType.Constructor)]
        private static void After_Avatar_t_Constructor(Avatar_t __instance)
        {
            try
            {
                Events.Avatar.AvatarsNamesCache[__instance.AvatarId] = __instance.AvatarName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_Avatar_t_Constructor)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.PopulateFromApiResponse))]
        private static void After_AvatarDetails_t_PopulateFromApiResponse(AvatarDetails_t __instance)
        {
            try
            {
                Events.Avatar.AvatarsNamesCache[__instance.AvatarId] = __instance.AvatarName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_AvatarDetails_t_PopulateFromApiResponse)} patch", e);
            }
        }

        // Avatar AAS Trigger Task
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask),
            nameof(CVRAdvancedAvatarSettingsTriggerTask.ExecuteTrigger))]
        private static void AfterAasTriggerExecuted(CVRAdvancedAvatarSettingsTriggerTask __instance)
        {
            try
            {
                Events.Avatar.OnAasTriggerExecuted(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterAasTriggerExecuted)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask),
            nameof(CVRAdvancedAvatarSettingsTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new Type[] { })]
        private static void AfterAasTriggerTriggeredByHelper(CVRAdvancedAvatarSettingsTriggerTask __instance)
        {
            try
            {
                Events.Avatar.OnAasTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterAasTriggerTriggeredByHelper)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask),
            nameof(CVRAdvancedAvatarSettingsTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new[] { typeof(CVRPointer), typeof(bool), typeof(float), typeof(bool) })]
        private static void AfterAasTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTask __instance, bool exit)
        {
            try
            {
                // Ignore the exit events on the enterTasks
                if (exit) return;
                Events.Avatar.OnAasTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterAasTriggerTriggered)} patch", e);
            }
        }

        // Avatar AAS Trigger Task Stay
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTaskStay),
            nameof(CVRAdvancedAvatarSettingsTriggerTaskStay.trigger))]
        private static void AfterAasStayTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTaskStay __instance)
        {
            try
            {
                Events.Avatar.OnAasStayTriggerTriggered(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterAasStayTriggerTriggered)} patch", e);
            }
        }

        // Worlds
        [HarmonyPostfix]
        [HarmonyPatch(typeof(World_t), MethodType.Constructor)]
        private static void After_World_t_Constructor(World_t __instance)
        {
            try
            {
                Events.World.WorldNamesCache[__instance.WorldId] = __instance.WorldName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_World_t_Constructor)} patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldDetails_t), nameof(WorldDetails_t.PopulateFromApiResponse))]
        private static void After_SpawnableDetail_t_PopulateFromApiResponse(WorldDetails_t __instance)
        {
            try
            {
                Events.World.WorldNamesCache[__instance.WorldId] = __instance.WorldName;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(After_SpawnableDetail_t_PopulateFromApiResponse)} patch", e);
            }
        }

        // Worlds
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.SetupWorldRules))]
        private static void Before_CVRWorld_SetupWorldRules()
        {
            try
            {
                Events.World.OnWorldFinishedConfiguration();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(Before_CVRWorld_SetupWorldRules)} patch", e);
            }
        }

        // Player
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BetterBetterCharacterController), nameof(BetterBetterCharacterController.AvatarAnimatorManager), MethodType.Setter)]
        private static void AfterUpdateAnimatorManager(BetterBetterCharacterController __instance)
        {
            try
            {
                Events.Avatar.OnAnimatorManagerUpdate(__instance.AvatarAnimatorManager);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterUpdateAnimatorManager)} patch", e);
            }
        }

        // Players
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void BeforeCreatePlayer(ref CVRPlayerManager __instance, out int __state)
        {
            try
            {
                __state = __instance.NetworkPlayers.Count;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(BeforeCreatePlayer)} patch", e);
                __state = -1;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void AfterCreatePlayer(ref CVRPlayerManager __instance, int __state)
        {
            try
            {
                if (__state < __instance.NetworkPlayers.Count && __state != -1)
                {
                    var player = __instance.NetworkPlayers[__state];
                    Events.Player.OnPlayerLoaded(player.Uuid, player.Username);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(AfterCreatePlayer)} patch", e);
            }
        }

        // Scene
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        private static void AfterMenuCreated(ref CVR_MenuManager __instance)
        {
            try
            {
                // Initialize the CCK Debugger Cohtml menu
                CohtmlMenuController.Create(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(AfterMenuCreated)} Postfix...", e);
            }

            // Add ourselves to the player list (why not here xd)
            Events.Player.OnPlayerLoaded(MetaPort.Instance.ownerId, AuthManager.Username);
        }

        public static bool EnabledEyeVisualizers;

        // EyeMovement
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EyeMovementController), nameof(EyeMovementController.LateUpdate))]
        private static void BeforeEyeMovementControllerLateUpdate(ref EyeMovementController __instance)
        {
            try
            {
                if (EnabledEyeVisualizers && __instance.IsLocal)
                {
                    foreach (EyeMovementTarget candidate in EyeMovementControllerManager.TargetCandidates)
                    {
                        RuntimeGizmos.DrawSphere(candidate.GetPosition(), 0.015f,
                            candidate == __instance.CurrentTarget ? Color.green : Color.blue, CVRLayers.UIInternal, 1f);
                        RuntimeGizmos.DrawText(candidate.GetPosition() + Vector3.up * 0.2f,
                            $"{candidate} - {(__instance._nextTargetTimeSeconds - Time.time):F1}", 0.025f,
                            candidate == __instance.CurrentTarget ? Color.green : Color.blue, CVRLayers.UIInternal);
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(BeforeEyeMovementControllerLateUpdate)} Postfix...", e);
            }
        }
    }
}
