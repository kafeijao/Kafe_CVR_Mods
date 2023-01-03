using ABI_RC.Core;
using ABI_RC.Core.Base;
using Dissonance;
using HarmonyLib;
using NAudio.Wave;

namespace BetterLipsync;

public class CVRMicLipsyncSubscriber : BaseMicrophoneSubscriber {

    private int _channels;
    private bool _initialized;
    private CVRLipSyncContext _lipSyncContext;
    private static CVRMicLipsyncSubscriber _instance;
    private static bool _muted;

    public void Initialize(CVRLipSyncContext lipSyncContext) {
        _lipSyncContext = lipSyncContext;
        _lipSyncContext.Muted = _muted;
        _initialized = true;
        _instance = this;
    }

    protected override void ProcessAudio(ArraySegment<float> data) {
        if (!_initialized) return;
        _lipSyncContext.ProcessAudioSamples(data.ToArray(), _channels);
    }

    protected override void ResetAudioStream(WaveFormat waveFormat) {
        _channels = waveFormat.Channels;
    }

    private void OnDestroy() {
        if (RootLogic.Instance.comms.MicrophoneCapture == null) return;
        RootLogic.Instance.comms.MicrophoneCapture.Unsubscribe(this);
    }

    private static void SetMuted(bool isMuted) {
        _muted = isMuted;
        if (_instance == null) return;
        _instance._lipSyncContext.Muted = isMuted;
    }

    [HarmonyPatch]
    private static class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Audio), nameof(Audio.SetMicrophoneActive))]
        private static void After_Audio_SetMicrophoneActive(bool muted) {
            SetMuted(muted);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DissonanceComms), nameof(DissonanceComms.IsMuted), MethodType.Setter)]
        private static void After_DissonanceComms_IsMuted_Setter(bool value) {
            SetMuted(value);
        }
    }
}
