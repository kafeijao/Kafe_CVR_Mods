using System.Reflection;
using ABI_RC.Core.Player;
using ABI_RC.Systems.Camera;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace EyeMovementFix;

public class EyeMovementFix : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    private static MelonPreferences_Entry<bool> _meForceLookAtCamera;
    private static MelonPreferences_Entry<bool> _meIgnorePortableMirrors;

    private static bool _hasPortableMirror;

    public override void OnApplicationStart() {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(EyeMovementFix));
        _meForceLookAtCamera = _melonCategory.CreateEntry("ForceLookAtCamera", true,
            description: "Whether to force everyone to look at the camera whn being held. The camera needs to be " +
                         "within the avatar field of view. And needs the camera setting to add the camera to look " +
                         "at targets activated (camera settings in-game).");

        _meIgnorePortableMirrors = _melonCategory.CreateEntry("IgnoreLookingAtPortableMirrors", true,
            description: "Whether or not to ignore mirrors created by portable mirror mod. When portable mirrors " +
                         "are active, they create targets for the eye movement. But since I use the mirror to mostly " +
                         "see what I'm doing, makes no sense to target players in the portable mirror.");

        // Check for portable mirror
        _hasPortableMirror = RegisteredMelons.FirstOrDefault(m => m.Info.Name == "PortableMirrorMod") != null;

        if (_hasPortableMirror && _meIgnorePortableMirrors.Value) {
            MelonLogger.Msg($"Detected PortableMirror mod, and as per this mod config we're going to prevent " +
                            $"Portable Mirror mod's mirrors from being added to eye movement targets.");

            // Manually patch the add mirror, since we don't want to do it if the mod is not present
            HarmonyInstance.Patch(
                typeof(CVREyeControllerManager).GetMethod(nameof(CVREyeControllerManager.addMirror)),
                null,
                new HarmonyMethod(typeof(EyeMovementFix).GetMethod(nameof(After_CVREyeControllerManager_addMirror), BindingFlags.NonPublic | BindingFlags.Static))
            );
        }

    }

    private static void After_CVREyeControllerManager_addMirror(CVREyeControllerManager __instance, CVRMirror cvrMirror) {
        if (!_hasPortableMirror || !_meIgnorePortableMirrors.Value) return;

        var mirrorParents = new[] {
            PortableMirror.Main._mirrorBase,
            PortableMirror.Main._mirror45,
            PortableMirror.Main._mirrorCeiling,
            PortableMirror.Main._mirrorMicro,
            PortableMirror.Main._mirrorTrans,
            PortableMirror.Main._mirrorCal,
        };
        var parentGo = cvrMirror.transform.parent;
        // Ignore objects without a parent, since portable mirror objects must have a parent
        if (parentGo == null) return;
        var isPortableMirror = mirrorParents.Contains(parentGo.gameObject);

        // If we added a portable mirror to the targets, remove it!
        if (isPortableMirror && __instance.mirrorList.Contains(cvrMirror)) {
            __instance.removeMirror(cvrMirror);
        }
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRParameterStreamEntry), nameof(CVRParameterStreamEntry.CheckUpdate))]
        private static void After_CVRParameterStreamEntry_CheckUpdate(
            CVRParameterStreamEntry __instance,
            CVRAvatar avatar,
            CVRSpawnable spawnable,
            CVRParameterStream.ReferenceType referenceType) {
            // All this code to change EyeMovementRightY and EyeMovementLeftY to actually fetch from eyeAngle.y instead
            // of eyeAngle.x
            // Yes I could use a transpiler but Daky would be mad at me

            if (referenceType != CVRParameterStream.ReferenceType.Avatar ||
                (__instance.type != CVRParameterStreamEntry.Type.EyeMovementRightY &&
                 __instance.type != CVRParameterStreamEntry.Type.EyeMovementLeftY)) return;

            var eyeAngleY = 0f;

            if (avatar != null) {
                var cvrEyeController = avatar.GetComponentInChildren<CVREyeController>();
                if (cvrEyeController == null) return;
                eyeAngleY = cvrEyeController.eyeAngle.y;
            }


            var otherParamValue = 0.0f;
            if (__instance.targetType == CVRParameterStreamEntry.TargetType.AvatarAnimator) {
                otherParamValue = PlayerSetup.Instance.GetAnimatorParam(__instance.parameterName);
            }

            var finalYyeAngleY = 0.0f;

            switch (__instance.applicationType) {
                case CVRParameterStreamEntry.ApplicationType.Override:
                    finalYyeAngleY = eyeAngleY;
                    break;
                case CVRParameterStreamEntry.ApplicationType.AddToCurrent:
                    finalYyeAngleY = otherParamValue + eyeAngleY;
                    break;
                case CVRParameterStreamEntry.ApplicationType.AddToStatic:
                    finalYyeAngleY = __instance.staticValue + eyeAngleY;
                    break;
                case CVRParameterStreamEntry.ApplicationType.SubtractFromCurrent:
                    finalYyeAngleY = otherParamValue - eyeAngleY;
                    break;
                case CVRParameterStreamEntry.ApplicationType.SubtractFromStatic:
                    finalYyeAngleY = __instance.staticValue - eyeAngleY;
                    break;
                case CVRParameterStreamEntry.ApplicationType.SubtractWithCurrent:
                    finalYyeAngleY = eyeAngleY - otherParamValue;
                    break;
                case CVRParameterStreamEntry.ApplicationType.SubtractWithStatic:
                    finalYyeAngleY = eyeAngleY - __instance.staticValue;
                    break;
                case CVRParameterStreamEntry.ApplicationType.MultiplyWithCurrent:
                    finalYyeAngleY = eyeAngleY * otherParamValue;
                    break;
                case CVRParameterStreamEntry.ApplicationType.MultiplyWithStatic:
                    finalYyeAngleY = eyeAngleY * __instance.staticValue;
                    break;
                case CVRParameterStreamEntry.ApplicationType.CompareLessThen:
                    finalYyeAngleY = eyeAngleY < __instance.staticValue ? 1f : 0.0f;
                    break;
                case CVRParameterStreamEntry.ApplicationType.CompareLessThenEquals:
                    finalYyeAngleY = eyeAngleY <= __instance.staticValue ? 1f : 0.0f;
                    break;
                case CVRParameterStreamEntry.ApplicationType.CompareEquals:
                    finalYyeAngleY = Mathf.Approximately(eyeAngleY, __instance.staticValue) ? 1f : 0.0f;
                    break;
                case CVRParameterStreamEntry.ApplicationType.CompareMoreThenEquals:
                    finalYyeAngleY = eyeAngleY >= __instance.staticValue ? 1f : 0.0f;
                    break;
                case CVRParameterStreamEntry.ApplicationType.CompareMoreThen:
                    finalYyeAngleY = eyeAngleY > __instance.staticValue ? 1f : 0.0f;
                    break;
                case CVRParameterStreamEntry.ApplicationType.Mod:
                    finalYyeAngleY = eyeAngleY % Mathf.Max(Mathf.Abs(__instance.staticValue), 0.0001f);
                    break;
                case CVRParameterStreamEntry.ApplicationType.Pow:
                    finalYyeAngleY = Mathf.Pow(eyeAngleY, __instance.staticValue);
                    break;
            }

            switch (__instance.targetType) {
                case CVRParameterStreamEntry.TargetType.Animator:
                    if (__instance.target == null) break;
                    var component1 = __instance.target.GetComponent<Animator>();
                    if (component1 == null || !component1.enabled || component1.runtimeAnimatorController == null ||
                        component1.IsParameterControlledByCurve(__instance.parameterName)) break;
                    component1.SetFloat(__instance.parameterName, finalYyeAngleY);
                    break;
                case CVRParameterStreamEntry.TargetType.VariableBuffer:
                    if (__instance.target == null) break;
                    var component2 = __instance.target.GetComponent<CVRVariableBuffer>();
                    if (component2 == null) break;
                    component2.SetValue(finalYyeAngleY);
                    break;
                case CVRParameterStreamEntry.TargetType.AvatarAnimator:
                    var descriptor = Traverse.Create(PlayerSetup.Instance).Field<CVRAvatar>("_avatarDescriptor").Value;
                    if (avatar == null || avatar != descriptor) break;
                    PlayerSetup.Instance.changeAnimatorParam(__instance.parameterName, finalYyeAngleY);
                    break;
                case CVRParameterStreamEntry.TargetType.CustomFloat:
                    if (spawnable == null) break;
                    var index = spawnable.syncValues.FindIndex((match => match.name == __instance.parameterName));
                    if (index < 0) break;
                    spawnable.SetValue(index, finalYyeAngleY);
                    break;
            }
        }

        private static CVRPickupObject _cameraPickup;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeControllerManager), nameof(CVREyeControllerManager.Update))]
        private static bool Before_CVREyeControllerManager_Update(CVREyeControllerManager __instance, bool ____cameraISLookAtTarget) {
            // This patch is to allow the option to make the camera the only possible target when grabbed
            // Only works if the camera setting camera is look at target is ON, is being held, it's configured to
            // be enabled on this mod, and it's within the fov of the player

            if (_cameraPickup == null) {
                _cameraPickup = CVRCamController.Instance.cvrCamera.GetComponent<CVRPickupObject>();
            }

            // Execute the original method normally
            if (!_meForceLookAtCamera.Value ||
                !CVRCamController.Instance.cvrCamera.activeSelf ||
                !_cameraPickup.IsGrabbedByMe() ||
                !____cameraISLookAtTarget) {
                return true;
            }

            // Otherwise lets ensure we only have the camera as eye track candidate
            __instance.targetCandidates.Clear();
            var controllerCandidate = new CVREyeControllerCandidate("CVRCamera",
                PortableCamera.Instance.cameraComponent.transform.position);
            __instance.targetCandidates.Add(controllerCandidate.Guid, controllerCandidate);
            return false;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), "Update")]
        private static void After_CVREyeController_Update(CVREyeController __instance, CVRAvatar ___avatar) {
            if (___avatar == null) return;

            var puppetMaster = Traverse.Create(___avatar).Field<PuppetMaster>("puppetMaster").Value;

            if (puppetMaster == null && !__instance.isLocal && ___avatar.mouthPointer != null) {
                // Wtf? remote users without puppet master? This results in the target position be completely random
                // Sometimes goes on top of the local player head, other times just stays at a random place in the world
                // Fallback to the mouth pointer
                __instance.viewPosition = ___avatar.mouthPointer.transform.position;
            }
            if (puppetMaster == null && !__instance.isLocal && ___avatar.mouthPointer == null && !Mathf.Approximately(__instance.viewPosition.x, 0f)) {
                // Let's add another fallback since there are avatars (namely generic rigs) that don't have a mouth pointer
                // We gonna use the avatar position + the avatar view point static position (kinda cancer but better than on my own head)
                __instance.viewPosition = ___avatar.transform.position + ___avatar.viewPosition;
            }
            else if (__instance.isLocal && PlayerSetup.Instance != null) {
                // Fix the local eye controller candidate, the viewpoint wouldn't follow the head Up/Down while in FBT
                // Also it seems in 3pt there was some weird jitter when moving and stopping using the thumbstick
                // The culprit of this is this.viewPosition = this.avatar.GetViewWorldPosition();
                // So the fix is just overwrite the value with the actual position
                __instance.viewPosition = PlayerSetup.Instance.GetActiveCamera().transform.position;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), "LateUpdate")]
        private static void After_CVREyeControllerManager_LateUpdate(
            ref Vector3 ___targetViewPosition,
            ref Vector3 ___viewDirection,
            ref CVRAvatar ___avatar,
            ref Animator ___animator,
            ref Transform ___EyeLeft,
            ref Quaternion ___EyeLeftBaseRot,
            ref Transform ___EyeRight,
            ref Quaternion ___EyeRightBaseRot,
            ref Vector2 ___eyeAngle) {
            if (___targetViewPosition == Vector3.zero || ___avatar == null || !___avatar.useEyeMovement) return;

            // Lets ignore avatars without a head and humanoid rig, because I suck at math
            if (!___animator || !___animator.isHuman || ___animator.GetBoneTransform(HumanBodyBones.Head) == null) return;

            // Attempt a simpler approach for the rotation of the eyes, I suck at math so this might be off
            var upHead = ___animator.GetBoneTransform(HumanBodyBones.Head).up;
            if (___EyeLeft != null) {
                ___EyeLeft.LookAt(___targetViewPosition, upHead);

                // Limit overall angle
                var targetRot = Quaternion.RotateTowards(___EyeLeftBaseRot, ___EyeLeft.localRotation, 25f);
                ___EyeLeft.localRotation = targetRot;

                // Todo: Configurable limits on X and Y
                // Todo: Update the ___eyeAngle
            }

            if (___EyeRight != null) {
                ___EyeRight.LookAt(___targetViewPosition, upHead);

                // Limit overall angle
                var targetRot = Quaternion.RotateTowards(___EyeRightBaseRot, ___EyeRight.localRotation, 25f);
                ___EyeRight.localRotation = targetRot;
            }
        }
    }
}
