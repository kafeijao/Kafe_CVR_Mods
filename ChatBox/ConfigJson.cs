using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Savior;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Kafe.ChatBox;

internal static class ConfigJson
{
    private static JsonConfig _config;

    private const string ConfigFileName = "Config.json";
    private const string TempConfigFileName = "Config.temp.json";
    private const int CurrentConfigVersion = 1;

    private static string _profanityPattern;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserOverride
    {
        Default,
        Show,
        Hide,
    }

    public record JsonConfig
    {
        public int ConfigVersion = CurrentConfigVersion;
        public readonly HashSet<string> ProfanityWords = new();
        public readonly Dictionary<string, UserOverride> UserVisibilityOverrides = new();
    }

    internal static void LoadConfigJson()
    {
        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", nameof(ChatBox), ConfigFileName));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        var tempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(ChatBox), TempConfigFileName));
        var tempConfigFile = new FileInfo(tempConfigPath);

        var resetConfigFiles = false;

        // Load the previous config
        if (configFile.Exists)
        {
            try
            {
                var fileContents = File.ReadAllText(configFile.FullName);
                if (fileContents.All(c => c == '\0'))
                {
                    throw new Exception($"The file {configFile.FullName} existed but is binary zero-filled file");
                }

                _config = JsonConvert.DeserializeObject<JsonConfig>(fileContents);
            }
            catch (Exception errMain)
            {
                try
                {
                    // Attempt to read from the temp file instead
                    MelonLogger.Warning(
                        $"Something went wrong when to load the {configFile.FullName}. Checking the backup...");
                    var fileContents = File.ReadAllText(tempConfigFile.FullName);
                    if (fileContents.All(c => c == '\0'))
                    {
                        throw new Exception($"The file {configFile.FullName} existed but is binary zero-filled file");
                    }

                    _config = JsonConvert.DeserializeObject<JsonConfig>(fileContents);
                    MelonLogger.Msg($"Loaded from the backup config successfully!");
                }
                catch (Exception errTemp)
                {
                    resetConfigFiles = true;
                    MelonLogger.Error(
                        $"Something went wrong when to load the {tempConfigFile.FullName} config! Resetting config...");
                    MelonLogger.Error(errMain);
                    MelonLogger.Error(errTemp);
                }
            }
        }

        // Create default config files
        if (!configFile.Exists || resetConfigFiles)
        {
            var config = new JsonConfig();
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {configFile.FullName}...");
            File.WriteAllText(configFile.FullName, jsonContent);
            File.Copy(configPath, tempConfigFile.FullName, true);
            _config = config;
        }

        UpdateProfanityPattern();
    }

    private static void SaveConfig()
    {
        var jsonContent = JsonConvert.SerializeObject(_config, Formatting.Indented);

        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(ChatBox), ConfigFileName));
        var instancesTempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(ChatBox), TempConfigFileName));

        // Save the temp file first
        var instancesTempConfigFile = new FileInfo(instancesTempConfigPath);
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesTempConfigFile.Directory?.Create();
        File.WriteAllText(instancesTempConfigFile.FullName, jsonContent);

        // Copy the temporary onto the actual file
        File.Copy(instancesTempConfigPath, instancesConfigFile.FullName, true);
    }

    internal static UserOverride GetUserOverride(string userGuid)
    {
        return _config.UserVisibilityOverrides.TryGetValue(userGuid, out var userOverride)
            ? userOverride
            : UserOverride.Default;
    }

    internal static void SetUserOverride(string userGuid, UserOverride userOverride)
    {
        _config.UserVisibilityOverrides[userGuid] = userOverride;
        SaveConfig();
    }

    internal static void ClearUserOverrides()
    {
        _config.UserVisibilityOverrides.Clear();
        SaveConfig();
    }

    internal static bool ShouldShowMessage(string playerGuid)
    {
        // Ignore messages from blocked users
        if (MetaPort.Instance.blockedUserIds.Contains(playerGuid))
        {
            return false;
        }

        var userOverride = GetUserOverride(playerGuid);
        // Ignore messages from non-friends (unless it's overriden to show)
        if (userOverride != UserOverride.Show && ModConfig.MeOnlyViewFriends.Value && !Friends.FriendsWith(playerGuid))
        {
            return false;
        }

        // Ignore messages from hidden users
        if (userOverride == UserOverride.Hide)
        {
            return false;
        }

        return true;
    }

    internal static HashSet<string> GetProfanityList()
    {
        return _config.ProfanityWords;
    }

    internal static void UpdateProfanityPattern()
    {
        _profanityPattern = "(" + string.Join("|", _config.ProfanityWords) + ")";
    }

    internal static string GetProfanityPattern() => _profanityPattern;

    internal static void AddProfanity(string profanityWord)
    {
        _config.ProfanityWords.Add(profanityWord);
        UpdateProfanityPattern();
        SaveConfig();
    }

    internal static void RemoveProfanity(string profanityWord)
    {
        _config.ProfanityWords.Remove(profanityWord);
        UpdateProfanityPattern();
        SaveConfig();
    }

    internal static void ClearProfanityList()
    {
        _config.ProfanityWords.Clear();
        SaveConfig();
    }
}
