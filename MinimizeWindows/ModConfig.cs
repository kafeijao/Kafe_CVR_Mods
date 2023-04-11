using MelonLoader;

namespace Kafe.MinimizeWindows;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeMinimizeGameWindowInDesktop;
    internal static MelonPreferences_Entry<bool> MeMinimizeMelonConsoleWindowInDesktop;
    internal static MelonPreferences_Entry<bool> MeMinimizeGameWindowInVR;
    internal static MelonPreferences_Entry<bool> MeMinimizeMelonConsoleWindowInVR;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(MinimizeWindows));

        MeMinimizeGameWindowInDesktop = _melonCategory.CreateEntry("MinimizeGameWindowInDesktop", false,
            description: "Whether to minimize the Game Window when starting the game in Desktop Mode or not.");

        MeMinimizeMelonConsoleWindowInDesktop = _melonCategory.CreateEntry("MinimizeMelonConsoleWindowInDesktop", false,
            description: "Whether to minimize the Melon Console Window when starting the game in Desktop Mode or not.");

        MeMinimizeGameWindowInVR = _melonCategory.CreateEntry("MinimizeGameWindowInVR", true,
            description: "Whether to minimize the Game Window when starting the game in VR Mode or not.");

        MeMinimizeMelonConsoleWindowInVR = _melonCategory.CreateEntry("MinimizeMelonConsoleWindowInVR", false,
            description: "Whether to minimize the Melon Console Window when starting the game in VR Mode or not.");

    }

}
