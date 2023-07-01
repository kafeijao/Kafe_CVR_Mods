using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using BTKUILib.UIObjects.Components;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace Kafe.ChatBox;

public static class ModConfig {

    public const float MessageTimeoutMin = 5f;
    private const float MessageTimeoutMax = 90f;

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeOnlyViewFriends;
    internal static MelonPreferences_Entry<bool> MeSoundOnStartedTyping;
    internal static MelonPreferences_Entry<bool> MeSoundOnMessage;
    internal static MelonPreferences_Entry<bool> MeMessageMentionGlobalAudio;
    internal static MelonPreferences_Entry<float> MeSoundsVolume;
    internal static MelonPreferences_Entry<float> MeNotificationSoundMaxDistance;
    internal static MelonPreferences_Entry<float> MeMessageTimeoutSeconds;
    internal static MelonPreferences_Entry<bool> MeMessageTimeoutDependsLength;
    internal static MelonPreferences_Entry<float> MeChatBoxOpacity;
    internal static MelonPreferences_Entry<float> MeChatBoxSize;
    internal static MelonPreferences_Entry<bool> MeIgnoreOscMessages;
    internal static MelonPreferences_Entry<bool> MeIgnoreModMessages;

    internal static MelonPreferences_Entry<bool> MeShowHistoryWindow;
    internal static MelonPreferences_Entry<bool> MeHistoryWindowOnCenter;
    internal static MelonPreferences_Entry<bool> MeHistoryWindowOpened;
    internal static MelonPreferences_Entry<float> MeHistoryFontSize;

    internal static MelonPreferences_Entry<bool> MeProfanityFilter;


    // Asset Bundle
    public static GameObject ChatBoxPrefab;
    public static GameObject ChatBoxHistoryPrefab;
    private const string ChatBoxAssetBundleName = "chatbox.assetbundle";
    private const string ChatBoxPrefabAssetPath = "Assets/Chatbox/ChatBox.prefab";
    private const string ChatBoxHistoryPrefabAssetPath = "Assets/Chatbox/History.prefab";

    internal static string JavascriptPatchesContent;
    private const string ChatBoxJsPatches = "chatbox.cohtml.cvrtest.ui.patches.js";

    // Files
    internal enum Sound {
        Typing,
        Message,
        MessageMention,
    }

    private const string ChatBoxSoundTyping = "chatbox.sound.typing.wav";
    private const string ChatBoxSoundMessage = "chatbox.sound.message.wav";
    private const string ChatBoxSoundMessageMention = "chatbox.sound.message.mention.wav";

    private static readonly Dictionary<Sound, string> AudioClipResourceNames = new() {
        {Sound.Typing, ChatBoxSoundTyping},
        {Sound.Message, ChatBoxSoundMessage},
        {Sound.MessageMention, ChatBoxSoundMessageMention},
    };

    internal static readonly Dictionary<Sound, AudioClip> AudioClips = new();

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(ChatBox));

        MeOnlyViewFriends = _melonCategory.CreateEntry("OnlyViewFriends", false,
            description: "Whether only show ChatBoxes on friends or not.");

        MeSoundOnStartedTyping = _melonCategory.CreateEntry("SoundOnStartedTyping", true,
            description: "Whether there should be a sound when someone starts typing or not.");

        MeSoundOnMessage = _melonCategory.CreateEntry("SoundOnMessage", true,
            description: "Whether there should be a sound when someone sends a message or not.");

        MeMessageMentionGlobalAudio = _melonCategory.CreateEntry("MessageMentionGlobalAudio", true,
            description: "Whether the sound when someone mentions your username in a message should be global or not.");

        MeSoundsVolume = _melonCategory.CreateEntry("SoundsVolume", 0.5f,
            description: "The volume of the sounds for the notification of typing/messages. Goes from 0 to 1.");

        MeNotificationSoundMaxDistance = _melonCategory.CreateEntry("NotificationSoundMaxDistance", 5f,
            description: "The distance where the notification sounds completely cuts off.");

        MeMessageTimeoutSeconds = _melonCategory.CreateEntry("MessageTimeoutSeconds", 30f,
            description: "How long should a message stay on top of a player's head after written.");

        MeMessageTimeoutDependsLength = _melonCategory.CreateEntry("MessageTimeoutDependsLength", true,
            description: "Whether the message timeout depends on the message length or not.");

        MeChatBoxOpacity = _melonCategory.CreateEntry("ChatBoxOpacity", 1f,
            description: "The opacity of the Chat Box, between 0 (invisible) and 1 (opaque).");

        MeChatBoxSize = _melonCategory.CreateEntry("ChatBoxSize", 1f,
            description: "The size of the Chat Box, between 0 (smallest) and 2 (biggest). The default is 0.5");

        MeIgnoreOscMessages = _melonCategory.CreateEntry("IgnoreOscMessages", false,
            description: "Whether to ignore messages sent via OSC or not.");

        MeIgnoreModMessages = _melonCategory.CreateEntry("IgnoreModMessages", false,
            description: "Whether to ignore messages sent via other Mods or not.");

        MeHistoryWindowOnCenter = _melonCategory.CreateEntry("HistoryWindowOnCenter", true,
            description: "Whether to show the history window on the center of the Quick Menu or not.");

        MeShowHistoryWindow = _melonCategory.CreateEntry("ShowHistoryWindow", true,
            description: "Whether to show the history window or not.");

        MeHistoryWindowOpened = _melonCategory.CreateEntry("HistoryWindowOpened", true,
            description: "Whether the history window is opened or not.");

        MeHistoryFontSize = _melonCategory.CreateEntry("HistoryFontSize", 32f,
            description: "The size of the font in the history window. Default is 32.");

        MeProfanityFilter = _melonCategory.CreateEntry("ProfanityFilter", true,
            description: "Whether to use the profanity filter or not.");
    }

    public static void LoadAssemblyResources(Assembly assembly) {

        try {

            using var resourceStream = assembly.GetManifestResourceStream(ChatBoxAssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {ChatBoxAssetBundleName}!");
                return;
            }
            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load ChatBox Prefab
            ChatBoxPrefab = assetBundle.LoadAsset<GameObject>(ChatBoxPrefabAssetPath);
            ChatBoxPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            ChatBoxHistoryPrefab = assetBundle.LoadAsset<GameObject>(ChatBoxHistoryPrefabAssetPath);
            ChatBoxHistoryPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }

        // Load/Create the sound files
        foreach (var audioClipResourceName in AudioClipResourceNames) {
            try {

                using var resourceStream = assembly.GetManifestResourceStream(audioClipResourceName.Value);

                // Create the directory if non-existent
                var audioPath = Path.GetFullPath(Path.Combine("UserData", nameof(ChatBox), audioClipResourceName.Value));
                var audioFile = new FileInfo(audioPath);
                audioFile.Directory?.Create();

                // If there is no audio file, write the default
                if (!audioFile.Exists) {
                    MelonLogger.Msg($"Saving default sound file to {audioFile.FullName}...");
                    using var fileStream = File.Open(audioPath, FileMode.Create, FileAccess.Write);
                    resourceStream!.CopyTo(fileStream);
                }

                // Read the sound file from disk
                MelonLogger.Msg($"Reading sound file from disk: {audioFile.FullName}");
                using var uwr = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.WAV);
                uwr.SendWebRequest();

                // I want this sync, should be fast since we're loading from the disk and not the webs
                while (!uwr.isDone) {}
                if (uwr.isNetworkError || uwr.isHttpError) {
                    MelonLogger.Error($"{uwr.error}");
                }
                else {
                    AudioClips[audioClipResourceName.Key] = DownloadHandlerAudioClip.GetContent(uwr);
                }
            }
            catch (Exception ex) {
                MelonLogger.Error($"Failed to Load the Audio Clips\n" + ex.Message);
            }
        }

        try {
            using var resourceStream = assembly.GetManifestResourceStream(ChatBoxJsPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {ChatBoxJsPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            JavascriptPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the resource: " + ex.Message);
        }

    }

    internal static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
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

    private static void AddMelonSlider(BTKUILib.UIObjects.Page page, MelonPreferences_Entry<float> entry, float min, float max, int decimalPlaces, string overrideName = null) {
        var slider = page.AddSlider(overrideName ?? entry.DisplayName, entry.Description, entry.Value, min, max, decimalPlaces);
        slider.OnValueUpdated += f => {
            if (!Mathf.Approximately(f, entry.Value)) entry.Value = f;
        };
        entry.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!Mathf.Approximately(newValue, slider.SliderValue)) slider.SetSliderValue(newValue);
        });
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Load icons
        const string iconMsg = $"{nameof(ChatBox)}-Msg";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconMsg,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Msg.png"));
        const string iconMsgSettings = $"{nameof(ChatBox)}-Msg_Settings";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconMsgSettings,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Msg_Settings.png"));
        const string iconMsgWindow = $"{nameof(ChatBox)}-Msg_Window";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconMsgWindow,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Msg_Window.png"));

        const string iconAdd = $"{nameof(ChatBox)}-Add";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconAdd,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Add.png"));
        const string iconDelete = $"{nameof(ChatBox)}-Delete";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconDelete,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Delete.png"));
        const string iconMenuPin = $"{nameof(ChatBox)}-Menu_Pin";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconMenuPin,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Menu_Pin.png"));
        const string iconProfanity = $"{nameof(ChatBox)}-Profanity";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconProfanity,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Profanity.png"));
        const string iconRemove = $"{nameof(ChatBox)}-Remove";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconRemove,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Remove.png"));

        const string iconVisShow = $"{nameof(ChatBox)}-Visibility_Show";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconVisShow,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Visibility_Show.png"));
        const string iconVisHide = $"{nameof(ChatBox)}-Visibility_Hide";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconVisHide,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Visibility_Hide.png"));
        const string iconVisDefault = $"{nameof(ChatBox)}-Visibility_Default";
        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(ChatBox), iconVisDefault,
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.ChatBox_Visibility_Default.png"));

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(ChatBox), nameof(ChatBox));

        miscCategory.AddButton("Send Message", iconMsg, "Opens the keyboard to send a message via the ChatBox").OnPress += () => {
            manager.ToggleQuickMenu(false);
            ChatBox.OpenKeyboard(false, "");
        };

        var modPage = miscCategory.AddPage($"{nameof(ChatBox)} Settings", iconMsgSettings, $"Configure the settings for {nameof(ChatBox)}.", nameof(ChatBox));
        modPage.MenuTitle = $"{nameof(ChatBox)} Settings";

        var modSettingsCategory = modPage.AddCategory("Settings");

        var historyOnCenter = AddMelonToggle(miscCategory, MeHistoryWindowOnCenter, "History Window on Center");

        BTKUILib.UIObjects.Page historyPage = null;
        if (historyOnCenter.ToggleValue) {
            historyPage = miscCategory.AddPage($"{nameof(ChatBox)} History", iconMsgWindow, "", nameof(ChatBox));
        }
        historyOnCenter.OnValueUpdated += isOn => {
            historyPage?.SubpageButton.Delete();
            historyPage?.Delete();
            if (isOn) {
                historyPage = miscCategory.AddPage($"{nameof(ChatBox)} History", iconMsgWindow, "", nameof(ChatBox));
            }
            HistoryBehavior.IsBTKUIHistoryPageOpened = false;
            HistoryBehavior.Instance.ParentTo(HistoryBehavior.MenuTarget.QuickMenu);
        };
        BTKUILib.QuickMenuAPI.OnOpenedPage += (targetPage, _) => {
            if (!historyOnCenter.ToggleValue) return;
            HistoryBehavior.IsBTKUIHistoryPageOpened = targetPage == "btkUI-ChatBox-ChatBoxHistory";
            HistoryBehavior.Instance.UpdateWhetherMenuIsShown();
        };
        BTKUILib.QuickMenuAPI.OnTabChange += (_, _) => {
            if (!historyOnCenter.ToggleValue) return;
            HistoryBehavior.IsBTKUIHistoryPageOpened = false;
            HistoryBehavior.Instance.UpdateWhetherMenuIsShown();
        };
        BTKUILib.QuickMenuAPI.OnBackAction += (_, _) => {
            if (!historyOnCenter.ToggleValue) return;
            HistoryBehavior.IsBTKUIHistoryPageOpened = false;
            HistoryBehavior.Instance.UpdateWhetherMenuIsShown();
        };

        AddMelonToggle(modSettingsCategory, MeSoundOnStartedTyping, "Typing Sound");
        AddMelonToggle(modSettingsCategory, MeSoundOnMessage, "Message Sound");
        AddMelonToggle(modSettingsCategory, MeMessageMentionGlobalAudio, "Mention Sound Global");
        AddMelonToggle(modSettingsCategory, MeOnlyViewFriends, "Only Friends");
        MeOnlyViewFriends.OnEntryValueChanged.Subscribe((_, _) => HistoryBehavior.MessageVisibilityChanged?.Invoke());
        AddMelonToggle(modSettingsCategory, MeMessageTimeoutDependsLength, "Dynamic Timeout");
        AddMelonToggle(modSettingsCategory, MeIgnoreOscMessages, "Hide OSC Msgs");
        AddMelonToggle(modSettingsCategory, MeIgnoreModMessages, "Hide Mod Msgs");

        var profanityPage = modSettingsCategory.AddPage("Manage Profanity", iconProfanity, "Manage the profanity filter.", nameof(ChatBox));
        var profanityCat = profanityPage.AddCategory("");
        var profanityListCat = profanityPage.AddCategory("ProfanityList");
        var profanityToggle = AddMelonToggle(profanityCat, MeProfanityFilter, "Use Profanity Filter");
        Button addProfanity = null;
        Button removeProfanity = null;
        PopulateProfanity(MeProfanityFilter.Value);
        profanityToggle.OnValueUpdated += PopulateProfanity;
        void PopulateProfanityList() {
            profanityListCat.ClearChildren();
            foreach (var profanityWord in ConfigJson.GetProfanityList()) {
                profanityListCat.AddButton(profanityWord, iconDelete, $"Removes the word {profanityWord} from the list.").OnPress += () => {
                    ConfigJson.RemoveProfanity(profanityWord);
                    PopulateProfanityList();
                };
            }
        }
        void PopulateProfanity(bool isFilterOn) {
            addProfanity?.Delete();
            removeProfanity?.Delete();
            PopulateProfanityList();
            if (!isFilterOn) return;
            addProfanity = profanityCat.AddButton("Add Profanity", iconAdd, "Add a profanity word to the list (it's case insensitive).");
            addProfanity.OnPress += () => BTKUILib.QuickMenuAPI.OpenKeyboard("", word => {
                ConfigJson.AddProfanity(word);
                PopulateProfanityList();
            });
            removeProfanity = profanityCat.AddButton("Remove Profanity", iconRemove, "Remove a profanity word from the list.");
            removeProfanity.OnPress += () => BTKUILib.QuickMenuAPI.OpenKeyboard("", word => {
                ConfigJson.RemoveProfanity(word);
                PopulateProfanityList();
            });
        }

        var pinButtonBTKUI = modSettingsCategory.AddButton("Pin History to QM", iconMenuPin,
            "Pins the History Window back to quick menu. Useful if you lost your window :)");
        pinButtonBTKUI.OnPress += () => HistoryBehavior.Instance.ParentTo(HistoryBehavior.MenuTarget.QuickMenu);

        AddMelonSlider(modPage, MeSoundsVolume, 0f, 1f, 1);
        AddMelonSlider(modPage, MeNotificationSoundMaxDistance, 1f, 25f, 1, "Sound Distance");
        AddMelonSlider(modPage, MeMessageTimeoutSeconds, MessageTimeoutMin, MessageTimeoutMax, 0, "Timeout (secs)");
        AddMelonSlider(modPage, MeChatBoxOpacity, 0.1f, 1f, 2);
        AddMelonSlider(modPage, MeChatBoxSize, 0f, 2f, 2);
        AddMelonSlider(modPage, MeHistoryFontSize, 25f, 50f, 0);

        // Player visibility overrides
        var playerCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(ChatBox), nameof(ChatBox));
        void UpdateOverrideButton(string playerName, string playerID) {
            playerCat.ClearChildren();
            var playerOverride = ConfigJson.GetUserOverride(playerID);
            var buttonTooltip = playerOverride switch {
                ConfigJson.UserOverride.Default => "Default - It will respect the setting to only show from friends.",
                ConfigJson.UserOverride.Show => "Show - Messages will be shown regardless the only show from friends setting.",
                ConfigJson.UserOverride.Hide => "Hide - Messages will not be displayed for this user.",
                _ => "N/A"
            };
            var buttonIcon = playerOverride switch {
                ConfigJson.UserOverride.Default => iconVisDefault,
                ConfigJson.UserOverride.Show => iconVisShow,
                ConfigJson.UserOverride.Hide => iconVisHide,
                _ => "N/A"
            };
            playerCat.AddButton("", buttonIcon, buttonTooltip).OnPress += () => {
                var values = Enum.GetValues(typeof(ConfigJson.UserOverride)).Cast<ConfigJson.UserOverride>().ToArray();
                var currentIndex = Array.IndexOf(values, playerOverride);
                var nextOverride = values[(currentIndex + 1) % values.Length];
                ConfigJson.SetUserOverride(playerID, nextOverride);
                UpdateOverrideButton(playerName, playerID);
                HistoryBehavior.MessageVisibilityChanged?.Invoke();
            };
        }
        BTKUILib.QuickMenuAPI.OnPlayerSelected += UpdateOverrideButton;
    }

}
