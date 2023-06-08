using UnityEngine;

namespace Kafe.CCK.Debugger.Components.PointerVisualizers;

public class PointerCapsuleVisualizer : PointerVisualizer {

    protected internal CapsuleCollider PointerCollider { private get; set; }

    protected void Start() {
        VisualizerGo.transform.localScale = Vector3.zero;

        // Update the rotation to match the pointer direction
        VisualizerGo.transform.localRotation = PointerCollider.direction switch {
            0 => Quaternion.Euler(0f, 0f, 90f),
            1 => Quaternion.Euler(0f, 0f, 0f),
            2 => Quaternion.Euler(90f, 0f, 0f),
            _ => VisualizerGo.transform.localRotation
        };
    }

    private void Update() {
        // Update the size and position to match the pointer
        VisualizerGo.transform.localScale = new Vector3(
            PointerCollider.radius*2f,
            PointerCollider.height/2f,
            PointerCollider.radius*2f);
        VisualizerGo.transform.localPosition = PointerCollider.center;
    }
}
