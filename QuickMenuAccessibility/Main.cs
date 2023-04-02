using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Valve.VR;

namespace Kafe.QuickMenuAccessibility;

public class QuickMenuAccessibility : MelonMod {

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        // Check for BTKUILib
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == "BTKUILib") != null) {
            MelonLogger.Msg($"Detected BTKUILib mod! Adding the integration!");
            ModConfig.InitializeBTKUI();
        }

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static GameObject _rightVrAnchor;
        private static bool _snappedToRightController;

        private static bool _initialized;

        // Save the last opened position/rotations for the left and right hand
        private static Vector3 _lastLeftPosOpened = Vector3.zero;
        private static Quaternion _lastLeftRotOpened = Quaternion.identity;
        private static Vector3 _lastRightPosOpened = Vector3.zero;
        private static Quaternion _lastRightRotOpened= Quaternion.identity;

        private static Vector3 MirrorPosition(Vector3 pos) {
            // Mirror the position offset by negating the X value
            pos.x = -pos.x;
            return pos;
        }

        private static Vector3 MirrorEulerRotation(Vector3 rot) {
            // Mirror the rotation offset by negating the Y and Z values
            rot.y = -rot.y;
            rot.z = -rot.z;
            return rot;
        }

        private static Quaternion MirrorRotation(Quaternion rot) {
            // Mirror the rotation by negating the x and w components of the Quaternion
            return new Quaternion(-rot.x, rot.y, rot.z, -rot.w);
        }

        private static void SetupRightAnchor(CVR_MenuManager menuManager, PlayerSetup playerSetup) {

            if (_initialized) {
                MelonLogger.Error($"Something went wrong... The SetupRightAnchor was called twice O_o");
                return;
            }

            // Create the Right VR Anchor
            _rightVrAnchor = new GameObject("Right VR Anchor");
            _rightVrAnchor.transform.SetParent(playerSetup.vrRightHandTracker.transform);

            _rightVrAnchor.transform.localPosition = MirrorPosition(menuManager.coreData.menuParameters.quickPositionOffset);
            _rightVrAnchor.transform.localEulerAngles = MirrorEulerRotation(menuManager.coreData.menuParameters.quickRotationOffset);

            // Handle config changes
            _snappedToRightController = ModConfig.MeSwapQuickMenuHands.Value;
            ModConfig.MeSwapQuickMenuHands.OnEntryValueChanged.Subscribe((_, snapToRightController) => {
                _snappedToRightController = snapToRightController;
            });

            _initialized = true;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {
                if (CVR_MenuManager.Instance == null) return;
                SetupRightAnchor(CVR_MenuManager.Instance, __instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        public static void After_CVR_MenuManager_Start(CVR_MenuManager __instance) {
            try {
                if (PlayerSetup.Instance == null) return;
                SetupRightAnchor(__instance, PlayerSetup.Instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }



        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        public static void After_CVR_MenuManager_ToggleQuickMenu(CVR_MenuManager __instance, bool show) {
            try {
                if (!_initialized || !show) return;

                var leftAnchor = __instance.leftVrController;
                var rightAnchor = _rightVrAnchor.transform;
                _lastLeftPosOpened = leftAnchor.position;
                _lastLeftRotOpened = leftAnchor.rotation;
                _lastRightPosOpened = rightAnchor.position;
                _lastRightRotOpened = rightAnchor.rotation;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.LateUpdate))]
        public static void After_CVR_MenuManager_LateUpdate(CVR_MenuManager __instance) {
            try {

                // Handle the position updates of the QM to match the hand on the config

                if (!_initialized) return;

                // Ignore first if and ignoring in that case
                if (__instance._quickMenuOpen && __instance._desktopMouseMode && !MetaPort.Instance.isUsingVr) {
                    return;
                }

                // Updates when we're in grab mode to reposition the QM
                if (__instance._quickMenuOpen && __instance.coreData.menuParameters.quickMenuInGrabMode && __instance._quickMenuGrabbed) {

                    if (_snappedToRightController) {

                        // Update the right vr hand anchor
                        var quickMenuTransform = __instance.quickMenu.transform;
                        _rightVrAnchor.transform.position = quickMenuTransform.position;
                        _rightVrAnchor.transform.rotation = quickMenuTransform.rotation;

                        // Lets save the settings mirrored, this way even if we swap hands the offsets should be good
                        __instance.coreData.menuParameters.quickPositionOffset = MirrorPosition(_rightVrAnchor.transform.localPosition);
                        __instance.coreData.menuParameters.quickRotationOffset = MirrorEulerRotation(_rightVrAnchor.transform.localEulerAngles);
                    }

                }

                // Normal updates of the QM position/rotation
                else if (MetaPort.Instance.isUsingVr && __instance._quickMenuCollider.enabled) {

                    // If we got the option to stuck the QM to the world, lets set the position to the initial one
                    if (ModConfig.MeDropQuickMenuInWorld.Value) {
                        var quickMenuTransform = __instance.quickMenu.transform;
                        quickMenuTransform.position = _snappedToRightController ? _lastRightPosOpened : _lastLeftPosOpened;
                        quickMenuTransform.rotation = _snappedToRightController ? _lastRightRotOpened : _lastLeftRotOpened;
                        return;
                    }

                    // If we chose using the right controller, set the position to the right anchor
                    if (_snappedToRightController) {
                        var quickMenuTransform = __instance.quickMenu.transform;
                        quickMenuTransform.position = _rightVrAnchor.transform.position;
                        quickMenuTransform.rotation = _rightVrAnchor.transform.rotation;
                    }
                }

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ExitRepositionMode))]
        public static void After_CVR_MenuManager_ExitRepositionMode(CVR_MenuManager __instance) {
            try {

                if (!_initialized) return;

                // Override the setting saving, so we always save the offsets of the left controller
                if (_snappedToRightController) {
                    __instance.settings.quickMenuPositionOffset = MirrorPosition(__instance.coreData.menuParameters.quickPositionOffset);
                    __instance.settings.quickMenuRotationOffset = MirrorEulerRotation(__instance.coreData.menuParameters.quickRotationOffset);
                    __instance.SaveSettings();
                }

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_ExitRepositionMode)}");
                MelonLogger.Error(e);
            }
        }

        private static SteamVR_Input_Sources ApplyButtonConfig(SteamVR_Input_Sources source) {
            if (ModConfig.MeSwampQuickMenuButton.Value) {
                if (source == SteamVR_Input_Sources.LeftHand) source = SteamVR_Input_Sources.RightHand;
                else if (source == SteamVR_Input_Sources.RightHand) source = SteamVR_Input_Sources.LeftHand;
            }
            return source;
        }

        private static bool GetStateDown(SteamVR_Input_Sources inputSource) {
            return InputModuleSteamVR.Instance.vrMenuButton.GetStateDown(ApplyButtonConfig(inputSource));
        }

        private static bool GetState(SteamVR_Input_Sources inputSource) {
            return InputModuleSteamVR.Instance.vrMenuButton.GetState(ApplyButtonConfig(inputSource));
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(InputModuleSteamVR), nameof(InputModuleSteamVR.UpdateInput))]
        private static IEnumerable<CodeInstruction> Transpiler_MovementSystem_Update(IEnumerable<CodeInstruction> instructions, ILGenerator il) {

            // Transpiler to invert the inputs of the QM and Big Menu buttons
            var patchedGetStateDownCount = 0;
            var patchedGetStateCount = 0;

            // Patch the GetStateDown on vrMenuButton
            var patchedInstructions = new CodeMatcher(instructions).MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo { Name: "vrMenuButton" }),
                new CodeMatch(_ => true),
                new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo { Name: "GetStateDown" }))
            .Repeat(matched => {
                matched.RemoveInstructions(2); // Remove this.vrMenuButton
                matched.Advance(1);
                matched.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(HarmonyPatches), nameof(GetStateDown)));
                patchedGetStateDownCount++;
            }).InstructionEnumeration();

            // Patch the GetState on vrMenuButton
            patchedInstructions =  new CodeMatcher(patchedInstructions).MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo { Name: "vrMenuButton" }),
                new CodeMatch(_ => true),
                new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo { Name: "GetState" }))
            .Repeat(matched => {
                matched.RemoveInstructions(2); // Remove this.vrMenuButton
                matched.Advance(1);
                matched.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(HarmonyPatches), nameof(GetState)));
                patchedGetStateCount++;
            }).InstructionEnumeration();

            MelonLogger.Msg($"Patches vrMenuButton -> GetStateDown: {patchedGetStateDownCount}/2, GetState: {patchedGetStateCount}/2");

            return patchedInstructions;
        }

    }
}
