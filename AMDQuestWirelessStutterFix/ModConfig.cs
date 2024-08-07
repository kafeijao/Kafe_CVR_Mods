using MelonLoader;

namespace Kafe.AMDQuestWirelessStutterFix;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<FixMode> MeFixMode;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(AMDQuestWirelessStutterFix));

        MeFixMode = _melonCategory.CreateEntry("FixMode", FixMode.HDR,
            description: "Disabled - The mod is disabled; " +
                         "SDR - The mod is enabled with grading mode SDR; " +
                         "HDR - The mod is enabled with grading mod HDR (default)");
        MeFixMode.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            AMDQuestWirelessStutterFix.AndApplyChanges();
        });
    }
}
