using ABI_RC.Core;
using ABI_RC.Core.Player;
using Dissonance;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace BetterLipsync;

public class CVRLipSyncContext : OVRLipSyncContextBase {

    // Config
    public bool Enabled = true;
    public bool SingleViseme = true;

    // Internal
    private bool _errored;
    private bool _initialized;
    private bool _isLocalPlayer;
    private CVRVisemeController _visemeController;

    // Muted property
    public bool Muted { get; set; }

    // Remote players talking state action
    private Action<VoicePlayerState> _playerStartedSpeaking;
    private Action<VoicePlayerState> _playerStoppedSpeaking;

    // CVRVisemeController Internals
    private Traverse<bool> _configSuccessful;
    private Traverse<float> _distance;
    private Traverse<int[]> _visemeBlendShapes;

    // Previous frame info
    private float[] _previousVisemes;
    private int _previousViseme;
    private float _previousVisemeLoudness;

    // Performance internals
    private static int _lastInstanceId = 0;
    private int _instanceId;

    // Threading
    private Task<OVRLipSync.Result> _lastProcessFrameTask;
    private int _skippedAudioData = 0;
    private bool _consumedProcessedFrame = true;

    internal void Initialize(CVRVisemeController visemeController, GameObject target, bool isLocalPlayer, string playerGuid) {

        _visemeController = visemeController;
        _isLocalPlayer = isLocalPlayer;

        // We require an audio source for remote players
        if (!_isLocalPlayer && !target.TryGetComponent(out AudioSource _)) {
            MelonLogger.Error($"Attempted to initialized a Lip Sync module on a remote player without an audio source.");
        }

        // Cache fields from visemeController
        var visemeControllerTraverse = Traverse.Create(visemeController);
        _configSuccessful = visemeControllerTraverse.Field<bool>("configSuccessful");
        _distance = visemeControllerTraverse.Field<float>("_distance");
        _visemeBlendShapes = visemeControllerTraverse.Field<int[]>("visemeBlendShapes");

        // Assign and increment instance id
        _instanceId = _lastInstanceId++;

        // Initialize first values
        _previousVisemes = new float[15];
        _previousViseme = 0;
        _previousVisemeLoudness = 1f;

        // Talking state handlers
        if (!isLocalPlayer) {
            _playerStartedSpeaking = (voicePlayerState) => {
                if (voicePlayerState.Name == playerGuid) Muted = false;
            };
            _playerStoppedSpeaking = (voicePlayerState) => {
                if (voicePlayerState.Name == playerGuid) Muted = true;
            };
            RootLogic.Instance.comms.OnPlayerStartedSpeaking += _playerStartedSpeaking;
            RootLogic.Instance.comms.OnPlayerStoppedSpeaking += _playerStoppedSpeaking;
        }

        _initialized = true;
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

            // If there is nothing to consume, skip getting visemes
            if (_consumedProcessedFrame) {
                // Todo: We could add some smoothing since the frame rate can be faster than the audio sample ticks
                return;
            }
            _consumedProcessedFrame = true;

            // Fetch the viseme values from oculus lip sync
            var (visemes, viseme, visemeLoudness) = GetViseme();

            // Update the avatar visemes and the parameters (if present)
            UpdateVisemes(visemes, viseme, visemeLoudness);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
            _errored = true;
        }
    }

    private (float[], int, float) GetViseme() {

        // Grab viseme and viseme loudness from the oculus lip sync
        var frame = GetCurrentPhonemeFrame();

        var visemeLoudness = 0f;
        var viseme = 0;
        var foundViseme = false;

        // Iterate all visemes (skip sil)
        for (var visemeIdx = 1; visemeIdx < frame.Visemes.Length; visemeIdx++) {
            var currVisemeLoudness = frame.Visemes[visemeIdx];
            // Ignore lower values
            if (currVisemeLoudness <= visemeLoudness) continue;
            viseme = visemeIdx;
            visemeLoudness = currVisemeLoudness;
            foundViseme = true;
        }

        // If no viseme was found, use sil
        if (!foundViseme) {
            viseme = 0;
            visemeLoudness = 1f;
        }

        // Update the loudness on the viseme controller (to keep things working properly)
        _visemeController.visemeLoudness = visemeLoudness;

        return (frame.Visemes, viseme, visemeLoudness);
    }

    private void UpdateVisemes(float[] visemes, int viseme, float visemeLoudness) {

        // Handle the face visemes from the descriptor
        if (_visemeController.avatar != null && _visemeController.avatar.useVisemeLipsync && _visemeController.avatar.bodyMesh != null) {

            for (var visemeIdx = 0; visemeIdx < visemes.Length; visemeIdx++) {

                // Ignore visemes set to none
                if (_visemeController.avatar.visemeBlendshapes[visemeIdx] == "-none-") continue;

                if (SingleViseme) {
                    // Set the picked viseme to the loudness or 0 if not the current viseme
                    _visemeController.avatar.bodyMesh.SetBlendShapeWeight(_visemeBlendShapes.Value[visemeIdx], visemeIdx == viseme ? Mathf.Clamp(visemeLoudness * 100f * 1.25f, 0, 100f) : 0f);
                }
                else {
                    // Set all visemes for their corresponding weight
                    _visemeController.avatar.bodyMesh.SetBlendShapeWeight(_visemeBlendShapes.Value[visemeIdx], visemes[visemeIdx] * 100f);
                }
            }
        }

        // Handle the animator parameters
        if (_isLocalPlayer) {
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterInt("Viseme", viseme);
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterFloat("VisemeWeight", visemeLoudness);
            PlayerSetup.Instance.animatorManager.SetAnimatorParameterFloat("VisemeLoudness", visemeLoudness);
        }

        // Save previous info
        _previousVisemes = visemes;
        _previousViseme = viseme;
        _previousVisemeLoudness = visemeLoudness;
    }

    public void ResetVisemes() {

        // Ignore resetting if already initial state
        if (_previousViseme == 0 && Mathf.Approximately(_previousVisemeLoudness, 1f)) return;

        ResetContext();
        UpdateVisemes(new float[15], 0, 1f);
    }

    private void OnDestroy() {
        // Clear the player talking delegates
        if (_isLocalPlayer) return;
        RootLogic.Instance.comms.OnPlayerStartedSpeaking -= _playerStartedSpeaking;
        RootLogic.Instance.comms.OnPlayerStoppedSpeaking -= _playerStoppedSpeaking;
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

            // Wait for the LateUpdate to consume the processed frame
            if (!_consumedProcessedFrame) {
                return;
            }

            // Wait for last frame process to end (if still processing)
            if (_lastProcessFrameTask is { IsCompleted: false }) {
                _skippedAudioData++;
                return;
            }

            if (_skippedAudioData > 1) {
                MelonLogger.Msg($"Fell behind on the ProcessAudioSamples task by {_skippedAudioData} ticks...");
            }

            // Actual process frame in a task
            _lastProcessFrameTask = Task<OVRLipSync.Result>.Factory.StartNew(() =>
                OVRLipSync.ProcessFrame(Context, data, Frame, channels == 2));

            _skippedAudioData = 0;
            _consumedProcessedFrame = false;
        }
    }

    private void OnAudioFilterRead(float[] data, int channels) {
        // Local player will handle the audio directly
        if (_isLocalPlayer) return;
        ProcessAudioSamples(data, channels);
    }
}
