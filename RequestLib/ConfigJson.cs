using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Kafe.RequestLib;

internal static class ConfigJson {

    private static JsonConfig _config;

    private const string ConfigFileName = "Config.json";
    private const string TempConfigFileName = "Config.temp.json";
    private const int CurrentConfigVersion = 1;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserOverride {
        Default,
        LetMeDecide,
        AutoDecline,
        AutoAccept,
    }

    public record JsonConfigPlayer {
        public string Username;
        public UserOverride PlayerGlobalSetting = UserOverride.Default;
        public Dictionary<string, UserOverride> PerModSetting = new();
    }

    public record JsonConfig {
        public int ConfigVersion = CurrentConfigVersion;
        public UserOverride GlobalSetting = UserOverride.LetMeDecide;
        public readonly Dictionary<string, JsonConfigPlayer> PlayerSettings = new();
    }

    internal static void LoadConfigJson() {

        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", nameof(RequestLib), ConfigFileName));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        var tempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(RequestLib), TempConfigFileName));
        var tempConfigFile = new FileInfo(tempConfigPath);

        var resetConfigFiles = false;

        // Load the previous config
        if (configFile.Exists) {
            try {
                var fileContents = File.ReadAllText(configFile.FullName);
                if (fileContents.All(c => c == '\0')) {
                    throw new Exception($"The file {configFile.FullName} existed but is binary zero-filled file");
                }
                _config = JsonConvert.DeserializeObject<JsonConfig>(fileContents);
            }
            catch (Exception errMain) {
                try {
                    // Attempt to read from the temp file instead
                    MelonLogger.Warning($"Something went wrong when to load the {configFile.FullName}. Checking the backup...");
                    var fileContents = File.ReadAllText(tempConfigFile.FullName);
                    if (fileContents.All(c => c == '\0')) {
                        throw new Exception($"The file {configFile.FullName} existed but is binary zero-filled file");
                    }
                    _config = JsonConvert.DeserializeObject<JsonConfig>(fileContents);
                    MelonLogger.Msg($"Loaded from the backup config successfully!");
                }
                catch (Exception errTemp) {
                    resetConfigFiles = true;
                    MelonLogger.Error($"Something went wrong when to load the {tempConfigFile.FullName} config! Resetting config...");
                    MelonLogger.Error(errMain);
                    MelonLogger.Error(errTemp);
                }
            }
        }

        // Create default config files
        if (!configFile.Exists || resetConfigFiles) {
            var config = new JsonConfig();
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {configFile.FullName}...");
            File.WriteAllText(configFile.FullName, jsonContent);
            File.Copy(configPath, tempConfigFile.FullName, true);
            _config = config;
        }
    }

    private static void SaveConfig() {

        var jsonContent = JsonConvert.SerializeObject(_config, Formatting.Indented);

        var configPath = Path.GetFullPath(Path.Combine("UserData", nameof(RequestLib), ConfigFileName));
        var tempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(RequestLib), TempConfigFileName));

        // Save the temp file first
        var tempConfigFile = new FileInfo(tempConfigPath);
        var configFile = new FileInfo(configPath);
        tempConfigFile.Directory?.Create();
        File.WriteAllText(tempConfigFile.FullName, jsonContent);

        // Copy the temporary onto the actual file
        File.Copy(tempConfigPath, configFile.FullName, true);
    }

    internal static UserOverride GetUserOverride(string userGuid, string modName) {
        // If we got no player settings, use the global setting
        if (!_config.PlayerSettings.TryGetValue(userGuid, out var userOverride)) return _config.GlobalSetting;
        // If we got a mod override that's not the default, use it
        if (userOverride.PerModSetting.TryGetValue(modName, out var userModOverride) && userModOverride != UserOverride.Default) return userModOverride;
        // If the user setting is not default, use it
        if (userOverride.PlayerGlobalSetting != UserOverride.Default) return userOverride.PlayerGlobalSetting;
        // Otherwise just send the global setting
        return _config.GlobalSetting;
    }

    private static UserOverride GetNextOverride(UserOverride currentOverride, bool allowDefault) {
        var values = Enum.GetValues(typeof(UserOverride)).Cast<UserOverride>().ToArray();
        var currentIndex = Array.IndexOf(values, currentOverride);
        var nextOverride = values[(currentIndex + 1) % values.Length];
        if (!allowDefault && nextOverride == UserOverride.Default) nextOverride = GetNextOverride(nextOverride, false);
        return nextOverride;
    }

    internal static UserOverride GetCurrentOverride() {
        return _config.GlobalSetting;
    }

    internal static void SwapOverride() {
        _config.GlobalSetting = GetNextOverride(_config.GlobalSetting, false);
        SaveConfig();
    }

    internal static UserOverride GetCurrentOverride(string playerGuid) {
        return _config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride) ? playerOverride.PlayerGlobalSetting : UserOverride.Default;
    }

    internal static IEnumerable<string> GetCurrentOverriddenMods(string playerGuid) {
        return _config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride) ? playerOverride.PerModSetting.Keys : Array.Empty<string>();
    }

    internal static void SwapOverride(string playerGuid, string playerName) {
        if (_config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride)) {
            playerOverride.PlayerGlobalSetting = GetNextOverride(playerOverride.PlayerGlobalSetting, true);
        }
        else {
            playerOverride = new JsonConfigPlayer {Username = playerName};
            playerOverride.PlayerGlobalSetting = GetNextOverride(playerOverride.PlayerGlobalSetting, true);
            _config.PlayerSettings[playerGuid] = playerOverride;
        }
        SaveConfig();
    }

    internal static UserOverride GetCurrentOverride(string playerGuid, string modName) {
        if (!_config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride)) return UserOverride.Default;
        return playerOverride.PerModSetting.TryGetValue(modName, out var playerModOverride) ? playerModOverride : UserOverride.Default;
    }

    internal static void SwapOverride(string playerGuid, string playerName, string modName) {
        if (!_config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride)) {
            playerOverride = new JsonConfigPlayer {Username = playerName};
            _config.PlayerSettings[playerGuid] = playerOverride;
        }
        if (playerOverride.PerModSetting.ContainsKey(modName)) {
            playerOverride.PerModSetting[modName] = GetNextOverride(playerOverride.PerModSetting[modName], true);
        }
        else {
            playerOverride.PerModSetting[modName] = GetNextOverride(UserOverride.Default, true);
        }
        SaveConfig();
    }

    internal static void ClearModOverrides(string playerGuid) {
        if (_config.PlayerSettings.TryGetValue(playerGuid, out var playerOverride)) {
            playerOverride.PerModSetting.Clear();
            SaveConfig();
        }
    }
}
