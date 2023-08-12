using ABI.CCK.Components;
using Kafe.CCK.Debugger.Resources;
using Kafe.CCK.Debugger.Utils;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.TriggerVisualizers;

[DefaultExecutionOrder(999999)]
public abstract class TriggerVisualizer : MonoBehaviour {

    private static readonly Dictionary<MonoBehaviour, TriggerVisualizer> VisualizersAll = new();
    private static readonly Dictionary<MonoBehaviour, TriggerVisualizer> VisualizersActive = new();

    private const string GameObjectName = "Visualizer";
    private const string GameObjectWrapperName = "[CCK.Debugger] Trigger Visualizer";

    protected Material MaterialStandard;
    protected Material MaterialNeitri;

    private GameObject _wrapperGo;
    protected MonoBehaviour TriggerBehavior;
    protected GameObject VisualizerGo;

    protected BoxCollider TriggerCollider { get; private set; }

    public static TriggerVisualizer CreateVisualizer(MonoBehaviour trigger) {

        // Check if the component already exists, if so ignore the creation request
        var wrapperTransform = trigger.transform.Find(GameObjectWrapperName);
        if (wrapperTransform != null && wrapperTransform.TryGetComponent(out TriggerVisualizer visualizer)) {
            return visualizer;
        }

        // Create the wrapper
        var wrapper = wrapperTransform == null
            ? new GameObject(GameObjectWrapperName) { layer = trigger.gameObject.layer }
            : wrapperTransform.gameObject;
        wrapper.transform.SetParent(trigger.transform, false);
        wrapper.SetActive(false);

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
                var type = trigger != null ? trigger.GetType().Name : "null";
                throw new NotImplementedException($"Attempted to add a visualizer to the trigger type {type}, " +
                                                  "but this trigger type is not yet supported! Contact the mod creator.");
        }

        // Instantiate the proper visualizer for the right type of trigger
        visualizer = (TriggerVisualizer) wrapper.AddComponent(visType);
        visualizer.TriggerBehavior = trigger;
        visualizer.enabled = false;
        visualizer._wrapperGo = wrapper;
        // This wrapper is so we can create the visualizer on a disabled GO to prevent awake from being called before we set the Pointer
        wrapper.SetActive(true);

        return visualizer;
    }

    private void Awake() {
        // Needs to be on Awake because OnDestroy is only called if the game object was active, and same goes for awake
        VisualizersAll[TriggerBehavior] = this;
    }

    private void InitializeVisualizer(Mesh mesh) {
        VisualizerGo = new GameObject(GameObjectName) { layer = _wrapperGo.layer };

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

        // Add as a child to the wrapper
        VisualizerGo.transform.SetParent(_wrapperGo.transform, false);

        // Hide by default
        VisualizerGo.SetActive(false);
    }

    protected virtual void Start() {

        // All triggers should have a collider at this point... Otherwise something went wrong
        if (!TriggerBehavior.TryGetComponent(out BoxCollider boxCollider)) {
            var err = $"Failed to create a trigger visualizer because it's missing a collider... Name: " +
                      $"{TriggerBehavior.gameObject.name}: IsActive: {TriggerBehavior.gameObject.activeSelf} " +
                      $"Components in the Game Object: \n";
            foreach (var monoBehaviour in TriggerBehavior.GetComponents<MonoBehaviour>()) {
                err += $"\tComponent Type: {monoBehaviour.GetType()}";
            }
            err += $"This is a bug, contact the mod creator with this information please.";
            MelonLogger.Error(err);
            return;
        }

        TriggerCollider = boxCollider;
        InitializeVisualizer(Misc.GetPrimitiveMesh(PrimitiveType.Cube));

        UpdateState();
    }

    private void UpdateState() {
        if (VisualizerGo == null || TriggerBehavior == null) return;
        VisualizerGo.SetActive(isActiveAndEnabled);
        if (isActiveAndEnabled && !VisualizersActive.ContainsKey(TriggerBehavior)) {
            VisualizersActive.Add(TriggerBehavior, this);
        }
        else if (!isActiveAndEnabled && VisualizersActive.ContainsKey(TriggerBehavior)) {
            VisualizersActive.Remove(TriggerBehavior);
        }
    }

    private void OnDestroy() {
        if (VisualizersActive.ContainsKey(TriggerBehavior)) VisualizersActive.Remove(TriggerBehavior);
        if (VisualizersAll.ContainsKey(TriggerBehavior)) VisualizersAll.Remove(TriggerBehavior);
    }

    private void OnEnable() => UpdateState();

    private void OnDisable() => UpdateState();

    private void ResetMaterialSettings() {
        MaterialStandard.SetColor(Misc.MatMainColor, Misc.ColorWhiteFade);
        MaterialNeitri.SetFloat(Misc.MatOutlineWidth, 0.8f);
        MaterialNeitri.SetColor(Misc.MatOutlineColor, Misc.ColorWhite);
    }

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var visualizer in VisualizersAll.Values.ToList()) {
            visualizer.enabled = false;
        }
    }
}
