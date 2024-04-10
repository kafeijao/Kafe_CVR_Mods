using MelonLoader;

namespace Kafe.SteamAudioOverrides;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeForceAddSteamAudioSources;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(SteamAudioOverrides));

        MeForceAddSteamAudioSources = _melonCategory.CreateEntry("ForceAddSteamAudioSources", true,
            description: "Whether to force add Steam Audio Sources to all audio sources or not..");
    }

}
