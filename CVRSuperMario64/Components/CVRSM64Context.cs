using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64CContext : MonoBehaviour {

    static CVRSM64CContext s_instance = null;

    List<CVRSM64CMario> _marios = new List<CVRSM64CMario>();
    readonly List<CVRSM64ColliderDynamic> _surfaceObjects = new List<CVRSM64ColliderDynamic>();

    // Audio
    private AudioSource _audioSource;
    private const int BufferSize = 544 * 2 * 2;
    private int _bufferPosition = BufferSize;
    private readonly short[] _audioBuffer = new short[BufferSize];
    private readonly float[] _processedAudioBuffer = new float[BufferSize];

    private void Awake() {

        SetAudioStuff();

        Interop.GlobalInit(CVRSuperMario64.SuperMario64UsZ64RomBytes);

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        CVRSuperMario64.MeIgnoreCollidersHigherThanPolygons.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        });
    }

    private void SetAudioStuff() {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = CVRSuperMario64.MeAudioVolume.Value;
        CVRSuperMario64.MeAudioVolume.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.volume = newValue);
        _audioSource.pitch = CVRSuperMario64.MeAudioPitch.Value;
        CVRSuperMario64.MeAudioPitch.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.pitch = newValue);
        //audioSource.clip = AudioClip.Create("CVRSM64Context", bufferSize, numChannels, sampleRate, false);
        _audioSource.loop = true;
        _audioSource.Play();
    }

    private void ProcessMoreSamples() {
        Interop.AudioTick(_audioBuffer, BufferSize);
        //MelonLogger.Msg($"numSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {audioBuffer.Min(s => s):F5}/{audioBuffer.Average(s => s):F5}/{audioBuffer.Max(s => s):F5}");
        for (var i = 0; i < BufferSize; i++) {
            _processedAudioBuffer[i] = Mathf.Min((float)_audioBuffer[i] / short.MaxValue, 1f);
        }
        _bufferPosition = 0;
    }


    private void OnAudioFilterRead(float[] data, int channels) {

        // Disable audio, it can get annoying
        if (CVRSuperMario64.MeDisableAudio.Value) return;

        var samplesRemaining = data.Length;
        while (samplesRemaining > 0) {
            var samplesToCopy = Mathf.Min(samplesRemaining, BufferSize - _bufferPosition);
            Array.Copy(_processedAudioBuffer, _bufferPosition, data, data.Length - samplesRemaining, samplesToCopy);
            _bufferPosition += samplesToCopy;
            samplesRemaining -= samplesToCopy;
            if (_bufferPosition >= BufferSize) {
                ProcessMoreSamples();
            }
        }
    }

    private void Start() {
        // Update the ticks at 30 times a second
        //InvokeRepeating(nameof(FunctionToCall), 0, 1f / 30f);

        InvokeRepeating(nameof(FixedUpdatee), 0, CVRSuperMario64.MeGameTickMs.Value / 1000f);
        CVRSuperMario64.MeGameTickMs.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            CancelInvoke(nameof(FixedUpdatee));
            InvokeRepeating(nameof(FixedUpdatee), 0, newValue / 1000f);
        });
    }



    // Todo: After get the audio properly working, let's put it in it's own thread!

    // private Thread audioThread;
    // private bool isRunning = false;
    // private readonly int tickInterval = 33;
    //
    // private void Start() {
    //     void AudioTickLoop() {
    //         while (isRunning) {
    //             AudioTick();
    //             Thread.Sleep(tickInterval);
    //         }
    //     }
    //     isRunning = true;
    //     audioThread = new Thread(AudioTickLoop);
    //     audioThread.Start();
    // }
    //
    // private void OnDestroy() {
    //     isRunning = false;
    //     audioThread?.Join();
    // }

    // private void Start() {
    //     // Update the ticks at 30 times a second
    //     //InvokeRepeating(nameof(FunctionToCall), 0, 1f / 30f);
    //     InvokeRepeating(nameof(Sample), 0, 0.033f);
    // }
    //
    // private void FunctionToCall() {
    //     FakeFixedUpdate();
    //     FakeUpdate();
    // }

    private void Update() {
        foreach (var o in _surfaceObjects) {
            o.ContextUpdate();
        }

        foreach (var o in _marios) {
            o.ContextUpdate();
        }
    }

    private void FixedUpdatee() {
        foreach (var o in _surfaceObjects) {
            o.ContextFixedUpdate();
        }

        foreach (var o in _marios) {
            o.ContextFixedUpdate();
        }
    }

    private void OnApplicationQuit() {
        Interop.GlobalTerminate();
        s_instance = null;
    }

    internal static void EnsureInstanceExists() {
        if (s_instance == null) {
            var contextGo = new GameObject("SM64_CONTEXT");
            contextGo.hideFlags |= HideFlags.HideInHierarchy;
            s_instance = contextGo.AddComponent<CVRSM64CContext>();
        }
    }

    public static void RefreshStaticTerrain() {
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
    }

    public static void RegisterMario(CVRSM64CMario mario) {
        EnsureInstanceExists();

        if (!s_instance._marios.Contains(mario)) {
            if (CVRSuperMario64.MePlayRandomMusicOnMarioJoin.Value) Interop.PlayRandomMusic();
            s_instance._marios.Add(mario);
        }
    }

    public static void UnregisterMario(CVRSM64CMario mario) {
        if (s_instance != null && s_instance._marios.Contains(mario)) {
            s_instance._marios.Remove(mario);
            if (s_instance._marios.Count == 0) {
                Interop.StopMusic();
            }
        }
    }

    public static void RegisterSurfaceObject(CVRSM64ColliderDynamic surfaceObject) {
        EnsureInstanceExists();

        if (!s_instance._surfaceObjects.Contains(surfaceObject)) {
            s_instance._surfaceObjects.Add(surfaceObject);
        }
    }

    public static void UnregisterSurfaceObject(CVRSM64ColliderDynamic surfaceObject) {
        if (s_instance != null && s_instance._surfaceObjects.Contains(surfaceObject)) {
            s_instance._surfaceObjects.Remove(surfaceObject);
        }
    }
}
