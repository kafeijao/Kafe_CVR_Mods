using ABI_RC.Core.InteractionSystem;
using MelonLoader;

namespace Kafe.QuickMenuAccessibility;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeSwapQuickMenuHands;
    internal static MelonPreferences_Entry<bool> MeSwampQuickMenuButton;
    internal static MelonPreferences_Entry<bool> MeDropQuickMenuInWorld;
    internal static MelonPreferences_Entry<bool> MeSingleButton;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(QuickMenuAccessibility));

        MeSwapQuickMenuHands = _melonCategory.CreateEntry("SwapQuickMenuHands", true,
            description: "Whether swap the quick menu hand or not.");

        MeSwampQuickMenuButton = _melonCategory.CreateEntry("SwampQuickMenuButton", true,
            description: "Whether to swap the big menu and quick menu open buttons.");

        MeDropQuickMenuInWorld = _melonCategory.CreateEntry("DropQuickMenuInWorld", false,
            description: "Whether to drop the quick menu in world space when opened or not.");

        MeSingleButton = _melonCategory.CreateEntry("SingleButton", false,
            description: "Whether to allow using the same button to toggle between QM and Main Menu or not.");
    }

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var cat = miscPage.AddCategory(nameof(QuickMenuAccessibility));

        var joinLastInstanceButton = cat.AddToggle("Swap Quick Menu Hands",
            "Swaps the hand the Quick Menu is attached to.",
            MeSwapQuickMenuHands.Value);
        joinLastInstanceButton.OnValueUpdated += b => {
            if (b == MeSwapQuickMenuHands.Value) return;
            MeSwapQuickMenuHands.Value = b;
        };
        MeSwapQuickMenuHands.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (joinLastInstanceButton.ToggleValue == newValue) return;
            joinLastInstanceButton.ToggleValue = newValue;
        });

        var swapQuickMenuButton = cat.AddToggle("Swap Quick Menu Button",
            "Swaps the button to open the Quick Menu and the Big Menu.",
            MeSwampQuickMenuButton.Value);
        swapQuickMenuButton.OnValueUpdated += b => {
            if (b == MeSwampQuickMenuButton.Value) return;
            MeSwampQuickMenuButton.Value = b;
        };
        MeSwampQuickMenuButton.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (swapQuickMenuButton.ToggleValue == newValue) return;
            swapQuickMenuButton.ToggleValue = newValue;
        });

        var dropQuickMenuInWorld = cat.AddToggle("Freeze Quick Menu in Place",
            "When opened, sticks the Quick Menu in World Space. This allows for one hand control.",
            MeDropQuickMenuInWorld.Value);
        dropQuickMenuInWorld.OnValueUpdated += b => {
            if (b == MeDropQuickMenuInWorld.Value) return;
            MeDropQuickMenuInWorld.Value = b;
        };
        MeDropQuickMenuInWorld.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (dropQuickMenuInWorld.ToggleValue == newValue) return;
            dropQuickMenuInWorld.ToggleValue = newValue;
        });

        var singleMenuButton = cat.AddToggle("2 Menus 1 Button",
            "Cycle thought both Quick Menu and Main Menu using a single button.",
            MeSingleButton.Value);
        singleMenuButton.OnValueUpdated += b => {
            if (b == MeSingleButton.Value) return;
            MeSingleButton.Value = b;
        };
        MeSingleButton.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (singleMenuButton.ToggleValue == newValue) return;
            singleMenuButton.ToggleValue = newValue;
        });
    }

}
