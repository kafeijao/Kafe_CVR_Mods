using ABI.CCK.Components;
using CCK.Debugger.Resources;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.PointerVisualizers;

public abstract class PointerVisualizer : MonoBehaviour {

    private static readonly Dictionary<CVRPointer, PointerVisualizer> VisualizersAll = new();
    private static readonly Dictionary<CVRPointer, PointerVisualizer> VisualizersActive = new();

    private const string GameObjectName = "[CCK.Debugger] Pointer Visualizer";

    private Material _materialStandard;
    private Material _materialNeitri;

    private CVRPointer _pointer;
    protected GameObject VisualizerGo;

    public static bool CreateVisualizer(CVRPointer pointer, out PointerVisualizer visualizer) {

        // Check if the component already exists, if so ignore the creation request but enable it
        if (pointer.TryGetComponent(out visualizer)) return true;

        // Ignore pointers without colliders (should never happen ?)
        if (!pointer.TryGetComponent(out Collider collider)) return false;

        // Instantiate the proper visualizer for each collider type
        switch (collider) {

            case SphereCollider sphereCollider:
                var sphereVisualizer = pointer.gameObject.AddComponent<PointerSphereVisualizer>();
                sphereVisualizer.PointerCollider = sphereCollider;
                visualizer = sphereVisualizer;
                visualizer.InitializeVisualizer(pointer, Utils.Misc.GetPrimitiveMesh(PrimitiveType.Sphere));
                break;

            case BoxCollider boxCollider:
                var boxVisualizer = pointer.gameObject.AddComponent<PointerBoxVisualizer>();
                boxVisualizer.PointerCollider = boxCollider;
                visualizer = boxVisualizer;
                visualizer.InitializeVisualizer(pointer, Utils.Misc.GetPrimitiveMesh(PrimitiveType.Cube));
                break;

            case CapsuleCollider capsuleCollider:
                var capsuleVisualizer = pointer.gameObject.AddComponent<PointerCapsuleVisualizer>();
                capsuleVisualizer.PointerCollider = capsuleCollider;
                visualizer = capsuleVisualizer;
                visualizer.InitializeVisualizer(pointer, Utils.Misc.GetPrimitiveMesh(PrimitiveType.Capsule));
                break;

            case MeshCollider meshCollider:
                var meshVisualizer = pointer.gameObject.AddComponent<PointerMeshVisualizer>();
                visualizer = meshVisualizer;
                visualizer.InitializeVisualizer(pointer, meshCollider.sharedMesh);
                break;

            default:
                // CVR only support those collider types, so we're going to ignore everything else
                return false;
        }

        visualizer.enabled = false;
        return true;
    }

    private bool IsInitialized() => VisualizerGo != null;


    private void InitializeVisualizer(CVRPointer pointer, Mesh mesh) {
        VisualizerGo = new GameObject(GameObjectName) {
            layer = LayerMask.NameToLayer("UI Internal")
        };

        _pointer = pointer;

        // Create mesh filter
        var sphereMeshFilter = VisualizerGo.AddComponent<MeshFilter>();
        sphereMeshFilter.mesh = mesh;

        // Create standard shader material with render type set to Fade
        _materialStandard = new Material(Misc.ShaderStandard);
        _materialStandard.SetInt(Misc.MatSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _materialStandard.SetInt(Misc.MatDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _materialStandard.SetInt(Misc.MatZWrite, 0);
        _materialStandard.DisableKeyword(Misc.ShaderAlphaTest);
        _materialStandard.EnableKeyword(Misc.ShaderAlphaBlend);
        _materialStandard.DisableKeyword(Misc.ShaderAlphaPreMultiply);
        _materialStandard.renderQueue = 3000;
        _materialStandard.SetColor(Misc.MatMainColor, Misc.ColorBlueFade);

        // Create neitri fade outline shader material
        _materialNeitri = new Material(AssetBundleLoader.GetShader(ShaderType.NeitriDistanceFadeOutline));
        _materialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        _materialNeitri.SetFloat(Misc.MatOutlineSmoothness, 0.1f);
        _materialNeitri.SetFloat(Misc.MatFadeInBehindObjectsDistance, 2f);
        _materialNeitri.SetFloat(Misc.MatFadeOutBehindObjectsDistance, 10f);
        _materialNeitri.SetFloat(Misc.MatFadeInCameraDistance, 10f);
        _materialNeitri.SetFloat(Misc.MatFadeOutCameraDistance, 15f);
        _materialNeitri.SetFloat(Misc.MatShowOutlineInFrontOfObjects, 0f);
        _materialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorBlue);


        // Create the renderer and assign material
        var renderer = VisualizerGo.AddComponent<MeshRenderer>();
        renderer.materials = new[] { _materialStandard, _materialNeitri };

        // Add as a child to the pointer
        VisualizerGo.transform.SetParent(pointer.transform, false);

        // Hide by default
        VisualizerGo.SetActive(false);
    }

    protected virtual void Start() {
        VisualizersAll[_pointer] = this;
    }

    private void OnDestroy() {
        VisualizersActive.Remove(_pointer);
        VisualizersAll.Remove(_pointer);
    }

    private void OnEnable() {
        if (!IsInitialized()) return;
        VisualizerGo.SetActive(true);
        VisualizersActive.Add(_pointer, this);
    }

    private void OnDisable() {
        if (!IsInitialized()) return;
        VisualizerGo.SetActive(false);
        VisualizersActive.Remove(_pointer);
    }

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var visualizer in VisualizersAll.Values.ToList()) {
            visualizer.enabled = false;
        }
    }
}
