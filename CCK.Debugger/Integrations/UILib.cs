using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.UI.UILib;
using Kafe.CCK.Debugger.Components;
using Kafe.CCK.Debugger.Properties;

namespace Kafe.CCK.Debugger.Integrations;

public static class UILib {

    public static void InitializeUILib() {
        QuickMenuAPI.OnMenuRegenerate += LoadUILib;
    }

    private static void LoadUILib(CVR_MenuManager manager) {
        QuickMenuAPI.OnMenuRegenerate -= LoadUILib;

        var miscPage = QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(AssemblyInfoParams.Name);

        var pinButtonUI = miscCategory.AddButton("Pin To Quick Menu", "",
            "Pins the Menu back to quick menu. Useful if you lost your menu :)");

        pinButtonUI.OnPress += Core.PinToQuickMenu;

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
