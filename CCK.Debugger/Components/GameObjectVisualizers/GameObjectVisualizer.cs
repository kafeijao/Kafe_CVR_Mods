using CCK.Debugger.Resources;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.GameObjectVisualizers;

[DefaultExecutionOrder(999999)]
public abstract class GameObjectVisualizer : MonoBehaviour {

    protected static readonly Dictionary<GameObject, GameObjectVisualizer> VisualizersAll = new();
    protected static readonly Dictionary<GameObject, GameObjectVisualizer> VisualizersActive = new();

    private const string GameObjectName = "[CCK.Debugger] GameObject Visualizer";

    private GameObject _targetGo;
    protected GameObject VisualizerGo;
    protected Material Material;

    private bool Initialized { get; set; }

    internal void InitializeVisualizer(GameObject prefab, GameObject target, GameObjectVisualizer visualizer) {

        _targetGo = target;

        // Instantiate the visualizer GameObject inside of the target
        VisualizerGo = Instantiate(prefab, target.transform);
        VisualizerGo.layer = target.layer;
        VisualizerGo.name = GameObjectName;

        // Get the renderer and assign material
        var renderer = VisualizerGo.GetComponent<MeshRenderer>();

        // Create neitri fade outline texture shader material
        Material = new Material(AssetBundleLoader.GetShader(ShaderType.NeitriDistanceFadeOutline));
        Material.SetFloat(Misc.MatOutlineWidth, 1f);
        Material.SetFloat(Misc.MatOutlineSmoothness, 0f);
        Material.SetFloat(Misc.MatFadeInBehindObjectsDistance, 0f);
        Material.SetFloat(Misc.MatFadeOutBehindObjectsDistance, 50f);
        Material.SetFloat(Misc.MatFadeInCameraDistance, 0f);
        Material.SetFloat(Misc.MatFadeOutCameraDistance, 50f);
        Material.SetFloat(Misc.MatShowOutlineInFrontOfObjects, 1f);
        Material.SetColor(Misc.MatOutlineColor, Color.white);
        Material.mainTexture = renderer.material.mainTexture;

        renderer.material = Material;

        // Hide by default
        VisualizerGo.SetActive(false);
    }

    protected virtual void Start() {
        VisualizersAll[_targetGo] = this;
        Initialized = true;
        UpdateState();
    }

    private void UpdateState() {
        if (!Initialized) return;
        VisualizerGo.SetActive(enabled);
        if (enabled && !VisualizersActive.ContainsKey(_targetGo)) {
            VisualizersActive.Add(_targetGo, this);
        }
        else if (!enabled && VisualizersActive.ContainsKey(_targetGo)) {
            VisualizersActive.Remove(_targetGo);
        }
    }

    protected virtual void OnDestroy() {
        if (VisualizersActive.ContainsKey(_targetGo)) VisualizersActive.Remove(_targetGo);
        if (VisualizersAll.ContainsKey(_targetGo)) VisualizersAll.Remove(_targetGo);
    }

    private void OnEnable() => UpdateState();

    private void OnDisable() => UpdateState();

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var visualizer in VisualizersAll.Values.ToList()) {
            visualizer.enabled = false;
        }
    }

    protected virtual void SetupVisualizer(float scale = 1f) { }
}
