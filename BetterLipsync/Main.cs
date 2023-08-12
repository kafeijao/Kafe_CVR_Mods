using System.Collections;
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
    internal static MelonPreferences_Entry<int> melonEntrySmoothing;
    private static MelonPreferences_Entry<bool> _melonEntryEnhancedMode;
    private static MelonPreferences_Entry<bool> _melonEntrySingleViseme;
    private static MelonPreferences_Entry<bool> _melonEntrySingleVisemeOriginalVolume;
    internal static MelonPreferences_Entry<bool> MelonEntryMultithreading;

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterLipsync));

        _melonEntryEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether this mod will be changing the visemes or not.");

        melonEntrySmoothing = _melonCategory.CreateEntry("VisemeSmoothing", 50,
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

#if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
#endif

    }

    private static IEnumerator InitializeLocalPlayerAudio(CVRLipSyncContext context) {

        // Wait for microphone capture
        while (RootLogic.Instance.comms.MicrophoneCapture == null) yield return null;

        #if DEBUG
        MelonLogger.Msg("Found the MicrophoneCapture! Creating mic subscriber and adding the local player voice state...");
        #endif

        // Create the mic listener for the local player, since we have no audio source
        var lipsyncSubscriber = RootLogic.Instance.comms.gameObject.AddComponent<CVRMicLipsyncSubscriber>();
        RootLogic.Instance.comms.MicrophoneCapture.Subscribe(lipsyncSubscriber);
        lipsyncSubscriber.SetContext(context);

        // Fetch the local player voice state, remote voice player states will be fetched when they speak
        var localPlayerVoiceState = RootLogic.Instance.comms.Players.FirstOrDefault(state => state.IsLocalPlayer);
        context.CurrentVoicePlayerState = localPlayerVoiceState;
    }

    private static CVRLipSyncContext CreateLipsyncContext(string playerGuid, GameObject target) {

        var isLocalPlayer = playerGuid == MetaPort.Instance.ownerId;

        // We require an audio source for remote players
        if (!isLocalPlayer && !target.TryGetComponent(out AudioSource _)) {
            MelonLogger.Error($"Attempted to initialized a Lip Sync module on a remote player without an audio source.");
        }

        #if DEBUG
        MelonLogger.Msg($"Creating a lipsync context for {playerGuid}. IsLocalPlayer: {isLocalPlayer}");
        #endif

        // Create context
        if (!target.TryGetComponent(out CVRLipSyncContext context)) {
            context = target.AddComponent<CVRLipSyncContext>();
        }
        else {
            MelonLogger.Error($"The context already existed! Resetting it! This shouldn't happen...");
        }

        // Initialize context
        context.Initialize(isLocalPlayer, playerGuid);

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

        return context;
    }


    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RootLogic), "Start")]
        private static void After_RootLogic_Start(RootLogic __instance) {
            try {

                #if DEBUG
                MelonLogger.Msg("Adding the OVRLipSync object to the DissonanceSetup object...");
                #endif

                var dissonanceSetupGameObject = __instance.comms.gameObject;

                // Add the lip sync instance to the scene
                dissonanceSetupGameObject.AddComponent<OVRLipSync>();

                #if DEBUG
                MelonLogger.Msg("Adding the Local Player context...");
                #endif

                // Created and Add the Local Lipsync context (This one won't ever be destroyed)
                var localContext = CreateLipsyncContext(MetaPort.Instance.ownerId, dissonanceSetupGameObject);

                // Create a coroutine to initialize the remaining local stuff
                MelonCoroutines.Start(InitializeLocalPlayerAudio(localContext));

                #if DEBUG
                MelonLogger.Msg("Setting up the context creation for remote players, using the dissonance sessions...");
                #endif

                // Create a lipsync context when a new player joins the dissonance session
                RootLogic.Instance.comms.OnPlayerJoinedSession += state => {
                    #if DEBUG
                    MelonLogger.Msg($"OnPlayerJoinedSession => {state.Name} has JOINED the comms session!");
                    #endif

                    CreateLipsyncContext(state.Name, ((Component)state.Playback)!.gameObject);
                };
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
