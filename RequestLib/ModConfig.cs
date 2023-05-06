using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.RequestLib;

internal static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeOnlyReceiveFromFriends;

    // Embed resources
    internal static string CVRTestJSPatchesContent;
    private const string CVRTestJSPatches = "cohtml.cvrtest.patches.js";
    internal static string CVRUIJSPatchesContent;
    private const string CVRUIJSPatches = "cohtml.cvrui.patches.js";
    
    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(RequestLib));

        MeOnlyReceiveFromFriends = _melonCategory.CreateEntry("OnlyReceiveFromFriends", false,
            description: "Whether only receive requests from friends or not.");

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
        var miscCategory = miscPage.AddCategory(nameof(RequestLib));

        var modPage = miscCategory.AddPage($"{nameof(RequestLib)} Settings", "", $"Configure the settings for {nameof(RequestLib)}.", nameof(RequestLib));
        modPage.MenuTitle = $"{nameof(RequestLib)} Settings";

        var modSettingsCategory = modPage.AddCategory("Settings");

        AddMelonToggle(modSettingsCategory, MeOnlyReceiveFromFriends);
    }

     public static void LoadAssemblyResources(Assembly assembly) {

        try {
            using var resourceStream = assembly.GetManifestResourceStream(CVRTestJSPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {CVRTestJSPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            CVRTestJSPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the resource: " + ex.Message);
        }

        try {
            using var resourceStream = assembly.GetManifestResourceStream(CVRUIJSPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {CVRUIJSPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            CVRUIJSPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the resource: " + ex.Message);
        }

    }

}
