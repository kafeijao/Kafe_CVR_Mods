using ABI_RC.Core;
using ABI_RC.Core.Base;
using Dissonance;
using HarmonyLib;
using NAudio.Wave;

namespace BetterLipsync;

public class CVRMicLipsyncSubscriber : BaseMicrophoneSubscriber {

    private static bool _muted;
    internal static CVRMicLipsyncSubscriber Instance;

    private int _channels;
    public CVRLipSyncContext lipSyncContext;

    private void Start() {
        Instance = this;
        UpdateMute();
    }

    private static void UpdateMute() {
        if (Instance != null && Instance.lipSyncContext != null) Instance.lipSyncContext.Muted = _muted;
    }

    public void SetContext(CVRLipSyncContext context) {
        lipSyncContext = context;
        lipSyncContext.Muted = _muted;
    }

    protected override void ProcessAudio(ArraySegment<float> data) {
        lipSyncContext.ProcessAudioSamples(data.ToArray(), _channels);
    }

    protected override void ResetAudioStream(WaveFormat waveFormat) {
        _channels = waveFormat.Channels;
    }

    private void OnDestroy() {
        if (RootLogic.Instance.comms.MicrophoneCapture == null) return;
        RootLogic.Instance.comms.MicrophoneCapture.Unsubscribe(this);
    }

    [HarmonyPatch]
    private static class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Audio), nameof(Audio.SetMicrophoneActive))]
        private static void After_Audio_SetMicrophoneActive(bool muted) {
            _muted = muted;
            UpdateMute();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DissonanceComms), nameof(DissonanceComms.IsMuted), MethodType.Setter)]
        private static void After_DissonanceComms_IsMuted_Setter(bool value) {
            _muted = value;
            UpdateMute();
        }
    }
}
