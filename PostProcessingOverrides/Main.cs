using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.Camera;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.PostProcessingOverrides.Properties;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Kafe.PostProcessingOverrides;

public enum OverrideType {
    Original,
    Global,
    Custom,
    Off,
}

public enum OverrideSetting {
    Off,
    Original,
    Override,
}

public class PostProcessingOverrides : MelonMod {

    internal static JsonConfig Config;

    private const string PostProcessingOverridesConfigFile = "PostProcessingOverridesModConfig.json";
    private const int CurrentConfigVersion = 1;

    // Layer Indices and Masks
    private const int PPLayer = CVRLayers.Water;
    private const int PPLayerMask = 1 << PPLayer;

    public static event Action ConfigChanged;

    private static CVRWorld _currentWorld;
    private static string _currentWorldGuid;
    private static Camera _currentWorldCamera;
    private static PostProcessLayer _currentWorldLayer;
    private static PostProcessVolume _currentWorldModVolume;
    private static readonly PostProcessProfile ModProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
    private static bool _currentWorldInitialized;

    // Current world original settings
    private static bool _originalEnabled;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        // Load The config
        var configPath = Path.GetFullPath(Path.Combine("UserData", PostProcessingOverridesConfigFile));
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
                MelonLogger.Error($"Something went wrong when to load the {configFile.FullName} config! " +
                                  $"You might want to delete/fix the file and try again...");
                MelonLogger.Error(e);
                return;
            }
        }

        // Check for BTKUILib
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.BTKUILibName) != null) {
            MelonLogger.Msg($"Initializing BTKUI integration...");
            ModConfig.InitializeBTKUI();
        }
        else {
            MelonLogger.Error($"Failed to find BTKUILib mod. Make sure you have it installed...");
            return;
        }

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    public static JsonConfigWorldPP GetCurrentConfigSettings() {
        return Config.WorldPPConfigs[_currentWorldGuid];
    }

    public static bool IsWorldLoaded() {
        return !string.IsNullOrEmpty(_currentWorldGuid);
    }

    internal static void SaveConfigAndApplyChanges(bool applyChanges) {

        // Save the current config to file
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", PostProcessingOverridesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(instancesConfigFile.FullName, jsonContent);

        // The config has changed event
        ConfigChanged?.Invoke();

        // Apply the current config to the world
        if (applyChanges) _currentWorld.UpdatePostProcessing();
    }

    public record JsonConfig {

        public int ConfigVersion = CurrentConfigVersion;

        public readonly JsonConfigPPSettings Global = new() {
            AO = new JsonConfigPPSettingAO { Active = OverrideSetting.Off },
            DepthOfField = new JsonConfigPPSettingDepthOfField { Active = OverrideSetting.Off },
            MotionBlur = new JsonConfigPPSettingMotionBlur { Active = OverrideSetting.Off },
            SpaceReflections = new JsonConfigPPSettingSpaceReflections { Active = OverrideSetting.Off },
            LensDistortion = new JsonConfigPPSettingLensDistortion { Active = OverrideSetting.Off },
            ChromaticAberration = new JsonConfigPPSettingChromaticAberration { Active = OverrideSetting.Off },
        };

        public readonly Dictionary<string, JsonConfigWorldPP> WorldPPConfigs = new();
    }

    public record JsonConfigWorldPP {
        [JsonConverter(typeof(StringEnumConverter))]
        public OverrideType ConfigType;
        public JsonConfigPPSettings CustomConfig;
    }

    public abstract record JsonConfigPPSetting {
        [JsonConverter(typeof(StringEnumConverter))] public OverrideSetting Active = OverrideSetting.Override;
    }

    public record JsonConfigPPSettingBloom : JsonConfigPPSetting {
        public float Intensity = 0.2f;
        public float Threshold = 1.0f;
    }
    public record JsonConfigPPSettingAO : JsonConfigPPSetting;
    public record JsonConfigPPSettingColorGrading : JsonConfigPPSetting {
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
    public record JsonConfigPPSettingSpaceReflections : JsonConfigPPSetting;
    public record JsonConfigPPSettingVignette : JsonConfigPPSetting;

    public record JsonConfigPPSettings() {
        public JsonConfigPPSettings GetDeepCopy() {
            return new JsonConfigPPSettings() {
                Bloom = Bloom,
                AO = AO,
                ColorGrading = ColorGrading,
                AutoExposure = AutoExposure,
                ChromaticAberration = ChromaticAberration,
                DepthOfField = DepthOfField,
                Grain = Grain,
                LensDistortion = LensDistortion,
                MotionBlur = MotionBlur,
                SpaceReflections = SpaceReflections,
                Vignette = Vignette,
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
        public JsonConfigPPSettingSpaceReflections SpaceReflections = new();
        public JsonConfigPPSettingVignette Vignette = new();
    }

    private static void UpdatePostProcessing() {

        var currentConfig = Config.WorldPPConfigs[_currentWorldGuid];
        JsonConfigPPSettings configToBeUsed = null;

        switch (currentConfig.ConfigType) {

            case OverrideType.Original:
                _currentWorldModVolume.enabled = false;
                _currentWorldLayer.enabled = _originalEnabled;
                PortableCamera.Instance.OnWorldLoaded(_currentWorldCamera);
                // We're done here, the original behavior was loaded by CVR before this was called
                return;

            case OverrideType.Off:
                _currentWorldModVolume.enabled = false;
                _currentWorldLayer.enabled = false;
                PortableCamera.Instance.OnWorldLoaded(_currentWorldCamera);
                // We're done here, we disabled everything
                return;

            case OverrideType.Global:
                configToBeUsed = Config.Global;
                break;

            case OverrideType.Custom:
                configToBeUsed = currentConfig.CustomConfig;
                break;
        }

        // Set the PP overrides state
        _currentWorldModVolume.enabled = true;
        _currentWorldLayer.enabled = true;
        PortableCamera.Instance.OnWorldLoaded(_currentWorldCamera);

        // Configure our global override profile

        // Bloom
        if (!ModProfile.TryGetSettings(out Bloom bloom)) {
            bloom = ModProfile.AddSettings<Bloom>();
        }
        bloom.enabled.value = configToBeUsed!.Bloom.Active == OverrideSetting.Override;
        bloom.enabled.overrideState = configToBeUsed.Bloom.Active == OverrideSetting.Override;
        // Bloom Intensity
        bloom.intensity.value = configToBeUsed.Bloom.Intensity;
        bloom.intensity.overrideState = configToBeUsed.Bloom.Active == OverrideSetting.Override;
        // Bloom Threshold
        bloom.threshold.value = configToBeUsed.Bloom.Threshold;
        bloom.threshold.overrideState = configToBeUsed.Bloom.Active == OverrideSetting.Override;

        // // ColorGrading
        // if (!ModProfile.TryGetSettings(out ColorGrading colorGrading)) {
        //     colorGrading = ModProfile.AddSettings<ColorGrading>();
        // }
        // colorGrading.enabled.value = configToBeUsed.ColorGrading.Active == OverrideSetting.Override;
        // colorGrading.enabled.overrideState = configToBeUsed.ColorGrading.Active == OverrideSetting.Override;
        // // ColorGrading Brightness
        // colorGrading.brightness.value = configToBeUsed.ColorGrading.Brightness;
        // colorGrading.brightness.overrideState = configToBeUsed.ColorGrading.Active == OverrideSetting.Override;
        // ColorGrading Hue
        // colorGrading.hueShift.value = configToBeUsed.ColorGrading.HueShift;
        // colorGrading.hueShift.overrideState = configToBeUsed.ColorGrading.Active == OverrideSetting.Override;
        // // ColorGrading Mode
        // colorGrading.gradingMode.value = configToBeUsed.ColorGrading.GradingMode;
        // colorGrading.gradingMode.overrideState = configToBeUsed.ColorGrading.Active == OverrideSetting.Override;

        // Disable/Enable all world's volumes according the config
        foreach (var postProcessingBloom in _currentWorld._postProcessingBloomList) {
            postProcessingBloom.active = configToBeUsed.Bloom.Active != OverrideSetting.Off;
        }
        foreach (var postProcessingAo in _currentWorld._postProcessingAOList) {
            postProcessingAo.active = configToBeUsed.AO.Active != OverrideSetting.Off;
        }
        foreach (var processingColorGrading in _currentWorld._postProcessingColorGradingList) {
            processingColorGrading.active = configToBeUsed.ColorGrading.Active != OverrideSetting.Off;
        }
        foreach (var processingAutoExposure in _currentWorld._postProcessingAutoExposureList) {
            processingAutoExposure.active = configToBeUsed.AutoExposure.Active != OverrideSetting.Off;
        }
        foreach (var chromaticAberration in _currentWorld._postProcessingChromaticAberrationList) {
            chromaticAberration.active = configToBeUsed.ChromaticAberration.Active != OverrideSetting.Off;
        }
        foreach (var processingDepthOfField in _currentWorld._postProcessingDepthOfFieldList) {
            processingDepthOfField.active = configToBeUsed.DepthOfField.Active != OverrideSetting.Off;
        }
        foreach (var postProcessingGrain in _currentWorld._postProcessingGrainList) {
            postProcessingGrain.active = configToBeUsed.Grain.Active != OverrideSetting.Off;
        }
        foreach (var processingLensDistortion in _currentWorld._postProcessingLensDistortionList) {
            processingLensDistortion.active = configToBeUsed.LensDistortion.Active != OverrideSetting.Off;
        }
        foreach (var processingMotionBlur in _currentWorld._postProcessingMotionBlurList) {
            processingMotionBlur.active = configToBeUsed.MotionBlur.Active != OverrideSetting.Off;
        }
        foreach (var spaceReflections in _currentWorld._postProcessingScreenSpaceReflectionsList) {
            spaceReflections.active = configToBeUsed.SpaceReflections.Active != OverrideSetting.Off;
        }
        foreach (var processingVignette in _currentWorld._postProcessingVignetteList) {
            processingVignette.active = configToBeUsed.Vignette.Active != OverrideSetting.Off;
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static void SetupPostProcessing(CVRWorld world) {

            // Save current world info
            _currentWorld = world;
            _currentWorldGuid = MetaPort.Instance.CurrentWorldId;
            _currentWorldInitialized = false;

            _currentWorldCamera = PlayerSetup.Instance.GetActiveCamera().GetComponent<Camera>();
            _currentWorldLayer = _currentWorldCamera.GetComponent<PostProcessLayer>();

            // If the camera doesn't have a post process layer, add one
            if (_currentWorldLayer == null)
            {
                _currentWorldLayer = _currentWorldCamera.gameObject.AddComponent<PostProcessLayer>();
                _currentWorldLayer.Init(MetaPort.Instance.postProcessResources);
                _currentWorldLayer.volumeTrigger = _currentWorldLayer.transform;
            }

            // Enforce the PPLayer on the Post Processing layer
            if (world.referenceCamera != null && world.referenceCamera.TryGetComponent(out PostProcessLayer refCameraPostProcessLayer)) {
                _currentWorldLayer.volumeLayer = refCameraPostProcessLayer.volumeLayer;
                _originalEnabled = refCameraPostProcessLayer.enabled;
            }
            else {
                _originalEnabled = false;
            }

            // Add our PP layer to the Mask
            _currentWorldLayer.volumeLayer |= PPLayerMask;

            // Create the mod override volume for the world
            var modPPVolumeGo = new GameObject("[PostProcessOverrides Mod] OverrideVolume") {
                layer = PPLayer
            };
            modPPVolumeGo.transform.SetParent(world.transform);
            _currentWorldModVolume = modPPVolumeGo.AddComponent<PostProcessVolume>();
            _currentWorldModVolume.profile = ModProfile;
            _currentWorldModVolume.isGlobal = true;
            _currentWorldModVolume.priority = float.MaxValue;
            _currentWorldModVolume.weight = 1f;
            _currentWorldModVolume.enabled = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.SetDefaultCamValues))]
        public static void After_CVRWorld_SetDefaultCamValues(CVRWorld __instance) {
            try {
                // Setup when there is no reference camera
                SetupPostProcessing(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_CopyRefCamValues)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.CopyRefCamValues))]
        public static void After_CVRWorld_CopyRefCamValues(CVRWorld __instance) {
            try {
                // Setup when there is a reference camera
                SetupPostProcessing(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_CopyRefCamValues)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.UpdatePostProcessing))]
        public static void After_CVRWorld_UpdatePostProcessing(CVRWorld __instance) {
            try {

                // The first update of post processing, we should grab the world defaults here!
                if (!_currentWorldInitialized) {

                    // Initialize the config if missing
                    if (!Config.WorldPPConfigs.ContainsKey(_currentWorldGuid)) {
                        var config = new JsonConfigWorldPP {
                            ConfigType = ModConfig.MeDefaultWorldConfig.Value,
                            CustomConfig = Config.Global.GetDeepCopy(),
                        };
                        Config.WorldPPConfigs.Add(_currentWorldGuid, config);
                        SaveConfigAndApplyChanges(false);
                    }

                    // Update the BTKUI menus
                    ConfigChanged?.Invoke();

                    _currentWorldInitialized = true;
                }

                // Everytime CVR Updates the post processing, we're going to catch it and do our own business
                UpdatePostProcessing();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_UpdatePostProcessing)}");
                MelonLogger.Error(e);
            }
        }

    }
}
