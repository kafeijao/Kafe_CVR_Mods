using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;

namespace Kafe.BetterDECtalk;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static MelonPreferences_Entry<int> SpeakingRate { private set; get; }
    public static MelonPreferences_Entry<bool> EnablePhonemes { private set; get; }

    private const int SpeakingRateMin = 50;
    private const int SpeakingRateMax = 600;
    private const int SpeakingRateDefault = 200;

    public static void LoadMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterDECtalk));

        SpeakingRate = _melonCategory.CreateEntry("SpeakingRate", SpeakingRateDefault,
            description: $"Speaking Rate in words per minute, can go from {SpeakingRateMin} to {SpeakingRateMax}.",
            display_name: "Speaking Rate [default: 200]");

        SpeakingRate.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            if (newValue is > SpeakingRateMax or < SpeakingRateMin)
            {
                MelonLogger.Warning($"Speaking Rate must be between {SpeakingRateMin} and {SpeakingRateMax} words " +
                                    $"per minute. You tried {newValue}, resetting to {SpeakingRateDefault}.");
                SpeakingRate.Value = SpeakingRateDefault;
                return;
            }

            MelonLogger.Msg($"Setting Speaking Rate to {SpeakingRate.Value} words per minute.");
            if (DECtalkEngine.Instance != null)
                DECtalkEngine.Instance.Rate = (uint)SpeakingRate.Value;
        });

        EnablePhonemes = _melonCategory.CreateEntry("EnablePhonemes", true,
            description: "Enables the funny singing phonemes from DECtalk.",
            display_name: "Phonemes [default: true]");

        EnablePhonemes.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            MelonLogger.Msg($"Setting phonemes enabled to: {SpeakingRate.Value}");
            if (DECtalkEngine.Instance != null)
                DECtalkEngine.Instance.SetPhonemes(newValue);
        });
    }


    private static readonly HashSet<string> NativeDllNames = new HashSet<string>
    {
        "DECtalk.dll",
        "dtalk_us.dll",
    };

    private static readonly HashSet<string> NativeFileNames = new HashSet<string>
    {
        "dtalk_us.dic",
    };

    public static void ExtractFilesAndBinaries(Assembly assembly)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        foreach (string resourceFile in NativeDllNames.Concat(NativeFileNames))
        {
            string dstPath = Path.Combine(MelonEnvironment.GameRootDirectory, resourceFile);
            try
            {
                using Stream resourceStream = assembly.GetManifestResourceStream(resourceFile);

                // Check if the existing file already matches the embedded resource
                if (File.Exists(dstPath))
                {
                    long embeddedLength = resourceStream!.Length;
                    byte[] embeddedHash = sha.ComputeHash(resourceStream);
                    resourceStream.Position = 0; // Reset for potential extraction later

                    var fileInfo = new FileInfo(dstPath);
                    if (fileInfo.Length == embeddedLength)
                    {
                        byte[] existingHash;
                        using (FileStream existingFile = File.OpenRead(dstPath))
                            existingHash = sha.ComputeHash(existingFile);

                        if (embeddedHash.SequenceEqual(existingHash))
                        {
                            MelonLogger.Msg($"{resourceFile} is already up to date, skipping");
                            continue;
                        }
                    }
                }

                MelonLogger.Msg($"Extracting {resourceFile} to {dstPath}");
                using FileStream fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
                resourceStream!.CopyTo(fileStream);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to extract {resourceFile} resource into {dstPath}", ex);
                return;
            }
        }
    }
}
