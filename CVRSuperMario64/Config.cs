using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public static class Config {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeDisableAudio;
    internal static MelonPreferences_Entry<float> MeAudioPitch;
    internal static MelonPreferences_Entry<float> MeAudioVolume;
    internal static MelonPreferences_Entry<int> MeGameTickMs;
    internal static MelonPreferences_Entry<bool> MePlayRandomMusicOnMarioJoin;
    internal static MelonPreferences_Entry<float> MeSkipFarMarioDistance;
    internal static MelonPreferences_Entry<int> MeMaxMariosAnimatedPerPerson;
    internal static MelonPreferences_Entry<int> MeMaxMeshColliderTotalTris;
    internal static MelonPreferences_Entry<bool> MeDeleteMarioAfterDead;
    internal static MelonPreferences_Entry<bool> MeAttemptToLoadWorldColliders;


    private static Action _worldLoaded;

    // Json
    private static JsonConfig _config;

    private const string MarioConfigFile = "CVRSuperMario64Config.json";
    private const int CurrentConfigVersion = 1;

    private record JsonConfig {
        public int ConfigVersion = CurrentConfigVersion;
        public readonly Dictionary<string, JsonConfigWorld> WorldConfigs = new();
    }

    private record JsonConfigWorld {
        public bool LoadWorldColliders = MeAttemptToLoadWorldColliders.Value;
    }

    private static void CreateDefaultJsonConfig(FileInfo configFile) {
        var config = new JsonConfig();
        var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
        MelonLogger.Msg($"Initializing the config file on {configFile.FullName}...");
        File.WriteAllText(configFile.FullName, jsonContent);
        _config = config;
    }

    private static void SaveJsonConfig() {
        // Save the current config to file
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", MarioConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(_config, Formatting.Indented);
        File.WriteAllText(instancesConfigFile.FullName, jsonContent);
    }

    public static void LoadJsonConfig() {
        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", MarioConfigFile));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        // Create default config
        if (!configFile.Exists) {
            CreateDefaultJsonConfig(configFile);
        }
        // Load the previous config
        else {
            try {
                _config = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(configFile.FullName));
            }
            catch (Exception e) {
                MelonLogger.Error($"Something went wrong when to load the {configFile.FullName} config... Recreating the config...");
                MelonLogger.Error(e);
                // Recreate the file with the default config...
                CreateDefaultJsonConfig(configFile);
            }
        }
    }

    public static bool ShouldAttemptToLoadWorldColliders() {
        return Instances.CurrentWorldId != null && _config.WorldConfigs.TryGetValue(Instances.CurrentWorldId, out var worldConfig)
            ? worldConfig.LoadWorldColliders
            : MeAttemptToLoadWorldColliders.Value;
    }

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(nameof(CVRSuperMario64));

        MeDisableAudio = _melonCategory.CreateEntry("DisableAudio", false,
            description: "Whether to disable the game audio or not.");

        MeAudioVolume = _melonCategory.CreateEntry("AudioVolume", 0.1f,
            description: "The audio volume.");

        MeAudioPitch = _melonCategory.CreateEntry("AudioPitch", 0.74f,
            description: "The audio pitch of the game sounds.");

        MeGameTickMs = _melonCategory.CreateEntry("GameTickMs", 25,
            description: "The game ticks frequency in Milliseconds.");

        MePlayRandomMusicOnMarioJoin = _melonCategory.CreateEntry("PlayRandomMusicOnMarioJoin", true,
            description: "Whether to play a random music when a mario joins or not.");
        MePlayRandomMusicOnMarioJoin.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!newValue) Interop.StopMusic();
        });

        MeMaxMariosAnimatedPerPerson = _melonCategory.CreateEntry("MaxMariosAnimatedPerPerson", 1,
            description: "The max number of marios other people can control at the same time.");

        MeSkipFarMarioDistance = _melonCategory.CreateEntry("SkipFarMarioDistance", 5f,
            description: "The max distance that we're going to calculate the mario animations for other people.");

        MeMaxMeshColliderTotalTris = _melonCategory.CreateEntry("MaxMeshColliderTotalTris", 50000,
            description: "The max total number of collision tris loaded from automatically generated static mesh colliders.");

        MeDeleteMarioAfterDead = _melonCategory.CreateEntry("DeleteMarioAfterDead", true,
            description: "Whether to automatically delete our marios after 15 seconds of being dead or not.");

        MeAttemptToLoadWorldColliders = _melonCategory.CreateEntry("AttemptToLoadWorldColliders", true,
            description: "Default option for whether it should attempt to load world colliders or not when joining new worlds.");
    }

    public static void InitializeBTKUI() {
        QuickMenuAPI.OnMenuRegenerate += LoadBTKUILib;
    }

    private static void LoadBTKUILib(CVR_MenuManager manager) {
        QuickMenuAPI.OnMenuRegenerate -= LoadBTKUILib;

        // Load the Mario Icon
        using (var stream = new MemoryStream(CVRSuperMario64.GetMarioSprite().texture.EncodeToPNG())) {
            QuickMenuAPI.PrepareIcon(nameof(CVRSuperMario64), "MarioIcon", stream);
        }

        var page = new Page(nameof(CVRSuperMario64), nameof(CVRSuperMario64), true, "MarioIcon") {
            MenuTitle = nameof(CVRSuperMario64),
            MenuSubtitle = "Configure CVR Super Mario 64 Mod",
        };

        var currentWorldCategory = page.AddCategory("Current World");

        var attemptToLoadWorldColliders = currentWorldCategory.AddToggle("Load this World's Colliders",
            "Whether to attempt to auto generate colliders for this world or not. Some worlds are just too laggy " +
            "to have their colliders generated... If that's the case disable this and use props to create colliders!",
            ShouldAttemptToLoadWorldColliders());
        // Update/Create the value in the config for the current world
        attemptToLoadWorldColliders.OnValueUpdated += isOn =>
        {
            if (string.IsNullOrEmpty(Instances.CurrentWorldId)) return;
            if (_config.WorldConfigs.TryGetValue(Instances.CurrentWorldId, out var worldConfig)) {
                worldConfig.LoadWorldColliders = isOn;
            }
            else {
                _config.WorldConfigs.Add(Instances.CurrentWorldId, new JsonConfigWorld());
            }
            SaveJsonConfig();
            CVRSM64Context.QueueStaticSurfacesUpdate();
        };
        // Update the toggle when changing worlds to match the current world setting
        _worldLoaded += () => attemptToLoadWorldColliders.ToggleValue = ShouldAttemptToLoadWorldColliders();

        var audioCategory = page.AddCategory("Audio");

        var disableAudio = audioCategory.AddToggle("Disable ALL Audio",
            "Whether to disable all Super Mario 64 Music/Sounds or not.",
            MeDisableAudio.Value);
        disableAudio.OnValueUpdated += b => {
            if (b != MeDisableAudio.Value) MeDisableAudio.Value = b;
        };
        MeDisableAudio.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) disableAudio.ToggleValue = newValue;
        });

        var playRandomMusicOnMarioStart = audioCategory.AddToggle("Play music on Mario Join",
            "Whether to play a random music on Mario Join or not.",
            MePlayRandomMusicOnMarioJoin.Value);
        playRandomMusicOnMarioStart.OnValueUpdated += b => {
            if (b != MePlayRandomMusicOnMarioJoin.Value) MePlayRandomMusicOnMarioJoin.Value = b;
        };
        MePlayRandomMusicOnMarioJoin.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) playRandomMusicOnMarioStart.ToggleValue = newValue;
        });

        var audioVolume = audioCategory.AddSlider(
            "Volume",
            "The volume for all the Super Mario 64 sounds.",
            MeAudioVolume.Value, 0f, 1f, 3);
        audioVolume.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeAudioVolume.Value)) MeAudioVolume.Value = newValue;
        };
        MeAudioVolume.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, audioVolume.SliderValue)) audioVolume.SetSliderValue(newValue);
        });

        // Performance Category
        var performanceCategory = page.AddCategory("Performance");

        var deleteMarioAfterDead = performanceCategory.AddToggle("Delete Mario 15s after Dead",
            "Whether to delete our Marios 15 seconds after Dead or not.",
            MeDeleteMarioAfterDead.Value);
        deleteMarioAfterDead.OnValueUpdated += b => {
            if (b != MeDeleteMarioAfterDead.Value) MeDeleteMarioAfterDead.Value = b;
        };
        MeDeleteMarioAfterDead.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) deleteMarioAfterDead.ToggleValue = newValue;
        });

        var skipFarMarioDistance = performanceCategory.AddSlider(
            "Skip Mario Engine Updates Distance",
            "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.",
            MeSkipFarMarioDistance.Value, 0f, 50f, 2);
        skipFarMarioDistance.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeSkipFarMarioDistance.Value)) MeSkipFarMarioDistance.Value = newValue;
        };
        MeSkipFarMarioDistance.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, skipFarMarioDistance.SliderValue)) skipFarMarioDistance.SetSliderValue(newValue);
        });

        var maxMariosAnimatedPerPerson = performanceCategory.AddSlider(
            "Max Marios per Player",
            "Max number of Marios per player that will be animated using the Super Mario 64 Engine.",
            MeMaxMariosAnimatedPerPerson.Value, 0, 20, 0);
        maxMariosAnimatedPerPerson.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeMaxMariosAnimatedPerPerson.Value) MeMaxMariosAnimatedPerPerson.Value = Mathf.RoundToInt(newValue);
        };
        MeMaxMariosAnimatedPerPerson.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(maxMariosAnimatedPerPerson.SliderValue)) maxMariosAnimatedPerPerson.SetSliderValue(newValue);
        });

        var maxMeshColliderTotalTris = performanceCategory.AddSlider(
            "Max Mesh Collider Total Triangles",
            "Maximum total number of triangles of automatically generated mesh colliders allowed.",
            MeMaxMeshColliderTotalTris.Value, 0, 250000, 0);
        maxMeshColliderTotalTris.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeMaxMeshColliderTotalTris.Value) MeMaxMeshColliderTotalTris.Value = Mathf.RoundToInt(newValue);
        };
        MeMaxMeshColliderTotalTris.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(maxMeshColliderTotalTris.SliderValue)) maxMeshColliderTotalTris.SetSliderValue(newValue);
        });

        // Engine Category
        var engineCategory = page.AddCategory("SM64 Engine");

        var audioPitch = engineCategory.AddSlider(
            "Pitch of the Audio [Default: 0.74]",
            "The audio pitch of the game sounds. You can use this to fine tune the Engine Sounds",
            MeAudioPitch.Value, 0f, 1f, 2);
        audioPitch.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeAudioPitch.Value)) MeAudioPitch.Value = newValue;
        };
        MeAudioPitch.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, audioPitch.SliderValue)) audioPitch.SetSliderValue(newValue);
        });

        var gameTicksMs = engineCategory.AddSlider(
            "Game Ticks Interval [Default: 25]",
            "The game ticks Interval in Milliseconds. This will directly impact the speed of Mario's behavior.",
            MeGameTickMs.Value, 1, 100, 0);
        gameTicksMs.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeGameTickMs.Value) MeGameTickMs.Value = Mathf.RoundToInt(newValue);
        };
        MeGameTickMs.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(gameTicksMs.SliderValue)) gameTicksMs.SetSliderValue(newValue);
        });
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.Start))]
        private static void After_CVRWorld_Awake() {
            var worldId = Instances.CurrentWorldId;
            if (_config.WorldConfigs.ContainsKey(worldId)) return;
            _config.WorldConfigs.Add(worldId, new JsonConfigWorld());
            SaveJsonConfig();
            _worldLoaded?.Invoke();
        }
    }
}
