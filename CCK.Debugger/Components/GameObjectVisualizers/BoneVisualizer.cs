using CCK.Debugger.Utils;
using UnityEngine;

namespace CCK.Debugger.Components.GameObjectVisualizers;

public class BoneVisualizer : GameObjectVisualizer {

    public static bool Create(GameObject target, out BoneVisualizer visualizer, float scale) {

        // Check if the component already exists, if so ignore the creation request but enable it
        if (target.TryGetComponent(out visualizer)) {
            visualizer.SetupVisualizer(scale);
            return true;
        }

        visualizer = target.AddComponent<BoneVisualizer>();
        visualizer.InitializeVisualizer(Resources.AssetBundleLoader.GetBoneVisualizerObject(), target, visualizer);
        visualizer.SetupVisualizer(scale);
        visualizer.enabled = false;
        return true;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Set transform components
        var visualizerTransform = _visualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform, 5.0f);
        visualizerTransform.transform.localScale *= scale;

        // Make them darker than trackers
        _material.SetColor(Misc.MatOutlineColor, new Color(0.65f, 0.65f, 0.65f, 1f));
    }

    private static readonly HumanBodyBones[] AvailableBones = {
        HumanBodyBones.Hips,
        HumanBodyBones.Spine,
        HumanBodyBones.Chest,
        HumanBodyBones.UpperChest,
        HumanBodyBones.Neck,
        HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder,
        HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder,
        HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.LeftFoot,
        HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg,
        HumanBodyBones.RightLowerLeg,
        HumanBodyBones.RightFoot,
        HumanBodyBones.RightToes,
    };

    internal static IEnumerable<GameObject> GetAvailableBones(Animator animator) {
        foreach (var availableBone in AvailableBones) {
            var boneTransform = animator.GetBoneTransform(availableBone);
            if (boneTransform == null) continue;
            yield return boneTransform.gameObject;
        }
    }
}
