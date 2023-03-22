using UnityEngine;

namespace Kafe.CCK.Debugger.Components.PointerVisualizers;

public class PointerCapsuleVisualizer : PointerVisualizer {

    protected internal CapsuleCollider PointerCollider { private get; set; }

    protected override void Start() {
        VisualizerGo.transform.localScale = Vector3.zero;

        base.Start();
    }

    private void Update() {
        if (!Initialized) return;

        // Update the size and position to match the pointer
        VisualizerGo.transform.localScale = new Vector3(
            PointerCollider.radius*2f,
            PointerCollider.height/2f,
            PointerCollider.radius*2f);
        VisualizerGo.transform.localPosition = PointerCollider.center;
    }
}
