using MelonLoader;

namespace Kafe.WrongMic;

public static class ModConfig {

    internal const string Undefined = $"N/A - {nameof(WrongMic)}";

    // Melon Prefs
    internal static MelonPreferences_Category MelonCategory;
    internal static MelonPreferences_Entry<string> MeMicVR;
    internal static MelonPreferences_Entry<string> MeMicDesktop;

    public static void InitializeMelonPrefs() {

        // Melon Config
        MelonCategory = MelonPreferences.CreateCategory(nameof(WrongMic));
        MelonCategory.IsHidden = true;

        MeMicVR = MelonCategory.CreateEntry("MicVR", Undefined,
            description: "Microphone to be used in VR. This should be set automatically don't change...");

        MeMicDesktop = MelonCategory.CreateEntry("MicDesktop", Undefined,
            description: "Microphone to be used in Desktop. This should be set automatically don't change...");
    }
}
