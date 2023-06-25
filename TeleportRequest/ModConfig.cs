using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.MovementSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.TeleportRequest;

public static class ModConfig {

    // Melon Prefs
    // private static MelonPreferences_Category _melonCategory;
    // internal static MelonPreferences_Entry<TeleportRequest.AutoAcceptPolicy> MeGlobalAutoAcceptPolicy;

    public static void InitializeMelonPrefs() {

        // Melon Config
        // _melonCategory = MelonPreferences.CreateCategory(nameof(TeleportRequest));

        // MeGlobalAutoAcceptPolicy = _melonCategory.CreateEntry("GlobalAutoAcceptPolicy", TeleportRequest.AutoAcceptPolicy.Off,
        //     description: "What policy should be used for Auto Attempting teleport requests.");
    }

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void AddMelonToggle(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<bool> entry) {
        var toggle = category.AddToggle(entry.DisplayName, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => {
            if (b != entry.Value) entry.Value = b;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue != toggle.ToggleValue) toggle.ToggleValue = newValue;
        });
    }

    private static void AddMelonSlider(BTKUILib.UIObjects.Page page, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces) {
        var slider = page.AddSlider(entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        // var miscCategory = miscPage.AddCategory(nameof(TeleportRequest));
        //
        // var modPage = miscCategory.AddPage($"{nameof(TeleportRequest)} Settings", "", $"Configure the settings for {nameof(TeleportRequest)}.", nameof(TeleportRequest));
        // modPage.MenuTitle = $"{nameof(TeleportRequest)} Settings";
        //
        // var modSettingsCategory = modPage.AddCategory("Settings");
        //
        // AddMelonToggle(modSettingsCategory, MeOnlyViewFriends);

        var playerCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(TeleportRequest));

        BTKUILib.QuickMenuAPI.OnPlayerSelected += (playerName, playerID) => {
            playerCat.ClearChildren();
            if (RequestLib.API.HasRequestLib(playerID)) {
                if (MovementSystem.Instance.canFly) {
                    var teleportButton = playerCat.AddButton("Request to Teleport", "", $"Send a request to teleport to the {playerName}");
                    teleportButton.OnPress += () => TeleportRequest.RequestToTeleport(playerName, playerID);
                }
                else {
                    playerCat.AddButton("World doesn't allow Flight", "", $"This world doesn't allow flight, so we can't request :(");
                }
            }
            else {
                playerCat.AddButton("Has no RequestLib", "", $"This player doesn't have the RequestLib, so we can't request :(");
            }
        };
    }

}
