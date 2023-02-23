using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64CMario : MonoBehaviour {

    [SerializeField] internal Material material = null;
    [SerializeField] internal bool replaceTextures = true;
    [SerializeField] internal List<string> propertiesToReplaceWithTexture = new() { "_MainTex" };

    private CVRSM64Input _inputProvider;

    private Vector3[][] _positionBuffers;
    private Vector3[][] _normalBuffers;
    private Vector3[] _lerpPositionBuffer;
    private Vector3[] _lerpNormalBuffer;
    private Vector3[] _colorBuffer;
    private Color[] _colorBufferColors;
    private Vector2[] _uvBuffer;
    private int _buffIndex;
    private Interop.SM64MarioState[] _states;

    private GameObject _marioRendererObject;
    private Mesh _marioMesh;
    private uint _marioId;

    private bool _enabled;

    // Threading
    //private Interop.SM64MarioInputs _currentInputs;
    private readonly object _lock = new();

    private void OnEnable() {
        CVRSM64CContext.RegisterMario(this);

        var initPos = transform.position;
        _marioId = Interop.MarioCreate(new Vector3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR);

        _inputProvider = GetComponent<CVRSM64Input>();
        if (_inputProvider == null) {
            throw new Exception("Need to add an input provider component to Mario");
        }

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
        var look = _inputProvider.GetCameraLookDirection();
        look.y = 0;
        look = look.normalized;

        var joystick = _inputProvider.GetJoystickAxes();

        inputs.camLookX = -look.x;
        inputs.camLookZ = look.z;
        inputs.stickX = joystick.x;
        inputs.stickY = -joystick.y;
        inputs.buttonA = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Jump) ? (byte)1 : (byte)0;
        inputs.buttonB = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Kick) ? (byte)1 : (byte)0;
        inputs.buttonZ = _inputProvider.GetButtonHeld(CVRSM64Input.Button.Stomp) ? (byte)1 : (byte)0;

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

            // Handle the position
            if (_inputProvider.IsMine() && !_inputProvider.IsPositionOverriden()) {
                transform.position = Vector3.LerpUnclamped(_states[_buffIndex].unityPosition, _states[j].unityPosition, t);
            }
            else {
                SetPosition(transform.position);
            }
        }

        _marioMesh.vertices = _lerpPositionBuffer;
        _marioMesh.normals = _lerpNormalBuffer;

        _marioMesh.RecalculateBounds();
        _marioMesh.RecalculateTangents();
    }

    public void SetPosition(Vector3 pos) {
        if (!_enabled) return;
        Interop.MarioSetPosition(_marioId, new Vector3(-pos.x, pos.y, pos.z) * Interop.SCALE_FACTOR);
    }
}
