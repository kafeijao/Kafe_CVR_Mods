using ABI.CCK.Components;
using CCK.Debugger.Resources;
using CCK.Debugger.Utils;
using MelonLoader;
using UnityEngine;

namespace CCK.Debugger.Components.TriggerVisualizers;

public abstract class TriggerVisualizer : MonoBehaviour {

    private static readonly Dictionary<MonoBehaviour, TriggerVisualizer> VisualizersAll = new();
    private static readonly Dictionary<MonoBehaviour, TriggerVisualizer> VisualizersActive = new();

    private const string GameObjectName = "[CCK.Debugger] Trigger Visualizer";

    protected Material MaterialStandard;
    protected Material MaterialNeitri;

    protected MonoBehaviour TriggerBehavior;
    protected GameObject VisualizerGo;

    protected BoxCollider TriggerCollider { get; private set; }

    public static bool CreateVisualizer(MonoBehaviour trigger, out TriggerVisualizer visualizer) {

        // Check if the component already exists, if so ignore the creation request
        if (trigger.TryGetComponent(out visualizer)) return true;

        // Ignore triggers without box collider (should never happen ?)
        if (!trigger.TryGetComponent(out BoxCollider boxCollider)) {
            visualizer = null;
            return false;
        }

        // Create a visualizer for the proper type ;_; why triggers don't have a base class
        Type visType;
        switch (trigger) {
            case CVRAdvancedAvatarSettingsTrigger:
                visType = typeof(TriggerAvatarVisualizer);
                break;
            case CVRSpawnableTrigger:
                visType = typeof(TriggerSpawnableVisualizer);
                break;
            default:
                MelonLogger.Error($"Attempted to add a visualizer to the trigger type {trigger.GetType().Name}, " +
                                  $"but this trigger type is not yet supported! Contact the mod creator.");
                visualizer = null;
                return false;
        }

        // Instantiate the proper visualizer for a cube collider
        visualizer = (TriggerVisualizer) trigger.gameObject.AddComponent(visType);
        visualizer.TriggerCollider = boxCollider;
        visualizer.InitializeVisualizer(trigger, Utils.Misc.GetPrimitiveMesh(PrimitiveType.Cube));

        // Disable the behavior
        visualizer.enabled = false;
        return true;
    }

    private bool IsInitialized() => VisualizerGo != null;

    private void InitializeVisualizer(MonoBehaviour trigger, Mesh mesh) {
        VisualizerGo = new GameObject(GameObjectName);

        TriggerBehavior = trigger;

        // Create mesh filter
        var meshFilter = VisualizerGo.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        // Create standard shader material with render type set to Fade
        MaterialStandard = new Material(Misc.ShaderStandard);
        MaterialStandard.SetInt(Misc.MatSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        MaterialStandard.SetInt(Misc.MatDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        MaterialStandard.SetInt(Misc.MatZWrite, 0);
        MaterialStandard.DisableKeyword(Misc.ShaderAlphaTest);
        MaterialStandard.EnableKeyword(Misc.ShaderAlphaBlend);
        MaterialStandard.DisableKeyword(Misc.ShaderAlphaPreMultiply);
        MaterialStandard.renderQueue = 3000;
        MaterialStandard.SetColor(Misc.MatMainColor, Misc.ColorWhiteFade);

        // Create neitri fade outline shader material
        MaterialNeitri = new Material(AssetBundleLoader.GetShader(ShaderType.NeitriDistanceFadeOutline));
        MaterialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        MaterialNeitri.SetFloat(Misc.MatOutlineSmoothness, 0.1f);
        MaterialNeitri.SetFloat(Misc.MatFadeInBehindObjectsDistance, 2f);
        MaterialNeitri.SetFloat(Misc.MatFadeOutBehindObjectsDistance, 10f);
        MaterialNeitri.SetFloat(Misc.MatFadeInCameraDistance, 10f);
        MaterialNeitri.SetFloat(Misc.MatFadeOutCameraDistance, 15f);
        MaterialNeitri.SetFloat(Misc.MatShowOutlineInFrontOfObjects, 0f);
        MaterialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorWhite);

        // Create the renderer and assign material
        var renderer = VisualizerGo.AddComponent<MeshRenderer>();
        renderer.materials = new[] { MaterialStandard, MaterialNeitri };

        // Add as a child to the pointer
        VisualizerGo.transform.SetParent(trigger.transform, false);

        // Hide by default
        VisualizerGo.SetActive(false);
    }

    private void Start() {
        VisualizersAll[TriggerBehavior] = this;
    }

    private void OnDestroy() {
        VisualizersActive.Remove(TriggerBehavior);
        VisualizersAll.Remove(TriggerBehavior);
    }

    private void OnEnable() {
        if (!IsInitialized()) return;
        VisualizerGo.SetActive(true);
        VisualizersActive.Add(TriggerBehavior, this);
    }

    private void OnDisable() {
        if (!IsInitialized()) return;
        VisualizerGo.SetActive(false);
        VisualizersActive.Remove(TriggerBehavior);
    }

    private void ResetMaterialSettings() {
        MaterialStandard.SetColor(Misc.MatMainColor, Misc.ColorWhiteFade);
        MaterialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        MaterialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorWhite);
    }

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var pointerVisualizer in VisualizersAll.Values.ToList()) {
            pointerVisualizer.enabled = false;
        }
    }
}
