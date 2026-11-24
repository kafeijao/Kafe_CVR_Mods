using ABI_RC.Core;
using ABI_RC.Systems.GameEventSystem;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Kafe.PostProcessingOverrides;

public enum OverrideType
{
    Original,
    Global,
    Custom,
    Off,
}

public enum OverrideSetting
{
    Off,
    Original,
    Override,
}

public class PostProcessingOverrides : MelonMod
{
    internal static JsonConfig Config;

    private const string PostProcessingOverridesConfigFile = "PostProcessingOverridesModConfig.json";
    private const int CurrentConfigVersion = 1;

    private const int OverrideVolumeLayer = CVRLayers.CVRReserved2;

    public static event Action CurrentWorldChanged;

    private const float SaveDelaySeconds = 1f;
    private static float? _pendingSaveTime;

    private static string _currentWorldGuid;

    private static PostProcessVolume _overrideVolume;

    private static PostProcessProfile _overrideProfile;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();

        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", PostProcessingOverridesConfigFile));
        var configFile = new FileInfo(configPath);
        configFile.Directory?.Create();

        // Create default config
        if (!configFile.Exists)
        {
            var config = new JsonConfig();
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {configFile.FullName}...");
            File.WriteAllText(configFile.FullName, jsonContent);
            Config = config;
        }
        // Load the previous config
        else
        {
            try
            {
                Config = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(configFile.FullName));
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Something went wrong when to load the {configFile.FullName} config! " +
                                  $"You might want to delete/fix the file and try again...");
                MelonLogger.Error(e);
                return;
            }
        }

        // Check for BTKUILib
        ModConfig.InitializeBTKUI();

        CVRGameEventSystem.World.OnLoad.AddListener(OnWorldLoad);
        CVRGameEventSystem.World.OnWorldUnload.AddListener((_, _) => OnWorldUnload());

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    /// <summary>
    /// Returns the settings of the world we're in, or null when we're not in one
    /// </summary>
    public static JsonConfigWorldPP GetCurrentWorldConfig()
    {
        if (_currentWorldGuid == null) return null;
        return Config.WorldPPConfigs.GetValueOrDefault(_currentWorldGuid);
    }

    /// <summary>
    /// Returns the Global or Custom settings a world uses, or null for Original and Off
    /// </summary>
    public static JsonConfigPPSettings GetUsedSettings(JsonConfigWorldPP worldConfig)
    {
        return worldConfig.ConfigType switch
        {
            OverrideType.Global => Config.Global,
            OverrideType.Custom => worldConfig.CustomConfig,
            _ => null,
        };
    }

    internal static void SaveConfigAndApplyChanges()
    {
        // Sliders change every frame while dragged, so wait for them to settle before writing
        _pendingSaveTime = Time.unscaledTime + SaveDelaySeconds;
        ApplyOverrides();
    }

    public override void OnUpdate()
    {
        if (_pendingSaveTime == null || Time.unscaledTime < _pendingSaveTime) return;
        SaveConfig();
    }

    public override void OnApplicationQuit()
    {
        if (_pendingSaveTime != null) SaveConfig();
    }

    /// <summary>
    /// Writes the config to the json file
    /// </summary>
    private static void SaveConfig()
    {
        _pendingSaveTime = null;
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", PostProcessingOverridesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(instancesConfigFile.FullName, jsonContent);
    }

    /// <summary>
    /// Sets up the settings of the world we joined and applies them
    /// </summary>
    private static void OnWorldLoad(string worldId)
    {
        _currentWorldGuid = worldId;

        if (!Config.WorldPPConfigs.ContainsKey(worldId))
        {
            Config.WorldPPConfigs.Add(worldId, new JsonConfigWorldPP
            {
                ConfigType = ModConfig.MeDefaultWorldConfig.Value,
                CustomConfig = Config.Global.GetDeepCopy(),
            });
            SaveConfig();
        }

        if (_overrideVolume == null) CreateOverrideVolume();

        ApplyOverrides();
        CurrentWorldChanged?.Invoke();
    }

    /// <summary>
    /// Stops overriding when we leave a world
    /// </summary>
    private static void OnWorldUnload()
    {
        _currentWorldGuid = null;
        ApplyOverrides();
        CurrentWorldChanged?.Invoke();
    }

    /// <summary>
    /// Creates the global volume that holds our overrides. It lives across world changes
    /// </summary>
    private static void CreateOverrideVolume()
    {
        _overrideProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
        _overrideProfile.hideFlags = HideFlags.DontUnloadUnusedAsset;

        var volumeObject = new GameObject($"[{nameof(PostProcessingOverrides)} Mod] Override Volume")
        {
            layer = OverrideVolumeLayer,
        };
        UnityEngine.Object.DontDestroyOnLoad(volumeObject);

        _overrideVolume = volumeObject.AddComponent<PostProcessVolume>();
        _overrideVolume.sharedProfile = _overrideProfile;
        _overrideVolume.isGlobal = true;
        // Highest priority so our values are blended last and win over the world volumes
        _overrideVolume.priority = float.MaxValue;
        _overrideVolume.weight = 1f;
    }

    /// <summary>
    /// Updates the override volume to match the settings of the current world
    /// </summary>
    private static void ApplyOverrides()
    {
        if (_overrideVolume == null) return;

        var worldConfig = GetCurrentWorldConfig();

        // Original doesn't touch the world post processing
        if (worldConfig == null || worldConfig.ConfigType == OverrideType.Original)
        {
            _overrideVolume.enabled = false;
            return;
        }

        _overrideVolume.enabled = true;

        // Off has no settings, which turns every effect off
        var settings = GetUsedSettings(worldConfig);

        ApplyOffSetting<AmbientOcclusion>(settings?.AO);
        ApplyOffSetting<AutoExposure>(settings?.AutoExposure);
        ApplyOffSetting<ChromaticAberration>(settings?.ChromaticAberration);
        ApplyOffSetting<ColorGrading>(settings?.ColorGrading);
        ApplyOffSetting<DepthOfField>(settings?.DepthOfField);
        ApplyOffSetting<Grain>(settings?.Grain);
        ApplyOffSetting<LensDistortion>(settings?.LensDistortion);
        ApplyOffSetting<MotionBlur>(settings?.MotionBlur);
        ApplyOffSetting<Vignette>(settings?.Vignette);

        var bloomConfig = settings?.Bloom;
        var bloom = GetOrAddEffect<Bloom>();
        var overrideBloom = bloomConfig?.Active == OverrideSetting.Override;
        bloom.enabled.overrideState = bloomConfig == null || bloomConfig.Active != OverrideSetting.Original;
        bloom.enabled.value = overrideBloom;
        bloom.intensity.overrideState = overrideBloom;
        bloom.threshold.overrideState = overrideBloom;
        if (bloomConfig == null) return;
        bloom.intensity.value = bloomConfig.Intensity;
        bloom.threshold.value = bloomConfig.Threshold;
    }

    /// <summary>
    /// Forces an effect off when its setting is Off, otherwise leaves it as the world and game settings have it
    /// </summary>
    private static void ApplyOffSetting<T>(JsonConfigPPSetting setting) where T : PostProcessEffectSettings
    {
        var effect = GetOrAddEffect<T>();
        // We have no values of our own for these effects, so Override behaves like Original
        effect.enabled.overrideState = setting == null || setting.Active == OverrideSetting.Off;
        effect.enabled.value = false;
    }

    /// <summary>
    /// Returns the effect entry from our profile, adding it when missing
    /// </summary>
    private static T GetOrAddEffect<T>() where T : PostProcessEffectSettings
    {
        return _overrideProfile.TryGetSettings(out T effect) ? effect : _overrideProfile.AddSettings<T>();
    }

    public record JsonConfig
    {
        public int ConfigVersion = CurrentConfigVersion;

        public readonly JsonConfigPPSettings Global = new JsonConfigPPSettings
        {
            AO = new JsonConfigPPSettingAO { Active = OverrideSetting.Off },
            DepthOfField = new JsonConfigPPSettingDepthOfField { Active = OverrideSetting.Off },
            MotionBlur = new JsonConfigPPSettingMotionBlur { Active = OverrideSetting.Off },
            LensDistortion = new JsonConfigPPSettingLensDistortion { Active = OverrideSetting.Off },
            ChromaticAberration = new JsonConfigPPSettingChromaticAberration { Active = OverrideSetting.Off },
        };

        public readonly Dictionary<string, JsonConfigWorldPP> WorldPPConfigs = new();
    }

    public record JsonConfigWorldPP
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OverrideType ConfigType;

        public JsonConfigPPSettings CustomConfig;
    }

    public abstract record JsonConfigPPSetting
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public OverrideSetting Active = OverrideSetting.Override;
    }

    public record JsonConfigPPSettingBloom : JsonConfigPPSetting
    {
        public float Intensity = 0.2f;
        public float Threshold = 1.0f;
    }

    public record JsonConfigPPSettingAO : JsonConfigPPSetting;

    public record JsonConfigPPSettingColorGrading : JsonConfigPPSetting
    {
        // public float Brightness = 0.2f;
        // public float HueShift = 0.0f;
        // public GradingMode GradingMode = GradingMode.HighDefinitionRange;
    }

    public record JsonConfigPPSettingAutoExposure : JsonConfigPPSetting;

    public record JsonConfigPPSettingChromaticAberration : JsonConfigPPSetting;

    public record JsonConfigPPSettingDepthOfField : JsonConfigPPSetting;

    public record JsonConfigPPSettingGrain : JsonConfigPPSetting;

    public record JsonConfigPPSettingLensDistortion : JsonConfigPPSetting;

    public record JsonConfigPPSettingMotionBlur : JsonConfigPPSetting;

    public record JsonConfigPPSettingVignette : JsonConfigPPSetting;

    public record JsonConfigPPSettings()
    {
        public JsonConfigPPSettings GetDeepCopy()
        {
            return new JsonConfigPPSettings()
            {
                Bloom = Bloom with { },
                AO = AO with { },
                ColorGrading = ColorGrading with { },
                AutoExposure = AutoExposure with { },
                ChromaticAberration = ChromaticAberration with { },
                DepthOfField = DepthOfField with { },
                Grain = Grain with { },
                LensDistortion = LensDistortion with { },
                MotionBlur = MotionBlur with { },
                Vignette = Vignette with { },
            };
        }

        public JsonConfigPPSettingBloom Bloom = new();
        public JsonConfigPPSettingAO AO = new();
        public JsonConfigPPSettingColorGrading ColorGrading = new();
        public JsonConfigPPSettingAutoExposure AutoExposure = new();
        public JsonConfigPPSettingChromaticAberration ChromaticAberration = new();
        public JsonConfigPPSettingDepthOfField DepthOfField = new();
        public JsonConfigPPSettingGrain Grain = new();
        public JsonConfigPPSettingLensDistortion LensDistortion = new();
        public JsonConfigPPSettingMotionBlur MotionBlur = new();
        public JsonConfigPPSettingVignette Vignette = new();
    }
}
