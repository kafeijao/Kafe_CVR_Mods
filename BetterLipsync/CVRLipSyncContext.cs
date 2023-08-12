using ABI_RC.Core;
using ABI_RC.Core.Player;
using Dissonance;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace BetterLipsync;

[DefaultExecutionOrder(999999)]
public class CVRLipSyncContext : OVRLipSyncContextBase {

    private static readonly Dictionary<string, CVRLipSyncContext> PlayersCurrentContext = new();
    private static readonly Dictionary<string, CVRVisemeController> PlayersCurrentVisemeController = new();

    // Config
    public bool Enabled { get; set; } = true;
    public bool SingleViseme { get; set; } = true;
    public bool SingleVisemeOriginalVolume { get; set; } = false;

    // Internal
    private bool _errored;
    private string _playerID;
    private bool _initialized;
    private bool _isLocalPlayer;
    private CVRVisemeController _visemeController;

    // Muted property
    public bool Muted { get; set; }

    // Remote players dissonance events
    private Action<VoicePlayerState> _playerStartedSpeaking;
    private Action<VoicePlayerState> _playerStoppedSpeaking;

    // CVRVisemeController Internals
    private Traverse<bool> _configSuccessful;
    private Traverse<float> _distance;
    private Traverse<int[]> _visemeBlendShapes;

    // Threading
    private static bool _multithreading;
    private int _skippedAudioData = 0;

    // Voice
    public VoicePlayerState CurrentVoicePlayerState;
    private float _detectedMaxVolume = 1f;

    // Lipsync Results
    private record LipsyncResult {
        internal bool Consumed { get; set; }
        internal float[] Visemes { get; init; } = new float[15];
        internal int Viseme { get; init; }
        internal float VisemeLoudness { get; init; }
    }
    private LipsyncResult _latestResult = new() { Consumed = true }; // Use lock (this) to access please
    private LipsyncResult _previousResult;

    static CVRLipSyncContext() {

        // Update whether we're going to use a separated thread of not
        _multithreading = BetterLipsync.MelonEntryMultithreading.Value;
        void LogThreadUsage() => MelonLogger.Msg($"We're{(_multithreading ? " " : "NOT ")}going to use multithreading while processing the Lipsync audio, as set in the config.");
        LogThreadUsage();
        BetterLipsync.MelonEntryMultithreading.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) _multithreading = newValue;
            LogThreadUsage();
        });

    }

    internal void Initialize(bool isLocalPlayer, string playerGuid) {

        _isLocalPlayer = isLocalPlayer;
        _playerID = playerGuid;

        // Setup Talking handlers for remote user contexts, the local one will be handled by the CVRMicLipsyncSubscriber
        if (!isLocalPlayer) {
            _playerStartedSpeaking = voicePlayerState => {
                if (voicePlayerState.Name != playerGuid) return;
                CurrentVoicePlayerState = voicePlayerState;
                Muted = false;
            };
            _playerStoppedSpeaking = (voicePlayerState) => {
                if (voicePlayerState.Name == playerGuid) Muted = true;
            };
            RootLogic.Instance.comms.OnPlayerStartedSpeaking += _playerStartedSpeaking;
            RootLogic.Instance.comms.OnPlayerStoppedSpeaking += _playerStoppedSpeaking;
        }

        PlayersCurrentContext[playerGuid] = this;

        // Check if we're ready to go already
        CheckVisemeController();
    }

    private void Start() {
        // Update smoothing value
        Smoothing = BetterLipsync.melonEntrySmoothing.Value;
        BetterLipsync.melonEntrySmoothing.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != oldValue) Smoothing = newValue;
        });
    }

    private void CheckVisemeController() {

        // If we got both a context (this instance) and a viseme controller. We're gucci to go!
        // Lets initialize stuff!
        if (PlayersCurrentVisemeController.ContainsKey(_playerID)) {
            var visemeController = PlayersCurrentVisemeController[_playerID];
            _visemeController = visemeController;
            // Cache fields from visemeController
            var visemeControllerTraverse = Traverse.Create(visemeController);
            _configSuccessful = visemeControllerTraverse.Field<bool>("configSuccessful");
            _distance = visemeControllerTraverse.Field<float>("_distance");
            _visemeBlendShapes = visemeControllerTraverse.Field<int[]>("visemeBlendShapes");
            _initialized = true;
        }

        // Otherwise, lets disable the lipsync for now
        else {
            _initialized = false;
        }

        #if DEBUG
        MelonLogger.Msg($"[CVRLipSyncContext.CheckVisemeController] Checking viseme controller for {_playerID}, the context is now active: {_initialized}");
        #endif
    }

    private bool ShouldComputeVisemes() {

        // Basic checks
        if (!_initialized || _errored || Muted) return false;

        // Ignore if viseme controller didn't initialize properly
        if (!_configSuccessful.Value) return false;

        // Ignore updates if too far away
        if (_distance.Value > 10.0) return false;

        return true;
    }


    private void LateUpdate() {

        // The mod is disabled
        if (!Enabled) return;

        try {

            // Check whether should do the heavy lifting or not
            if (!ShouldComputeVisemes()) {
                ResetVisemes();
                return;
            }

            LipsyncResult latestResult;
            lock (this) {

                // If there is nothing to consume return. Edit: orrrr just re-use the last viseme
                // Because some avatars set visemes via animations, which then makes it bork ;_;
                if (_latestResult.Consumed) {
                    latestResult = _latestResult;
                }
                else {
                    // Otherwise get and mark the result as consumed
                    latestResult = _latestResult;
                    _latestResult.Consumed = true;
                }

            }

            // Update the avatar visemes and the parameters (if present)
            UpdateVisemes(latestResult);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
            _errored = true;
        }
    }

    private float GetAmplitude() {
        if (CurrentVoicePlayerState == null) return 0;
        return CurrentVoicePlayerState.Amplitude;
    }

    private void UpdateVisemes(LipsyncResult latestResult) {

        // Handle the face visemes from the descriptor
        if (_visemeController.avatar != null && _visemeController.avatar.useVisemeLipsync && _visemeController.avatar.bodyMesh != null) {

            // MelonLogger.Msg($"Visemes {_playerID}: {GetAmplitude():F2} {visemes.Join((f => f.ToString("F2")))}");

            for (var visemeIdx = 0; visemeIdx < latestResult.Visemes.Length; visemeIdx++) {

                // Ignore visemes set to none
                if (_visemeController.avatar.visemeBlendshapes[visemeIdx] == "-none-") continue;

                if (SingleViseme) {
                    var clampedLoudness = Mathf.Clamp(latestResult.VisemeLoudness * 100f, 0, 100f);

                    // Set the picked viseme to the loudness or 0 if not the current viseme
                    _visemeController.avatar.bodyMesh.SetBlendShapeWeight(_visemeBlendShapes.Value[visemeIdx], visemeIdx == latestResult.Viseme ? clampedLoudness : 0f);
                }
                else {
                    // Set all visemes for their corresponding weight
                    _visemeController.avatar.bodyMesh.SetBlendShapeWeight(_visemeBlendShapes.Value[visemeIdx], latestResult.Visemes[visemeIdx] * 100f);
                }
            }
        }

        // Handle the animator parameters
        if (_isLocalPlayer) {
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterInt("Viseme", latestResult.Viseme);
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterFloat("VisemeWeight", latestResult.VisemeLoudness);
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterFloat("VisemeLoudness", latestResult.VisemeLoudness);
        }

        // Save previous info
        _previousResult = latestResult;
    }

    public void ResetVisemes() {

        // Ignore resetting if already initial state
        if (!_initialized || _previousResult == null || _previousResult.Viseme == 0 && Mathf.Approximately(_previousResult.VisemeLoudness, 0f)) return;

        ResetContext();
        UpdateVisemes(new LipsyncResult() { Consumed = false });
    }

    private void OnDisable() {
        // This should never happen since the local player context is on the DissonanceSetup object!
        if (_isLocalPlayer) return;

        #if DEBUG
        MelonLogger.Msg($"[CVRLipSyncContext.OnDisable] The game object has been turned off! The player {_playerID}");
        #endif
        Destroy(this);
    }

    private new void OnDestroy() {

        #if DEBUG
        MelonLogger.Msg($"[CVRLipSyncContext.OnDestroy] The {(_isLocalPlayer ? "LOCAL" : "")} player {_playerID} has been nuked!");
        #endif

        if (_isLocalPlayer) {
            // Clear the player talking delegates
            RootLogic.Instance.comms.OnPlayerStartedSpeaking -= _playerStartedSpeaking;
            RootLogic.Instance.comms.OnPlayerStoppedSpeaking -= _playerStoppedSpeaking;
        }

        // Clear the cache for the context being destroyed
        if (PlayersCurrentContext.ContainsKey(_playerID)) {
            PlayersCurrentContext.Remove(_playerID);
        }

        // And call destroy of the base class
        base.OnDestroy();
    }

    private void ComputeViseme() {

        var viseme = 0;
        var visemeHighestLoudness = 0f;

        // Grab viseme and viseme loudness from the oculus lip sync
        var frame = GetCurrentPhonemeFrame();

        for (var visemeIdx = 0; visemeIdx < frame.Visemes.Length; visemeIdx++) {
            var currVisemeLoudness = frame.Visemes[visemeIdx];

            // Ignore visemes that have lower loudness than the highest
            if (currVisemeLoudness <= visemeHighestLoudness) continue;

            // Otherwise set as the highest viseme
            viseme = visemeIdx;
            visemeHighestLoudness = currVisemeLoudness;
        }

        // If the viseme is SIL, set the loudness to zero
        if (viseme == (int)OVRLipSync.Viseme.sil) {
            visemeHighestLoudness = 0f;
        }

        if (SingleVisemeOriginalVolume) {
            var voiceAmplitude = GetAmplitude() * _visemeController.amplifyMultipleExperimental;
            _detectedMaxVolume = voiceAmplitude <= _detectedMaxVolume
                ? Mathf.Max(1f, _detectedMaxVolume * 0.999f)
                : voiceAmplitude;
            visemeHighestLoudness = voiceAmplitude / _detectedMaxVolume;
        }

        // Update the loudness on the viseme controller (to keep things working properly)
        _visemeController.visemeLoudness = visemeHighestLoudness;

        // Create a result object
        lock (this) {
            _latestResult = new LipsyncResult {
                Consumed = false,
                Visemes = frame.Visemes,
                Viseme = viseme,
                VisemeLoudness = visemeHighestLoudness,
            };
        }
    }

    public void ProcessAudioSamples(float[] data, int channels) {

        // The mod is disabled
        if (!Enabled) return;

        // Do not process if we are not initialized
        if (OVRLipSync.IsInitialized() != OVRLipSync.Result.Success) {
            return;
        }

        // Ignore sending audio data if we shouldn't be computing
        if (!ShouldComputeVisemes()) return;

        // Send data into Phoneme context for processing (if context is not 0)
        lock (this) {
            if (Context == 0 || OVRLipSync.IsInitialized() != OVRLipSync.Result.Success) return;

            lock (this) {
                // Wait for the LateUpdate to consume the processed frame
                if (!_latestResult.Consumed) {
                    _skippedAudioData++;
                    return;
                }
            }

            #if DEBUG
            if (_skippedAudioData > 10) {
                MelonLogger.Msg($"Fell behind on the ProcessAudioSamples task by {_skippedAudioData} ticks. This is normal if the game is having lag spikes...");
            }
            #endif

            if (_multithreading) {

                // Create a deep copy of the data, since it will be recycled for the other ProcessAudioSamples calls ?
                var bufferedData = new float[data.Length];
                data.CopyTo(bufferedData, 0);

                // Using a thread pool to run the tasks
                Task.Factory.StartNew(() => {
                    OVRLipSync.ProcessFrame(Context, bufferedData, Frame, channels == 2);
                    ComputeViseme();
                });
            }

            else {
                // Just process this on the current thread, we're on the Audio thread thought...
                OVRLipSync.ProcessFrame(Context, data, Frame, channels == 2);
                ComputeViseme();
            }

            _skippedAudioData = 0;
        }
    }

    private void OnAudioFilterRead(float[] data, int channels) {
        // This event will only be called when there is actually data, so we don't need to check for muted
        // Also frigging data is the same object across all game object that call this, which means if we want to do
        // async stuff with it, we need to deep copy. Come on unity a note indicating this would save me some pain ;_;

        // Local player will handle the audio directly
        if (_isLocalPlayer) return;

        ProcessAudioSamples(data, channels);
    }


    [HarmonyPatch]
    private static class HarmonyPatches {


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRVisemeController), "Start")]
        private static void After_CVRVisemeController_Start(CVRVisemeController __instance, string ___playerGuid) {
            try {

                #if DEBUG
                MelonLogger.Msg($"[CVRVisemeController.Start] {___playerGuid} has INITIALIZED their viseme controller!");
                #endif

                // Cache the viseme controller
                PlayersCurrentVisemeController[___playerGuid] = __instance;

                // Update the context for this change
                if (PlayersCurrentContext.ContainsKey(___playerGuid)) PlayersCurrentContext[___playerGuid].CheckVisemeController();
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRVisemeController), "OnDestroy")]
        private static void After_CVRVisemeController_OnDestroy(CVRVisemeController __instance, string ___playerGuid) {
            try {

                #if DEBUG
                MelonLogger.Msg($"[CVRVisemeController.Start] {___playerGuid} has DESTROYED their viseme controller!");
                #endif

                // Clear viseme controller from cache
                if (PlayersCurrentVisemeController.ContainsKey(___playerGuid)) {
                    PlayersCurrentVisemeController.Remove(___playerGuid);
                }

                // Update the context for this changes
                if (PlayersCurrentContext.ContainsKey(___playerGuid)) PlayersCurrentContext[___playerGuid].CheckVisemeController();
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }
    }
}
