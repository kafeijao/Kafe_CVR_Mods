using UnityEngine;
using UnityEngine.Animations;

namespace Kafe.TheClapper;

public abstract class Clappable : MonoBehaviour {

    private static readonly HashSet<Clappable> Clappables = new();
    private const float MinimumDistance = 0.15f;
    private GameObject _visualizer;
    private PositionConstraint _positionConstraint;

    protected Transform TransformToFollow;

    internal static void OnGestureClapped(float _, Transform leftHand, Transform rightHand) {
        var handsCenterPos = Vector3.Lerp(leftHand.position, rightHand.position, 0.5f);

        // Find the closest capable that's within the min distance
        Clappable closestClappable = null;
        var closestClappableDistance = float.MaxValue;
        var closestClappablePosition = Vector3.zero;
        foreach (var clappable in Clappables) {
            if (!clappable.IsClappable()) continue;
            var currentClappablePosition = clappable.GetPosition();
            var currentDistance = Vector3.Distance(handsCenterPos, currentClappablePosition);
            if (currentDistance < MinimumDistance && currentDistance < closestClappableDistance) {
                closestClappableDistance = currentDistance;
                closestClappablePosition = currentClappablePosition;
                closestClappable = clappable;
            }
        }

        // Call the closest clapped
        if (closestClappable != null) closestClappable.OnClapped(closestClappablePosition);
    }

    protected abstract void OnClapped(Vector3 position);
    protected virtual bool IsClappable() => true;

    private Vector3 GetPosition() {
        return TransformToFollow == null ? transform.position : TransformToFollow.position;
    }

    private void Start() {

        // Create visualizer
        _visualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _visualizer.name = "[TheClapper] Visualizer";
        _visualizer.layer = LayerMask.NameToLayer("UI Internal");
        _visualizer.transform.position = transform.position;
        _visualizer.transform.rotation = transform.rotation;
        _visualizer.transform.localScale = Vector3.one * MinimumDistance * 2;
        _visualizer.transform.SetParent(transform, true);

        _positionConstraint = _visualizer.AddComponent<PositionConstraint>();
        UpdateVisualizerTransform();

        // Make material transparent
        var mat = _visualizer.GetComponent<MeshRenderer>().material;
        mat.SetFloat("_Mode", 3);
        mat.SetColor("_Color", new Color(mat.color.r, mat.color.g, mat.color.b, 0.25f));
        mat.renderQueue = 3000;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Destroy the collider
        Destroy(_visualizer.GetComponent<Collider>());

        _visualizer.SetActive(false);
        Clappables.Add(this);
    }

    internal void UpdateVisualizerTransform() {
        if (_positionConstraint == null) return;

        // If there is no transform reset the visualizer to the root
        if (TransformToFollow == null) {
            _positionConstraint.constraintActive = false;
            _visualizer.transform.localPosition = Vector3.zero;
            return;
        }

        // Otherwise set the constraint as the new target
        var source = new ConstraintSource { sourceTransform = TransformToFollow, weight = 1f};
        _positionConstraint.SetSources(new List<ConstraintSource> {source});
        _positionConstraint.constraintActive = true;
    }

    internal static void UpdateVisualizersShown(bool isVisible) {
        foreach (var clappable in Clappables) {
            if (clappable == null) continue;

            if (!clappable.IsClappable()) {
                if (clappable._visualizer.activeSelf) clappable._visualizer.SetActive(false);
            }
            else if (clappable._visualizer.activeSelf != isVisible) clappable._visualizer.SetActive(isVisible);
        }
    }

    private void OnDestroy() {
        Clappables.Remove(this);
    }
}
