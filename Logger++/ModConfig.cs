using Kafe.LoggerPlusPlus.Properties;
using MelonLoader;

namespace Kafe.LoggerPlusPlus;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeShowCVRInfo;
    internal static MelonPreferences_Entry<bool> MeShowCVRWarning;
    internal static MelonPreferences_Entry<bool> MeShowCVRError;

    internal static MelonPreferences_Entry<bool> MeShowCohtmlInfo;
    internal static MelonPreferences_Entry<bool> MeShowCohtmlWarning;
    internal static MelonPreferences_Entry<bool> MeShowCohtmlError;

    internal static MelonPreferences_Entry<bool> MeShowMissingScripts;

    internal static MelonPreferences_Entry<bool> MeShowAvPro;

    internal static MelonPreferences_Entry<bool> MeShowSpamMessages;
    internal static MelonPreferences_Entry<bool> MeShowUselessMessages;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(AssemblyInfoParams.Name);

        MeShowCVRInfo = _melonCategory.CreateEntry("ShowCVRInfo", false,
            description: "Whether only show CVR Game Info logs or not.");

        MeShowCVRWarning = _melonCategory.CreateEntry("ShowCVRWarning", false,
            description: "Whether only show CVR Game Warning logs or not.");

        MeShowCVRError = _melonCategory.CreateEntry("ShowCVRError", true,
            description: "Whether only show CVR Game Error logs or not.");

        MeShowCohtmlInfo = _melonCategory.CreateEntry("ShowCohtmlInfo", false,
            description: "Whether only show Cohtml Info logs or not.");

        MeShowCohtmlWarning = _melonCategory.CreateEntry("ShowCohtmlWarning", false,
            description: "Whether only show Cohtml Warning logs or not.");

        MeShowCohtmlError = _melonCategory.CreateEntry("ShowCohtmlError", false,
            description: "Whether only show Cohtml Error logs or not.");

        MeShowMissingScripts = _melonCategory.CreateEntry("ShowMissingScripts", false,
            description: "Whether only show missing script logs or not.");

        MeShowAvPro = _melonCategory.CreateEntry("ShowAvPro", false,
            description: "Whether only show AV Pro logs or not.");

        MeShowSpamMessages = _melonCategory.CreateEntry("ShowSpamMessages", false,
            description: "Whether only show known spam-y logs or not.");

        MeShowUselessMessages = _melonCategory.CreateEntry("ShowUselessMessages", false,
            description: "Whether only show known useless logs or not.");
    }

}
