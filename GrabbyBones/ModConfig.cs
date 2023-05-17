using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.GrabbyBones;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeEnabled;
    internal static MelonPreferences_Entry<bool> MeOnlyFriends;
    internal static MelonPreferences_Entry<int> MeMaxPlayerDistance;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(GrabbyBones));

        MeEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether the mod is enabled or not.");

        MeOnlyFriends = _melonCategory.CreateEntry("OnlyFriends", false,
            description: "Whether to only calculate grabbing for friends or not.");

        MeMaxPlayerDistance = _melonCategory.CreateEntry("MaxPlayerDistance", 15,
            description: "Max distance from us to the player in order to see them grabbing bones. Set 0 for unlimited.");
    }

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void AddMelonToggle(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<bool> entry) {
        var toggle = category.AddToggle(entry.DisplayName, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => {
            if (b != entry.Value) entry.Value = b;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue != toggle.ToggleValue) toggle.ToggleValue = newValue;
        });
    }

    private static void AddMelonSlider(BTKUILib.UIObjects.Page page, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces) {
        var slider = page.AddSlider(entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(GrabbyBones));

        AddMelonToggle(miscCategory, MeEnabled);
        AddMelonToggle(miscCategory, MeOnlyFriends);
    }

}
