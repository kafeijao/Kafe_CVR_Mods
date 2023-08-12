using ABI_RC.Core.Savior;
using MelonLoader;
using Newtonsoft.Json;

namespace Kafe.RealisticFlight;

internal static class ConfigJson {

    private static JsonConfig _config;

    private const string ConfigFileName = "Config.json";
    private const string TempConfigFileName = "Config.temp.json";
    private const int CurrentConfigVersion = 1;

    public record JsonConfigAvatar {
        public bool Override = false;
        public bool Enabled = true;
        public float MeFlapMultiplier = ModConfig.MeFlapMultiplier.Value;
    }

    public record JsonConfig {
        public int ConfigVersion = CurrentConfigVersion;
        public bool GlobalEnabled = true;
        public readonly Dictionary<string, JsonConfigAvatar> AvatarSettings = new();
    }

    internal static void LoadConfigJson() {

        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", nameof(RealisticFlight), ConfigFileName));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        var tempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(RealisticFlight), TempConfigFileName));
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

        var configPath = Path.GetFullPath(Path.Combine("UserData", nameof(RealisticFlight), ConfigFileName));
        var tempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(RealisticFlight), TempConfigFileName));

        // Save the temp file first
        var tempConfigFile = new FileInfo(tempConfigPath);
        var configFile = new FileInfo(configPath);
        tempConfigFile.Directory?.Create();
        File.WriteAllText(tempConfigFile.FullName, jsonContent);

        // Copy the temporary onto the actual file
        File.Copy(tempConfigPath, configFile.FullName, true);
    }

    internal static bool GetGlobalEnabled() {
        return _config.GlobalEnabled;
    }

    internal static void SetGlobalEnabled(bool isEnabled) {
        _config.GlobalEnabled = isEnabled;
        SaveConfig();
    }

    internal static bool GetCurrentAvatarOverriding() {
        return _config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar) && configAvatar.Override;
    }

    internal static void SetCurrentAvatarOverriding(bool isOverriding) {
        if (!_config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar)) {
            configAvatar = new JsonConfigAvatar();
            _config.AvatarSettings[MetaPort.Instance.currentAvatarGuid] = configAvatar;
        }
        configAvatar.Override = isOverriding;
        SaveConfig();
    }

    internal static bool GetCurrentAvatarEnabled() {
        return _config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar) && configAvatar.Override ? configAvatar.Enabled : _config.GlobalEnabled;
    }

    internal static void SetCurrentAvatarEnabled(bool isEnabled) {
        if (!_config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar)) {
            configAvatar = new JsonConfigAvatar();
            _config.AvatarSettings[MetaPort.Instance.currentAvatarGuid] = configAvatar;
        }
        configAvatar.Enabled = isEnabled;
        SaveConfig();
    }

    internal static float GetCurrentAvatarFlapModifier() {
        return _config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar) && configAvatar.Override && configAvatar.Enabled ? configAvatar.MeFlapMultiplier : ModConfig.MeFlapMultiplier.Value;
    }

    internal static void SetCurrentAvatarFlapModifier(float newModifier) {
        if (!_config.AvatarSettings.TryGetValue(MetaPort.Instance.currentAvatarGuid, out var configAvatar))  {
            configAvatar = new JsonConfigAvatar();
            _config.AvatarSettings[MetaPort.Instance.currentAvatarGuid] = configAvatar;
        }
        configAvatar.MeFlapMultiplier = newModifier;
        SaveConfig();
    }
}
