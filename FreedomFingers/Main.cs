using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.InputModules;
using ABI_RC.Systems.InputManagement.XR;
using HarmonyLib;
using MelonLoader;
using Valve.VR;

namespace Kafe.FreedomFingers;

public class FreedomFingers : MelonMod {

	private static MelonPreferences_Category _melonCategoryFreedomFingers;
    private static MelonPreferences_Entry<bool> _melonEntryEnableNotification;

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategoryFreedomFingers = MelonPreferences.CreateCategory(nameof(FreedomFingers));

        _melonEntryEnableNotification = _melonCategoryFreedomFingers.CreateEntry("EnableNotifications", false,
            description: "Whether the mod should send notifications when toggling gestures.");
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

	    private static readonly SteamVR_Action_Boolean GestureToggleAction = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("ControllerToggleGestures");

	    private static float _lockedLeftGesture;
		private static float _lockedRightGesture;
	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.Update_Emotes))]
	    private static void After_CVRInputModule_XR_Update_Emotes(CVRInputModule_XR __instance) {

		    // Undo the default clicking of the button. We have our own detection (so you can change the keybind in steamvr)
		    if (__instance._leftModule.Type == EXRControllerType.Index && (!__instance.firstFindIndex ||
		                                                                   !__instance._inputManager.oneHanded &&
		                                                                   __instance._leftModule.PrimaryButton)) {
			    // Revert the toggling
				Traverse.Create(__instance).Property<bool>(nameof(__instance.GestureToggleValue)).Value = !__instance.GestureToggleValue;

			    __instance._inputManager.individualFingerTracking = true;
		    }

		    // if (controllerModule.Type == EXRControllerType.Index && (!__instance.firstFindIndex || !__instance._inputManager.oneHanded && __instance._leftModule.PrimaryButton)) {
		    if ((__instance._leftModule.Type == EXRControllerType.Index || __instance._rightModule.Type == EXRControllerType.Index) && GestureToggleAction.lastStateDown) {

			    // Since we prevented the toggling, we now need to do it
				Traverse.Create(__instance).Property<bool>(nameof(__instance.GestureToggleValue)).Value = !__instance.GestureToggleValue;

				// Actual toggle (since we undid the default)
			    __instance._inputManager.individualFingerTracking = true;

			    // Send the notification when toggling gestures
			    if (_melonEntryEnableNotification.Value && CohtmlHud.Instance != null) {
				    CohtmlHud.Instance.ViewDropTextImmediate("", "", $"Gestures {(__instance.GestureToggleValue ? "Enabled" : "Disabled")}");
			    }

			    // Since the Gesture is reset every frame, we need to set it to the one we're doing when locked
			    if (!__instance.GestureToggleValue) {
				    _lockedLeftGesture = __instance._leftModule.GestureRaw;
				    _lockedRightGesture = __instance._rightModule.GestureRaw;

				    // Lock on the same frame we triggered (on next frames we will use Update_Gestures_Index patch)
				    __instance._inputManager.gestureLeft = _lockedLeftGesture;
				    __instance._inputManager.gestureRight = _lockedRightGesture;
			    }
		    }
	    }

	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(CVRXRModule), nameof(CVRXRModule.Update_Gestures_Index))]
	    private static void After_CVRXRModule_Update_Gestures_Index(CVRXRModule __instance) {
		    if (!CVRInputManager._moduleXR.GestureToggleValue) {
			    // Use the locked gesture
			    __instance.Gesture = __instance.IsLeftHand ? _lockedLeftGesture : _lockedRightGesture;
		    }
	    }

	    private const string GestureAnimationsDuringFingerTrackingSetting = "GestureAnimationsDuringFingerTracking";

	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(CVRInputModule_XR), nameof(CVRInputModule_XR.ModuleAdded))]
	    private static void After_CVRInputModule_XR_ModuleAdded(CVRInputModule_XR __instance) {
		    // Prevent the setting from working, we want to have finger tracking always on
		    // As time of implementing, this was the only place it was set, we might need to change later
		    var gestureAnimationsDuringFingerTracking = Traverse.Create(__instance).Property<bool>(GestureAnimationsDuringFingerTrackingSetting);
		    gestureAnimationsDuringFingerTracking.Value = false;
		    //___GestureAnimationsDuringFingerTracking = true;

		    // Also handle the changes (since we're adding our listener after it should run after)
		    MetaPort.Instance.settings.settingBoolChanged.AddListener( (name, value) => {
			    if (name == GestureAnimationsDuringFingerTrackingSetting) {
				    gestureAnimationsDuringFingerTracking.Value = false;
			    }
		    });
	    }

	    [HarmonyPrefix]
	    [HarmonyPatch(typeof(CohtmlHud), nameof(CohtmlHud.ViewDropTextImmediate))]
	    private static bool After_PlayerSetup_SetFingerTracking(string cat, string headline, string small) {
		    // Skip the execution of the message if it's the Skeletal Input changed.
		    return headline != "Skeletal Input changed ";
	    }
    }
}
