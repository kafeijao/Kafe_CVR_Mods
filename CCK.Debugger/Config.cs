using ABI_RC.Core.InteractionSystem;
using Kafe.CCK.Debugger.Components;
using Kafe.CCK.Debugger.Properties;
using MelonLoader;

namespace Kafe.CCK.Debugger;

public static class Config {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeIsHidden;
    internal static MelonPreferences_Entry<bool> MeOverwriteUIResources;

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(AssemblyInfoParams.Name);

        MeOverwriteUIResources = _melonCategory.CreateEntry("OverwriteUIResources", true,
            description: "Whether the mod should overwrite all Cohtml UI resources when loading or not.");

        MeIsHidden = _melonCategory.CreateEntry("Hidden", false,
            description: "Whether to hide completely the CCK Debugger menu or not.");

        MeIsHidden.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            // Update whether the menu is visible or not
            CohtmlMenuController.Instance.UpdateMenuState();
            Core.PinToQuickMenu();
        });
    }

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
            MeIsHidden.Value);

        hideMenuToggle.OnValueUpdated += b => {
            if (b != MeIsHidden.Value) MeIsHidden.Value = b;
        };

        MeIsHidden.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != hideMenuToggle.ToggleValue) hideMenuToggle.ToggleValue = newValue;
        });
    }

}
