using System;
using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine;

namespace BetterLipsync;

public class BetterLipsync : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    private static MelonPreferences_Entry<bool> _melonEntryEnabled;
    private static MelonPreferences_Entry<int> _melonEntrySmoothing;
    private static MelonPreferences_Entry<int> _melonEntrySkipFrames;
    private static MelonPreferences_Entry<bool> _melonEntryEnhancedMode;

    private static Dictionary<string, GameObject> _playbackGameObjects = new();
    private static Dictionary<string, CVRVisemeController> _visemeControllers = new();

    public override void OnApplicationStart() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterLipsync));

        _melonEntryEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether this mod will be changing the visemes or not.");

        _melonEntrySmoothing = _melonCategory.CreateEntry("VisemeSmoothing", 70,
            description: "How smooth should the viseme transitions be [0, 100] where 100 is maximum smoothing. " +
                         "Requires EnhancedMode activated to work.",
            validator: new IntValidator(0, 100));

        _melonEntrySkipFrames = _melonCategory.CreateEntry("CalculateVisemesEveryXFrame", 1,
            description: "How many frames to skip between viseme checks [1,25], skipping more = more performance.",
            validator: new IntValidator(1, 25));

        _melonEntryEnhancedMode = _melonCategory.CreateEntry("EnhancedMode", true,
            description: "Where to use enhanced mode or original, original doesn't have smoothing but is more performant.");


        // Extract the native binary to the plugins folder
        var dllName = "OVRLipSync.dll";
        var dstPath = "ChilloutVR_Data/Plugins/x86_64/" + dllName;

        try {
            MelonLogger.Msg($"Copying the OVRLipSync.dll to ChilloutVR_Data/Plugins/ ...");
            using var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(dllName);
            using var fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
            resourceStream.CopyTo(fileStream);
        }
        catch (IOException ex) {
            MelonLogger.Error("Failed to copy native library: " + ex.Message);
        }
    }

    private static void CreateLipsyncContext(string playerGuid) {

        var isLocalPlayer = playerGuid == MetaPort.Instance.ownerId;

        // Check if the current player guid was initialized and currently exists, otherwise ignore
        if (!_visemeControllers.ContainsKey(playerGuid) || _visemeControllers[playerGuid] == null ||
            (!isLocalPlayer && (!_playbackGameObjects.ContainsKey(playerGuid) || _playbackGameObjects[playerGuid] == null))) {
            return;
        }

        // Pick the target to add our lipsync component
        GameObject target;
        if (isLocalPlayer) {
            target = _visemeControllers[playerGuid].gameObject;
        }
        else {
            if (!_playbackGameObjects.ContainsKey(playerGuid)) {
                MelonLogger.Error($"Attempted to initialize lipsync for player {playerGuid}, but the " +
                                  $"playback game object was not initialized yet...");
            }
            target = _playbackGameObjects[playerGuid];
        }

        // Create context if doesn't exist
        if (!target.TryGetComponent(out CVRLipSyncContext context)) {
            context = target.AddComponent<CVRLipSyncContext>();
            // The original provider seems to be more optimized
            // context.provider = OVRLipSync.ContextProviders.Original;
        }

        // Initialize context
        // var playerName = (isLocalPlayer ? "Local " : "") + $"player {___playerGuid}";
        // MelonLogger.Msg($"Initializing CVRLipSyncContext for {playerName}");
        context.Initialize(_visemeControllers[playerGuid], target, isLocalPlayer, playerGuid);

        // Update smoothing value
        context.Smoothing = _melonEntrySmoothing.Value;
        _melonEntrySmoothing.OnValueChangedUntyped += () => context.Smoothing = _melonEntrySmoothing.Value;

        // Update skip frame value
        context.UpdateVisemeFrameSkip = _melonEntrySkipFrames.Value;
        _melonEntrySkipFrames.OnValueChangedUntyped += () => context.UpdateVisemeFrameSkip = _melonEntrySkipFrames.Value;

        // Update the mode settings
        context.provider = _melonEntryEnhancedMode.Value
            ? OVRLipSync.ContextProviders.Enhanced
            : OVRLipSync.ContextProviders.Original;
        _melonEntryEnhancedMode.OnValueChangedUntyped += () => context.provider = _melonEntryEnhancedMode.Value
            ? OVRLipSync.ContextProviders.Enhanced
            : OVRLipSync.ContextProviders.Original;

        // Update whether is enabled or not
        context.Enabled = _melonEntryEnabled.Value;
        _melonEntryEnabled.OnValueChangedUntyped += () => context.Enabled = _melonEntryEnabled.Value;

        // Add the dissonance lip sync subscriber to the local player
        if (isLocalPlayer) {
            if (!target.TryGetComponent(out CVRMicLipsyncSubscriber lipsyncSubscriber)) {
                lipsyncSubscriber = target.AddComponent<CVRMicLipsyncSubscriber>();
                RootLogic.Instance.comms.MicrophoneCapture.Subscribe(lipsyncSubscriber);
            }
            lipsyncSubscriber.Initialize(context);
        }
    }


    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), "Start")]
        private static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {
                // Add the lip sync instance to the scene
                __instance.gameObject.AddComponent<OVRLipSync>();
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RootLogic), "Start")]
        private static void After_RootLogic_Start() {
            try {
                // When a new playback game object is created/enabled save it associated to its user id
                RootLogic.Instance.comms.OnPlayerJoinedSession += state => {
                    // MelonLogger.Msg($"The player {state.Name} has joined the comms session!");

                    var playbackGameObject = ((Component)state.Playback)?.gameObject;

                    // Cache playback game object using userid (state name)
                    _playbackGameObjects[state.Name] = playbackGameObject;

                    CreateLipsyncContext(state.Name);
                };
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRVisemeController), "Start")]
        private static void After_CVRVisemeController_Start(CVRVisemeController __instance, string ___playerGuid) {
            try {
                //MelonLogger.Msg($"The player {___playerGuid} has initialized a CVRVisemeController!");

                // Cache playback game object using userid (state name)
                _visemeControllers[___playerGuid] = __instance;

                CreateLipsyncContext(___playerGuid);
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRVisemeController), "LateUpdate")]
        private static bool Before_CVRVisemeController_LateUpdate() {
            // Skip the original function if the mod is enabled
            return !_melonEntryEnabled.Value;
        }
    }

    private class IntValidator : ValueValidator {
        private int _min;
        private int _max;
        public IntValidator(int min, int max) {
            _min = min;
            _max = max;
        }
        public override bool IsValid(object value) {
            return value is int intValue && intValue >= _min && intValue <= _max;
        }
        public override object EnsureValid(object value) {
            if (value is int intValue) {
                return Math.Min(Math.Max(intValue, 0), 100);
            }
            return null;
        }
    }
}
