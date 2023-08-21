using ABI_RC.Core.Player;
using Kafe.CCK.Debugger.Utils;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.GameObjectVisualizers;

public class EyeTargetVisualizer : GameObjectVisualizer {

    protected override string GetName() => "[CCK.Debugger] Eye Target Visualizer";

    private CVREyeControllerCandidate _candidate;
    private static readonly Color DarkerColor = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color BrighterColor = new Color(2f, 2f, 2f, 0.75f);

    private Vector3 _baseScale;

    public static void Create(GameObject parentOfTarget, string guid, CVREyeControllerCandidate candidate) {

        // Create or get the game object to save our visualizer
        var candidateId = $"[CCK.Debugger] EyeTargetVisualizer - {guid}";
        var candidateTransform = parentOfTarget.transform.Find(candidateId);
        if (candidateTransform == null) {
            candidateTransform = new GameObject(candidateId).transform;
            candidateTransform.SetParent(parentOfTarget.transform);
            candidateTransform.gameObject.SetActive(false);
        }
        var target = candidateTransform.gameObject;

        // If the component still doesn't exist, create it!
        if (!target.TryGetComponent(out EyeTargetVisualizer visualizer)) {
            visualizer = target.AddComponent<EyeTargetVisualizer>();
            visualizer.InitializeVisualizer(ModConfig.BoneVisualizerPrefab, target);
        }

        visualizer._candidate = candidate;
        visualizer.SetupVisualizer();
        target.SetActive(true);
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Enforce eye targets to be on the UI Internal, so they are not visible in most mirrors, because they also
        // have mirror behavior
        VisualizerGo.layer = LayerMask.NameToLayer("UI Internal");

        // Set transform components
        var visualizerTransform = VisualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform, 5.0f);
        visualizerTransform.transform.localScale *= scale;
        _baseScale = visualizerTransform.localScale;

        Material.SetColor(Misc.MatOutlineColor, DarkerColor);
    }

    internal static void UpdateActive(bool isOn, ICollection<CVREyeControllerCandidate> candidates, string targetGuid) {
        foreach (var goVis in VisualizersAll) {
            if (goVis.Value is EyeTargetVisualizer vis) {
                var isCandidate = isOn && candidates.Contains(vis._candidate);
                vis.enabled = isCandidate;
                vis.VisualizerGo.SetActive(isCandidate);

                if (!isCandidate) continue;
                var isTarget = vis._candidate.Guid == targetGuid;
                vis.Material.SetColor(Misc.MatOutlineColor, isTarget ? BrighterColor : DarkerColor);
                vis.VisualizerGo.transform.transform.localScale = vis._baseScale * (isTarget ? 2f : 1f);
                vis.transform.position = vis._candidate.Position;
            }
        }
    }
}
