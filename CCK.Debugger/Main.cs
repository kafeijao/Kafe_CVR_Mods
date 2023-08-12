using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.CCK.Debugger.Components;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
using Kafe.CCK.Debugger.Properties;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger;

public class CCKDebugger : MelonMod {

    internal static bool TestMode;

    private const string CouiPath = @"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\CCKDebugger";
    private const string CouiManifestResourcePrefix = @"CCK.Debugger.Resources.UIResources.";

    public override void OnInitializeMelon() {

        // Melon Config
        Config.InitializeMelonPrefs();

        // Check if it is in debug mode (to test functionalities that are waiting for bios to be enabled)
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

        if (Config.MeOverwriteUIResources.Value) {
            // Copy the UI Resources from the assembly to the CVR Cohtml UI folder
            // Warning: The file and folder names inside of UIResources cannot contain dot characters "." nor "-" (except on
            // the extensions that must include a dot ".", and always require an extension, example "index.js"
            var fileNames = new List<string>();
            foreach (var manifestResourceName in MelonAssembly.Assembly.GetManifestResourceNames()) {
                if (!manifestResourceName.StartsWith(CouiManifestResourcePrefix)) continue;

                // Convert assembly resource namespace into a path
                var resourceName = manifestResourceName.Remove(0, CouiManifestResourcePrefix.Length);
                var resourceExtension = Path.GetExtension(manifestResourceName);
                var resourcePath = Path.GetFileNameWithoutExtension(resourceName).Replace('.', Path.DirectorySeparatorChar) + resourceExtension;
                fileNames.Add(resourcePath);
                var resourceFullPath = Path.Combine(CouiPath, resourcePath);

                // Create folder if doesn't exist and save into a file
                var directoryPath = Path.GetDirectoryName(resourceFullPath);
                Directory.CreateDirectory(directoryPath!);
                var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(manifestResourceName);
                if (resourceStream == null) {
                    var ex = $"Failed to find the Resource {manifestResourceName} in the Assembly.";
                    MelonLogger.Error($"Failed to find the Resource {manifestResourceName} in the Assembly.");
                    throw new Exception(ex);
                }
                using var resourceOutputFile = new FileStream(resourceFullPath, FileMode.Create);
                resourceStream.CopyTo(resourceOutputFile);
            }
            MelonLogger.Msg($"Loaded and saved all UI Resource files: {string.Join(", ", fileNames.ToArray())}");
        }
        else {
            MelonLogger.Msg("Skipping copying the Cohtml resources as define in the configuration... You should " +
                            "only see this message if you are manually editing the Cohtml UI Resources!");
        }

        // Check for BTKUILib
        if (RegisteredMelons.Any(m => m.Info.Name == AssemblyInfoParams.BTKUILibName)) {
            MelonLogger.Msg($"Detected BTKUILib mod, we're adding the integration!");
            Integrations.BTKUILibIntegration.InitializeBTKUI();
        }
        else {
            MelonLogger.Warning("BTKUILib is a required dependency. It allows to restore the menu to the quick menu.");
        }

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    public override void OnLateUpdate() {
        if (Input.GetKeyDown(KeyCode.F5)) Events.DebuggerMenuCohtml.OnCohtmlMenuReload();
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        // Avatar Destroyed
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.OnDestroy))]
        static void After_CVAvatar_OnDestroy(CVRAvatar __instance) {
            try {
                Events.Avatar.OnCVRAvatarDestroyed(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error("Error executing After_CVAvatar_OnDestroy Postfix...");
                MelonLogger.Error(e);
                throw;
            }
        }
        // Avatar Started
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.Start))]
        static void After_CVAvatar_Start(CVRAvatar __instance) {
            try {
                Events.Avatar.OnCVRAvatarStarted(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error("Error executing After_CVAvatar_Start Postfix...");
                MelonLogger.Error(e);
                throw;
            }
        }

        // Spawnable Destroyed
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
        static void After_CVRSpawnable_OnDestroy(CVRSpawnable __instance) {
            try {
                Events.Spawnable.OnCVRSpawnableDestroyed(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error("Error executing After_CVRSpawnable_OnDestroy Postfix...");
                MelonLogger.Error(e);
                throw;
            }
        }
        // Spawnable Started
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.Start))]
        static void After_CVRSpawnable_Start(CVRSpawnable __instance) {
            try {
                Events.Spawnable.OnCVRSpawnableStarted(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error("Error executing After_CVRSpawnable_Start Postfix...");
                MelonLogger.Error(e);
                throw;
            }
        }

        // Spawnables
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Spawnable_t), nameof(Spawnable_t.Recycle))]
        static void BeforeSpawnableDetailsRecycle(Spawnable_t __instance) {
            Events.Spawnable.OnSpawnableDetailsRecycled(__instance);
        }

        // Spawnable Trigger Task
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.ExecuteTrigger))]
        static void AfterSpawnableTriggerExecuted(CVRSpawnableTriggerTask __instance) {
            Events.Spawnable.OnSpawnableTriggerExecuted(__instance);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new Type[]{})]
        static void AfterSpawnableTriggerTriggeredByHelper(CVRSpawnableTriggerTask __instance) {
            Events.Spawnable.OnSpawnableTriggerTriggered(__instance);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTask), nameof(CVRSpawnableTriggerTask.Trigger))]
        [HarmonyPatch( argumentTypes: new[] { typeof(CVRPointer), typeof(bool), typeof(float), typeof(bool) })]
        static void AfterSpawnableTriggerTriggered(CVRSpawnableTriggerTask __instance, bool exit) {
            // Ignore the exit events on the enterTasks
            if (exit) return;
            Events.Spawnable.OnSpawnableTriggerTriggered(__instance);
        }
        // Spawnable Trigger Task Stay
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnableTriggerTaskStay), nameof(CVRSpawnableTriggerTaskStay.trigger))]
        static void AfterSpawnableStayTriggerTriggered(CVRSpawnableTriggerTaskStay __instance) {
            Events.Spawnable.OnSpawnableStayTriggerTriggered(__instance);
        }

        // Avatar
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.Recycle))]
        static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance) {
            Events.Avatar.OnAvatarDetailsRecycled(__instance);
        }

        // Avatar AAS Trigger Task
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask), nameof(CVRAdvancedAvatarSettingsTriggerTask.ExecuteTrigger))]
        static void AfterAasTriggerExecuted(CVRAdvancedAvatarSettingsTriggerTask __instance) {
            Events.Avatar.OnAasTriggerExecuted(__instance);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask), nameof(CVRAdvancedAvatarSettingsTriggerTask.Trigger))]
        [HarmonyPatch(argumentTypes: new Type[]{})]
        static void AfterAasTriggerTriggeredByHelper(CVRAdvancedAvatarSettingsTriggerTask __instance) {
            Events.Avatar.OnAasTriggerTriggered(__instance);
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTask), nameof(CVRAdvancedAvatarSettingsTriggerTask.Trigger))]
        [HarmonyPatch( argumentTypes: new[] { typeof(CVRPointer), typeof(bool), typeof(float), typeof(bool) })]
        static void AfterAasTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTask __instance, bool exit) {
            // Ignore the exit events on the enterTasks
            if (exit) return;
            Events.Avatar.OnAasTriggerTriggered(__instance);
        }
        // Avatar AAS Trigger Task Stay
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettingsTriggerTaskStay), nameof(CVRAdvancedAvatarSettingsTriggerTaskStay.trigger))]
        static void AfterAasStayTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTaskStay __instance) {
            Events.Avatar.OnAasStayTriggerTriggered(__instance);
        }

        // Player
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateAnimatorManager))]
        static void AfterUpdateAnimatorManager(CVRAnimatorManager manager) {
            Events.Avatar.OnAnimatorManagerUpdate(manager);
        }

        //Quick Menu
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        private static void AfterMenuToggle(bool show) {
            Events.QuickMenu.OnQuickMenuIsShownChanged(show);
        }

        // Players
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void BeforeCreatePlayer(ref CVRPlayerManager __instance, out int __state) {
            __state = __instance.NetworkPlayers.Count;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void AfterCreatePlayer(ref CVRPlayerManager __instance, int __state) {
            if(__state < __instance.NetworkPlayers.Count) {
                var player = __instance.NetworkPlayers[__state];
                Events.Player.OnPlayerLoaded(player.Uuid, player.Username);
            }
        }

        // Scene
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        private static void AfterMenuCreated(ref CVR_MenuManager __instance) {

            try {
                // Initialize the CCK Debugger Cohtml menu
                CohtmlMenuController.Create(__instance.quickMenu.gameObject);
            }
            catch (Exception e) {
                MelonLogger.Error("Error executing After_CVR_MenuManager_InitializeQuickMenu Postfix...");
                MelonLogger.Error(e);
                throw;
            }

            // Add ourselves to the player list (why not here xd)
            Events.Player.OnPlayerLoaded(MetaPort.Instance.ownerId, AuthManager.username);
        }
    }
}
