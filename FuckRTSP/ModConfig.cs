using MelonLoader;

namespace Kafe.FuckRTSP;

internal static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeEnabled;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(FuckRTSP));

        MeEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether we should ignore RTSP videos or not.");

    }

}
