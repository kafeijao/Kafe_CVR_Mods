using System.Reflection;
using System.Reflection.Emit;
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

    public override void OnInitializeMelon() {
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


        [HarmonyTranspiler]
        [HarmonyPatch(typeof(CVRParameterStreamEntry), nameof(CVRParameterStreamEntry.CheckUpdate))]
        private static IEnumerable<CodeInstruction> Transpiler_CVRParameterStreamEntry_CheckUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator il) {
            // Sorry Daky I added the transpiler ;_;
            // The other code was too big and would probably break with any update

            var skippedX = false;

            // Look for all: component.eyeAngle.x;
            var matcher = new CodeMatcher(instructions).MatchForward(false,
                OpCodes.Ldloc_S,
                new CodeMatch(i => i.opcode == OpCodes.Ldflda && i.operand is FieldInfo { Name: "eyeAngle" }),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo { Name: "x" }));

            // Replace the 2nd match with: component.eyeAngle.y;
            return matcher.Repeat(innerMatcher => {
                // Skip first match, the first match is assigning correctly the X vector to the X angle
                if (!skippedX) {
                    skippedX = true;
                    innerMatcher.Advance(2);
                }
                // Find the second match which is assigning EyeMovementLeftY/EyeMovementRightY the eyeAngle.x and fix it
                else {
                    skippedX = false;
                    innerMatcher.Advance(2);
                    // Set operand to Y instead of X of the Vector2
                    innerMatcher.SetOperandAndAdvance(AccessTools.Field(typeof(Vector2), "y"));
                }
            }).InstructionEnumeration();
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


        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeController), "Start")]
        private static void Before_CVREyeControllerManager_Start(CVREyeController __instance, ref Transform ___EyeLeft, ref Transform ___EyeRight) {

            if (__instance.isLocal) return;
            try {
                // Mega hack because for some reason the CVRAvatar Start is not being called...
                // And I really need the puppet master to be there so it can properly calculate the eye positions!
                var avatar = __instance.GetComponent<CVRAvatar>();
                Traverse.Create(avatar).Field("puppetMaster").SetValue(__instance.transform.parent.parent.GetComponentInParent<PuppetMaster>());
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeControllerManager), nameof(CVREyeControllerManager.GetNewTarget))]
        private static bool Before_CVREyeControllerManager_Start(CVREyeController controller, ref string __result) {
            // Prevent executing and send the same target as the previous. We'll be implementing our own
            __result = Traverse.Create(controller).Field<string>("targetGuid").Value;
            return false;
        }
    }
}
