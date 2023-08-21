using Kafe.CCK.Debugger.Utils;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.GameObjectVisualizers;

public class BoneVisualizer : GameObjectVisualizer {

    private const string GameObjectWrapperName = "[CCK.Debugger] Bone Visualizer";

    public static BoneVisualizer Create(GameObject target, float scale) {

        // Check if the component already exists, if so ignore the creation request
        var wrapperTransform = target.transform.Find(GameObjectWrapperName);
        if (wrapperTransform != null && wrapperTransform.TryGetComponent(out BoneVisualizer visualizer)) {
            visualizer.SetupVisualizer(scale);
            return visualizer;
        }

        // Create the wrapper
        var wrapper = wrapperTransform == null
            ? new GameObject(GameObjectWrapperName) { layer = target.layer }
            : wrapperTransform.gameObject;
        wrapper.transform.SetParent(target.transform, false);
        wrapper.SetActive(false);

        visualizer = wrapper.AddComponent<BoneVisualizer>();
        visualizer.InitializeVisualizer(ModConfig.BoneVisualizerPrefab, wrapper);
        visualizer.SetupVisualizer(scale);

        visualizer.enabled = false;
        wrapper.SetActive(true);
        return visualizer;
    }

    protected override void SetupVisualizer(float scale = 1f) {

        // Set transform components
        var visualizerTransform = VisualizerGo.transform;
        visualizerTransform.localPosition = Vector3.zero;
        visualizerTransform.localRotation = Quaternion.identity;
        visualizerTransform.localScale = Misc.GetScaleFromAbsolute(transform, 5.0f) * scale;

        // Make them darker than trackers
        Material.SetColor(Misc.MatOutlineColor, new Color(0.65f, 0.65f, 0.65f, 1f));
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
