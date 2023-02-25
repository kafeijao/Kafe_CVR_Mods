using ABI_RC.Core.Player;
using ABI_RC.Core.Player.AvatarTracking.Remote;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
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
    [SerializeField] private Transform cameraPositionTransform;


    // Components
    private CVRPickupObject _pickup;
    private CVRPlayerEntity _owner;
    private Traverse<RemoteHeadPoint> _ownerViewPoint;

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

    // Renderer
    private GameObject _marioRendererObject;
    private Mesh _marioMesh;

    // Internal
    private uint _marioId;
    private bool _enabled;

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
    private int _syncedHealthIndex;
    private CVRSpawnableValue _syncedHealth;

    // Animators
    private enum LocalParameterNames {
        Lives,
        HasMod,
    }
    private static readonly Dictionary<LocalParameterNames, int> LocalParameters = new() {
        { LocalParameterNames.Lives, Animator.StringToHash(nameof(LocalParameterNames.Lives)) },
        { LocalParameterNames.HasMod, Animator.StringToHash(nameof(LocalParameterNames.HasMod)) },
    };

    // Threading
    //private Interop.SM64MarioInputs _currentInputs;
    private readonly object _lock = new();

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

        MelonLogger.Msg($"Initializing a SM64Mario Spawnable...");

        // Check for Spawnable component
        if (spawnable != null) {
            MelonLogger.Msg($"SM64Mario Spawnable was set! We don't need to look for it!");
        }
        else {
            spawnable = GetComponent<CVRSpawnable>();
            if (spawnable == null) {
                var err = $"{nameof(CVRSM64Mario)} requires a ${nameof(CVRSpawnable)} on the same GameObject!";
                MelonLogger.Error(err);
                Destroy(this);
                return;
            }
            MelonLogger.Msg($"SM64Mario Spawnable was missing, but we look at the game object and found one!");
        }


        if (!spawnable.IsMine()) {
            _owner = MetaPort.Instance.PlayerManager.NetworkPlayers.Find(entity => entity.Uuid == spawnable.ownerId);
            _ownerViewPoint = Traverse.Create(_owner.PuppetMaster).Field<RemoteHeadPoint>("_viewPoint");
            if (_ownerViewPoint == null || _ownerViewPoint.Value == null) {
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
        LoadInput(out _syncedHealth, out _syncedHealthIndex, "Health");

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
        if (!spawnable.IsMine() && _pickup != null) {
            Destroy(_pickup);
        }

        // Check for the SM64Mario component
        var mario = GetComponent<CVRSM64Mario>();
        if (mario == null) {
            MelonLogger.Msg($"Adding the ${nameof(CVRSM64Mario)} Component...");
            gameObject.AddComponent<CVRSM64Mario>();
        }

        MelonLogger.Msg($"A SM64Mario Spawnable was initialize! Is ours: {spawnable.IsMine()}");

        if (spawnable != null && spawnable.IsMine()) {
            MarioInputModule.Instance.controllingMarios++;
        }
    }

    private void OnEnable() {
        CVRSM64CContext.RegisterMario(this);

        var initPos = transform.position;
        _marioId = Interop.MarioCreate(new Vector3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR);

        _marioRendererObject = new GameObject("MARIO");
        _marioRendererObject.hideFlags |= HideFlags.HideInHierarchy;

        var renderer = _marioRendererObject.AddComponent<MeshRenderer>();
        var meshFilter = _marioRendererObject.AddComponent<MeshFilter>();

        _states = new Interop.SM64MarioState[2] {
            new Interop.SM64MarioState(),
            new Interop.SM64MarioState()
        };

        // If not material is set, let's set our fallback one
        if (material == null) {
            MelonLogger.Msg($"CVRSM64Mario didn't have a material, assigning the default material...");
            material = CVRSuperMario64.GetMarioMaterial();
        }
        else {
            MelonLogger.Msg($"CVRSM64Mario had a material! Using the existing one...");
        }

        renderer.material = material;

        // Replace the material's texture with mario's textures
        if (replaceTextures) {
            foreach (var propertyToReplaceWithTexture in propertiesToReplaceWithTexture) {
                try {
                    renderer.sharedMaterial.SetTexture(propertyToReplaceWithTexture, Interop.marioTexture);
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
        if (spawnable != null && spawnable.IsMine()) {
            MarioInputModule.Instance.controllingMarios--;
        }
        OnDisable();
    }

    private void OnDisable() {

        if (_marioRendererObject != null) {
            Destroy(_marioRendererObject);
            _marioRendererObject = null;
        }

        if (Interop.isGlobalInit) {
            CVRSM64CContext.UnregisterMario(this);
            Interop.MarioDelete(_marioId);
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

    public void ContextFixedUpdateSynced() {
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

        _states[_buffIndex] = Interop.MarioTick(_marioId, inputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer);

        for (var i = 0; i < _colorBuffer.Length; ++i) {
            _colorBufferColors[i] = new Color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z, 1);
        }

        _marioMesh.colors = _colorBufferColors;
        _marioMesh.uv = _uvBuffer;

        _buffIndex = 1 - _buffIndex;
    }

    public void ContextUpdateSynced() {
        var t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

        lock (_lock) {
            var j = 1 - _buffIndex;

            for (var i = 0; i < _lerpPositionBuffer.Length; ++i) {
                _lerpPositionBuffer[i] = Vector3.LerpUnclamped(_positionBuffers[_buffIndex][i], _positionBuffers[j][i], t);
                _lerpNormalBuffer[i] = Vector3.LerpUnclamped(_normalBuffers[_buffIndex][i], _normalBuffers[j][i], t);
            }

            // Handle the position and rotation
            if (spawnable.IsMine() && !IsPositionOverriden()) {
                transform.position = Vector3.LerpUnclamped(_states[_buffIndex].UnityPosition, _states[j].UnityPosition, t);
                transform.rotation = Quaternion.LerpUnclamped(_states[_buffIndex].UnityRotation, _states[j].UnityRotation, t);
            }
            else {
                SetPosition(transform.position);
                SetRotation(transform.rotation);
            }

            // Handle other synced params
            if (spawnable.IsMine()) {
                spawnable.SetValue(_syncedHealthIndex, _states[j].Lives);
            }
            else {
                SetLives(_syncedHealth.currentValue);
            }

            // Handle local lives param
            foreach (var animator in animators) {
                animator.SetInteger(LocalParameters[LocalParameterNames.Lives], (int) _states[j].Lives);
            }
        }

        _marioMesh.vertices = _lerpPositionBuffer;
        _marioMesh.normals = _lerpNormalBuffer;

        _marioMesh.RecalculateBounds();
        _marioMesh.RecalculateTangents();
    }

    public void SetPosition(Vector3 pos) {
        if (!_enabled) return;
        Interop.MarioSetPosition(_marioId, pos);
    }

    public void SetRotation(Quaternion rot) {
        if (!_enabled) return;
        Interop.MarioSetRotation(_marioId, rot);
    }

    public void SetLives(float lives) {
        if (!_enabled) return;
        Interop.MarioSetLives(_marioId, lives);
    }

    private bool IsPositionOverriden() {
        return _pickup != null && _pickup.IsGrabbedByMe();
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

        // Use our own camera
        if (spawnable.IsMine()) {
            return PlayerSetup.Instance.GetActiveCamera().transform.forward;
        }

        // Use the remote player viewpoint. This value will be overwritten after with the prop face angle sync
        if (_ownerViewPoint.Value) {
            return _ownerViewPoint.Value.transform.forward;
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

    #if DEBUG
    private void Update() {
        if (Input.GetKeyDown(KeyCode.End)) {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.5f);
            foreach (Collider collider in hitColliders) {
                if (!Utils.IsGoodCollider(collider)) continue;
                MelonLogger.Msg("Collider within 0.5 units: " + collider.gameObject.name);
            }
        }
    }
    #endif
}
