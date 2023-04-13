using MelonLoader;

namespace Kafe.BetterAFK;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeAfkWhileSteamOverlay;
    internal static MelonPreferences_Entry<bool> MeUseEndKeyToToggleAFK;
    internal static MelonPreferences_Entry<bool> MeSetAnimatorParameterAFK;
    internal static MelonPreferences_Entry<bool> MeSetAnimatorParameterAFKTimer;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterAFK));

        MeAfkWhileSteamOverlay = _melonCategory.CreateEntry("AfkWhileSteamOverlay", true,
            description: "Whether to mark as AFK while Steam Overlay is opened or not.");

        MeUseEndKeyToToggleAFK = _melonCategory.CreateEntry("UseEndKeyToToggleAFK", true,
            description: "Whether to allow pressing the END key to override the AFK State or not.");
        MeUseEndKeyToToggleAFK.OnEntryValueChanged.Subscribe((_, _) => BetterAFK.IsEndKeyOverridingAFK = false);

        MeSetAnimatorParameterAFK = _melonCategory.CreateEntry("SetAnimatorParameterAFK", true,
            description: "Whether to attempt to set the bool parameter AFK when AFK or not.");

        MeSetAnimatorParameterAFKTimer = _melonCategory.CreateEntry("SetAnimatorParameterAFKTimer", true,
            description: "Whether to attempt to set the int or float parameter AFKTimer with the time you've been afk for in seconds or not.");

    }

}
