using System.Collections;
using ABI_RC.Core.Base;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Core.Util;
using ABI_RC.Systems.InputManagement;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace Kafe.BetterPortals;

public class BetterPortals : MelonMod {

    private static bool _initialized;
    private static bool _portalPlacementCoolingDown;
    private static bool _portalClickingCoolingDown;

    private static readonly int AnimatorParameterShow = Animator.StringToHash("Show");

    private static TextMeshPro _hudTextTmpDesktop;
    private static Animator _hudTextAnimatorDesktop;

    private static TextMeshPro _hudTextTmpVR;
    private static Animator _hudTextAnimatorVR;

    private static bool _showingJoinPrompt;
    private static CVRPortalManager _showingJoinPromptPortal;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);
    }

    private static Vector3 GetPlayerRootPosition() {
        return PlayerSetup.Instance._movementSystem.rotationPivot.position with {
            y = PlayerSetup.Instance._movementSystem.transform.position.y
        };
    }

    private static IEnumerator HandlePortalPlacementCooldown() {
        _portalPlacementCoolingDown = true;
        yield return new WaitForSeconds(5);
        _portalPlacementCoolingDown = false;
    }

    private static IEnumerator JoinPortal() {
        _portalClickingCoolingDown = true;
        Instances.SetJoinTarget(_showingJoinPromptPortal.Portal.InstanceId, _showingJoinPromptPortal.Portal.WorldId);
        _showingJoinPromptPortal.Despawn();
        DisableJoiningPrompt();
        yield return new WaitForSeconds(3);
        _portalClickingCoolingDown = false;
    }

    private static void DisableJoiningPrompt() {
        if (_showingJoinPrompt) {
            _hudTextAnimatorDesktop.SetBool(AnimatorParameterShow, false);
            _hudTextAnimatorVR.SetBool(AnimatorParameterShow, false);
            _showingJoinPrompt = false;
            _showingJoinPromptPortal = null;
        }
    }

    private static void HandleClosestPortal(CVRPortalManager portal) {

        if (portal == null) {
            DisableJoiningPrompt();
        }
        else if (portal != _showingJoinPromptPortal) {
            _hudTextAnimatorDesktop.SetBool(AnimatorParameterShow, true);
            _hudTextTmpDesktop.text = $"Press <b><color=green>J</color></b> to enter the <color=#0090ff>{portal.Portal.PortalName}</color> portal";
            _hudTextAnimatorVR.SetBool(AnimatorParameterShow, true);
            _hudTextTmpVR.text = $"Press <b><color=green>Trigger</color></b> to enter the <color=#0090ff>{portal.Portal.PortalName}</color> portal";
            _showingJoinPrompt = true;
            _showingJoinPromptPortal = portal;
        }
    }

    private static void HandleJointPromptInputs() {
        // Ignore if there is no portal
        if (!_showingJoinPrompt || _showingJoinPromptPortal == null) return;

        // Needs a trigger to join the instance
        if (ModConfig.MeNeedInputToTriggerJoining.Value) {
            if (MetaPort.Instance.isUsingVr) {
                // Pressed both triggers
                if (CVRInputManager.Instance.interactLeftDown || CVRInputManager.Instance.interactRightDown) {
                    MelonCoroutines.Start(JoinPortal());
                }
            }
            else {
                // Pressed J without any modifier being held
                if (Input.GetKeyDown(KeyCode.J)
                    && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)
                    && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)
                    && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt)) {
                    MelonCoroutines.Start(JoinPortal());
                }
            }
        }
        // Join the instance right away
        else {
            MelonCoroutines.Start(JoinPortal());
        }
    }

    public override void OnUpdate() {

        if (!_initialized || !ModConfig.MeJoinPortalWhenClose.Value) return;

        // We still cooling down from clicking a portal...
        if (_portalClickingCoolingDown) return;

        var currentMinDistance = float.MaxValue;
        CVRPortalManager currentMinDistancePortal = null;

        foreach (var cvrPortalManager in Portals.List.FindAll(x => x.IsVisible && x.IsInitialized && x.type == CVRPortalManager.PortalType.Instance)) {
            var portalDistance = Vector3.Distance(GetPlayerRootPosition(), cvrPortalManager.transform.position);
            if (portalDistance < ModConfig.MeEnterPortalDistance.Value && portalDistance < currentMinDistance) {
                currentMinDistance = portalDistance;
                currentMinDistancePortal = cvrPortalManager;
            }
        }

        HandleClosestPortal(currentMinDistancePortal);
        HandleJointPromptInputs();
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HudOperations), nameof(HudOperations.Start))]
        public static void After_HudOperations_Start(HudOperations __instance) {
            // Initialize hud prefabs
            try {
                var desktopHud = __instance.worldLoadingItemDesktop.transform.parent;
                var desktopHudText = UnityEngine.Object.Instantiate(ModConfig.TextPrefab, desktopHud, false);
                desktopHudText.transform.localPosition = new Vector3(-1300f, -80f, 0f);
                desktopHudText.transform.localScale = new Vector3(20f, 20f, 20f);
                _hudTextAnimatorDesktop = desktopHudText.GetComponent<Animator>();
                _hudTextTmpDesktop = desktopHudText.GetComponent<TextMeshPro>();
                _hudTextTmpDesktop.text = "";

                var vrHud = __instance.worldLoadingItemVr.transform.parent;
                var vrHudText = UnityEngine.Object.Instantiate(ModConfig.TextPrefab, vrHud, false);
                vrHudText.transform.localPosition = new Vector3(-800f, 200f, 0f);
                vrHudText.transform.localScale = new Vector3(25f, 25f, 25f);
                _hudTextAnimatorVR = vrHudText.GetComponent<Animator>();
                _hudTextTmpVR = vrHudText.GetComponent<TextMeshPro>();
                _hudTextTmpVR.text = "";

                _initialized = true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_HudOperations_Start)}");
                MelonLogger.Error(e);
            }
        }


        private static readonly Vector3 Offset = new Vector3(1f, 0.0f, 1f);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.DropPortal))]
        public static bool Before_PlayerSetup_DropPortal(PlayerSetup __instance, string instanceID) {
            try {
                if (_portalPlacementCoolingDown) return false;
                var rotationPivot = __instance._movementSystem.rotationPivot;
                var origin = rotationPivot.position + Vector3.Scale(rotationPivot.forward, Offset).normalized;
                if (Physics.Raycast(origin, Vector3.down, out var hitInfo, 4f, -33 & -32769)) {
                    CVRSyncHelper.SpawnPortal(instanceID, hitInfo.point.x, hitInfo.point.y, hitInfo.point.z);
                    MelonCoroutines.Start(HandlePortalPlacementCooldown());
                }
                else if (ModConfig.MePlacePortalsMidAir.Value) {
                    var target = GetPlayerRootPosition() + Vector3.Scale(rotationPivot.forward, Offset).normalized;
                    CVRSyncHelper.SpawnPortal(instanceID, target.x, target.y, target.z);
                    MelonCoroutines.Start(HandlePortalPlacementCooldown());
                }
                else {
                    ViewManager.Instance.TriggerAlert("Portal Error", "No suitable surface was found to spawn a Portal.", -1, true);
                }
                return false;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_PlayerSetup_DropPortal)}");
                MelonLogger.Error(e);
            }

            return true;
        }

        private static string GetPortalText(ABI_RC.Core.InteractionSystem.PortalEntity_t portal, float secondsLeft) {
            var str = portal.PortalName;
            if (!string.IsNullOrEmpty(portal.InstanceOwnerName)) {
                str = str + "\nOwner: " + portal.InstanceOwnerName;
            }
            if (!string.IsNullOrEmpty(portal.InstanceId)) {
                str = str + "\n" + portal.InstanceType;
            }
            str = str + "\n" + (int) Math.Round(secondsLeft, 0);
            return str;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPortalManager), nameof(CVRPortalManager.WriteData))]
        public static void After_CVRPortalManager_WriteData(CVRPortalManager __instance) {
            try {
                // Ignore world portals
                if (__instance.type != CVRPortalManager.PortalType.Instance) return;

                __instance.textObject.text = GetPortalText(__instance.Portal, __instance.portalTime);

                // Warn user if dropped on top of them
                if (ModConfig.MeNotifyOnInvisiblePortalDrop.Value) {
                    var playerDistance = Vector3.Distance(PlayerSetup.Instance._movementSystem.rotationPivot.position, __instance.transform.position);
                    var maxDistance = MetaPort.Instance.settings.GetSettingsFloat("GeneralPortalSafeDistance") / 100f;
                    if (!__instance.IsVisible && __instance.IsInitialized && playerDistance <= maxDistance && __instance.portalOwner != MetaPort.Instance.ownerId) {
                        CohtmlHud.Instance.ViewDropText("", "A portal was dropped on top of you, walk away to make it visible.");
                    }
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRPortalManager_WriteData)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPortalManager), nameof(CVRPortalManager.Tick))]
        public static void After_CVRPortalManager_Tick(CVRPortalManager __instance) {
            try {
                // Ignore not started portals and world portals
                if (!__instance.timerStarted || __instance.type != CVRPortalManager.PortalType.Instance) return;

                var portalTimeLeft = __instance.portalTime - __instance.portalCurrentTime;
                __instance.textObject.text = GetPortalText(__instance.Portal, portalTimeLeft);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRPortalManager_Tick)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            try {
                // Inject our Cohtml
                __instance.gameMenuView.Listener.FinishLoad += _ => {
                    __instance.gameMenuView.View.ExecuteScript(ModConfig.JavascriptPatchesContent);
                };
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
