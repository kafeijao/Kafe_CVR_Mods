using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Social;
using BTKUILib.UIObjects;
using MelonLoader;
using UnityEngine;

namespace Kafe.RequestLib;

internal static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeOnlyReceiveFromFriends;
    internal static MelonPreferences_Entry<bool> MeHudNotificationOnAutoAccept;
    internal static MelonPreferences_Entry<bool> MeHudNotificationOnAutoDecline;

    // Embed resources
    internal static string CVRTestJSPatchesContent;
    private const string CVRTestJSPatches = "cohtml.cvrtest.patches.js";
    internal static string CVRUIJSPatchesContent;
    private const string CVRUIJSPatches = "cohtml.cvrui.patches.js";
    
    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(RequestLib));

        MeOnlyReceiveFromFriends = _melonCategory.CreateEntry("OnlyReceiveFromFriends", false,
            description: "Whether only receive requests from friends or not.");

        MeHudNotificationOnAutoAccept = _melonCategory.CreateEntry("HudNotificationOnAutoAccept", true,
            description: "Whether to send a HUD notification whenever auto accepts a request or not.");

        MeHudNotificationOnAutoDecline = _melonCategory.CreateEntry("HudNotificationOnAutoDecline", true,
            description: "Whether to send a HUD notification whenever auto declines a request or not.");

    }

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void AddMelonToggle(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<bool> entry, string nameOverride = "") {
        var toggle = category.AddToggle(string.IsNullOrWhiteSpace(nameOverride) ? entry.DisplayName : nameOverride, entry.Description, entry.Value);
        toggle.OnValueUpdated += b => {
            if (b != entry.Value) entry.Value = b;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue != toggle.ToggleValue) toggle.ToggleValue = newValue;
        });
    }

    private static void AddMelonSlider(BTKUILib.UIObjects.Category category, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces) {
        var slider = category.AddSlider(entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }

    private const string IconAutoAccept = $"{nameof(RequestLib)}-AutoAccept";
    private const string IconAutoDecline = $"{nameof(RequestLib)}-AutoDecline";
    private const string IconDefault = $"{nameof(RequestLib)}-Default";
    private const string IconLetMeDecide = $"{nameof(RequestLib)}-LetMeDecide";

    private static BTKUILib.UIObjects.Components.Button GetOverrideButton(BTKUILib.UIObjects.Category cat, string buttonName, ConfigJson.UserOverride userOverride, string playerName = "", string modName = "") {
        var sourceInfo = string.IsNullOrWhiteSpace(playerName) ? " " : $" from {playerName} ";
        sourceInfo = string.IsNullOrWhiteSpace(modName) ? sourceInfo : $"{sourceInfo} via {modName} ";
        var buttonTooltip = userOverride switch {
            ConfigJson.UserOverride.Default => "Default - Checks player global setting, and global setting, if still default picks LetMeDecide.",
            ConfigJson.UserOverride.LetMeDecide => $"Let Me Decide - The requests{sourceInfo}will be prompt to you, similarly to invites.",
            ConfigJson.UserOverride.AutoAccept => $"Auto Accept - The requests{sourceInfo}will be automatically accepted.",
            ConfigJson.UserOverride.AutoDecline => $"Auto Decline - The requires{sourceInfo}will be automatically declined.",
            _ => "N/A"
        };
        var buttonIcon = userOverride switch {
            ConfigJson.UserOverride.Default => IconDefault,
            ConfigJson.UserOverride.LetMeDecide => IconLetMeDecide,
            ConfigJson.UserOverride.AutoAccept => IconAutoAccept,
            ConfigJson.UserOverride.AutoDecline => IconAutoDecline,
            _ => "N/A"
        };
        return cat.AddButton(buttonName, buttonIcon, buttonTooltip);
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Load icons
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RequestLib), IconAutoAccept, Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.Quadro_Toggle_AutoAccept.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RequestLib), IconAutoDecline, Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.Quadro_Toggle_AutoDecline.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RequestLib), IconDefault, Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.Quadro_Toggle_Default.png"));
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RequestLib), IconLetMeDecide, Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.Quadro_Toggle_LetMeDecide.png"));

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(RequestLib), nameof(RequestLib));

        AddMelonToggle(miscCategory, MeOnlyReceiveFromFriends, "Only from friends");

        void CreateGlobalOverrideButton() {
            var globalOverrideButton = GetOverrideButton(miscCategory, "Global Setting", ConfigJson.GetCurrentOverride());
            globalOverrideButton.OnPress += () => {
                ConfigJson.SwapOverride();
                globalOverrideButton.Delete();
                CreateGlobalOverrideButton();
            };
        }

        CreateGlobalOverrideButton();

        // Player visibility overrides
        var playerCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(RequestLib), nameof(RequestLib));
        Page playerModPage = null;

        void PopulatePlayerCategory(string playerName, string playerID) {

            playerCat.ClearChildren();

            // If we're not friends, let's show a quick way to toggle to receive stuff from non-friends
            if (!Friends.FriendsWith(playerID)) {

                playerCat.AddToggle("Only from friends", "Whether to receive request only from friends or not.",
                        MeOnlyReceiveFromFriends.Value).OnValueUpdated += onlyFromFriends => {
                    MeOnlyReceiveFromFriends.Value = onlyFromFriends;
                    PopulatePlayerCategory(playerName, playerID);
                };

                if (MeOnlyReceiveFromFriends.Value) return;
            }

            GetOverrideButton(playerCat, "Player Setting", ConfigJson.GetCurrentOverride(playerID), playerName).OnPress += () => {
                ConfigJson.SwapOverride(playerID, playerName);
                PopulatePlayerCategory(playerName, playerID);
            };

            playerModPage?.Delete();
            playerModPage = playerCat.AddPage("Per Mod Settings", "", $"Per mod settings for {playerName}", nameof(RequestLib));
            var playerModCat = playerModPage.AddCategory("");

            // Add clear mod settings button
            playerCat.AddButton("Clear Mod Settings", "", "Clears all mod specific settings.").OnPress += () => {
                ConfigJson.ClearModOverrides(playerID);
            };

            void PopulatePlayerModPage() {
                playerModCat.ClearChildren();

                // Make a list of all the available mods and turn them into buttons
                var availableMods = new SortedSet<string>();
                availableMods.UnionWith(API.RemotePlayerMods.TryGetValue(playerID, out var playerMods) ? playerMods : Array.Empty<string>());
                availableMods.UnionWith(ConfigJson.GetCurrentOverriddenMods(playerID));
                foreach (var availableMod in availableMods) {
                    GetOverrideButton(playerModCat, availableMod, ConfigJson.GetCurrentOverride(playerID, availableMod), playerName, availableMod).OnPress += () => {
                        ConfigJson.SwapOverride(playerID, playerName, availableMod);
                        PopulatePlayerModPage();
                    };
                }
            }

            playerModPage.SubpageButton.OnPress += PopulatePlayerModPage;
        }

        BTKUILib.QuickMenuAPI.OnPlayerSelected += PopulatePlayerCategory;
    }

     public static void LoadAssemblyResources(Assembly assembly) {

        try {
            using var resourceStream = assembly.GetManifestResourceStream(CVRTestJSPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {CVRTestJSPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            CVRTestJSPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the assembly resource");
            MelonLogger.Error(ex);
        }

        try {
            using var resourceStream = assembly.GetManifestResourceStream(CVRUIJSPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {CVRUIJSPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            CVRUIJSPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the assembly resource");
            MelonLogger.Error(ex);
        }

    }

}
