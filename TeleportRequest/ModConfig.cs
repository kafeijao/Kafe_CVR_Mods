using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.Movement;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.TeleportRequest;

public static class ModConfig {

    internal static bool HasChatBoxMod = false;

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeEnableChatBoxIntegration;
    internal static MelonPreferences_Entry<bool> MeShowCommandsOnChatBox;
    internal static MelonPreferences_Entry<bool> MeShowHudMessages;

    internal static MelonPreferences_Entry<string> MeCommandTeleportRequest;
    internal static MelonPreferences_Entry<string> MeCommandTeleportAccept;
    internal static MelonPreferences_Entry<string> MeCommandTeleportDecline;
    internal static MelonPreferences_Entry<string> MeCommandTeleportBack;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(TeleportRequest));

        MeEnableChatBoxIntegration = _melonCategory.CreateEntry("EnableChatBoxIntegration", true,
            description: "Whether the ChatBox is listening to the commands or not.");
        MeShowHudMessages = _melonCategory.CreateEntry("ShowHudMessages", true,
            description: "Whether to show Hud messages with information about the requests or not.");
        MeShowCommandsOnChatBox = _melonCategory.CreateEntry("ShowCommandsOnChatBox", true,
            description: "Whether to show the commands on the ChatBox (local and remote players).");

        MeCommandTeleportRequest = _melonCategory.CreateEntry("CommandTeleportRequest", "/tpr",
            description: "Command to be used to request to teleport to a player. Default: /tpr");
        MeCommandTeleportAccept = _melonCategory.CreateEntry("CommandTeleportAccept", "/tpa",
            description: "Command to be used to request to teleport to a player. Default: /tpa");
        MeCommandTeleportDecline = _melonCategory.CreateEntry("CommandTeleportDecline", "/tpd",
            description: "Command to be used to request to teleport to a player. Default: /tpd");
        MeCommandTeleportBack = _melonCategory.CreateEntry("CommandTeleportBack", "/tpb",
            description: "Command to be used to request to teleport back to a previous location. Default: /tpb");
    }

    public static void InitializeBTKUI() {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void AddMelonToggle(Category category, MelonPreferences_Entry<bool> entry, string nameOverride = "") {
        var toggle = category.AddToggle(string.IsNullOrWhiteSpace(nameOverride) ? entry.DisplayName : nameOverride, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => {
            if (b != entry.Value) entry.Value = b;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue != toggle.ToggleValue) toggle.ToggleValue = newValue;
        });
    }

    private static void AddMelonSlider(Category category, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces) {
        var slider = category.AddSlider(entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        var miscPage = QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(TeleportRequest), nameof(TeleportRequest));

        if (HasChatBoxMod) {
            AddMelonToggle(miscCategory, MeEnableChatBoxIntegration, "ChatBox Integration");
            AddMelonToggle(miscCategory, MeShowCommandsOnChatBox, "Show Commands on ChatBox");
        }

        Button goBackButton = null;
        void HandleGoBackButton() {
            goBackButton?.Delete();
            var goBackCount = TeleportRequest.GoBackCount();
            if (goBackCount > 0) {
                goBackButton = miscCategory.AddButton($"Go Back [{goBackCount}]", "",
                    $"Goes back to the location before teleporting to a player, there's {goBackCount} previous locations saved.");
                goBackButton.OnPress += TeleportRequest.TeleportBack;
            }
            else {
                goBackButton = miscCategory.AddButton("Go Back [Not Available]", "", "Can't teleport " +
                                                                      "back since there is no previous locations to teleport to :(");
            }
        }

        TeleportRequest.PreviousTeleportLocationsChanged += HandleGoBackButton;
        HandleGoBackButton();

        var playerCat = QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(TeleportRequest));

        QuickMenuAPI.OnPlayerSelected += (playerName, playerID) => {
            playerCat.ClearChildren();
            if (RequestLib.API.HasRequestLib(playerID)) {
                if (BetterBetterCharacterController.Instance.CanFly()) {
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
