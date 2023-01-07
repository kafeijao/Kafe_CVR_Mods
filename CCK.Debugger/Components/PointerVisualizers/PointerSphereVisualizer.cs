using MelonLoader;
using UnityEngine;

namespace CCK.Debugger.Components.PointerVisualizers;

public class PointerSphereVisualizer : PointerVisualizer {

    protected internal SphereCollider PointerCollider { private get; set; }

    private const float MinimumRadius = 0.015f;

    protected override void Start() {

        // Sphere colliders might not be present when the visualizer was created, because cvr default colliders only
        // get added by the Start Event of CVRPointer
        if (PointerCollider == null) {

            // At this point the pointer NEED to have a collider, something went wrong...
            if (!Pointer.TryGetComponent(out SphereCollider collider)) {
                var err = $"Failed to create a sphere pointer visualizer because it's missing a collider... " +
                          $"Name: {Pointer.gameObject.name}: IsActive: {Pointer.gameObject.activeSelf} " +
                          $"Components in the Game Object: \n";
                foreach (var monoBehaviour in Pointer.GetComponents<MonoBehaviour>()) {
                    err += $"\tComponent Type: {monoBehaviour.GetType()}";
                }
                err += $"This is a bug, contact the mod creator with this information please.";
                MelonLogger.Error(err);

                return;
            }

            PointerCollider = collider;
        }

        VisualizerGo.transform.localScale = Vector3.zero;

        base.Start();
    }

    private void Update() {
        if (!Initialized) return;

        // Update the size and position to match the pointer
        var lossyScale = PointerCollider.transform.lossyScale.x;
        var scaledRadius = lossyScale == 0 ? 0 : Mathf.Max(PointerCollider.radius, MinimumRadius / lossyScale);
        VisualizerGo.transform.localScale = Vector3.one * scaledRadius;
        VisualizerGo.transform.localPosition = PointerCollider.center;
    }
}
