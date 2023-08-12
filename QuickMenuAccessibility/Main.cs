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

    public static Action<Transform> AnchorChanged;

    private static GameObject _rightVrAnchor;
    private static bool _snappedToRightController;

    private static GameObject _worldVrAnchor;
    private static bool _snappedToWorldAnchor;

    private static bool _initialized;

    public static bool _shouldSkipQuickMenu;

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

    public override void OnLateInitializeMelon() {
        // Start the giga hacks

        // Check for Menu Scale Patch
        var possibleMenuScalePatch = RegisteredMelons.FirstOrDefault(m => m.Info.Name == "MenuScalePatch");
        if (possibleMenuScalePatch != null) CompatibilityHacksMenuScalePatch.Initialize(possibleMenuScalePatch);

        // Check for Action Menu
        var possibleActionMenu = RegisteredMelons.FirstOrDefault(m => m.Info.Name == "Action Menu");
        if (possibleActionMenu != null) CompatibilityHacksActionMenu.Initialize(HarmonyInstance, possibleActionMenu);
    }

    public static SteamVR_Input_Sources ApplyAndGetButtonConfig(SteamVR_Input_Sources source) {
        if (ModConfig.MeSwampQuickMenuButton.Value) {
            if (source == SteamVR_Input_Sources.LeftHand) source = SteamVR_Input_Sources.RightHand;
            else if (source == SteamVR_Input_Sources.RightHand) source = SteamVR_Input_Sources.LeftHand;
        }
        return source;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

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

        private static Transform GetCurrentAnchor() {
            if (_snappedToWorldAnchor || VRTrackerManager.Instance != null && !VRTrackerManager.Instance.CheckTwoTrackedHands()) return _worldVrAnchor.transform;
            if (_snappedToRightController) return _rightVrAnchor.transform;
            return CVR_MenuManager.Instance._leftVrAnchor.transform;
        }

        private static void SetupCustomAnchors(CVR_MenuManager menuManager, PlayerSetup playerSetup) {

            if (_initialized) {
                MelonLogger.Error($"Something went wrong... The SetupRightAnchor was called twice O_o");
                return;
            }

            // Create the Right VR Anchor
            _rightVrAnchor = new GameObject("Right VR Anchor");
            _rightVrAnchor.transform.SetParent(playerSetup.vrRightHandTracker.transform);

            _rightVrAnchor.transform.localPosition = MirrorPosition(menuManager.coreData.menuParameters.quickPositionOffset);
            _rightVrAnchor.transform.localEulerAngles = MirrorEulerRotation(menuManager.coreData.menuParameters.quickRotationOffset);

            // Create the World VR Anchor
            _worldVrAnchor = new GameObject("World VR Anchor");
            _worldVrAnchor.transform.SetParent(menuManager.transform);

            // Handle right controller snap config changes
            _snappedToRightController = ModConfig.MeSwapQuickMenuHands.Value;
            ModConfig.MeSwapQuickMenuHands.OnEntryValueChanged.Subscribe((_, snapToRightController) => {
                _snappedToRightController = snapToRightController;
                AnchorChanged?.Invoke(GetCurrentAnchor());
            });

            // Handle world space snap config changes
            _snappedToWorldAnchor = ModConfig.MeDropQuickMenuInWorld.Value;
            ModConfig.MeDropQuickMenuInWorld.OnEntryValueChanged.Subscribe((_, snapToWorld) => {
                _snappedToWorldAnchor = snapToWorld;
                AnchorChanged?.Invoke(GetCurrentAnchor());
            });

            AnchorChanged?.Invoke(GetCurrentAnchor());

            _initialized = true;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {
                if (CVR_MenuManager.Instance == null) return;
                SetupCustomAnchors(CVR_MenuManager.Instance, __instance);
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
                SetupCustomAnchors(__instance, PlayerSetup.Instance);
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

                // Set the world anchor to face the player
                var rotationPivot = PlayerSetup.Instance._movementSystem.rotationPivot;
                var rotationPivotForward = rotationPivot.forward;
                _worldVrAnchor.transform.rotation = Quaternion.LookRotation(rotationPivotForward, Vector3.up);
                _worldVrAnchor.transform.position = rotationPivot.position + rotationPivotForward * 0.5f * ViewManager.Instance.scaleFactor;

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_ToggleQuickMenu)}");
                MelonLogger.Error(e);
            }
        }

        private static bool _lastCheckTrackedHands;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.LateUpdate))]
        public static void After_CVR_MenuManager_LateUpdate(CVR_MenuManager __instance) {
            try {

                // If we happen to lose tracking on one of the hands, enable the world space QM
                if (VRTrackerManager.Instance != null && VRTrackerManager.Instance.CheckTwoTrackedHands() != _lastCheckTrackedHands) {
                    AnchorChanged?.Invoke(GetCurrentAnchor());
                    _lastCheckTrackedHands = VRTrackerManager.Instance.CheckTwoTrackedHands();
                }

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

                    // If we got the option to stuck the QM to the world, set the position to the world anchor
                    if (_snappedToWorldAnchor || VRTrackerManager.Instance != null && !VRTrackerManager.Instance.CheckTwoTrackedHands()) {
                        var quickMenuTransform = __instance.quickMenu.transform;
                        quickMenuTransform.position = _worldVrAnchor.transform.position;
                        quickMenuTransform.rotation = _worldVrAnchor.transform.rotation;
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

        private static bool GetStateDown(SteamVR_Input_Sources inputSource) {
            return InputModuleSteamVR.Instance.vrMenuButton.GetStateDown(ApplyAndGetButtonConfig(inputSource));
        }

        private static bool GetState(SteamVR_Input_Sources inputSource) {
            return InputModuleSteamVR.Instance.vrMenuButton.GetState(ApplyAndGetButtonConfig(inputSource));
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

            #if DEBUG
            MelonLogger.Msg($"Patches vrMenuButton -> GetStateDown: {patchedGetStateDownCount}/2, GetState: {patchedGetStateCount}/2");
            #endif

            return patchedInstructions;
        }

        // 2 Menu 1 Button
        private static int _handledQMFrame;
        private static int _handledMMFrame;

        [HarmonyPrefix]
        [HarmonyPriority(99999)]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        public static bool Before_CVR_MenuManager_ToggleQuickMenu(CVR_MenuManager __instance, ref bool show) {
            try {

                // This is pure cancer :/ Whoever tries to understand this I'm sorry

                if (_shouldSkipQuickMenu) return false;

                if (!_initialized || !CVRInputManager.Instance.quickMenuButton || !MetaPort.Instance.isUsingVr) return true;

                if (_handledQMFrame == Time.frameCount) return false;

                if (ModConfig.MeSingleButton.Value || !VRTrackerManager.Instance.CheckTwoTrackedHands()) {

                    _handledQMFrame = Time.frameCount;

                    var mainMenu = ViewManager.Instance;

                    if (!mainMenu._gameMenuOpen && !__instance._quickMenuOpen) {
                        mainMenu.UiStateToggle(false);
                        __instance.ToggleQuickMenu(true);
                    }
                    else if (!mainMenu._gameMenuOpen && __instance._quickMenuOpen) {
                        mainMenu.UiStateToggle(true);
                        __instance.ToggleQuickMenu(false);
                    }
                    else {
                        mainMenu.UiStateToggle(false);
                        // Need to set show ref to false because otherwise it would open the menu
                        show = false;
                        __instance.ToggleQuickMenu(false);
                    }

                    return false;
                }

                return true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_CVR_MenuManager_ToggleQuickMenu)}");
                MelonLogger.Error(e);
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.UiStateToggle), argumentTypes: new Type[0])]
        public static bool Before_ViewManager_UiStateToggle(ViewManager __instance) {
            try {

                if (!_initialized || _handledMMFrame == Time.frameCount || !CVRInputManager.Instance.mainMenuButton || !MetaPort.Instance.isUsingVr) return true;

                if (ModConfig.MeSingleButton.Value || !VRTrackerManager.Instance.CheckTwoTrackedHands()) {

                    _handledMMFrame = Time.frameCount;

                    var quickMenu = CVR_MenuManager.Instance;

                    if (!__instance._gameMenuOpen && !quickMenu._quickMenuOpen) {
                        __instance.UiStateToggle(true);
                        quickMenu.ToggleQuickMenu(false);
                    }
                    else if (__instance._gameMenuOpen && !quickMenu._quickMenuOpen) {
                        __instance.UiStateToggle(false);
                        quickMenu.ToggleQuickMenu(true);
                    }
                    else {
                        __instance.UiStateToggle(false);
                        quickMenu.ToggleQuickMenu(false);
                    }

                    return false;
                }

                return true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_ViewManager_UiStateToggle)}");
                MelonLogger.Error(e);
            }
            return true;
        }

    }
}
