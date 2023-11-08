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
        controllerRay.attachmentPoint = attachPoint.transform;
        controllerRay.pivotPoint = attachPoint.transform;
        posOffset = attachPoint.transform.localPosition;
        rotOffset = attachPoint.transform.localRotation;
        // #if DEBUG
        // Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(controllerRayGo, "ControllerRayHolder");
        // Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(attachPoint, "AttachmentPoint");
        // #endif
        return controllerRay;
    }
}
