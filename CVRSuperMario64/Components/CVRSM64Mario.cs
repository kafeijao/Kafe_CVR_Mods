using ABI_RC.Core.Player;
using ABI_RC.Core.Player.AvatarTracking;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;


[DefaultExecutionOrder(999999)]
public class CVRSM64Mario : MonoBehaviour {

    // Main
    [SerializeField] private CVRSpawnable spawnable;
    [SerializeField] private bool advancedOptions = false;

    // Material & Textures
    [SerializeField] private Material material = null;
    [SerializeField] private bool replaceTextures = true;
    [SerializeField] private List<string> propertiesToReplaceWithTexture = new() { "_MainTex" };

    // Animators
    [SerializeField] private List<Animator> animators = new();

    // Camera override
    [SerializeField] private bool overrideCameraPosition = false;
    [SerializeField] private Transform cameraPositionTransform = null;

    // Camera Mod Override
    [SerializeField] private Transform cameraModTransform = null;
    [SerializeField] private List<Renderer> cameraModTransformRenderersToHide = new();

    // Material Properties
    private const float VanishOpacity = 0.5f;
    private Color _colorNormal;
    private Color _colorVanish;
    private Color _colorInvisible;
    private readonly int _colorProperty = Shader.PropertyToID("_Color");
    private readonly List<int> _metallicProperties = new() {
        Shader.PropertyToID("_Metallic"),
        Shader.PropertyToID("_MochieMetallicMultiplier"),
        Shader.PropertyToID("_Glossiness"),
        Shader.PropertyToID("_MochieRoughnessMultiplier"),
    };

    // Components
    private CVRPickupObject _pickup;
    private CVRPlayerEntity _owner;
    private Transform _localPlayerTransform;
    private AvatarHeadPoint _ownerViewPoint;

    // Mario State
    private Vector3[][] _positionBuffers;
    private Vector3[][] _normalBuffers;
    private Vector3[] _lerpPositionBuffer;
    private Vector3[] _lerpNormalBuffer;
    private Vector3[] _colorBuffer;
    private Color[] _colorBufferColors;
    private Vector2[] _uvBuffer;
    private int _buffIndex;
    private Interop.SM64MarioState[] _states;
    private ushort _numTrianglesUsed;
    private ushort _previousNumTrianglesUsed;

    // Renderer
    private GameObject _marioRendererObject;
    private MeshRenderer _marioMeshRenderer;
    private Mesh _marioMesh;

    // Internal
    [NonSerialized] public uint MarioId;
    [NonSerialized] private bool _enabled;
    [NonSerialized] private bool _initialized;
    [NonSerialized] private bool _wasPickedUp;
    [NonSerialized] private bool _initializedByRemote;
    [NonSerialized] private bool _isDying;
    [NonSerialized] private bool _isNuked;

    // Teleporters
    [NonSerialized] private float _startedTeleporting = float.MinValue;
    [NonSerialized] private Transform _teleportTarget;

    // Bypasses
    [NonSerialized] private bool _wasBypassed;
    [NonSerialized] private bool _isOverMaxCount;
    [NonSerialized] private bool _isOverMaxDistance;

    // Spawnable Inputs
    private int _inputHorizontalIndex;
    private CVRSpawnableValue _inputHorizontal;
    private int _inputVerticalIndex;
    private CVRSpawnableValue _inputVertical;
    private int _inputJumpIndex;
    private CVRSpawnableValue _inputJump;
    private int _inputKickIndex;
    private CVRSpawnableValue _inputKick;
    private int _inputStompIndex;
    private CVRSpawnableValue _inputStomp;

    // Spawnable State Synced Params
    private enum SyncedParameterNames {
        Health,
        Flags,
        Action,
        HasCameraMod,
    }
    private readonly Dictionary<SyncedParameterNames, Tuple<int, CVRSpawnableValue>> _syncParameters = new();

    // Animators
    private enum LocalParameterNames {
        HealthPoints,
        HasMod,
        HasMetalCap,
        HasWingCap,
        HasVanishCap,
        IsMine,
        IsBypassed,
        IsTeleporting,
    }
    private static readonly Dictionary<LocalParameterNames, int> LocalParameters = new() {
        { LocalParameterNames.HealthPoints, Animator.StringToHash(nameof(LocalParameterNames.HealthPoints)) },
        { LocalParameterNames.HasMod, Animator.StringToHash(nameof(LocalParameterNames.HasMod)) },
        { LocalParameterNames.HasMetalCap, Animator.StringToHash(nameof(LocalParameterNames.HasMetalCap)) },
        { LocalParameterNames.HasWingCap, Animator.StringToHash(nameof(LocalParameterNames.HasWingCap)) },
        { LocalParameterNames.HasVanishCap, Animator.StringToHash(nameof(LocalParameterNames.HasVanishCap)) },
        { LocalParameterNames.IsMine, Animator.StringToHash(nameof(LocalParameterNames.IsMine)) },
        { LocalParameterNames.IsBypassed, Animator.StringToHash(nameof(LocalParameterNames.IsBypassed)) },
        { LocalParameterNames.IsTeleporting, Animator.StringToHash(nameof(LocalParameterNames.IsTeleporting)) },
    };

    // Threading
    //private Interop.SM64MarioInputs _currentInputs;
    private readonly object _lock = new();

    // Melon prefs
    private static float _skipFarMarioDistance;

    static CVRSM64Mario() {
        _skipFarMarioDistance = Config.MeSkipFarMarioDistance.Value;
        Config.MeSkipFarMarioDistance.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            _skipFarMarioDistance = newValue;
            MelonLogger.Msg($"Changed the distance that will skip animating other marios {oldValue} to {newValue}.");
        });
    }

    private void LoadInput(out CVRSpawnableValue parameter, out int index, string inputName) {
        try {
            index = spawnable.syncValues.FindIndex(value => value.name == inputName);
            parameter = spawnable.syncValues[index];
        }
        catch (ArgumentException) {
            var err = $"{nameof(CVRSM64Mario)} requires a ${nameof(CVRSpawnable)} with a synced value named ${inputName}!";
            MelonLogger.Error(err);
            spawnable.Delete();
            throw new Exception(err);
        }
    }

    private void Start() {

        if (!CVRSuperMario64.FilesLoaded) {
            MelonLogger.Error($"The mod files were not properly loaded! Check the errors at the startup!");
            Destroy(this);
            return;
        }

        #if DEBUG
        MelonLogger.Msg($"Initializing a SM64Mario Spawnable...");
        #endif

        // Check for Spawnable component
        if (spawnable != null) {
            #if DEBUG
            MelonLogger.Msg($"SM64Mario Spawnable was set! We don't need to look for it!");
            #endif
        }
        else {
            spawnable = GetComponent<CVRSpawnable>();
            if (spawnable == null) {
                var err = $"{nameof(CVRSM64Mario)} requires a ${nameof(CVRSpawnable)} on the same GameObject!";
                MelonLogger.Error(err);
                Destroy(this);
                return;
            }

            #if DEBUG
            MelonLogger.Msg($"SM64Mario Spawnable was missing, but we look at the game object and found one!");
            #endif
        }


        if (!spawnable.IsMine()) {
            _owner = MetaPort.Instance.PlayerManager.NetworkPlayers.Find(entity => entity.Uuid == spawnable.ownerId);
            _ownerViewPoint = _owner.PuppetMaster.ViewPoint;
            if (_ownerViewPoint == null || _ownerViewPoint == null) {
                var err = $"{nameof(CVRSM64Mario)} failed to start because couldn't find the viewpoint of the owner of it!";
                MelonLogger.Error(err);
                spawnable.Delete();
                return;
            }
        }

        // Load the spawnable inputs
        LoadInput(out _inputHorizontal, out _inputHorizontalIndex, "Horizontal");
        LoadInput(out _inputVertical, out _inputVerticalIndex, "Vertical");
        LoadInput(out _inputJump, out _inputJumpIndex, "Jump");
        LoadInput(out _inputKick, out _inputKickIndex, "Kick");
        LoadInput(out _inputStomp, out _inputStompIndex, "Stomp");

        // Load the spawnable synced params
        foreach (SyncedParameterNames syncedParam in Enum.GetValues(typeof(SyncedParameterNames))) {
            LoadInput(out var syncedValue, out var syncedValueIndex, syncedParam.ToString());
            _syncParameters.Add(syncedParam, new Tuple<int, CVRSpawnableValue>(syncedValueIndex, syncedValue));
        }

        // Check the advanced settings
        if (advancedOptions) {

            // Check the animators
            var toNuke = new HashSet<Animator>();
            foreach (var animator in animators) {
                if (animator == null || animator.runtimeAnimatorController == null) {
                    toNuke.Add(animator);
                }
                else {
                    animator.SetBool(LocalParameters[LocalParameterNames.HasMod], true);
                    if (spawnable.IsMine()) {
                        animator.SetBool(LocalParameters[LocalParameterNames.IsMine], true);
                    }
                }
            }
            foreach (var animatorToNuke in toNuke) animators.Remove(animatorToNuke);
            if (toNuke.Count > 0) {
                var animatorsToNukeStr = toNuke.Select(animToNuke => animToNuke.gameObject.name);
                MelonLogger.Warning($"Removing animators: {string.Join(", ", animatorsToNukeStr)} because they were null or had no controllers slotted.");
            }
        }

        // Pickup
        _pickup = GetComponent<CVRPickupObject>();
        if (_pickup != null && !IsMine()) {
            _pickup.enabled = false;
        }

        // Player setup transform
        _localPlayerTransform = PlayerSetup.Instance.transform;

        // Ensure the list of renderers is not null and has no null values
        cameraModTransformRenderersToHide ??= new List<Renderer>();
        cameraModTransformRenderersToHide.RemoveAll(r => r == null);

        // Check for the SM64Mario component
        var mario = GetComponent<CVRSM64Mario>();
        if (mario == null) {
            #if DEBUG
            MelonLogger.Msg($"Adding the ${nameof(CVRSM64Mario)} Component...");
            #endif
            gameObject.AddComponent<CVRSM64Mario>();
        }

        CVRSM64Context.UpdateMarioCount();
        CVRSM64Context.UpdatePlayerMariosState();

        #if DEBUG
        MelonLogger.Msg($"A SM64Mario Spawnable was initialize! Is ours: {spawnable.IsMine()}");
        #endif

        _initialized = true;
    }

    private void OnEnable() {
        CVRSM64Context.RegisterMario(this);
        CVRSM64Context.UpdateMarioCount();

        var initPos = transform.position;
        MarioId = Interop.MarioCreate(new Vector3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR);

        _marioRendererObject = new GameObject("MARIO");
        _marioRendererObject.hideFlags |= HideFlags.HideInHierarchy;

        _marioMeshRenderer = _marioRendererObject.AddComponent<MeshRenderer>();
        var meshFilter = _marioRendererObject.AddComponent<MeshFilter>();

        lock (_lock) {
            _states = new Interop.SM64MarioState[2] {
                new Interop.SM64MarioState(),
                new Interop.SM64MarioState()
            };
        }

        // If not material is set, let's set our fallback one
        if (material == null) {
            #if DEBUG
            MelonLogger.Msg($"CVRSM64Mario didn't have a material, assigning the default material...");
            #endif
            material = CVRSuperMario64.GetMarioMaterial();
        }
        else {
            #if DEBUG
            MelonLogger.Msg($"CVRSM64Mario had a material! Using the existing one...");
            #endif
        }

        // Create a new instance of the material, so marios don't interfere with each other
        material = new Material(material);
        _colorNormal = new Color(material.color.r, material.color.g, material.color.b, material.color.a);
        _colorVanish = new Color(material.color.r, material.color.g, material.color.b, VanishOpacity);
        _colorInvisible = new Color(material.color.r, material.color.g, material.color.b, 0f);

        _marioMeshRenderer.material = material;

        // Replace the material's texture with mario's textures
        if (replaceTextures) {
            foreach (var propertyToReplaceWithTexture in propertiesToReplaceWithTexture) {
                try {
                    _marioMeshRenderer.sharedMaterial.SetTexture(propertyToReplaceWithTexture, Interop.marioTexture);
                }
                catch (Exception e) {
                    MelonLogger.Error($"Attempting to replace the texture in the shader property name {propertyToReplaceWithTexture}...");
                    MelonLogger.Error(e);
                }
            }
        }

        _marioRendererObject.transform.localScale = new Vector3(-1, 1, 1) / Interop.SCALE_FACTOR;
        _marioRendererObject.transform.localPosition = Vector3.zero;

        _lerpPositionBuffer = new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _lerpNormalBuffer = new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _positionBuffers = new Vector3[][] { new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _normalBuffers = new Vector3[][] { new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _colorBuffer = new Vector3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _colorBufferColors = new Color[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _uvBuffer = new Vector2[3 * Interop.SM64_GEO_MAX_TRIANGLES];

        _marioMesh = new Mesh {
            vertices = _lerpPositionBuffer,
            triangles = Enumerable.Range(0, 3 * Interop.SM64_GEO_MAX_TRIANGLES).ToArray(),
        };
        meshFilter.sharedMesh = _marioMesh;

        _enabled = true;
    }

    private void OnDestroy() {
        OnDisable();
    }

    private void OnDisable() {

        if (_marioRendererObject != null) {
            Destroy(_marioRendererObject);
            _marioRendererObject = null;
        }

        if (Interop.isGlobalInit) {
            CVRSM64Context.UnregisterMario(this);
            Interop.MarioDelete(MarioId);
            CVRSM64Context.UpdateMarioCount();
        }

        _enabled = false;
    }

    // private void UpdateCurrentInputs() {
    //
    //     var currentInputs = new Interop.SM64MarioInputs();
    //
    //     var look = _inputProvider.GetCameraLookDirection();
    //     look.y = 0;
    //     look = look.normalized;
    //
    //     var joystick = _inputProvider.GetJoystickAxes();
    //
    //     currentInputs.camLookX = -look.x;
    //     currentInputs.camLookZ = look.z;
    //     currentInputs.stickX = joystick.x;
    //     currentInputs.stickY = -joystick.y;
    //     currentInputs.buttonA = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Jump) ? (byte)1 : (byte)0;
    //     currentInputs.buttonB = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Kick) ? (byte)1 : (byte)0;
    //     currentInputs.buttonZ = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Stomp) ? (byte)1 : (byte)0;
    //
    //     lock (_lock) {
    //         _currentInputs = currentInputs;
    //     }
    // }
    //
    // internal void Sm64MarioTickThread() {
    //     lock (_lock) {
    //         _states[_buffIndex] = Interop.MarioTick(_marioId, _currentInputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer);
    //
    //         for (var i = 0; i < _colorBuffer.Length; ++i) {
    //             _colorBufferColors[i] = new Color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z, 1);
    //         }
    //
    //     }
    // }
    //
    // internal void Sm64MarioTickMain() {
    //     lock (_lock) {
    //         _marioMesh.colors = _colorBufferColors;
    //         _marioMesh.uv = _uvBuffer;
    //
    //         _buffIndex = 1 - _buffIndex;
    //     }
    //
    //     UpdateCurrentInputs();
    // }

    public void ContextFixedUpdateSynced(List<CVRSM64Mario> marios) {

        if (!_enabled || !_initialized || _isNuked) return;

        // Janky remote sync check
        if (!IsMine() && !_initializedByRemote) {
            if (_syncParameters[SyncedParameterNames.Health].Item2.currentValue != 0) _initializedByRemote = true;
            else return;
        }

        UpdateIsOverMaxDistance();

        if (_wasBypassed) return;

        var inputs = new Interop.SM64MarioInputs();
        var look = GetCameraLookDirection();
        look.y = 0;
        look = look.normalized;

        var joystick = GetJoystickAxes();

        inputs.camLookX = -look.x;
        inputs.camLookZ = look.z;
        inputs.stickX = joystick.x;
        inputs.stickY = -joystick.y;
        inputs.buttonA = GetButtonHeld(Button.Jump) ? (byte)1 : (byte)0;
        inputs.buttonB = GetButtonHeld(Button.Kick) ? (byte)1 : (byte)0;
        inputs.buttonZ = GetButtonHeld(Button.Stomp) ? (byte)1 : (byte)0;

        lock (_lock) {
            _states[_buffIndex] = Interop.MarioTick(MarioId, inputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer, out _numTrianglesUsed);

            // If the tris count changes, reset the buffers
            if (_previousNumTrianglesUsed != _numTrianglesUsed) {
                for (var i = _numTrianglesUsed * 3; i < _positionBuffers[_buffIndex].Length; i++) {
                    _positionBuffers[_buffIndex][i] = Vector3.zero;
                    _normalBuffers[_buffIndex][i] = Vector3.zero;
                }
                _positionBuffers[_buffIndex].CopyTo(_positionBuffers[1 - _buffIndex], 0);
                _normalBuffers[_buffIndex].CopyTo(_normalBuffers[1 - _buffIndex], 0);
                _positionBuffers[_buffIndex].CopyTo(_lerpPositionBuffer, 0);
                _normalBuffers[_buffIndex].CopyTo(_lerpNormalBuffer, 0);

                _previousNumTrianglesUsed = _numTrianglesUsed;
            }

            _buffIndex = 1 - _buffIndex;
        }


        var currentStateFlags = GetCurrentState().flags;
        var currentStateAction = GetCurrentState().action;

        if (spawnable.IsMine()) {

            if (_pickup != null && _pickup.IsGrabbedByMe != _wasPickedUp) {
                if (_wasPickedUp) Throw();
                else Hold();
                _wasPickedUp = _pickup.IsGrabbedByMe;
            }

            // Send the current flags and action to remotes
            spawnable.SetValue(_syncParameters[SyncedParameterNames.Flags].Item1, Convert.ToSingle(currentStateFlags));
            spawnable.SetValue(_syncParameters[SyncedParameterNames.Action].Item1, Convert.ToSingle(currentStateAction));

            // Check Interactables (trigger mario caps)
            CVRSM64Interactable.HandleInteractables(this, currentStateFlags);

            // Check Teleporters
            CVRSM64Teleporter.HandleTeleporters(this, currentStateFlags, ref _startedTeleporting, ref _teleportTarget);

            // Check for deaths, so we delete the prop
            if (!_isDying && IsDead()) {
                _isDying = true;
                Invoke(nameof(SetMarioAsNuked), 15f);
            }
        }
        else {

            // Grab the current flags and action from the owner
            var syncedFlags = Convert.ToUInt32(_syncParameters[SyncedParameterNames.Flags].Item2.currentValue);
            var syncedAction = Convert.ToUInt32(_syncParameters[SyncedParameterNames.Action].Item2.currentValue);

            // This seems to be kinda broken, maybe revisit syncing the WHOLE state instead
            // if (currentStateFlags != syncedFlags) SetState(syncedAction);
            // if (currentStateAction != syncedAction) SetAction(syncedAction);

            // Trigger the cap if the synced values have cap (if we already have the cape it will ignore)
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.VanishCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.VanishCap, false);
            }
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.MetalCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.MetalCap, false);
            }
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.WingCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.WingCap, false);
            }

            // Trigger teleport for remotes
            if (Utils.IsTeleporting(syncedFlags) && Time.time > _startedTeleporting + 5 * CVRSM64Teleporter.TeleportDuration) {
                _startedTeleporting = Time.time;
            }
        }

        // Update Caps material and animator's parameters
        var hasVanishCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.VanishCap);
        var hasWingCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.WingCap);
        var hasMetalCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.MetalCap);
        var isTeleporting = Utils.IsTeleporting(currentStateFlags);

        // Handle teleporting fading in
        if (Time.time > _startedTeleporting && Time.time <= _startedTeleporting + CVRSM64Teleporter.TeleportDuration) {
            material.color = Color.Lerp(_colorNormal, _colorInvisible, Mathf.Clamp(Time.time-_startedTeleporting, 0f, 1f));
        }
        // Handle teleport fading out
        else if (Time.time > _startedTeleporting + CVRSM64Teleporter.TeleportDuration && Time.time <= _startedTeleporting + 2 * CVRSM64Teleporter.TeleportDuration) {
            material.color = Color.Lerp(_colorInvisible, _colorNormal, Mathf.Clamp(Time.time-_startedTeleporting-CVRSM64Teleporter.TeleportDuration, 0f, 1f));
        }
        else {
            material.SetColor(_colorProperty, hasVanishCap ? _colorVanish : _colorNormal);
            foreach (var metallicProperty in _metallicProperties) {
                material.SetFloat(metallicProperty, hasMetalCap ? 1f : 0f);
            }
        }

        foreach (var animator in animators) {
            animator.SetBool(LocalParameters[LocalParameterNames.HasVanishCap], hasVanishCap);
            animator.SetBool(LocalParameters[LocalParameterNames.HasWingCap], hasWingCap);
            animator.SetBool(LocalParameters[LocalParameterNames.HasMetalCap], hasMetalCap);
            animator.SetBool(LocalParameters[LocalParameterNames.IsTeleporting], isTeleporting);
        }

        // Check if we're taking damage
        lock (marios) {
            var attackingMario = marios.FirstOrDefault(mario =>
                mario != this && mario.GetCurrentState().IsAttacking() &&
                Vector3.Distance(mario.transform.position, this.transform.position) <= 0.1f);
            if (attackingMario != null) {
                TakeDamage(attackingMario.transform.position, 1);
            }
        }

        for (var i = 0; i < _colorBuffer.Length; ++i) {
            _colorBufferColors[i] = new Color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z, 1f);
        }

        _marioMesh.colors = _colorBufferColors;
        _marioMesh.uv = _uvBuffer;
    }

    public void ContextUpdateSynced() {
        if (!_enabled || !_initialized || _isNuked) return;

        if (!IsMine() && !_initializedByRemote) return;

        if (_wasBypassed) return;

        var t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

        lock (_lock) {
            var j = 1 - _buffIndex;

            for (var i = 0; i < _numTrianglesUsed * 3; ++i) {
                _lerpPositionBuffer[i] = Vector3.LerpUnclamped(_positionBuffers[_buffIndex][i], _positionBuffers[j][i], t);
                _lerpNormalBuffer[i] = Vector3.LerpUnclamped(_normalBuffers[_buffIndex][i], _normalBuffers[j][i], t);
            }

            // Handle the position and rotation
            if (spawnable.IsMine() && !IsBeingGrabbedByMe()) {
                transform.position = Vector3.LerpUnclamped(_states[_buffIndex].UnityPosition, _states[j].UnityPosition, t);
                transform.rotation = Quaternion.LerpUnclamped(_states[_buffIndex].UnityRotation, _states[j].UnityRotation, t);
            }
            else {
                SetPosition(transform.position);
                SetFaceAngle(transform.rotation);
            }

            // Handle other synced params
            if (spawnable.IsMine()) {

                // Handle health sync
                spawnable.SetValue(_syncParameters[SyncedParameterNames.Health].Item1, _states[j].HealthPoints);

                // Handle controlling mario with camera mod sub-sync sync
                if (MarioCameraMod.Instance.IsControllingMario(this)) {
                    if (advancedOptions && cameraModTransform != null) {
                        var camTransform = MarioCameraMod.Instance.GetCameraTransform();
                        cameraModTransform.SetPositionAndRotation(camTransform.position, camTransform.rotation);
                    }
                }
            }
            else {
                SetHealthPoints(_syncParameters[SyncedParameterNames.Health].Item2.currentValue);
            }

            // Handle local healthPoints param
            foreach (var animator in animators) {
                animator.SetInteger(LocalParameters[LocalParameterNames.HealthPoints], (int) _states[j].HealthPoints);
            }
        }

        _marioMesh.vertices = _lerpPositionBuffer;
        _marioMesh.normals = _lerpNormalBuffer;

        _marioMesh.RecalculateBounds();
        _marioMesh.RecalculateTangents();
    }

    private Vector2 GetJoystickAxes() {
        // Update the spawnable sync values and send the values
        if (spawnable.IsMine()) {
            var horizontal = MarioInputModule.Instance.horizontal;
            var vertical = MarioInputModule.Instance.vertical;
            spawnable.SetValue(_inputHorizontalIndex, horizontal);
            spawnable.SetValue(_inputVerticalIndex, vertical);
            return new Vector2(horizontal, vertical);
        }

        // Send the current values from the spawnable
        return new Vector2(_inputHorizontal.currentValue, _inputVertical.currentValue);
    }

    private Vector3 GetCameraLookDirection() {

        // If we're overriding the camera position transform use it instead.
        if (overrideCameraPosition && cameraPositionTransform != null) {
            return cameraPositionTransform.forward;
        }

        // If we're using the CVR Camera Mod
        if (spawnable.IsMine()) {
            if (MarioCameraMod.Instance.IsControllingMario(this)) {
                spawnable.SetValue(_syncParameters[SyncedParameterNames.HasCameraMod].Item1, 1f);
                return MarioCameraMod.Instance.GetCameraTransform().forward;
            }
            spawnable.SetValue(_syncParameters[SyncedParameterNames.HasCameraMod].Item1, 0f);
        }

        // Use our own camera
        if (spawnable.IsMine()) {
            return PlayerSetup.Instance.activeCam.transform.forward;
        }

        // Use the remote player viewpoint. This value will be overwritten after with the prop face angle sync
        if (_ownerViewPoint) {
            return _ownerViewPoint.transform.forward;
        }

        return Vector3.zero;
    }

    private enum Button {
        Jump,
        Kick,
        Stomp,
    }

    private bool GetButtonHeld(Button button) {
        if (spawnable.IsMine()) {
            switch (button) {
                case Button.Jump: {
                    var jump = MarioInputModule.Instance.jump;
                    spawnable.SetValue(_inputJumpIndex, jump ? 1f : 0f);
                    return jump;
                }
                case Button.Kick: {
                    var kick = MarioInputModule.Instance.kick;
                    spawnable.SetValue(_inputKickIndex, kick ? 1f : 0f);
                    return kick;
                }
                case Button.Stomp: {
                    var stomp = MarioInputModule.Instance.stomp;
                    spawnable.SetValue(_inputStompIndex, stomp ? 1f : 0f);
                    return stomp;
                }
            }

            return false;
        }

        switch (button) {
            case Button.Jump: return _inputJump.currentValue > 0.5f;
            case Button.Kick: return _inputKick.currentValue > 0.5f;
            case Button.Stomp: return _inputStomp.currentValue > 0.5f;
        }

        return false;
    }

    public void SetIsOverMaxCount(bool isOverTheMaxCount) {
        _isOverMaxCount = isOverTheMaxCount;
        UpdateIsBypassed();
    }

    private void UpdateIsOverMaxDistance() {
        // Check the distance to see if we should ignore the updates
        _isOverMaxDistance =
            !IsMine()
            && Vector3.Distance(transform.position, _localPlayerTransform.position) > _skipFarMarioDistance
            && (!MarioCameraMod.IsControllingAMario(out var mario) || Vector3.Distance(transform.position, mario.transform.position) > _skipFarMarioDistance);
        UpdateIsBypassed();
    }

    private void UpdateIsBypassed() {
        if (!_initialized) return;
        var isBypassed = _isOverMaxDistance || _isOverMaxCount;
        if (isBypassed == _wasBypassed) return;
        _wasBypassed = isBypassed;

        // Handle local bypassed parameter
        foreach (var animator in animators) {
            animator.SetBool(LocalParameters[LocalParameterNames.IsBypassed], isBypassed);
        }

        // Enable/Disable the mario's mesh renderer
        _marioMeshRenderer.enabled = !isBypassed;
    }

    private Interop.SM64MarioState GetCurrentState() {
        lock (_lock) {
            return _states[1 - _buffIndex];
        }
    }

    private Interop.SM64MarioState GetPreviousState() {
        lock (_lock) {
            return _states[_buffIndex];
        }
    }

    public void SetPosition(Vector3 pos) {
        if (!_enabled) return;
        Interop.MarioSetPosition(MarioId, pos);
    }

    public void SetRotation(Quaternion rot) {
        if (!_enabled) return;
        Interop.MarioSetRotation(MarioId, rot);
    }

    public void SetFaceAngle(Quaternion rot) {
        if (!_enabled) return;
        Interop.MarioSetFaceAngle(MarioId, rot);
    }

    public void SetHealthPoints(float healthPoints) {
        if (!_enabled) return;
        Interop.MarioSetHealthPoints(MarioId, healthPoints);
    }

    public void TakeDamage(Vector3 worldPosition, uint damage) {
        if (!_enabled) return;
        Interop.MarioTakeDamage(MarioId, worldPosition, damage);
    }

    internal void WearCap(uint flags, Utils.MarioCapType capType, bool playMusic) {
        if (!_enabled) return;

        if (Utils.HasCapType(flags, capType)) return;
        switch (capType) {
            case Utils.MarioCapType.VanishCap:
                // Original game is 15 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_VANISH_CAP, 40f, playMusic);
                break;
            case Utils.MarioCapType.MetalCap:
                // Originally game is 15 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_METAL_CAP, 40f, playMusic);
                break;
            case Utils.MarioCapType.WingCap:
                // Originally game is 40 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_WING_CAP, 40f, playMusic);
                break;
        }
    }

    private bool IsBeingGrabbedByMe() {
        return _pickup != null && _pickup.IsGrabbedByMe;
    }

    public bool IsMine() => spawnable.IsMine();

    public string GetOwnerId() => spawnable.ownerId;

    private bool IsDead() {
        lock (_lock) {
            return GetCurrentState().health < 1 * Interop.SM64_HEALTH_PER_HEALTH_POINT;
        }
    }

    private void SetMarioAsNuked() {
        lock (_lock) {
            _isNuked = true;
            var deleteMario = Config.MeDeleteMarioAfterDead.Value;
            #if DEBUG
            MelonLogger.Msg($"One of our Marios died, it has been 15 seconds and we're going to " +
                            $"{(deleteMario ? "delete the mario" : "stopping its engine updates")}.");
            #endif
            if (deleteMario) spawnable.Delete();
        }
    }

    private void SetAction(ActionFlags actionFlags) {
        Interop.MarioSetAction(MarioId, actionFlags);
    }

    private void SetAction(uint actionFlags) {
        Interop.MarioSetAction(MarioId, actionFlags);
    }

    private void SetState(uint flags) {
        Interop.MarioSetState(MarioId, flags);
    }

    private void SetVelocity(Vector3 unityVelocity) {
        Interop.MarioSetVelocity(MarioId, unityVelocity);
    }

    private void SetForwardVelocity(float unityVelocity) {
        Interop.MarioSetForwardVelocity(MarioId, unityVelocity);
    }

    private void Hold() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_GRABBED);
    }

    public void TeleportStart() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_TELEPORT_FADE_OUT);
    }

    public void TeleportEnd() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_TELEPORT_FADE_IN);
    }

    private void Throw() {
        if (IsDead()) return;
        var currentState = GetCurrentState();
        var throwVelocityFlat = currentState.UnityPosition - GetPreviousState().UnityPosition;
        SetFaceAngle(Quaternion.LookRotation(throwVelocityFlat));
        var hasWingCap = Utils.HasCapType(currentState.flags, Utils.MarioCapType.WingCap);
        SetAction(hasWingCap ? ActionFlags.ACT_FLYING : ActionFlags.ACT_THROWN_FORWARD);
        SetVelocity(throwVelocityFlat);
        SetForwardVelocity(throwVelocityFlat.magnitude);
    }

    public void Heal(byte healthPoints) {
        if (!_enabled) return;

        if (IsDead()) {
            // Revive (not working)
            Interop.MarioSetHealthPoints(MarioId, healthPoints + 1);
            SetAction(ActionFlags.ACT_FLAG_IDLE);
        }
        else {
            Interop.MarioHeal(MarioId, healthPoints);
        }
    }

    public void PickupCoin(CVRSM64InteractableParticles.ParticleType coinType) {
        if (!_enabled) return;

        switch (coinType) {
            case CVRSM64InteractableParticles.ParticleType.GoldCoin:
                Interop.MarioHeal(MarioId, 1);
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_COIN);
                break;
            case CVRSM64InteractableParticles.ParticleType.BlueCoin:
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_COIN);
                Interop.MarioHeal(MarioId, 5);
                break;
            case CVRSM64InteractableParticles.ParticleType.RedCoin:
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_RED_COIN);
                Interop.MarioHeal(MarioId, 2);
                break;
        }
    }

    public List<Renderer> GetRenderersToHideFromCamera() => cameraModTransformRenderersToHide;

    public bool IsFirstPerson() {
        lock (_lock) {
            return GetCurrentState().IsFlyingOrSwimming();
        }
    }

    public bool IsSwimming() {
        lock (_lock) {
            return GetCurrentState().IsSwimming();
        }
    }

    public bool IsFlying() {
        lock (_lock) {
            return GetCurrentState().IsFlying();
        }
    }
}
