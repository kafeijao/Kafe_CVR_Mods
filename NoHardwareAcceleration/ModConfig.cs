using MelonLoader;

namespace Kafe.NoHardwareAcceleration;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeDisableHardwareAcceleration;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(NoHardwareAcceleration));

        MeDisableHardwareAcceleration = _melonCategory.CreateEntry("DisableHardwareAcceleration", true,
            description: "Whether to disable AV Pro Hardware acceleration or not. Only affects newly spawned Video Players.");
    }

}
