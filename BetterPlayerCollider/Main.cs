using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.IK.SubSystems;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using Valve.VR;

#if DEBUG
using ABI_RC.Core.Savior;
using Kafe.CCK.Debugger.Components;
using CCKDebugger = Kafe.CCK.Debugger;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
#endif

namespace Kafe.BetterPlayerCollider;

public class BetterPlayerCollider : MelonMod {

    private static bool _enabled;

    private static Transform _leftFootTransform;
    private static Transform _rightFootTransform;

    private static Vector3 _lastColliderPosition;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    [HarmonyPatch]
    internal class HarmonyPatches {


        [HarmonyPostfix]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Start))]
        public static void After_MovementSystem_Start(MovementSystem __instance) {
            __instance.gameObject.AddComponent<BetterPlayerColliderHelper>();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.ClearAvatar))]
        public static void Before_PlayerSetup_ClearAvatar(PlayerSetup __instance) {
            _enabled = false;
        }

        private static GameObject _center;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.CalibrateAvatar))]
        public static void After_PlayerSetup_CalibrateAvatar(PlayerSetup __instance) {
            try {

                if (__instance._avatar == null
                    || __instance._animator == null
                    || !MetaPort.Instance.isUsingVr
                    || !__instance._animator.isHuman)
                    return;


                #if DEBUG
                _center = new GameObject("CenterCollider");
                _center.transform.SetParent(__instance._avatar.transform);
                _center.transform.localPosition = Vector3.zero;
                _center.transform.eulerAngles = Vector3.zero;

                LabeledVisualizer.Create(_center, "Center Collider");

                #endif

                _leftFootTransform = __instance._animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFootTransform = __instance._animator.GetBoneTransform(HumanBodyBones.RightFoot);

                if (_leftFootTransform == null || _rightFootTransform == null) {
                    _enabled = false;
                    return;
                }

                _enabled = true;

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_CalibrateAvatar)}");
                MelonLogger.Error(e);
            }
        }

        private static bool _previousWasColliding;
        private static bool _previousCanRot;
        private static bool _justRestored;
        private static Vector3 _lastUpdateColliderWorldPos;

        private static Vector3 _rootBeforeColliding;
        private static Quaternion _rootBeforeCollidingRot;

        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateColliderCenter))]
        // public static bool Before_MovementSystem_UpdateColliderCenter(MovementSystem __instance) {
        //     return !_previousWasColliding;
        // }

        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(VRIKRootController), nameof(VRIKRootController.OnPreUpdate))]
        // public static void After_VRIKRootController_OnPreUpdate(VRIKRootController __instance) {
        //     if (!MetaPort.Instance.isUsingVr) return;
        //     if (BetterPlayerColliderHelper.IsCollidingWithWall()) {
        //         __instance.ik.references.root.position = _rootBeforeColliding;
        //     }
        //     else {
        //         _rootBeforeColliding = __instance.ik.references.root.position;
        //     }
        // }
        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(IKSolverVR), nameof(IKSolverVR.OnUpdate))]
        // public static void After_IKSolverVR_OnUpdate(IKSolverVR __instance) {
        //     if (!MetaPort.Instance.isUsingVr) return;
        //     if (BetterPlayerColliderHelper.IsCollidingWithWall()) {
        //         __instance.root.position = _rootBeforeColliding;
        //     }
        //     else {
        //         _rootBeforeColliding = __instance.root.position;
        //     }
        // }

        // private static void OnPreSolverUpdate() {
        //     if (ModConfig.MePreventWallPushback.Value && BetterPlayerColliderHelper.IsCollidingWithWall()) {
        //         IKSystem.VrikRootController.enabled = false;
        //         IKSystem.vrik.references.root.position = _rootBeforeColliding;
        //         IKSystem.vrik.references.root.rotation = _rootBeforeCollidingRot;
        //     }
        // }
        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(VRIKRootController), nameof(VRIKRootController.Awake))]
        // public static void After_VRIKRootController_Awake(VRIKRootController __instance) {
        //     __instance.ik.onPreSolverUpdate.AddListener(OnPreSolverUpdate);
        // }









        // internal class PosRot {
        //     internal Vector3 Pos;
        //     internal Quaternion Rot;
        // }
        //
        // private static readonly PosRot PelvisCache = new();
        //
        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(VRIKRootController), nameof(VRIKRootController.OnPreUpdate))]
        // public static void Before_VRIKRootController_OnPreUpdate(VRIKRootController __instance, out PosRot __state) {
        //     __state = null;
        //     if (!__instance.enabled || __instance.pelvisTarget == null) return;
        //     PelvisCache.Pos = __instance.ik.references.pelvis.position;
        //     PelvisCache.Rot = __instance.ik.references.pelvis.rotation;
        //     __state = PelvisCache;
        // }
        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(VRIKRootController), nameof(VRIKRootController.OnPreUpdate))]
        // public static void After_VRIKRootController_OnPreUpdate(VRIKRootController __instance, PosRot __state) {
        //     if (!__instance.enabled || __instance.pelvisTarget == null) return;
        //     __instance.ik.references.pelvis.position = __state.Pos;
        //     __instance.ik.references.pelvis.rotation = __state.Rot;
        // }










        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Start))]
        // public static void After_MovementSystem_UpdateColliderCenter(MovementSystem __instance) {
        //     __instance.controller.skinWidth = 0.2f;
        //     __instance.controller.enableOverlapRecovery = false;
        //
        //     // Create the fat collider
        //     FatPlayerCollider.Create(__instance);
        // }
        //
        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateColliderCenter))]
        // public static void Before_MovementSystem_UpdateColliderCenter(MovementSystem __instance, ref Vector3 position) {
        //
        //     if (ModConfig.MePreventWallPushback.Value && BetterPlayerColliderHelper.IsCollidingWithWall()) {
        //         if (__instance.canRot) {
        //             __instance.canRot = false;
        //         }
        //         _justRestored = true;
        //         position = _lastUpdateColliderWorldPos;
        //     }
        //     else {
        //         if (_justRestored) {
        //             __instance.canRot = _previousCanRot;
        //             _justRestored = false;
        //         }
        //         _previousCanRot = __instance.canRot;
        //         _lastUpdateColliderWorldPos = position;
        //     }
        //     #if DEBUG
        //     _center.transform.localPosition = __instance._colliderCenter;
        //     #endif
        // }












        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Update))]
        // public static void After_MovementSystem_Update(MovementSystem __instance) {
        //     try {
        //         if (_enabled) {
        //
        //             // Override rotation pivot?
        //             // when colliding with walls
        //
        //             // if (!ModConfig.MePreventWallPushback.Value || !BetterPlayerColliderHelper.IsCollidingWithWall()) return;
        //
        //             if (!CVRInputManager.Instance.independentHeadTurn && !CVRInputManager.Instance.independentHeadToggle && !__instance.sitting && __instance.canRot) {
        //
        //                 // var transform = __instance.rotationPivot;
        //                 // if (!MetaPort.Instance.isUsingVr && PlayerSetup.Instance.animatorManager.AnimatorIsHuman()) {
        //                 //     transform = PlayerSetup.Instance.animatorManager.getHumanHeadTransform();
        //                 //     if (transform == null) {
        //                 //         transform = __instance.rotationPivot;
        //                 //     }
        //                 // }
        //
        //                 // if (transform != null) {
        //                 if (MetaPort.Instance.isUsingVr) {
        //                     __instance.transform.RotateAround(__instance._colliderCenter, Vector3.up, (float) ((double) CVRInputManager.Instance.lookVector.x * __instance.rotationMultiplier * Time.deltaTime * 90.0));
        //                 }
        //                 else {
        //                     __instance.transform.RotateAround(__instance._colliderCenter, Vector3.up, CVRInputManager.Instance.lookVector.x * __instance.rotationMultiplier);
        //                 }
        //                 __instance.transform.RotateAround(__instance._colliderCenter, Vector3.up, CVRInputManager.Instance.sectionTurn * __instance.sectionTurnDegrees);
        //                 // }
        //             }
        //         }
        //     }
        //     catch (Exception e) {
        //         MelonLogger.Error($"Error during the patched function {nameof(After_MovementSystem_Update)}");
        //         MelonLogger.Error(e);
        //     }
        // }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.LateUpdate))]
        public static void After_PlayerSetup_LateUpdate(PlayerSetup __instance) {
            try {
                if (_enabled) {

                    // // Send the last position we have before collide with a wall (the y is still calculated after)
                    // if (ModConfig.MePreventWallPushback.Value && BetterPlayerColliderHelper.IsCollidingWithWall()) {
                    //     __instance._movementSystem.UpdateColliderCenter(_lastColliderPosition);
                    //     if (!_previousWasColliding) {
                    //         MovementSystem.Instance.controller.enableOverlapRecovery = false;
                    //         _previousWasColliding = true;
                    //
                    //         // _rootBeforeColliding = IKSystem.vrik.references.root.position;
                    //         // _rootBeforeCollidingRot = IKSystem.vrik.references.root.rotation;
                    //
                    //
                    //     }
                    //     // IKSystem.vrik.references.root.localPosition = _rootBeforeColliding;
                    //     return;
                    // }
                    //
                    // // if (ModConfig.MePreventWallPushback.Value && !BetterPlayerColliderHelper.IsCollidingWithWall()) {
                    // //     _rootBeforeColliding = IKSystem.vrik.references.root.localPosition;
                    // // }
                    //
                    //
                    // if (_previousWasColliding && !BetterPlayerColliderHelper.IsCollidingWithWall()) {
                    //     MovementSystem.Instance.controller.enableOverlapRecovery = false;
                    //     _previousWasColliding = false;
                    // }


                    // Move the collider to the feet and save the position of the collider (feet)
                    if (ModConfig.MePlaceColliderOnFeet.Value) {
                       // _lastColliderPosition = Vector3.Lerp(_leftFootTransform.position, _rightFootTransform.position, 0.5f);
                        __instance._movementSystem.UpdateColliderCenter(Vector3.Lerp(_leftFootTransform.position, _rightFootTransform.position, 0.5f));
                    }
                    //
                    // // Just save the position of the collider (cvr uses the humanoid head by default)
                    // else {
                    //     //_lastColliderPosition = __instance._animator.GetBoneTransform(HumanBodyBones.Head).position;
                    // }
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }
}
