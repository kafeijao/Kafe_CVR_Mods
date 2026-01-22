using MelonLoader;

namespace Kafe.YoutubeFixSABR;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static bool Enabled = true;

    public static bool UseNightly = true;

    public static bool UseCustomArgs;

    public static List<string> CustomArgsToRemove;

    public static List<string> CustomArgsToAdd;

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(YoutubeFixSABR));

        MelonPreferences_Entry<bool> enabledEntry = _melonCategory.CreateEntry(nameof(Enabled), true, "Enabled");
        enabledEntry.OnEntryValueChanged.Subscribe((_, newValue) => Enabled = newValue);
        Enabled = enabledEntry.Value;

        MelonPreferences_Entry<bool> useNightlyEntry = _melonCategory.CreateEntry(nameof(UseNightly), true, "Use Nightly yt-dlp");
        useNightlyEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            UseNightly = newValue;
            YoutubeFixSABR.UpdateYoutubeDlLinks();
            MelonLogger.Warning("ty-dlp is checked/downloaded on authentication, so you need to restart the game for this changes to happen.\n" +
                            "I didn't want this toggle to trigger the download since it can be easily spammed, and it uses github's api");
        });
        UseNightly = useNightlyEntry.Value;

        MelonPreferences_Entry<bool> useCustomArgsEntry = _melonCategory.CreateEntry(nameof(UseCustomArgs) + "v2", false, "Use Custom Args",
            "If this is set to true, it will use the Custom Args to Add and Custom Args to Remove instead of the defaults");
        useCustomArgsEntry.OnEntryValueChanged.Subscribe((_, newValue) => UseCustomArgs = newValue);
        UseCustomArgs = useCustomArgsEntry.Value;

        MelonPreferences_Entry<List<string>> customArgsToRemoveEntry = _melonCategory.CreateEntry(nameof(CustomArgsToRemove) + "v2", YoutubeFixSABR.ArgsToRemove, "Custom Args to Remove");
        customArgsToRemoveEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            CustomArgsToRemove = newValue;
            MelonLogger.Msg($"Changed the Custom Args to remove to:\n{string.Join("\n", CustomArgsToRemove)}");
        });
        CustomArgsToRemove = customArgsToRemoveEntry.Value;

        MelonPreferences_Entry<List<string>> customArgsToAddEntry = _melonCategory.CreateEntry(nameof(CustomArgsToAdd) + "v2", YoutubeFixSABR.ArgsToAdd, "Custom Args to Add");
        customArgsToAddEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
        {
            CustomArgsToAdd = newValue;
            MelonLogger.Msg($"Changed the Custom Args to add to:\n{string.Join("\n", CustomArgsToAdd)}");
        });
        CustomArgsToAdd = customArgsToAddEntry.Value;

        MelonLogger.Msg($"Using the {(UseNightly ? "cvr default" : "nightly")} version of yt-dlp");
        if (UseCustomArgs)
        {
            MelonLogger.Msg("Using custom args");
            MelonLogger.Msg($"Args to Remove:\n- {string.Join("\n- ", CustomArgsToRemove)}");
            MelonLogger.Msg($"Args to Add:\n- {string.Join("\n- ", CustomArgsToAdd)}");
        }
        else
        {
            MelonLogger.Msg("Using default args");
        }
    }
}
