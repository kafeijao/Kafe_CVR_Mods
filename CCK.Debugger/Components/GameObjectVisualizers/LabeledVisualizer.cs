using ABI_RC.Core.Player;
using Kafe.CCK.Debugger.Utils;
using TMPro;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.GameObjectVisualizers;

public class LabeledVisualizer : GameObjectVisualizer {

    private string _label;
    private RectTransform _labelTransform;
    private TextMeshPro _labelTMP;

    private Camera _playerCamera;

    public static void Create(GameObject target, string label = "") {

        // If the component still doesn't exist, create it!
        if (!target.TryGetComponent(out LabeledVisualizer visualizer)) {
            visualizer = target.AddComponent<LabeledVisualizer>();
            visualizer.InitializeVisualizer(Resources.AssetBundleLoader.GetLabelVisualizerObject(), target, visualizer);
        }

        visualizer._label = label;
        visualizer.SetupVisualizer();
    }

    private static Vector3 GetLocalScale(Transform target) {
        return Misc.GetScaleFromAbsolute(target.transform, 5.0f) * PlayerSetup.Instance._avatarHeight;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // VisualizerGo.layer = LayerMask.NameToLayer("UI Internal");

        // Set transform components
        var visualizerTransform = VisualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = GetLocalScale(visualizerTransform);

        // Setup Label
        _labelTransform = (RectTransform) visualizerTransform.Find("Label");
        _labelTMP = _labelTransform.GetComponent<TextMeshPro>();
        _labelTMP.text = _label;
        _labelTMP.fontSize = 30f;

        _playerCamera = PlayerSetup.Instance.GetActiveCamera().GetComponent<Camera>();
    }

    private void Update() {

        var cameraPos = _playerCamera.transform.position;

        foreach (var visualizer in VisualizersActive.Values) {
            if (visualizer is not LabeledVisualizer otherLabelVis || otherLabelVis == null || otherLabelVis == this) continue;
            if (Vector3.Distance(_labelTransform.transform.position, otherLabelVis._labelTransform.position) < 0.025f * PlayerSetup.Instance._avatarHeight) {
                _labelTransform.Translate(0f, 0.025f * PlayerSetup.Instance._avatarHeight, 0f, Space.Self);
            }
        }

        // Force labels to look at the player's camera
        _labelTransform.rotation = Quaternion.LookRotation(_labelTransform.position - cameraPos);

    }

    internal static bool HasLabeledVisualizersActive() {
        foreach (var visualizer in VisualizersActive) {
            if (visualizer.Value is LabeledVisualizer { enabled: true }) {
                return true;
            }
        }
        return false;
    }

    internal static void ToggleLabeledVisualizers(bool isOn) {
        foreach (var visualizer in VisualizersAll.Values.ToArray()) {
            if (visualizer is LabeledVisualizer vis) {
                vis.enabled = isOn;
            }
        }
    }
}
