using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util.AssetFiltering;
using ABI.CCK.Components;
using HarmonyLib;
using EyeMovementFix.CCK;
using Kafe.EyeMovementFix.Properties;
using MelonLoader;
using UnityEngine;

namespace Kafe.EyeMovementFix;

public class EyeMovementFix : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    public static MelonPreferences_Entry<bool> MeForceLookAtCamera;
    private static MelonPreferences_Entry<bool> _meIgnorePortableMirrors;

    private static bool _hasPortableMirror;

    public override void OnInitializeMelon() {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(EyeMovementFix));

        MeForceLookAtCamera = _melonCategory.CreateEntry("ForceLookAtCamera", true,
            description: "Whether to force everyone to look at the camera whn being held. The camera needs to be " +
                         "within the avatar field of view. And needs the camera setting to add the camera to look " +
                         "at targets activated (camera settings in-game).");

        _meIgnorePortableMirrors = _melonCategory.CreateEntry("IgnoreLookingAtPortableMirrors", true,
            description: "Whether or not to ignore mirrors created by portable mirror mod. When portable mirrors " +
                         "are active, they create targets for the eye movement. But since I use the mirror to mostly " +
                         "see what I'm doing, makes no sense to target players in the portable mirror.");

        // Add our CCK component to the whitelist
        SharedFilter._avatarWhitelist.Add(typeof(EyeRotationLimits));

        // Check for portable mirror
        _hasPortableMirror = RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.PortableMirrorModName) != null;

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

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeController), nameof(CVREyeController.Start))]
        private static void Before_CVREyeControllerManager_Start(CVREyeController __instance, ref Transform ___EyeLeft, ref Transform ___EyeRight) {

            if (__instance.isLocal) return;
            try {
                // Mega hack because for some reason the CVRAvatar Start is not being called...
                // And I really need the puppet master to be there so it can properly calculate the eye positions!
                var avatar = __instance.GetComponent<CVRAvatar>();
                if (avatar.puppetMaster == null) {
                    avatar.puppetMaster = __instance.transform.parent.parent.GetComponentInParent<PuppetMaster>();
                }
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }


        private static void CacheInitialEyeRotations(Transform transform) {
            try {
                var avatar = transform.GetComponent<CVRAvatar>();
                if (avatar == null) return;
                var animator = avatar.GetComponent<Animator>();
                if (animator == null) return;
                var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                if (leftEye != null) BetterEyeController.OriginalLeftEyeLocalRotation[avatar] = Quaternion.Inverse(transform.rotation) * leftEye.rotation;
                var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
                if (rightEye != null) BetterEyeController.OriginalRightEyeLocalRotation[avatar] = Quaternion.Inverse(transform.rotation) * rightEye.rotation;

                #if DEBUG
                if (leftEye != null) {
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] LeftEye: {leftEye.position.ToString("F2")} - {leftEye.rotation.eulerAngles.ToString("F2")}");
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] Avatar: {transform.position.ToString("F2")} - {transform.rotation.eulerAngles.ToString("F2")}");
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] Saved: {leftEye.rotation.eulerAngles.ToString("F2")} -> {(Quaternion.Inverse(transform.rotation) * leftEye.rotation).eulerAngles.ToString("F2")} -> {(avatar.transform.rotation * (Quaternion.Inverse(transform.rotation) * leftEye.rotation)).eulerAngles.ToString("F2")}");
                }
                if (rightEye != null) {
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] RightEye: {leftEye.position.ToString("F2")} - {rightEye.rotation.eulerAngles.ToString("F2")}");
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] Avatar: {transform.position.ToString("F2")} - {transform.rotation.eulerAngles.ToString("F2")}");
                    MelonLogger.Msg($"[PlayerSetup][Pre][{avatar.GetInstanceID()}] Saved: {rightEye.rotation.eulerAngles.ToString("F2")} -> {(Quaternion.Inverse(transform.rotation) * rightEye.rotation).eulerAngles.ToString("F2")} -> {(avatar.transform.rotation * (Quaternion.Inverse(transform.rotation) * rightEye.rotation)).eulerAngles.ToString("F2")}");
                }
                #endif
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.AvatarInstantiated))]
        private static void Before_CVREyeControllerManager_Start(PuppetMaster __instance) {
            try {
                CacheInitialEyeRotations(__instance.transform);
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.SetupAvatar))]
        private static void Before_PlayerSetup_SetupAvatar(GameObject inAvatar) {
            try {
                CacheInitialEyeRotations(inAvatar.transform);
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.OnDestroy))]
        private static void Before_CVREyeControllerManager_Start(CVRAvatar __instance) {
            try {
                if (BetterEyeController.OriginalLeftEyeLocalRotation.ContainsKey(__instance)) {
                    BetterEyeController.OriginalLeftEyeLocalRotation.Remove(__instance);
                }
                if (BetterEyeController.OriginalRightEyeLocalRotation.ContainsKey(__instance)) {
                    BetterEyeController.OriginalRightEyeLocalRotation.Remove(__instance);
                }
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }
    }
}
