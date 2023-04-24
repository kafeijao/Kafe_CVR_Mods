using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Valve.VR;

namespace Kafe.BetterAFK;

public class BetterAFK : MelonMod {

    private const string AFK = "AFK";
    private const string AFKTimer = "AFKTimer";

    public static bool IsEndKeyOverridingAFK;

    private static bool _isAFK;
    private static bool _previousIsAFK;
    private static float _headsetRemoveTime;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VRTrackerManager), nameof(VRTrackerManager.Update))]
        public static void After_VRTrackerManager_Update(VRTrackerManager __instance) {
            try {

                // Handle the pressing END to toggle the AFK
                if (ModConfig.MeUseEndKeyToToggleAFK.Value && Input.GetKeyDown(KeyCode.End)) {
                    IsEndKeyOverridingAFK = !IsEndKeyOverridingAFK;
                    #if DEBUG
                    MelonLogger.Msg($"Pressed END! Overriding AFK State via END key: {IsEndKeyOverridingAFK}");
                    #endif
                }

                _isAFK = IsEndKeyOverridingAFK;

                // Handle being in the Steam Overlay to toggle the AFK
                if (!_isAFK && MetaPort.Instance.isUsingVr && ModConfig.MeAfkWhileSteamOverlay.Value && OpenVR.Overlay.IsDashboardVisible()) {
                    _isAFK = true;
                }

                // handle setting AFK is we detect the proximity sensor to be off
                if (!_isAFK && MetaPort.Instance.isUsingVr && OpenVR.System.GetTrackedDeviceActivityLevel(OpenVR.k_unTrackedDeviceIndex_Hmd) != EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction) {
                    _isAFK = true;
                }

                // Handle AFK State changes
                if (_isAFK != _previousIsAFK) {
                    _headsetRemoveTime = Time.time;
                    _previousIsAFK = _isAFK;
                }

                // Set the headset on head and the headset remove time on the Tracker Manager
                if (MetaPort.Instance.isUsingVr) {
                    __instance.headsetOnHead = !_isAFK;
                    __instance.headsetRemoveTime = _headsetRemoveTime;
                }

                // Set the animator AFK property
                if (ModConfig.MeSetAnimatorParameterAFK.Value) {
                    PlayerSetup.Instance.animatorManager.SetAnimatorParameter(AFK, _isAFK ? 1f : 0f);
                }

                // Set the animator AFKTimer property
                if (ModConfig.MeSetAnimatorParameterAFKTimer.Value) {
                    var afkTimer = _isAFK ? Time.time - _headsetRemoveTime : -1f;
                    PlayerSetup.Instance.animatorManager.SetAnimatorParameter(AFKTimer, afkTimer);
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_VRTrackerManager_Update)}");
                MelonLogger.Error(e);
            }
        }
    }
}
