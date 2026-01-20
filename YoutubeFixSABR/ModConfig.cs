using MelonLoader;

namespace Kafe.YoutubeFixSABR;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static bool Enabled = true;

    public static bool UseCustomArgs;

    public static List<string> ArgsToRemove = new List<string>
    {
        " --impersonate=Safari-15.3",
        " --extractor-arg \"youtube:player_client=web\"",
    };

    public static List<string> ArgsToAdd = new List<string>
    {
        $" --js-runtimes \"deno:{YoutubeFixSABR.DenoExePath}\"",
        " --extractor-args \"youtube:player-client=default,-web_safari\"",
    };

    // Note: These settings don't really matter since it seems CVR is ignoring the recommended formats
    // that means that whatever we put on -f parameters is ignored
    //
    // public static bool PreferWebM = true;
    // public static bool DisallowAV1 = true;
    // public static bool ForceDash = true;
    //
    // public static readonly Dictionary<int, bool> IgnoreFormats = new Dictionary<int, bool>();
    //
    // public static readonly int[] KnownFormatIds =
    // {
    //     330, 331, 332, 333, 334, 335, 336, 337,
    //     394, 395, 396, 397, 398, 399, 400, 401,
    //     964, 965, 966, 967, 968, 969, 970, 971
    // };

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(YoutubeFixSABR));

        _melonCategory.CreateEntry(nameof(Enabled), true, "Enabled")
            .OnEntryValueChanged.Subscribe((_, newValue) => Enabled = newValue);

        _melonCategory.CreateEntry(nameof(UseCustomArgs), false, "Use Custom Args",
                "If this is set to true, it will use the Custom Args to Add and Custom Args to Remove instead of the defaults")
            .OnEntryValueChanged.Subscribe((_, newValue) => UseCustomArgs = newValue);

        _melonCategory.CreateEntry(nameof(ArgsToRemove), ArgsToRemove, "Custom Args to Remove")
            .OnEntryValueChanged.Subscribe((_, newValue) =>
            {
                ArgsToRemove = newValue;
                MelonLogger.Msg($"Changed the Custom Args to remove to:\n{string.Join("\n", ArgsToRemove)}");
            });

        _melonCategory.CreateEntry(nameof(ArgsToAdd), ArgsToAdd, "Custom Args to Add")
            .OnEntryValueChanged.Subscribe((_, newValue) =>
            {
                ArgsToAdd = newValue;
                MelonLogger.Msg($"Changed the Custom Args to add to:\n{string.Join("\n", ArgsToAdd)}");
            });

        if (UseCustomArgs)
        {
            MelonLogger.Msg("Using custom args");
            MelonLogger.Msg($"Args to Remove:\n- {string.Join("\n- ", ArgsToRemove)}");
            MelonLogger.Msg($"Args to Add:\n- {string.Join("\n- ", ArgsToAdd)}");
        }
        else
        {
            MelonLogger.Msg("Using default args");
        }

        // _melonCategory.CreateEntry(nameof(PreferWebM), true, "Prefer WebM Codec")
        //     .OnEntryValueChanged.Subscribe((_, newValue) => PreferWebM = newValue);
        //
        // _melonCategory.CreateEntry(nameof(DisallowAV1), true, "Disallow AV1 Codec")
        //     .OnEntryValueChanged.Subscribe((_, newValue) => DisallowAV1 = newValue);
        //
        // _melonCategory.CreateEntry(nameof(ForceDash), true, "Force Dash")
        //     .OnEntryValueChanged.Subscribe((_, newValue) => ForceDash = newValue);
        //
        // foreach (int id in KnownFormatIds)
        // {
        //     string entryName = $"IgnoreFormat{id}";
        //     MelonPreferences_Entry<bool> entry = _melonCategory.CreateEntry(
        //         entryName,
        //         true,
        //         $"Ignore Format {id}"
        //     );
        //     IgnoreFormats[id] = entry.Value;
        //     entry.OnEntryValueChanged.Subscribe((_, ignoreFormat) =>
        //     {
        //         IgnoreFormats[id] = ignoreFormat;
        //     });
        // }
    }
}
