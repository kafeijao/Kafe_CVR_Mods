using System.Reflection;
using ABI_RC.Core.InteractionSystem;
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
    internal static MelonPreferences_Entry<float> MeSoundsVolume;
    internal static MelonPreferences_Entry<float> MeNotificationSoundMaxDistance;
    internal static MelonPreferences_Entry<float> MeMessageTimeoutSeconds;
    internal static MelonPreferences_Entry<bool> MeMessageTimeoutDependsLength;
    internal static MelonPreferences_Entry<float> MeChatBoxOpacity;
    internal static MelonPreferences_Entry<float> MeChatBoxSize;
    internal static MelonPreferences_Entry<bool> MeIgnoreOscMessages;
    internal static MelonPreferences_Entry<bool> MeIgnoreModMessages;

    // Asset Bundle
    public static GameObject ChatBoxPrefab;
    private const string ChatBoxAssetBundleName = "chatbox.assetbundle";
    private const string ChatBoxPrefabAssetPath = "Assets/Chatbox/ChatBox.prefab";

    internal static string javascriptPatchesContent;
    private const string ChatBoxJSPatches = "chatbox.cohtml.cvrtest.ui.patches.js";

    // Files
    internal enum Sound {
        Typing,
        Message,
    }

    private const string ChatBoxSoundTyping = "chatbox.sound.typing.wav";
    private const string ChatBoxSoundMessage = "chatbox.sound.message.wav";

    private static readonly Dictionary<Sound, string> AudioClipResourceNames = new() {
        {Sound.Typing, ChatBoxSoundTyping},
        {Sound.Message, ChatBoxSoundMessage},
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
            description: "The size of the Chat Box, between 0 (smallest) and 2 (biggest). The default is 1.");

        MeIgnoreOscMessages = _melonCategory.CreateEntry("IgnoreOscMessages", false,
            description: "Whether to ignore messages sent via OSC or not.");

        MeIgnoreModMessages = _melonCategory.CreateEntry("IgnoreModMessages", false,
            description: "Whether to ignore messages sent via other Mods or not.");

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
            using var resourceStream = assembly.GetManifestResourceStream(ChatBoxJSPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {ChatBoxJSPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            javascriptPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the resource: " + ex.Message);
        }

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

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(ChatBox));

        miscCategory.AddButton("Send Message", "", "Opens the keyboard to send a message via the ChatBox").OnPress += () => {
            manager.ToggleQuickMenu(false);
            ChatBox.OpenKeyboard(false, "");
        };

        var modPage = miscCategory.AddPage($"{nameof(ChatBox)} Settings", "", $"Configure the settings for {nameof(ChatBox)}.", nameof(ChatBox));
        modPage.MenuTitle = $"{nameof(ChatBox)} Settings";

        var modSettingsCategory = modPage.AddCategory("Settings");

        var num = 0;
        var ico = new[] { "TT_Off", "TT_Original" };
        var button = modSettingsCategory.AddButton("button", ico[num], "button tooltip");
        button.OnPress += () => {
            button.ButtonText = $"UGABUGA-{num++}";
            button.ButtonIcon = $"UGABUGA-{ico[num%2]}";
        };

        AddMelonToggle(modSettingsCategory, MeSoundOnStartedTyping);
        AddMelonToggle(modSettingsCategory, MeSoundOnMessage);
        AddMelonToggle(modSettingsCategory, MeOnlyViewFriends);
        AddMelonToggle(modSettingsCategory, MeMessageTimeoutDependsLength);
        AddMelonToggle(modSettingsCategory, MeIgnoreOscMessages);
        AddMelonToggle(modSettingsCategory, MeIgnoreModMessages);

        AddMelonSlider(modPage, MeSoundsVolume, 0f, 1f, 1);
        AddMelonSlider(modPage, MeNotificationSoundMaxDistance, 1f, 25f, 1);
        AddMelonSlider(modPage, MeMessageTimeoutSeconds, MessageTimeoutMin, MessageTimeoutMax, 0);
        AddMelonSlider(modPage, MeChatBoxOpacity, 0.1f, 1f, 2);
        AddMelonSlider(modPage, MeChatBoxSize, 0.0f, 2f, 2);
    }

}
