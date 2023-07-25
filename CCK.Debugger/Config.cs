using Kafe.CCK.Debugger.Components;
using MelonLoader;

namespace Kafe.CCK.Debugger;

public static class Config {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeIsHidden;
    internal static MelonPreferences_Entry<bool> MeOverwriteUIResources;

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(nameof(CCKDebugger));

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
}
