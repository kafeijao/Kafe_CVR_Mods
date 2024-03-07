using ABI_RC.Core.Player;
using ABI_RC.Systems.IK;
using Kafe.CCK.Debugger.Utils;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.GameObjectVisualizers;

public class TrackerVisualizer : GameObjectVisualizer {

    private const string GameObjectWrapperName = "[CCK.Debugger] Label Visualizer";

    public static void ToggleTrackers(bool isOn) {

        // Toggle controller visualizers
        IKSystem.Instance.leftControllerModel.SetActive(isOn);
        IKSystem.Instance.rightControllerModel.SetActive(isOn);

        var activeTrackers = new HashSet<TrackerVisualizer>();
        foreach (var visualizer in VisualizersActive) {
            if (visualizer.Value is TrackerVisualizer trackerVisualizer) {
                activeTrackers.Add(trackerVisualizer);
            }
        }

        // If it's turning on, enable the current trackers
        if (isOn) {

            var avatarHeight = PlayerSetup.Instance._avatarHeight;
            var trackers = IKSystem.Instance.TrackingSystem.AllTrackingPoints.FindAll(t => t.isActive && t.isValid && t.suggestedRole != TrackingPoint.TrackingRole.Invalid);

            // Iterate the visualizers for the trackers, creating if needed
            foreach (var tracker in trackers) {

                // Ignore invalid trackers
                if (tracker.assignedRole == TrackingPoint.TrackingRole.Invalid) continue;

                var target = tracker.referenceGameObject;

                // If wrapper doesn't exist, create it
                var wrapperTransform = target.transform.Find(GameObjectWrapperName);
                var wrapper = wrapperTransform == null
                    ? new GameObject(GameObjectWrapperName) { layer = target.layer }
                    : wrapperTransform.gameObject;
                wrapper.transform.SetParent(target.transform, false);
                wrapper.SetActive(false);

                // Create the component if doesn't exist
                if (!wrapper.TryGetComponent(out TrackerVisualizer visualizer)) {
                    visualizer = wrapper.AddComponent<TrackerVisualizer>();
                    visualizer.InitializeVisualizer(ModConfig.TrackerVisualizerPrefab, wrapper);
                }

                // Since we're enabling remove from the list to disable
                if (activeTrackers.Contains(visualizer)) activeTrackers.Remove(visualizer);

                visualizer.SetupVisualizer(avatarHeight);
                visualizer.enabled = true;
                wrapper.SetActive(true);

                visualizer.UpdateState();
            }
        }

        // Disable remaining trackers
        foreach (var trackerVisualizer in activeTrackers.ToArray()) {
            trackerVisualizer.enabled = false;
        }
    }

    internal static bool HasTrackersActive() {
        if (IKSystem.Instance.leftControllerModel.activeSelf || IKSystem.Instance.rightControllerModel.activeSelf) return true;
        return VisualizersActive.Any(visualizer => visualizer.Value is TrackerVisualizer { enabled: true });
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Set transform components
        var visualizerTransform = VisualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform, scale) * 0.5f;
    }

}
