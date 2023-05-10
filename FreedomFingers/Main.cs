using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.IK;
using HarmonyLib;
using MelonLoader;

namespace Kafe.FreedomFingers;

public class FreedomFingers : MelonMod {

    private static MelonPreferences_Category _melonCategoryFreedomFingers;
    private static MelonPreferences_Entry<bool> _melonEntryEnableNotification;
    private static MelonPreferences_Entry<bool> _melonEntryStartWithGesturesEnabled;

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategoryFreedomFingers = MelonPreferences.CreateCategory(nameof(FreedomFingers));

        _melonEntryEnableNotification = _melonCategoryFreedomFingers.CreateEntry("EnableNotifications", false,
            description: "Whether the mod should send notifications when toggling gestures.");

        _melonEntryStartWithGesturesEnabled = _melonCategoryFreedomFingers.CreateEntry("StartWithGesturesEnabled", true,
            description: "Whether the gestures start enabled or disabled when starting the game.");
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

	    private static bool _isDefaultGestureSet;

	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.SetFingerTracking))]
	    private static void After_PlayerSetup_SetFingerTracking(bool status) {

		    // On the first time ran, set the gesture toggle to our default value
		    if (!_isDefaultGestureSet) {
			    InputModuleSteamVR.Instance._steamVrIndexGestureToggleValue = _melonEntryStartWithGesturesEnabled.Value;
				_isDefaultGestureSet = true;
		    }

		    // Keep finger tracking always active
		    CVRInputManager.Instance.individualFingerTracking = true;
		    IKSystem.Instance.FingerSystem.controlActive = true;

		    // Send the notification when toggling gestures
		    if (_melonEntryEnableNotification.Value && CohtmlHud.Instance != null) {
			    CohtmlHud.Instance.ViewDropTextImmediate("", "", $"Gestures {(!status ? "Enabled" : "Disabled")}");
		    }
	    }

	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(InputModuleSteamVR), nameof(InputModuleSteamVR.Start))]
	    private static void After_InputModuleSteamVR_Start(InputModuleSteamVR __instance) {

			// Prevent the setting from working, we want to have finger tracking always on
		    __instance._gestureAnimationsDuringFingerTracking = false;
	    }

	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(InputModuleSteamVR), nameof(InputModuleSteamVR.SettingsBoolChanged))]
	    private static void After_InputModuleSteamVR_SettingBoolChanged(InputModuleSteamVR __instance, string name) {

		    // Prevent the setting from working, we want to have finger tracking always on
		    if (name != "ControlEnableGesturesWhileFingerTracking") return;
		    __instance._gestureAnimationsDuringFingerTracking = false;
	    }

	    [HarmonyPrefix]
	    [HarmonyPatch(typeof(CohtmlHud), nameof(CohtmlHud.ViewDropTextImmediate))]
	    private static bool After_PlayerSetup_SetFingerTracking(string cat, string headline, string small) {

		    // Skip the execution of the message if it's the Skeletal Input changed.
		    return headline != "Skeletal Input changed ";
	    }
    }
}
