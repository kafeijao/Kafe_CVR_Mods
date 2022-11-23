using ABI_RC.Systems.IK;
using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.GameObjectVisualizers;

public class TrackerVisualizer : GameObjectVisualizer {

    public static bool Create(TrackingPoint tracker, out TrackerVisualizer visualizer, float scale = 1f) {

        // Ignore invalid trackers
        if (tracker.assignedRole == TrackingPoint.TrackingRole.Invalid) {
            visualizer = default;
            return false;
        }

        var target = tracker.referenceGameObject;

        // Check if the component already exists, if so ignore the creation request but enable it
        if (target.TryGetComponent(out visualizer)) {
            visualizer.SetupVisualizer(scale);
            return true;
        }

        visualizer = target.AddComponent<TrackerVisualizer>();
        visualizer.InitializeVisualizer(Resources.AssetBundleLoader.GetTrackerVisualizerObject(), target, visualizer);
        visualizer.SetupVisualizer(scale);
        visualizer.enabled = false;
        return true;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Set transform components
        var visualizerTransform = _visualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform);
    }

}
