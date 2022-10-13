using UnityEngine;

namespace CCK.Debugger.Components.PointerVisualizers;

public class PointerSphereVisualizer : PointerVisualizer {

    protected internal SphereCollider PointerCollider { private get; set; }

    private const float MinimumRadius = 0.015f;

    protected override void Start() {
        base.Start();

        VisualizerGo.transform.localScale = Vector3.zero;
    }

    private void Update() {
        // Update the size and position to match the pointer
        var lossyScale = PointerCollider.transform.lossyScale.x;
        var scaledRadius = lossyScale == 0 ? 0 : Mathf.Max(PointerCollider.radius, MinimumRadius / lossyScale);
        VisualizerGo.transform.localScale = Vector3.one * scaledRadius;
        VisualizerGo.transform.localPosition = PointerCollider.center;
    }
}
