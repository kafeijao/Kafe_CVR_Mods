using ABI_RC.Core.Player;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.GameObjectVisualizers;

public class EyeTargetVisualizer : GameObjectVisualizer {

    private CVREyeControllerCandidate _candidate;
    private static readonly Color DarkerColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color BrighterColor = new Color(2f, 2f, 2f, 0.75f);

    private Vector3 _baseScale;

    public static bool Create(GameObject parentOfTarget, out EyeTargetVisualizer visualizer, string guid, CVREyeControllerCandidate candidate) {

        // Create or get the game object to save our visualizer
        var candidateId = $"[CCK.Debugger] EyeTargetVisualizer - {guid}";
        var candidateTransform = parentOfTarget.transform.Find(candidateId);
        if (candidateTransform == null) {
            candidateTransform = new GameObject(candidateId).transform;
            candidateTransform.SetParent(parentOfTarget.transform);
            candidateTransform.gameObject.SetActive(true);
        }
        var target = candidateTransform.gameObject;

        // Check if the component already exists, if so ignore the creation request but enable it
        if (target.TryGetComponent(out visualizer)) {
            visualizer._candidate = candidate;
            visualizer.SetupVisualizer();
            return true;
        }

        visualizer = target.AddComponent<EyeTargetVisualizer>();
        visualizer._candidate = candidate;
        visualizer.InitializeVisualizer(Resources.AssetBundleLoader.GetBoneVisualizerObject(), target, visualizer);
        visualizer.SetupVisualizer();
        visualizer.enabled = true;
        return true;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Enforce eye targets to be on the UI Internel, so they are not visible in most mirrors, because they also
        // have mirror behavior
        _visualizerGo.layer = LayerMask.NameToLayer("UI Internal");

        // Set transform components
        var visualizerTransform = _visualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform, 5.0f);
        visualizerTransform.transform.localScale *= scale;
        _baseScale = visualizerTransform.localScale;

        _material.SetColor(Misc.MatOutlineColor, DarkerColor);
    }

    internal static void UpdateActive(bool isOn, ICollection<CVREyeControllerCandidate> candidates, string targetGuid) {
        foreach (var goVis in VisualizersAll) {
            if (goVis.Value is EyeTargetVisualizer vis) {
                var isCandidate = isOn && candidates.Contains(vis._candidate);
                vis.enabled = isCandidate;
                vis._visualizerGo.SetActive(isCandidate);

                if (!isCandidate) continue;
                var isTarget = vis._candidate.Guid == targetGuid;
                vis._material.SetColor(Misc.MatOutlineColor, isTarget ? BrighterColor : DarkerColor);
                vis._visualizerGo.transform.transform.localScale = vis._baseScale * (isTarget ? 2f : 1f);
                vis.transform.position = vis._candidate.Position;
            }
        }
    }
}
