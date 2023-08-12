using Kafe.LoggerPlusPlus.Properties;
using MelonLoader;

namespace Kafe.LoggerPlusPlus;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeShowUnknownInfo;
    internal static MelonPreferences_Entry<bool> MeShowUnknownWarning;
    internal static MelonPreferences_Entry<bool> MeShowUnknownError;
    internal static MelonPreferences_Entry<bool> MeShowStackTraces;

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

    internal static MelonPreferences_Entry<bool> MeFullStackForErrors;
    internal static MelonPreferences_Entry<bool> MeFullStackForExceptions;
    internal static MelonPreferences_Entry<bool> MeFullStackForAsserts;
    internal static MelonPreferences_Entry<bool> MeFullStackForWarnings;
    internal static MelonPreferences_Entry<bool> MeFullStackForLogs;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(AssemblyInfoParams.Name);

        MeShowUnknownInfo = _melonCategory.CreateEntry("ShowUnknownInfo", true,
            description: "Whether only show Unknown Info logs or not.");

        MeShowUnknownWarning = _melonCategory.CreateEntry("ShowUnknownWarning", true,
            description: "Whether only show Unknown Warning logs or not.");

        MeShowUnknownError = _melonCategory.CreateEntry("ShowUnknownError", true,
            description: "Whether only show Unknown Error logs or not.");

        MeShowStackTraces = _melonCategory.CreateEntry("ShowStackTraces", true,
            description: "Whether only show Stack Traces logs or not.");

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

        MeFullStackForErrors = _melonCategory.CreateEntry("FullStackForErrors", false,
            description: "Whether the Error logs will include full stack traces or not.");

        MeFullStackForExceptions = _melonCategory.CreateEntry("FullStackForExceptions", false,
            description: "Whether the Exceptions logs will include full stack traces or not.");

        MeFullStackForAsserts = _melonCategory.CreateEntry("FullStackForAsserts", false,
            description: "Whether the Asserts logs will include full stack traces or not.");

        MeFullStackForWarnings = _melonCategory.CreateEntry("FullStackForWarnings", false,
            description: "Whether the Warnings logs will include full stack traces or not.");

        MeFullStackForLogs = _melonCategory.CreateEntry("FullStackForLogs", false,
            description: "Whether the Info logs will include full stack traces or not.");
    }

}
