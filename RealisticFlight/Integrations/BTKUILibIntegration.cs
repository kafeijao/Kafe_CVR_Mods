using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight.Integrations;

public static class BTKUILibIntegration {

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += LoadBTKUILib;
    }

    private static void LoadBTKUILib(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= LoadBTKUILib;

        const string icon = $"{nameof(RealisticFlight)}-icon";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RealisticFlight), icon,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(RealisticFlight), nameof(RealisticFlight));

        AddMelonToggle(miscCategory, ModConfig.MeCustomFlightInDesktop, "Enabled in Desktop");
        AddMelonToggle(miscCategory, ModConfig.MeCustomFlightInVR, "Enabled in VR");
        AddMelonToggle(miscCategory, ModConfig.MeBothArmsDownToStopGliding, "Arms down to stop Gliding");
        AddMelonToggle(miscCategory, ModConfig.MeBothArmsUpToStopGliding, "Arms up to stop Gliding");

        var advancedPage = miscCategory.AddPage("Advanced Settings", icon, "Advanced settings for realistic flight.", nameof(RealisticFlight));
        var general = advancedPage.AddCategory("General");
        AddMelonValuePicker(general, ModConfig.MeFlapMultiplier, "Set Flap Multiplier");
        AddMelonValuePicker(general, ModConfig.MeFlapMultiplierHorizontal, "Set Horizontal Flap Multiplier");

        BTKUILib.UIObjects.Components.ToggleButton globalEnableSetting = null;
        BTKUILib.UIObjects.Category avatarCategory = null;

        var useOverrides = AddMelonToggle(general, ModConfig.MeUseAvatarOverrides, "Use Avatar Overrides");

        void GenerateAdvancedPage() {
            globalEnableSetting?.Delete();
            avatarCategory?.Delete();

            if (!ModConfig.MeUseAvatarOverrides.Value) return;

            globalEnableSetting = general.AddToggle("Global Default Enabled", "Whether the default Enabled value for avatars is true or false.", ConfigJson.GetGlobalEnabled());
            globalEnableSetting.OnValueUpdated += ConfigJson.SetGlobalEnabled;

            avatarCategory = advancedPage.AddCategory("Current Avatar");

            var overrideButton = avatarCategory.AddToggle("Override", "Whether to override the settings for this avatar or not", ConfigJson.GetCurrentAvatarOverriding());
            overrideButton.OnValueUpdated += isOn => {
                ConfigJson.SetCurrentAvatarOverriding(isOn);
                GenerateAdvancedPage();
            };
            if (!ConfigJson.GetCurrentAvatarOverriding()) return;

            var enabledButton = avatarCategory.AddToggle("Enable", "Whether to enable the realistic for this avatar or not", ConfigJson.GetCurrentAvatarEnabled());
            enabledButton.OnValueUpdated += isOn => {
                ConfigJson.SetCurrentAvatarEnabled(isOn);
                GenerateAdvancedPage();
            };
            if (!ConfigJson.GetCurrentAvatarEnabled()) return;

            var flapButton = avatarCategory.AddButton($"Flap Modifier [{ConfigJson.GetCurrentAvatarFlapModifier()}]", "", "Value of flap modifier for this avatar.");
            flapButton.OnPress += () => {
                BTKUILib.QuickMenuAPI.OpenNumberInput("Flap Modifier", ConfigJson.GetCurrentAvatarFlapModifier(), newValue => {
                    ConfigJson.SetCurrentAvatarFlapModifier(newValue);
                    GenerateAdvancedPage();
                });
            };
        }

        useOverrides.OnValueUpdated += _ => GenerateAdvancedPage();
        advancedPage.SubpageButton.OnPress += GenerateAdvancedPage;
        GenerateAdvancedPage();
    }

    private static BTKUILib.UIObjects.Components.ToggleButton AddMelonToggle(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<bool> entry, string overrideName = null) {
        var toggle = category.AddToggle(overrideName ?? entry.DisplayName, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => {
            if (b != entry.Value) entry.Value = b;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue != toggle.ToggleValue) toggle.ToggleValue = newValue;
        });
        return toggle;
    }

    private static BTKUILib.UIObjects.Components.Button AddMelonValuePicker(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<float> entry, string overrideName = null) {
        var pickerName = overrideName ?? entry.DisplayName;
        var button = category.AddButton($"{pickerName} [{entry.Value}]", "", entry.Description);
        button.OnPress += () => {
            BTKUILib.QuickMenuAPI.OpenNumberInput(pickerName, entry.Value, newValue => {
                if (!Mathf.Approximately(newValue, entry.Value)) {
                    entry.Value = newValue;
                }
            });
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            button.ButtonText = $"{pickerName} [{newValue}]";
        });
        return button;
    }

    private static void AddMelonSlider(BTKUILib.UIObjects.Page page, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces, string overrideName = null) {
        var slider = page.AddSlider(overrideName ?? entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }
}
