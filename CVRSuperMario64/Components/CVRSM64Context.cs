using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64CContext : MonoBehaviour {

    static CVRSM64CContext s_instance = null;

    List<CVRSM64CMario> _marios = new List<CVRSM64CMario>();
    readonly List<CVRSM64ColliderDynamic> _surfaceObjects = new List<CVRSM64ColliderDynamic>();

    private short[] audioBuffer;
    private float[] processedAdioBuffer;
    private AudioSource audioSource;

    private void Awake() {

        SetAudioStuff();

        // audioSource.clip = AudioClip.Create("CVRSM64Context", bufferSize, numChannels, sampleRate, true, data => {
        //     var numSamples = Interop.AudioTick(audioBuffer, (uint) bufferSize);
        //     MelonLogger.Msg($"numSamples: {numSamples} data.Length: {data.Length} -> {audioBuffer.Min(s => s):F5}/{audioBuffer.Average(s => s):F5}/{audioBuffer.Max(s => s):F5}");
        //     for (var i = 0; i < bufferSize; i++) {
        //         data[i] = (float) audioBuffer[i] / short.MaxValue;
        //     }
        // });

        //Interop.GlobalInit( File.ReadAllBytes( Application.dataPath + "/../baserom.us.z64" ));
        Interop.GlobalInit(CVRSuperMario64.SuperMario64UsZ64RomBytes);
        //RefreshStaticTerrain();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
    }

    public void SetAudioStuff() {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioBuffer = new short[bufferSize];
        processedAdioBuffer = new float[bufferSize];
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.clip = AudioClip.Create("CVRSM64Context", bufferSize, numChannels, sampleRate, false);
        audioSource.loop = false;
        //audioSource.Play();
    }

    private int bufferSize = 544 * 2 * 2;
    private int numChannels = 2;
    private int sampleRate = 32000;

    private bool playOnUpdate = false;

    private float _nextProc;
    private int _intervalProcMs = 33;

    //
    // void OnAudioFilterRead(float[] data, int channels) {
    //     Interop.AudioTick(audioBuffer, (uint) bufferSize);
    //     for (var i = 0; i < bufferSize; i++) {
    //         processedAdioBuffer[i] = (float) audioBuffer[i] / short.MaxValue;
    //     }
    //     audioSource.clip.SetData(processedAdioBuffer, 0);
    // }


    public void Sample() {

        // Disable audio, it can get annoying
        if (CVRSuperMario64.MeDisableAudio.Value) return;

        var numSamples = Interop.AudioTick(audioBuffer, (uint) bufferSize);
        //MelonLogger.Msg($"numSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {audioBuffer.Min(s => s):F5}/{audioBuffer.Average(s => s):F5}/{audioBuffer.Max(s => s):F5}");
        for (var i = 0; i < audioBuffer.Length; i++) {
            processedAdioBuffer[i] = (float)audioBuffer[i] / short.MaxValue;
        }
        //MelonLogger.Msg($"\tnumSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {processedAdioBuffer.Min(s => s):F5}/{processedAdioBuffer.Average(s => s):F5}/{processedAdioBuffer.Max(s => s):F5}");
        audioSource.clip = AudioClip.Create("CVRSM64Context",bufferSize, numChannels, sampleRate, false);
        audioSource.clip.SetData(processedAdioBuffer, 0);
        audioSource.Play();
    }

    // public void Sample() {
    //     var numSamples = Interop.AudioTick(audioBuffer, (uint) bufferSize);
    //     MelonLogger.Msg($"numSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {audioBuffer.Min(s => s):F5}/{audioBuffer.Average(s => s):F5}/{audioBuffer.Max(s => s):F5}");
    //     for (var i = 0; i < audioBuffer.Length; i++) {
    //         //processedAdioBuffer[(i+audioSource.timeSamples)%bufferSize] = (float)audioBuffer[i] / short.MaxValue;
    //         processedAdioBuffer[i] = (float)audioBuffer[i] / short.MaxValue;
    //     }
    //     MelonLogger.Msg($"\tnumSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {processedAdioBuffer.Min(s => s):F5}/{processedAdioBuffer.Average(s => s):F5}/{processedAdioBuffer.Max(s => s):F5}");
    //     audioSource.clip.SetData(processedAdioBuffer, 0);
    //     if (!audioSource.isPlaying) {
    //         audioSource.Play();
    //     }
    // }

    private void AudioProcess() {

        // Call the external library to fill the buffer with audio data
        var numSamples = Interop.AudioTick(audioBuffer, (uint) bufferSize);

        //MelonLogger.Msg($"numSamples: {numSamples} -> audioSource.timeSamples {audioSource.timeSamples} -> {audioBuffer.Min(s => s):F5}/{audioBuffer.Average(s => s):F5}/{audioBuffer.Max(s => s):F5}");

        // Convert the short array to a float array
        processedAdioBuffer = new float[audioBuffer.Length * 2];
        for (var i = 0; i < audioBuffer.Length; i++) {
            processedAdioBuffer[i] = processedAdioBuffer[i+1] = (float)audioBuffer[i] / short.MaxValue;
            //processedAdioBuffer[(i+audioSource.timeSamples)%audioBuffer.Length] = (float)audioBuffer[i] / short.MaxValue;
        }

        // Set the AudioSource clip data to the new audio data
        audioSource.clip.SetData(processedAdioBuffer, 0);

        // Check if the AudioSource is still playing
        audioSource.timeSamples = 0;
        if (!audioSource.isPlaying) {
            // If not, restart the clip playback
            audioSource.Play();
        }
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

    private void Start() {
        // Update the ticks at 30 times a second
        //InvokeRepeating(nameof(FunctionToCall), 0, 1f / 30f);
        InvokeRepeating(nameof(Sample), 0, 0.033f);
    }
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

        if (playOnUpdate) {
            if (Time.time >= _nextProc) {
                Sample();
                _nextProc = Time.time + (_intervalProcMs / 1000f) ;
            }
        }
    }

    private void FixedUpdate() {
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

    private static void EnsureInstanceExists() {
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
            s_instance._marios.Add(mario);
        }
    }

    public static void UnregisterMario(CVRSM64CMario mario) {
        if (s_instance != null && s_instance._marios.Contains(mario)) {
            s_instance._marios.Remove(mario);
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
