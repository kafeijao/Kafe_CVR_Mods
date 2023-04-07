using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using Valve.VR;

namespace Kafe.QuickMenuAccessibility;

public static class CompatibilityHacksActionMenu {

    public static void Initialize(HarmonyLib.Harmony harmonyInstance, MelonMod possibleActionMenu) {

        var actionMenu = (ActionMenu.ActionMenuMod) possibleActionMenu;
        var isCompatibleVersion = actionMenu.Info.Version == "1.0.4";
        if (isCompatibleVersion) {
            MelonLogger.Msg($"[Compatibility] Detected ActionMenu mod version {actionMenu.Info.Version}. Integrating...");
        }
        else {
            MelonLogger.Warning($"[Compatibility] Detected ActionMenu mod version {actionMenu.Info.Version}." +
                                $"This version integration is untested, you might run into errors/strange behavior with the quick menu.");
        }

        // Manually patch the add mirror, since we don't want to do it if the mod is not present
        harmonyInstance.Patch(
            AccessTools.Method(typeof(ActionMenu.ActionMenuMod), "OnUpdateInputSteamVR"),
            new HarmonyMethod(AccessTools.Method(typeof(CompatibilityHacksActionMenu), nameof(OnUpdateInputSteamVR)))
        );
    }

    private static bool OnUpdateInputSteamVR(InputModuleSteamVR __instance) {
        if (__instance == null || __instance.vrMenuButton == null) return false;
        //ActionMenuMod.instance.OnUpdateInput(__instance.vrMenuButton.GetStateDown(SteamVR_Input_Sources.LeftHand), __instance.vrMenuButton.GetStateUp(SteamVR_Input_Sources.LeftHand));
        var quickButtonCache = __instance._inputManager.quickMenuButton;
        QuickMenuAccessibility._shouldSkipQuickMenu = true;
        var actionMenu = Traverse.Create(typeof(ActionMenu.ActionMenuMod)).Field<ActionMenu.ActionMenuMod>("instance");
        Traverse.Create(actionMenu).Method("OnUpdateInput").GetValue(
            __instance.vrMenuButton.GetStateDown(QuickMenuAccessibility.ApplyAndGetButtonConfig(SteamVR_Input_Sources.LeftHand)),
            __instance.vrMenuButton.GetStateUp(QuickMenuAccessibility.ApplyAndGetButtonConfig(SteamVR_Input_Sources.LeftHand)));
        __instance._inputManager.quickMenuButton = quickButtonCache;
        QuickMenuAccessibility._shouldSkipQuickMenu = false;
        return false;
    }

}
