using System.Text.RegularExpressions;
using ABI_RC.Core.InteractionSystem;
using ABI.CCK.Components;
using UnityEngine;

namespace Kafe.NavMeshFollower;

public static class Utils {

    public static string GetPrettyClassName(object obj) {
        var className = obj.GetType().Name;
        return Regex.Replace(className, "(?<!^)([A-Z])", " $1");
    }

    public static (int[], Dictionary<Animator, AnimatorControllerParameterType>, int) GetSpawnableAndAnimatorIndexes(CVRSpawnable spawnable, Animator[] animators, string syncedValueName) {
        var spawnableIndexes = spawnable.syncValues
            .Select((value, index) => new { value, index })
            .Where(pair => pair.value.name == syncedValueName)
            .Select(pair => pair.index)
            .ToArray();

        var animatorHash = Animator.StringToHash("#" + syncedValueName);

        var animatorParameterTypes = new Dictionary<Animator, AnimatorControllerParameterType>();
        foreach (var animator in animators) {
            if (animator == null) continue;
            var foundParam = animator.parameters.FirstOrDefault(p => p.nameHash == animatorHash);
            if (foundParam == null) continue;
            animatorParameterTypes[animator] = foundParam.type;
        }

        return (spawnableIndexes, animatorParameterTypes, animatorHash);
    }

    public static ControllerRay GetFakeControllerRay(Transform target, Vector3 worldSpaceOffset, out Vector3 posOffset, out Quaternion rotOffset) {
        // Create a fake controller ray so our follower can grab stuff
        var controllerRayGo = new GameObject("ControllerRayHolder");
        controllerRayGo.transform.SetParent(target, false);
        controllerRayGo.SetActive(false);
        var controllerRay = controllerRayGo.AddComponent<ControllerRay>();
        controllerRay.enabled = false;
        var attachPoint = new GameObject("AttachmentPoint");
        attachPoint.transform.SetParent(controllerRayGo.transform, false);
        attachPoint.transform.position = worldSpaceOffset;
        controllerRay.attachmentPoint = attachPoint;
        posOffset = attachPoint.transform.localPosition;
        rotOffset = attachPoint.transform.localRotation;
        // #if DEBUG
        // Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(controllerRayGo, "ControllerRayHolder");
        // Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(attachPoint, "AttachmentPoint");
        // #endif
        return controllerRay;
    }
}
