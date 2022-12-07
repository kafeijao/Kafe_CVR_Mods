using ABI_RC.Core.Base;
using Dissonance;
using HarmonyLib;
using NAudio.Wave;

namespace BetterLipsync;

public class CVRMicLipsyncSubscriber : BaseMicrophoneSubscriber {

    private int _channels;
    private bool _initialized;
    private CVRLipSyncContext _lipSyncContext;
    private static CVRMicLipsyncSubscriber Instance;

    public void Initialize(CVRLipSyncContext lipSyncContext) {
        _lipSyncContext = lipSyncContext;
        _initialized = true;
        Instance = this;
    }

    protected override void ProcessAudio(ArraySegment<float> data) {
        if (!_initialized) return;
        _lipSyncContext.ProcessAudioSamples(data.ToArray(), _channels);
    }

    protected override void ResetAudioStream(WaveFormat waveFormat) {
        _channels = waveFormat.Channels;
    }

    [HarmonyPatch]
    private static class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Audio), nameof(Audio.SetMicrophoneActive))]
        private static void After_Audio_SetMicrophoneActive(bool muted) {
            if (Instance == null) return;
            Instance._lipSyncContext.Muted = muted;
        }
    }
}
