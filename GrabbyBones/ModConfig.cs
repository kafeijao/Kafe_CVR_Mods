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
    internal static MelonPreferences_Entry<bool> MePreventGrabIKBones;
    internal static MelonPreferences_Entry<bool> MeUseFistGestureToGrab;
    internal static MelonPreferences_Entry<bool> MeUseFingerCurlsToGrab;

    // Finger curls min values
    internal static MelonPreferences_Entry<float> MeThumbMinFingerCurl;
    internal static MelonPreferences_Entry<float> MeIndexMinFingerCurl;
    internal static MelonPreferences_Entry<float> MeMiddleMinFingerCurl;
    internal static MelonPreferences_Entry<float> MeRingMinFingerCurl;
    internal static MelonPreferences_Entry<float> MePinkyMinFingerCurl;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(GrabbyBones));

        MeEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether the mod is enabled or not.");

        MeOnlyFriends = _melonCategory.CreateEntry("OnlyFriends", false,
            description: "Whether to only calculate grabbing for friends or not.");

        MeMaxPlayerDistance = _melonCategory.CreateEntry("MaxPlayerDistance", 15,
            description: "Max distance from us to the player in order to see them grabbing bones. Set 0 for unlimited.");

        MePreventGrabIKBones = _melonCategory.CreateEntry("PreventGrabIKBones", false,
            description: "Whether to prevent grabbing IK bones (part of the animator) or not.");

        MeUseFistGestureToGrab = _melonCategory.CreateEntry("UseFistGestureToGrab", true,
            description: "Whether to use the fist gesture (gesture == 1) to detect grabbing or not. Defaults to true.");

        MeUseFingerCurlsToGrab = _melonCategory.CreateEntry("UseFingerCurlsToGrab", true,
            description: "Whether to use the hand finger curls to detect grabbing or not. Defaults to true.");

        MeThumbMinFingerCurl = _melonCategory.CreateEntry("ThumbMinFingerCurl", 0.5f,
            description: "The minimum thumb finger curl value to consider to be grabbing [0, 1]. " +
                         "Defaults to 0.5. Use 0 to not require this finger curl.");

        MeIndexMinFingerCurl = _melonCategory.CreateEntry("IndexMinFingerCurl", 0.5f,
            description: "The minimum index finger curl value to consider to be grabbing [0, 1]. " +
                         "Defaults to 0.5. Use 0 to not require this finger curl.");

        MeMiddleMinFingerCurl = _melonCategory.CreateEntry("MiddleMinFingerCurl", 0.5f,
            description: "The minimum middle finger curl value to consider to be grabbing [0, 1]. " +
                         "Defaults to 0.5. Use 0 to not require this finger curl.");

        MeRingMinFingerCurl = _melonCategory.CreateEntry("RingMinFingerCurl", 0.5f,
            description: "The minimum ring finger curl value to consider to be grabbing [0, 1]. " +
                         "Defaults to 0.5. Use 0 to not require this finger curl.");

        MePinkyMinFingerCurl = _melonCategory.CreateEntry("PinkyMinFingerCurl", 0.5f,
            description: "The minimum pinky finger curl value to consider to be grabbing [0, 1]. " +
                         "Defaults to 0.5. Use 0 to not require this finger curl.");
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

    private static void AddMelonSlider(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces) {
        var slider = category.AddSlider(entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
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
