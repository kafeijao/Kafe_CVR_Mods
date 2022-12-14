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
    private static MelonPreferences_Entry<bool> _melonEntryEnhancedMode;
    private static MelonPreferences_Entry<bool> _melonEntrySingleViseme;
    private static MelonPreferences_Entry<bool> _melonEntrySingleVisemeOriginalVolume;
    internal static MelonPreferences_Entry<bool> MelonEntryMultithreading;

    private static readonly Dictionary<string, GameObject> PlaybackGameObjects = new();
    private static readonly Dictionary<string, CVRVisemeController> VisemeControllers = new();

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterLipsync));

        _melonEntryEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether this mod will be changing the visemes or not.");

        _melonEntrySmoothing = _melonCategory.CreateEntry("VisemeSmoothing", 50,
            description: "How smooth should the viseme transitions be [0, 100] where 100 is maximum smoothing. " +
                         "Requires EnhancedMode activated to work.",
            validator: new IntValidator(0, 100));

        _melonEntryEnhancedMode = _melonCategory.CreateEntry("EnhancedMode", true,
            description: "Where to use enhanced mode or original, original doesn't have smoothing but is more performant.");

        _melonEntrySingleViseme = _melonCategory.CreateEntry("SingleViseme", false,
            description: "Whether to set the viseme closest to the current phoneme to the max weight (true), or set " +
                         "all the visemes for their corresponding weight (false). Having this set to false might " +
                         "lead to less performance, as it will result in several visemes active at the same time, " +
                         "it might look better...");

        _melonEntrySingleVisemeOriginalVolume = _melonCategory.CreateEntry("SingleVisemeOriginalVolume", false,
            description: "Whether to use the original CVR viseme volume detection (how much the mouth opens) when " +
                         "talking (true), or use the Oculus Lipsync highest viseme level (false).");

        MelonEntryMultithreading = _melonCategory.CreateEntry("UseMultithreading", true,
            description: "Whether or not to process the Lipsync audio using multithreading.");


        // Extract the native binary to the plugins folder
        const string dllName = "OVRLipSync.dll";
        const string dstPath = "ChilloutVR_Data/Plugins/x86_64/" + dllName;

        try {
            MelonLogger.Msg($"Copying the OVRLipSync.dll to ChilloutVR_Data/Plugins/ ...");
            using var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(dllName);
            using var fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
            resourceStream!.CopyTo(fileStream);
        }
        catch (IOException ex) {
            MelonLogger.Error("Failed to copy native library: " + ex.Message);
        }
    }

    private static void CreateLipsyncContext(string playerGuid) {

        var isLocalPlayer = playerGuid == MetaPort.Instance.ownerId;

        // Check if the current player guid was initialized and currently exists, otherwise ignore
        if (!VisemeControllers.ContainsKey(playerGuid) || VisemeControllers[playerGuid] == null ||
            (!isLocalPlayer && (!PlaybackGameObjects.ContainsKey(playerGuid) || PlaybackGameObjects[playerGuid] == null))) {
            return;
        }

        // Pick the target to add our lipsync component
        GameObject target;
        if (isLocalPlayer) {
            target = VisemeControllers[playerGuid].gameObject;
        }
        else {
            if (!PlaybackGameObjects.ContainsKey(playerGuid)) {
                MelonLogger.Error($"Attempted to initialize lipsync for player {playerGuid}, but the " +
                                  $"playback game object was not initialized yet...");
            }
            target = PlaybackGameObjects[playerGuid];
        }

        // Create context if doesn't exist
        if (!target.TryGetComponent(out CVRLipSyncContext context)) {
            context = target.AddComponent<CVRLipSyncContext>();
        }

        // Initialize context
        // var playerName = (isLocalPlayer ? "Local " : "") + $"player {___playerGuid}";
        // MelonLogger.Msg($"Initializing CVRLipSyncContext for {playerName}");
        context.Initialize(VisemeControllers[playerGuid], target, isLocalPlayer, playerGuid);

        // Update smoothing value
        context.Smoothing = _melonEntrySmoothing.Value;
        _melonEntrySmoothing.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) context.Smoothing = newValue;
        });

        // Update the mode settings
        context.provider = _melonEntryEnhancedMode.Value
            ? OVRLipSync.ContextProviders.Enhanced
            : OVRLipSync.ContextProviders.Original;
        _melonEntryEnhancedMode.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) context.provider = newValue
                ? OVRLipSync.ContextProviders.Enhanced
                : OVRLipSync.ContextProviders.Original;
        });

        // Update whether is enabled or not
        context.Enabled = _melonEntryEnabled.Value;
        _melonEntryEnabled.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) context.Enabled = newValue;
        });

        // Update whether a single viseme should be used or not
        context.SingleViseme = _melonEntrySingleViseme.Value;
        _melonEntrySingleViseme.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) context.SingleViseme = newValue;
        });

        // Update whether a single viseme original volume or the oculus loudest viseme
        context.SingleVisemeOriginalVolume = _melonEntrySingleVisemeOriginalVolume.Value;
        _melonEntrySingleVisemeOriginalVolume.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) context.SingleVisemeOriginalVolume = newValue;
        });

        if (!isLocalPlayer) return;
        // Handle local player specifics
        // Add the dissonance lip sync subscriber to the local player
        if (!target.TryGetComponent(out CVRMicLipsyncSubscriber lipsyncSubscriber)) {
            if (RootLogic.Instance.comms.MicrophoneCapture == null) {
                MelonLogger.Error("Attempted to initialize the microphone subscriber, but the MicrophoneCapture " +
                                  "was null...");
                return;
            }
            lipsyncSubscriber = target.AddComponent<CVRMicLipsyncSubscriber>();
            RootLogic.Instance.comms.MicrophoneCapture.Subscribe(lipsyncSubscriber);
        }
        lipsyncSubscriber.Initialize(context);

        // Fetch the local player voice state, remote voice player states will be fetched when they speak
        var localPlayerVoiceState = RootLogic.Instance.comms.Players.FirstOrDefault(state => state.IsLocalPlayer);
        if (localPlayerVoiceState == null) {
            MelonLogger.Error("Attempted to fetch local player voice state, but it was null?");
            return;
        }
        context.CurrentVoicePlayerState = localPlayerVoiceState;
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
                    PlaybackGameObjects[state.Name] = playbackGameObject;

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
                VisemeControllers[___playerGuid] = __instance;

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
