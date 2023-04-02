using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64Context : MonoBehaviour {

    private static CVRSM64Context _instance = null;

    private readonly List<CVRSM64Mario> _marios = new();
    private readonly List<CVRSM64ColliderDynamic> _surfaceObjects = new();
    private readonly List<CVRSM64LevelModifier> _levelModifierObjects = new();

    // Audio
    private AudioSource _audioSource;
    private const int BufferSize = 544 * 2 * 2;
    private int _bufferPosition = BufferSize;
    private readonly short[] _audioBuffer = new short[BufferSize];
    private readonly float[] _processedAudioBuffer = new float[BufferSize];


    // Melon prefs
    private int _maxMariosAnimatedPerPerson;

    private void Awake() {

        Interop.GlobalInit(CVRSuperMario64.SuperMario64UsZ64RomBytes);

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        Config.MeMaxMeshColliderTotalTris.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        });

        // Setup melon pref updates
        _maxMariosAnimatedPerPerson = Config.MeMaxMariosAnimatedPerPerson.Value;
        Config.MeMaxMariosAnimatedPerPerson.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            _maxMariosAnimatedPerPerson = newValue;
            UpdatePlayerMariosState();
            MelonLogger.Msg($"Changed the Max Marios animated per player from {oldValue} to {newValue}.");
        });
    }

    private void SetAudioSource() {

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;

        _audioSource.volume = Config.MeAudioVolume.Value;
        Config.MeAudioVolume.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.volume = newValue);

        _audioSource.pitch = Config.MeAudioPitch.Value;
        Config.MeAudioPitch.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.pitch = newValue);

        _audioSource.loop = true;
        _audioSource.Play();
    }

    private void ProcessMoreSamples() {
        Interop.AudioTick(_audioBuffer, BufferSize);
        for (var i = 0; i < BufferSize; i++) {
            _processedAudioBuffer[i] = Mathf.Min((float)_audioBuffer[i] / short.MaxValue, 1f);
        }
        _bufferPosition = 0;
    }

    private void OnAudioFilterRead(float[] data, int channels) {

        // Disable audio, it can get annoying
        if (Config.MeDisableAudio.Value) return;

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

        // Game ticks via invoking
        InvokeRepeating(nameof(SM64GameTick), 0, Config.MeGameTickMs.Value / 1000f);
        Config.MeGameTickMs.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            CancelInvoke(nameof(SM64GameTick));
            InvokeRepeating(nameof(SM64GameTick), 0, newValue / 1000f);
        });

        // CVRSuperMario64.MeGameTickMs.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
        //     if (newValue == oldValue) return;
        //     lock (_tickIntervalLock) {
        //         _tickInterval = newValue;
        //     }
        // });
        //
        // _isRunning = true;
        // _gameThread = new Thread(GameTickLoop);
        // _gameThread.Start();
    }

    // private Thread _gameThread;
    // private bool _isRunning = false;
    // private readonly object _tickIntervalLock = new();
    // private int _tickInterval = CVRSuperMario64.MeGameTickMs.Value;
    //
    // private void GameTickLoop() {
    //     while (_isRunning) {
    //         lock (_tickIntervalLock) {
    //             lock (_marios) {
    //                 foreach (var o in _marios) {
    //                     o.Sm64MarioTickThread();
    //                 }
    //             }
    //             Thread.Sleep(_tickInterval);
    //         }
    //     }
    // }
    //
    // private void OnDestroy() {
    //     _isRunning = false;
    //     _gameThread?.Join();
    // }

    private void Update() {
        lock (_marios) {
            foreach (var o in _marios) {
                o.ContextUpdateSynced();
            }
        }
    }

    private void SM64GameTick() {

        lock (_surfaceObjects) {
            foreach (var o in _surfaceObjects) {
                o.ContextFixedUpdateSynced();
            }
        }

        lock (_marios) {
            CVRSM64LevelModifier.ContextTick(_marios);

            foreach (var o in _marios) {
                o.ContextFixedUpdateSynced(_marios);
            }
        }
    }

    private void OnApplicationQuit() {
        Interop.GlobalTerminate();
        _instance = null;
    }

    private static void EnsureInstanceExists() {
        if (_instance != null) return;

        var contextGo = new GameObject("SM64_CONTEXT");
        contextGo.hideFlags |= HideFlags.HideInHierarchy;
        _instance = contextGo.AddComponent<CVRSM64Context>();
    }

    public static void QueueStaticSurfacesUpdate() {
        if (_instance == null) return;
        // If there was a queued update, cancel it first
        _instance.CancelInvoke(nameof(StaticTerrainUpdate));
        _instance.Invoke(nameof(StaticTerrainUpdate), 1.5f);
    }

    private void StaticTerrainUpdate() {
        if (_instance == null) return;
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
    }

    public static void UpdateMarioCount() {
        if (_instance == null || MarioInputModule.Instance == null) return;
        lock (_instance._marios) {
            var ourMarios = _instance._marios.FindAll(m => m.IsMine());
            MarioInputModule.Instance.controllingMarios = ourMarios.Count;
            MarioCameraMod.Instance.UpdateOurMarios(ourMarios);
        }
    }

    public static void UpdatePlayerMariosState() {
        if (_instance == null) return;

        lock (_instance._marios) {
            foreach (var playerMarios in _instance._marios.GroupBy(m => m.GetOwnerId())) {
                if (playerMarios.First().IsMine()) continue;
                var maxMarios = _instance._maxMariosAnimatedPerPerson;
                foreach (var playerMario in playerMarios) {
                    playerMario.SetIsOverMaxCount(maxMarios-- <= 0);
                }
            }
        }
    }

    public static void RegisterMario(CVRSM64Mario mario) {
        EnsureInstanceExists();

        // Note: Don't use mario.IsMine() or mario.IsDead() here, they're still not initialized!
        lock (_instance._marios) {
            if (_instance._marios.Contains(mario)) return;

            _instance._marios.Add(mario);

            if (Config.MePlayRandomMusicOnMarioJoin.Value) Interop.PlayRandomMusic();

            CVRSM64LevelModifier.MarkForUpdate();
        }
    }

    public static void UnregisterMario(CVRSM64Mario mario) {
        if (_instance == null) return;

        lock (_instance._marios) {
            if (!_instance._marios.Contains(mario)) return;

            _instance._marios.Remove(mario);
            UpdatePlayerMariosState();

            if (_instance._marios.Count == 0) {
                Interop.StopMusic();
            }
        }
    }

    public static void RegisterSurfaceObject(CVRSM64ColliderDynamic surfaceObject) {
        EnsureInstanceExists();

        lock (_instance._surfaceObjects) {
            if (!_instance._surfaceObjects.Contains(surfaceObject)) {
                _instance._surfaceObjects.Add(surfaceObject);
            }
        }
    }

    public static void UnregisterSurfaceObject(CVRSM64ColliderDynamic surfaceObject) {
        if (_instance == null) return;

        lock (_instance._surfaceObjects) {
            if (_instance._surfaceObjects.Contains(surfaceObject)) {
                _instance._surfaceObjects.Remove(surfaceObject);
            }
        }
    }
}
