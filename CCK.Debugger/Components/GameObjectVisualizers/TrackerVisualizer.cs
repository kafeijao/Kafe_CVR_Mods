using ABI_RC.Core.Player;
using ABI_RC.Systems.IK;
using Kafe.CCK.Debugger.Utils;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.GameObjectVisualizers;

public class TrackerVisualizer : GameObjectVisualizer {

    public static void ToggleTrackers(bool isOn) {

        DisableAllTrackerVisualizers();

        // We already turned off all the trackers, so only proceed if we want to turn them on
        if (!isOn) return;

        var avatarHeight = PlayerSetup.Instance._avatarHeight;
        var trackers = IKSystem.Instance.AllTrackingPoints.FindAll(t => t.isActive && t.isValid && t.suggestedRole != TrackingPoint.TrackingRole.Invalid);

        // Add visualizers to the trackers
        foreach (var tracker in trackers) {

            // Ignore invalid trackers
            if (tracker.assignedRole == TrackingPoint.TrackingRole.Invalid) continue;

            var target = tracker.referenceGameObject;

            // Check if the component already exists, if so ignore the creation request but enable it
            if (target.TryGetComponent(out TrackerVisualizer visualizer)) {
                visualizer.SetupVisualizer(avatarHeight);
                visualizer.enabled = true;
                continue;
            }

            visualizer = target.AddComponent<TrackerVisualizer>();
            visualizer.InitializeVisualizer(Resources.AssetBundleLoader.GetTrackerVisualizerObject(), target, visualizer);
            visualizer.SetupVisualizer(avatarHeight);
            visualizer.enabled = true;
        }

        // Enable controller visualizers
        IKSystem.Instance.leftHandModel.SetActive(true);
        IKSystem.Instance.rightHandModel.SetActive(true);
    }

    private static void DisableAllTrackerVisualizers() {
        var activeTrackers = new HashSet<TrackerVisualizer>();
        foreach (var visualizer in VisualizersActive) {
            if (visualizer.Value is TrackerVisualizer trackerVisualizer) {
                activeTrackers.Add(trackerVisualizer);
            }
        }
        foreach (var trackerVisualizer in activeTrackers) {
            trackerVisualizer.enabled = false;
        }
        // Disable controller visualizers
        IKSystem.Instance.leftHandModel.SetActive(false);
        IKSystem.Instance.rightHandModel.SetActive(false);
    }

    internal static bool HasTrackersActive() {
        if (IKSystem.Instance.leftHandModel.activeSelf || IKSystem.Instance.rightHandModel.activeSelf) return true;
        foreach (var visualizer in VisualizersActive) {
            if (visualizer.Value is TrackerVisualizer { enabled: true }) {
                return true;
            }
        }
        return false;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Set transform components
        var visualizerTransform = VisualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform);
    }

}
