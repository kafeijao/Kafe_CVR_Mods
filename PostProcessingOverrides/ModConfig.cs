using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using dotmore.Collections.Generic;
using MelonLoader;

namespace Kafe.PostProcessingOverrides;

public static class ModConfig
{
    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<OverrideType> MeDefaultWorldConfig;

    // BTKUI Stuff
    private static Page _page;

    private static Category _worldCategory;
    private static CycleButton _worldTypeButton;
    private static Category _settingsCategory;
    private static readonly List<Action<PostProcessingOverrides.JsonConfigPPSettings>> EffectButtonUpdaters = new();
    private static SliderFloat _bloomIntensity;
    private static SliderFloat _bloomThreshold;

    // Internal
    private static readonly int SettingCount = Enum.GetValues(typeof(OverrideSetting)).Length;

    public static void InitializeMelonPrefs()
    {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(PostProcessingOverrides));

        MeDefaultWorldConfig = _melonCategory.CreateEntry("DefaultWorldConfig", OverrideType.Original,
            description:
            "When joining a world for the first time which configuration should be used? Original = It won't override the world PP, " +
            "Default options will override with the respective configurations, Custom will override with a custom config with the defaults " +
            "matching the device you're using. Off is disabled PP.");
    }

    public static void InitializeBTKUI()
    {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager)
    {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Load icons
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "Icon",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Off",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Off.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Original",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Original.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Override",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Override.png"));

        _page = new Page(nameof(PostProcessingOverrides), nameof(PostProcessingOverrides), true, "Icon")
        {
            MenuTitle = nameof(PostProcessingOverrides),
            MenuSubtitle = "Configure Post Processing Overrides per world",
        };

        _worldCategory = _page.AddCategory("Current World");
        _worldTypeButton = _worldCategory.AddCycleButton(new OrderedDictionary<string, string>
            {
                { nameof(OverrideType.Original), GetTripleToggleIcon(OverrideSetting.Original) },
                { nameof(OverrideType.Global), GetTripleToggleIcon(OverrideSetting.Override) },
                { nameof(OverrideType.Custom), GetTripleToggleIcon(OverrideSetting.Override) },
                { nameof(OverrideType.Off), GetTripleToggleIcon(OverrideSetting.Off) },
            },
            "Which override type should we use? Original: Original Post processing of the world. " +
            "Global: Uses your Global override, shared by every world set to Global. " +
            "Custom: Override specific for this world. Off: Disables PP.");
        _worldTypeButton.OnChange += index =>
        {
            var worldConfig = PostProcessingOverrides.GetCurrentWorldConfig();
            if (worldConfig == null) return;
            worldConfig.ConfigType = (OverrideType)index;
            PostProcessingOverrides.SaveConfigAndApplyChanges();
            UpdateMenu();
        };

        _settingsCategory = _page.AddCategory("Overrides");
        CreateEffectButton("Bloom", s => s.Bloom, true);
        CreateEffectButton("Ambient Occlusion", s => s.AO);
        CreateEffectButton("Color Grading", s => s.ColorGrading);
        CreateEffectButton("Auto Exposure", s => s.AutoExposure);
        CreateEffectButton("Chromatic Aberration", s => s.ChromaticAberration);
        CreateEffectButton("Depth of Field", s => s.DepthOfField);
        CreateEffectButton("Grain", s => s.Grain);
        CreateEffectButton("Lens Distortion", s => s.LensDistortion);
        CreateEffectButton("Motion Blur", s => s.MotionBlur);
        CreateEffectButton("Vignette", s => s.Vignette);

        var defaultBloom = new PostProcessingOverrides.JsonConfigPPSettingBloom();

        _bloomIntensity = _settingsCategory.AddSlider("Bloom Intensity", "Set the Bloom Intensity",
            defaultBloom.Intensity, 0f, 20f, 2, defaultBloom.Intensity, true);
        // Only saves and applies, updating the menu here would fight the slider while it's being dragged
        _bloomIntensity.OnValueUpdated += newValue => UpdateShownSettings(s => s.Bloom.Intensity = newValue);

        _bloomThreshold = _settingsCategory.AddSlider("Bloom Threshold", "Set the Bloom Threshold",
            defaultBloom.Threshold, 0f, 5f, 2, defaultBloom.Threshold, true);
        _bloomThreshold.OnValueUpdated += newValue => UpdateShownSettings(s => s.Bloom.Threshold = newValue);

        UpdateMenu();
        PostProcessingOverrides.CurrentWorldChanged += UpdateMenu;
    }

    private static string GetTripleToggleIcon(OverrideSetting setting)
    {
        return setting switch
        {
            OverrideSetting.Off => "TT_Off",
            OverrideSetting.Original => "TT_Original",
            OverrideSetting.Override => "TT_Override",
            _ => "",
        };
    }

    /// <summary>
    /// Returns the setting to show on the button. Effects that can't be overridden show Override as Original
    /// </summary>
    private static OverrideSetting GetShownSetting(OverrideSetting setting, bool canOverride)
    {
        return !canOverride && setting == OverrideSetting.Override ? OverrideSetting.Original : setting;
    }

    /// <summary>
    /// Adds a button that cycles an effect between Off, Original and Override when it can be overridden
    /// </summary>
    private static void CreateEffectButton(
        string effectName,
        Func<PostProcessingOverrides.JsonConfigPPSettings, PostProcessingOverrides.JsonConfigPPSetting> getSetting,
        bool canOverride = false)
    {
        var tooltip = canOverride
            ? $"{effectName}: Off, Original from the world, or Override with the values below"
            : $"{effectName}: Off, or Original from the world";
        var button = _settingsCategory.AddButton(effectName, GetTripleToggleIcon(OverrideSetting.Original), tooltip);
        var optionCount = canOverride ? SettingCount : SettingCount - 1;

        button.OnPress += () => UpdateShownSettings(settings =>
        {
            var setting = getSetting(settings);
            var next = ((int)GetShownSetting(setting.Active, canOverride) + 1) % optionCount;
            setting.Active = (OverrideSetting)next;
            UpdateMenu();
        });

        EffectButtonUpdaters.Add(settings =>
            button.ButtonIcon = GetTripleToggleIcon(GetShownSetting(getSetting(settings).Active, canOverride)));
    }

    /// <summary>
    /// Returns the Global or Custom settings of the current world, or null when there are none to edit
    /// </summary>
    private static PostProcessingOverrides.JsonConfigPPSettings GetShownSettings()
    {
        var worldConfig = PostProcessingOverrides.GetCurrentWorldConfig();
        return worldConfig == null ? null : PostProcessingOverrides.GetUsedSettings(worldConfig);
    }

    /// <summary>
    /// Changes the shown settings, then saves and applies them
    /// </summary>
    private static void UpdateShownSettings(Action<PostProcessingOverrides.JsonConfigPPSettings> change)
    {
        var settings = GetShownSettings();
        if (settings == null) return;
        change(settings);
        PostProcessingOverrides.SaveConfigAndApplyChanges();
    }

    /// <summary>
    /// Updates the menu elements to show the current world settings
    /// </summary>
    private static void UpdateMenu()
    {
        var worldConfig = PostProcessingOverrides.GetCurrentWorldConfig();
        _worldCategory.Hidden = worldConfig == null;
        if (worldConfig != null) _worldTypeButton.SetOption((int)worldConfig.ConfigType);

        var settings = GetShownSettings();
        _settingsCategory.Hidden = settings == null;
        if (settings == null) return;

        _settingsCategory.CategoryName = worldConfig!.ConfigType == OverrideType.Global
            ? "Global Override"
            : "Custom Override";

        foreach (var updateButton in EffectButtonUpdaters)
            updateButton(settings);

        var showBloomSliders = settings.Bloom.Active == OverrideSetting.Override;
        _bloomIntensity.SetSliderValue(settings.Bloom.Intensity);
        _bloomThreshold.SetSliderValue(settings.Bloom.Threshold);
        _bloomIntensity.Hidden = !showBloomSliders;
        _bloomThreshold.Hidden = !showBloomSliders;
    }
}
