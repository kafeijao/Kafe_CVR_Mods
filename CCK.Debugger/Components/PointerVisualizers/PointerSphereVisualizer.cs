using UnityEngine;

namespace CCK.Debugger.Components.PointerVisualizers;

public class PointerSphereVisualizer : PointerVisualizer {

    protected internal SphereCollider PointerCollider { private get; set; }

    private const float MinimumRadius = 0.015f;

    private void Start() {
        VisualizerGo.transform.localScale = Vector3.zero;
    }

    private void Update() {
        // Update the size and position to match the pointer
        VisualizerGo.transform.localScale = Vector3.one * Mathf.Max(PointerCollider.radius, MinimumRadius);
        VisualizerGo.transform.localPosition = PointerCollider.center;
    }
}
