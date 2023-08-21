using ABI.CCK.Components;
using Kafe.CCK.Debugger.Utils;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.PointerVisualizers;

[DefaultExecutionOrder(999999)]
public abstract class PointerVisualizer : MonoBehaviour {
    protected static readonly Dictionary<CVRPointer, PointerVisualizer> VisualizersAll = new();
    private static readonly Dictionary<CVRPointer, PointerVisualizer> VisualizersActive = new();

    private const string GameObjectName = "Visualizer";
    private const string GameObjectWrapperName = "[CCK.Debugger] Pointer Visualizer";

    private Material _materialStandard;
    private Material _materialNeitri;

    protected CVRPointer Pointer;
    protected GameObject VisualizerGo;

    public static PointerVisualizer CreateVisualizer(CVRPointer pointer) {

        // Check if the component already exists, if so ignore the creation request but enable it
        var wrapperTransform = pointer.transform.Find(GameObjectWrapperName);
        if (wrapperTransform != null && wrapperTransform.TryGetComponent(out PointerVisualizer visualizer)) {
            return visualizer;
        }

        // Create the wrapper
        var wrapper = wrapperTransform == null
            ? new GameObject(GameObjectWrapperName) { layer = pointer.gameObject.layer }
            : wrapperTransform.gameObject;
        wrapper.transform.SetParent(pointer.transform, false);
        wrapper.SetActive(false);

        // If there is no collider, assume the type to be a sphere (which is the collider type CVRPointer will add)
        if (!pointer.TryGetComponent(out Collider collider)) {
            visualizer = wrapper.AddComponent<PointerSphereVisualizer>();
            visualizer.InitializeVisualizer(wrapper, Misc.GetPrimitiveMesh(PrimitiveType.Sphere));
            // We're adding PointerCollider reference later when the visualizer is initialized (Start event)
        }

        // Otherwise just instantiate the proper type of visualizer for each collider type
        else {
            switch (collider) {

                case SphereCollider sphereCollider:
                    var sphereVisualizer = wrapper.AddComponent<PointerSphereVisualizer>();
                    sphereVisualizer.PointerCollider = sphereCollider;
                    visualizer = sphereVisualizer;
                    visualizer.InitializeVisualizer(wrapper, Misc.GetPrimitiveMesh(PrimitiveType.Sphere));
                    break;

                case BoxCollider boxCollider:
                    var boxVisualizer = wrapper.AddComponent<PointerBoxVisualizer>();
                    boxVisualizer.PointerCollider = boxCollider;
                    visualizer = boxVisualizer;
                    visualizer.InitializeVisualizer(wrapper, Misc.GetPrimitiveMesh(PrimitiveType.Cube));
                    break;

                case CapsuleCollider capsuleCollider:
                    var capsuleVisualizer = wrapper.AddComponent<PointerCapsuleVisualizer>();
                    capsuleVisualizer.PointerCollider = capsuleCollider;
                    visualizer = capsuleVisualizer;
                    visualizer.InitializeVisualizer(wrapper, Misc.GetPrimitiveMesh(PrimitiveType.Capsule));
                    break;

                case MeshCollider meshCollider:
                    var meshVisualizer = wrapper.AddComponent<PointerMeshVisualizer>();
                    visualizer = meshVisualizer;
                    visualizer.InitializeVisualizer(wrapper, meshCollider.sharedMesh);
                    break;

                default:
                    // CVR only support those collider types, so we're going to ignore everything else
                    throw new NotImplementedException($"CCK.Debugger does not support pointers with colliders of " +
                                                      $"type: {collider.GetType()}. Report to the mod creator if " +
                                                      $"you think this is a bug.");
            }
        }

        visualizer.Pointer = pointer;
        visualizer.enabled = false;
        // This wrapper is so we can create the visualizer on a disabled GO to prevent awake from being called before we set the Pointer
        wrapper.SetActive(true);

        return visualizer;
    }

    private void Awake() {
        // Needs to be on Awake because OnDestroy is only called if the game object was active, and same goes for awake
        VisualizersAll[Pointer] = this;
    }

    private void InitializeVisualizer(GameObject wrapper, Mesh mesh) {
        VisualizerGo = new GameObject(GameObjectName) { layer = wrapper.layer };

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
        _materialNeitri = new Material(ModConfig.ShaderCache[ModConfig.ShaderType.NeitriDistanceFadeOutline]);
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

        // Add as a child to the wrapper
        VisualizerGo.transform.SetParent(wrapper.transform, false);

        // Hide by default
        VisualizerGo.SetActive(false);
    }

    private void UpdateState() {
        if (VisualizerGo == null || Pointer == null) return;
        VisualizerGo.SetActive(isActiveAndEnabled);
        if (isActiveAndEnabled && !VisualizersActive.ContainsKey(Pointer)) {
            VisualizersActive.Add(Pointer, this);
        }
        else if (!isActiveAndEnabled && VisualizersActive.ContainsKey(Pointer)) {
            VisualizersActive.Remove(Pointer);
        }
    }

    private void OnDestroy() {
        if (VisualizersActive.ContainsKey(Pointer)) VisualizersActive.Remove(Pointer);
        if (VisualizersAll.ContainsKey(Pointer)) VisualizersAll.Remove(Pointer);
    }

    private void OnEnable() => UpdateState();

    private void OnDisable() => UpdateState();

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        foreach (var visualizer in VisualizersAll.Values.ToList()) {
            visualizer.enabled = false;
        }
    }
}
