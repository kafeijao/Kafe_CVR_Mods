using ABI_RC.Core.InteractionSystem;
using HarmonyLib;
using Kafe.CCK.Debugger.Components;
using Kafe.CCK.Debugger.Properties;

namespace Kafe.CCK.Debugger.Integrations;

public static class BTKUILibIntegration {

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += LoadBTKUILib;
    }

    private static void LoadBTKUILib(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= LoadBTKUILib;

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(AssemblyInfoParams.Name);

        var pinButtonBTKUI = miscCategory.AddButton("Pin To Quick Menu", "",
            "Pins the Menu back to quick menu. Useful if you lost your menu :)");

        pinButtonBTKUI.OnPress += Core.PinToQuickMenu;

        var hideMenuToggle = miscCategory.AddToggle("Hide the Menu",
            "Whether to completely hide the CCK Debugger Menu or not.",
            ModConfig.MeIsHidden.Value);

        hideMenuToggle.OnValueUpdated += b => {
            if (b != ModConfig.MeIsHidden.Value) ModConfig.MeIsHidden.Value = b;
        };

        ModConfig.MeIsHidden.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != hideMenuToggle.ToggleValue) hideMenuToggle.ToggleValue = newValue;
        });
    }

}
