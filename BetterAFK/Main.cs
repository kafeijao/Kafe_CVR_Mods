using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using Unity.XR.OpenVR;
using UnityEngine;
using UnityEngine.XR.Management;
using Valve.VR;

namespace Kafe.BetterAFK;

public class BetterAFK : MelonMod {

    private const string AFK = "AFK";
    private const string AFKTimer = "AFKTimer";

    public static bool IsEndKeyOverridingAFK;

    private static bool _isAFK;
    private static bool _previousIsAFK;
    private static float _headsetRemoveTime;

    // CVR Stream Parameter
    private static bool _streamParamHeadsetOnHead;
    private static float _streamParamHeadsetRemoveTime;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();

        CVRGameEventSystem.VRModeSwitch.OnPreSwitch.AddListener(_ => _isSwitchingMode = true);
        CVRGameEventSystem.VRModeSwitch.OnPostSwitch.AddListener(_ => _isSwitchingMode = false);
        CVRGameEventSystem.VRModeSwitch.OnFailedSwitch.AddListener(_ => _isSwitchingMode = false);
    }

    private bool _isSwitchingMode;

    private static bool IsUsingOpenVR() {
        return MetaPort.Instance.isUsingVr && SteamVR.enabled;
    }

    public override void OnUpdate() {
        try {

            if (PlayerSetup.Instance == null) return;

            if (_isSwitchingMode) return;

            // Handle the pressing END to toggle the AFK
            if (ModConfig.MeUseEndKeyToToggleAFK.Value && Input.GetKeyDown(KeyCode.End)) {
                IsEndKeyOverridingAFK = !IsEndKeyOverridingAFK;
                #if DEBUG
                MelonLogger.Msg($"Pressed END! Overriding AFK State via END key: {IsEndKeyOverridingAFK}");
                #endif
            }

            _isAFK = IsEndKeyOverridingAFK;

            if (MetaPort.Instance.isUsingVr) {

                // Handle OpenVR AFK
                if (XRGeneralSettings.Instance.Manager.activeLoader is OpenVRLoader) {

                    // Handle being in the Steam Overlay to toggle the AFK
                    if (!_isAFK && ModConfig.MeAfkWhileSteamOverlay.Value &&
                        OpenVR.Overlay.IsDashboardVisible()) {
                        _isAFK = true;
                    }

                    // handle setting AFK is we detect the proximity sensor to be off
                    if (!_isAFK && MetaPort.Instance.isUsingVr && OpenVR.System != null &&
                        OpenVR.System.GetTrackedDeviceActivityLevel(OpenVR.k_unTrackedDeviceIndex_Hmd) !=
                        EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction) {
                        _isAFK = true;
                    }
                }

            }

            // Handle AFK State changes
            if (_isAFK != _previousIsAFK) {
                _headsetRemoveTime = Time.time;
                _previousIsAFK = _isAFK;
            }

            // Set the headset on head and the headset remove time so we later update the CVR Stream Parameter
            if (MetaPort.Instance.isUsingVr) {
                _streamParamHeadsetOnHead = !_isAFK;
                _streamParamHeadsetRemoveTime = _headsetRemoveTime;
            }

            // Set the animator AFK property
            if (ModConfig.MeSetAnimatorParameterAFK.Value) {
                PlayerSetup.Instance.AnimatorManager.SetParameter(AFK, _isAFK);
            }

            // Set the animator AFKTimer property
            if (ModConfig.MeSetAnimatorParameterAFKTimer.Value) {
                var afkTimer = _isAFK ? Time.time - _headsetRemoveTime : -1f;
                PlayerSetup.Instance.AnimatorManager.SetParameter(AFKTimer, afkTimer);
            }
        }
        catch (Exception e) {
            MelonLogger.Error($"Error during the patched function {nameof(BetterAFK)}.{nameof(OnUpdate)}");
            MelonLogger.Error(e);
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRParameterStreamEntry), nameof(CVRParameterStreamEntry.CheckUpdate))]
        public static void After_CVRParameterStreamEntry_CheckUpdate(CVRParameterStreamEntry __instance, CVRParameterStream stream) {

            try {
                var num1 = 0.0f;

                if ((stream.referenceType == CVRParameterStream.ReferenceType.Avatar && stream.avatar == null) ||
                    stream.referenceType == CVRParameterStream.ReferenceType.Spawnable && stream.spawnable == null) return;

                switch (__instance.type) {
                    case CVRParameterStreamEntry.Type.HeadsetOnHead:
                        if (IsUsingOpenVR()) {
                            num1 = _streamParamHeadsetOnHead ? 1f : 0f;
                        }
                        else return;
                        break;
                    case CVRParameterStreamEntry.Type.TimeSinceHeadsetRemoved:
                        if (IsUsingOpenVR()) {
                            num1 = _streamParamHeadsetRemoveTime;
                        }
                        else return;
                        break;
                    default:
                        return;
                }

                var num12 = 0.0f;
                if (__instance.targetType == CVRParameterStreamEntry.TargetType.AvatarAnimator) {
                    num12 = PlayerSetup.Instance.GetAnimatorParam(__instance.parameterName);
                }

                var num13 = 0.0f;
                switch (__instance.applicationType) {
                    case CVRParameterStreamEntry.ApplicationType.Override:
                        num13 = num1;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.AddToCurrent:
                        num13 = num12 + num1;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.AddToStatic:
                        num13 = __instance.staticValue + num1;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.SubtractFromCurrent:
                        num13 = num12 - num1;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.SubtractFromStatic:
                        num13 = __instance.staticValue - num1;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.SubtractWithCurrent:
                        num13 = num1 - num12;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.SubtractWithStatic:
                        num13 = num1 - __instance.staticValue;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.MultiplyWithCurrent:
                        num13 = num1 * num12;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.MultiplyWithStatic:
                        num13 = num1 * __instance.staticValue;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.CompareLessThen:
                        num13 = num1 < __instance.staticValue ? 1f : 0.0f;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.CompareLessThenEquals:
                        num13 = num1 <= __instance.staticValue ? 1f : 0.0f;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.CompareEquals:
                        num13 = num1 == __instance.staticValue ? 1f : 0.0f;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.CompareMoreThenEquals:
                        num13 = num1 >= __instance.staticValue ? 1f : 0.0f;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.CompareMoreThen:
                        num13 = num1 > __instance.staticValue ? 1f : 0.0f;
                        break;
                    case CVRParameterStreamEntry.ApplicationType.Mod:
                        num13 = num1 % Mathf.Max(Mathf.Abs(__instance.staticValue), 0.0001f);
                        break;
                    case CVRParameterStreamEntry.ApplicationType.Pow:
                        num13 = Mathf.Pow(num1, __instance.staticValue);
                        break;
                }

                switch (__instance.targetType) {
                    case CVRParameterStreamEntry.TargetType.Animator:
                        if (__instance.target == null) break;
                        var component1 = __instance.target.GetComponent<Animator>();
                        if (component1 == null || !component1.enabled ||
                            component1.runtimeAnimatorController == null ||
                            component1.IsParameterControlledByCurve(__instance.parameterName))
                            break;
                        component1.SetFloat(__instance.parameterName, num13);
                        break;
                    case CVRParameterStreamEntry.TargetType.VariableBuffer:
                        if (__instance.target == null)
                            break;
                        var component2 = __instance.target.GetComponent<CVRVariableBuffer>();
                        if (component2 == null)
                            break;
                        component2.SetValue(num13);
                        break;
                    case CVRParameterStreamEntry.TargetType.AvatarAnimator:
                        if (stream.avatar == null || stream.avatar != PlayerSetup.Instance.AvatarDescriptor)
                            break;
                        PlayerSetup.Instance.ChangeAnimatorParam(__instance.parameterName, num13);
                        break;
                    case CVRParameterStreamEntry.TargetType.CustomFloat:
                        if (stream.spawnable == null) break;
                        var index = stream.spawnable.syncValues.FindIndex(match => match.name == __instance.parameterName);
                        if (index < 0) break;
                        stream.spawnable.SetValue(index, num13);
                        break;
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRParameterStreamEntry_CheckUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }
}
