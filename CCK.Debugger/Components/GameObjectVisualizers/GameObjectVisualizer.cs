using CCK.Debugger.Resources;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.GameObjectVisualizers;

public abstract class GameObjectVisualizer : MonoBehaviour {

    protected static readonly Dictionary<Tuple<GameObject, Type>, GameObjectVisualizer> VisualizersAll = new();
    protected static readonly Dictionary<Tuple<GameObject, Type>, GameObjectVisualizer> VisualizersActive = new();

    private const string GameObjectName = "[CCK.Debugger] GameObject Visualizer";

    private GameObject _targetGo;
    private Type _visualizerType;
    protected GameObject _visualizerGo;
    protected Material _material;

    private bool IsInitialized() => _visualizerGo != null;

    internal void InitializeVisualizer(GameObject prefab, GameObject target, GameObjectVisualizer visualizer) {

        _targetGo = target;
        _visualizerType = visualizer.GetType();

        // Instantiate the visualizer GameObject inside of the target
        _visualizerGo = Instantiate(prefab, target.transform);
        _visualizerGo.layer = LayerMask.NameToLayer("UI Internal");
        _visualizerGo.name = GameObjectName;

        // Get the renderer and assign material
        var renderer = _visualizerGo.GetComponent<MeshRenderer>();

        // Create neitri fade outline texture shader material
        _material = new Material(AssetBundleLoader.GetShader(ShaderType.NeitriDistanceFadeOutline));
        _material.SetFloat(Misc.MatOutlineWidth, 1f);
        _material.SetFloat(Misc.MatOutlineSmoothness, 0f);
        _material.SetFloat(Misc.MatFadeInBehindObjectsDistance, 0f);
        _material.SetFloat(Misc.MatFadeOutBehindObjectsDistance, 50f);
        _material.SetFloat(Misc.MatFadeInCameraDistance, 0f);
        _material.SetFloat(Misc.MatFadeOutCameraDistance, 50f);
        _material.SetFloat(Misc.MatShowOutlineInFrontOfObjects, 1f);
        _material.SetColor(Misc.MatOutlineColor, Color.white);
        _material.mainTexture = renderer.material.mainTexture;

        renderer.material = _material;

        // Hide by default
        _visualizerGo.SetActive(false);
    }

    protected virtual void Start() {
        VisualizersAll[Tuple.Create(_targetGo, _visualizerType)] = this;
    }

    private void OnDestroy() {
        VisualizersActive.Remove(Tuple.Create(_targetGo, _visualizerType));
        VisualizersAll.Remove(Tuple.Create(_targetGo, _visualizerType));
    }

    private void OnEnable() {
        if (!IsInitialized()) return;
        _visualizerGo.SetActive(true);
        VisualizersActive.Add(Tuple.Create(_targetGo, _visualizerType), this);
    }

    private void OnDisable() {
        if (!IsInitialized()) return;
        _visualizerGo.SetActive(false);
        VisualizersActive.Remove(Tuple.Create(_targetGo, _visualizerType));
    }

    internal static bool HasActive() => VisualizersActive.Count > 0;

    internal static void DisableAll() {
        // Iterate over a copy of the values because they're going to be removed when disabled
        foreach (var visualizer in VisualizersAll.Values.ToList()) {
            visualizer.enabled = false;
        }
    }

    protected virtual void SetupVisualizer(float scale = 1f) { }
}
