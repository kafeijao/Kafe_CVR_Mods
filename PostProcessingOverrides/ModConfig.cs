using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;

namespace Kafe.PostProcessingOverrides;

public static class ModConfig {

    private const int Multiplier = 10;

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<OverrideType> MeDefaultWorldConfig;

    // BTKUI Stuff
    private static BTKUILib.UIObjects.Page _page;

    // Internal
    private static readonly int TypeCount = Enum.GetValues(typeof(OverrideType)).Length;
    private static readonly int SettingCount = Enum.GetValues(typeof(OverrideSetting)).Length;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(PostProcessingOverrides));

        MeDefaultWorldConfig = _melonCategory.CreateEntry("DefaultWorldConfig", OverrideType.Original,
            description: "When joining a world for the first time which configuration should be used? Original = It won't override the world PP, " +
                         "Default options will override with the respective configurations, Custom will override with a custom config with the defaults " +
                         "matching the device you're using. Off is disabled PP.");
    }

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Load icons
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "Icon",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Off",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Off.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Original",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Original.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Override",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Override.png"));

        _page = new BTKUILib.UIObjects.Page(nameof(PostProcessingOverrides), nameof(PostProcessingOverrides), true, "Icon") {
            MenuTitle = nameof(PostProcessingOverrides),
            MenuSubtitle = "Configure Post Processing Overrides per world",
        };

        if (PostProcessingOverrides.IsWorldLoaded())
            SetupOverrideButtons();

        // Since updating buttons is bugged currently in BTKUI I need to recreate the whole menu ;_;
        // Todo: Implement Input updates instead of recreating, waiting on: https://github.com/BTK-Development/BTKUILib/issues/8
        PostProcessingOverrides.ConfigChanged += SetupOverrideButtons;
    }

    private static string GetTripleToggleIcon(OverrideSetting setting) {
        return setting switch {
            OverrideSetting.Off => "TT_Off",
            OverrideSetting.Original => "TT_Original",
            OverrideSetting.Override => "TT_Override",
            _ => ""
        };
    }

    private static void CreateButton(BTKUILib.UIObjects.Category currentOverride, PostProcessingOverrides.JsonConfigPPSetting setting, string settingName) {
        currentOverride.AddButton(settingName, GetTripleToggleIcon(setting.Active), $"{settingName} Control").OnPress += () => {
            setting.Active = (OverrideSetting)(((int)setting.Active + 1) % SettingCount);
            PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        };
    }

    private static void SetupOverrideButtons() {

        _page.ClearChildren();

        var currentWorld = _page.AddCategory("Current World");

        var config = PostProcessingOverrides.GetCurrentConfigSettings();
        PostProcessingOverrides.JsonConfigPPSettings current = null;

        var overrideButton = currentWorld.AddButton($"PostProcessing: {config.ConfigType.ToString()}", "",
            "Which override type should we use? Original: Original Post processing of the world. " +
            "Global: Uses your Global override. Custom: Override specific for this world. Off: Disables PP.");

        overrideButton.OnPress += () => {
            var nextType = (OverrideType)(((int)config.ConfigType + 1) % TypeCount);
            config.ConfigType = nextType;
            PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        };

        BTKUILib.UIObjects.Category currentOverride = null;

        switch (config.ConfigType) {

            case OverrideType.Original:
            case OverrideType.Off:
                return;

            case OverrideType.Global:
                currentOverride = _page.AddCategory("Global Override");
                current = PostProcessingOverrides.Config.Global;
                break;

            case OverrideType.Custom:
                currentOverride = _page.AddCategory("Custom Override");
                current = config.CustomConfig;
                break;
        }

        // Toggles for the overrides
        CreateButton(currentOverride, current!.Bloom, "Bloom");
        CreateButton(currentOverride, current.AO, "Ambient Occlusion");
        CreateButton(currentOverride, current.ColorGrading, "Color Grading");
        CreateButton(currentOverride, current.AutoExposure, "Auto Exposure");
        CreateButton(currentOverride, current.ChromaticAberration, "Chromatic Aberration");
        CreateButton(currentOverride, current.DepthOfField, "Depth of Field");
        CreateButton(currentOverride, current.Grain, "Grain");
        CreateButton(currentOverride, current.LensDistortion, "Lens Distortion");
        CreateButton(currentOverride, current.MotionBlur, "Motion Blur");
        CreateButton(currentOverride, current.SpaceReflections, "Space Reflections");
        CreateButton(currentOverride, current.Vignette, "Vignette");

        // Sliders
        currentOverride.AddSlider("Bloom Intensity", "Set the Bloom Intensity", current.Bloom.Intensity/Multiplier, 0f, 5f, 2).OnValueUpdated += newValue => {
            current.Bloom.Intensity = newValue * Multiplier;
            PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        };
        currentOverride.AddSlider("Bloom Threshold", "Set the Bloom Threshold", current.Bloom.Threshold, 0f, 5f, 2).OnValueUpdated += newValue => {
            current.Bloom.Threshold = newValue;
            PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        };

        // currentOverride.AddSlider("ColorGrading Hue Shift", "Set the ColorGrading Hue Shift", current.ColorGrading.HueShift, 0f, 360f, 2).OnValueUpdated += newValue => {
        //     current.ColorGrading.HueShift = newValue;
        //     PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        // };
        // Button hdrButton = currentOverride.AddButton($"ColorGrading Mode: {current.ColorGrading.GradingMode.ToString()}", "", $"ColorGrading Grading Mode", ButtonStyle.TextOnly);
        // hdrButton.OnPress += () => {
        //     current.ColorGrading.GradingMode = (GradingMode)(((int)current.ColorGrading.GradingMode + 1) % Enum.GetValues(typeof(GradingMode)).Length);
        //     hdrButton.ButtonText = $"ColorGrading Mode: {current.ColorGrading.GradingMode.ToString()}";
        //     PostProcessingOverrides.SaveConfigAndApplyChanges(true);
        // };
    }
}
