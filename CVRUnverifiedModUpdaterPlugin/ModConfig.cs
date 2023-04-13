using MelonLoader;
using Newtonsoft.Json;

namespace Kafe.CVRUnverifiedModUpdaterPlugin;

public static class ModConfig {

    public static JsonConfig Config;

    private const string JsonConfigFile = "CVRUnverifiedModUpdaterPluginConfig.json";
    private const int CurrentConfigVersion = 1;

    public record JsonConfig {
        public int ConfigVersion = CurrentConfigVersion;
        public DateTime NextCheck = DateTime.UtcNow;
        public List<JsonConfigRepo> RepoConfigs = new();
    }

    public record JsonConfigRepo {
        public string Owner;
        public string Repo;
        public List<JsonConfigRepoFile> Files = new();
        public string GetUrl() => $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    }

    public record JsonConfigRepoFile {
        public string Name;
        public string UpdatedAt = "";
    }

    public static void SaveJsonConfig() {
        // Save the current config to file
        var configPath = Path.GetFullPath(Path.Combine("UserData", JsonConfigFile));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(configFile.FullName, jsonContent);
    }

    public static void InitializeJsonConfig() {

        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", JsonConfigFile));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        // Create default config
        if (!configFile.Exists) {
            var config = new JsonConfig();
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {configFile.FullName}...");
            File.WriteAllText(configFile.FullName, jsonContent);
            Config = config;
        }
        // Load the previous config
        else {
            try {
                Config = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(configFile.FullName));
            }
            catch (Exception e) {
                MelonLogger.Error($"Something went wrong when to load the {configFile.FullName} config... Fix the configuration file...");
                MelonLogger.Error(e);
                throw;
            }
        }
    }

}
