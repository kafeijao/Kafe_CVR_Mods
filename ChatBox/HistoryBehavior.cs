using System.Globalization;
using System.Text.RegularExpressions;
using ABI_RC.Core;
using ABI_RC.Core.AudioEffects;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
#if UNITY_6
using ABI_RC.Core.Util.AssetFiltering;
#endif
using ABI_RC.Systems.InputManagement;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kafe.ChatBox;

public class HistoryBehavior : MonoBehaviour {

    internal static HistoryBehavior Instance;

    // Colors
    public static readonly Color ColorWhite = new Color(1f, 1f, 1f);
    public static readonly Color ColorBlue = new Color(0f, .69f, 1f);
    public static readonly Color ColorYellow = new Color(1f, .95f, 0f);
    public static readonly Color ColorOrange = new Color(.9882f, .4157f, .0118f);
    public static readonly Color ColorIvory = new Color(.9023f, .8398f, .5664f);
    public static readonly Color ColorPink = new Color(.9882f, .2f, .5f);
    public static readonly Color ColorGreen = new Color(.2f, 1f, .2f);

    public static readonly Color ColorBackground = new Color(0f, 0f, 0f, 0.749f);
    public static readonly Color ColorBTKUIMenuBackground = new Color(41f / 255f, 41f / 255f, 41f / 255f, 1f);

    private const float TimestampFontSizeModifier = 1.125f;
    private const float UsernameFontSizeModifier = 1.5625f;

    private Transform _quickMenuGo;

    // Menu Current Settings
    private MenuTarget _currentMenuParent;
    private const float MenuScale = 0.0004f;
    private readonly Vector3 _menuScaleVector = Vector3.one * MenuScale;

    // Main
    private RectTransform _rootRectTransform;
    private Image _rootRectImage;
    private CVRPickupObject _rootRectPickup;
    private MeshRenderer _rootRectPickupHighlight;
    private BoxCollider _rootRectCollider;

    // Main Sub-Components
    private GameObject _togglesView;
    private Image _togglesViewBackground;
    private GameObject _header;
    private TextMeshProUGUI _titleText;
    private GameObject _scrollView;
    private GameObject _pickupHighlight;

    // Power Toggle
    private Toggle _powerToggle;
    private Image _powerBackground;
    private Image _powerImage;

    // Message Button
    private Button _messageButton;

    // Pin Toggle
    private Toggle _pinToggle;
    private Image _pinImage;

    // Hud Toggle
    private Toggle _hudToggle;
    private Image _hudImage;

    // Grab Toggle
    private Toggle _grabToggle;
    private Image _grabImage;

    // Content
    private RectTransform _contentRectTransform;

    // Templates
    private GameObject _templateChatEntry;

    // Messages
    private static readonly HashSet<ChatBoxEntry> ChatBoxEntries = new();

    internal static Action MessageVisibilityChanged;

    internal enum MenuTarget {
        QuickMenu,
        World,
        HUD,
    }

    private class ChatBoxEntry {
        private readonly string _senderGuid;
        private readonly GameObject _gameObject;
        private readonly TextMeshProUGUI _usernameTmp;
        private readonly TextMeshProUGUI _timestampTmp;
        private readonly TextMeshProUGUI _messageTmp;

        public ChatBoxEntry(string senderGuid, GameObject gameObject, TextMeshProUGUI usernameTmp, TextMeshProUGUI timestampTmp, TextMeshProUGUI messageTmp) {
            _senderGuid = senderGuid;
            _gameObject = gameObject;
            _usernameTmp = usernameTmp;
            _timestampTmp = timestampTmp;
            _messageTmp = messageTmp;
        }

        internal void UpdateFontSize(float newValue) {
            _timestampTmp.fontSize = newValue * TimestampFontSizeModifier;
            _usernameTmp.fontSize = newValue * UsernameFontSizeModifier;
            _messageTmp.fontSize = newValue;
        }

        internal void UpdateVisibility() {
            _gameObject.SetActive(ConfigJson.ShouldShowMessage(_senderGuid));
        }
    }

    private void SetGameLayerRecursive(GameObject go, int layer) {
        go.layer = layer;
        foreach (Transform child in go.transform) {
            SetGameLayerRecursive(child.gameObject, layer);
        }
    }

    internal void ParentTo(MenuTarget targetType) {

        var menuControllerTransform = (RectTransform) transform;

        switch (targetType) {
            case MenuTarget.QuickMenu:
                menuControllerTransform.SetParent(_quickMenuGo.transform, true);
                if (ModConfig.MeHistoryWindowOnCenter.Value) {
                    menuControllerTransform.localPosition = new Vector3(-0.05f, -0.055f, -0.001f);
                    ModConfig.MeHistoryWindowOpened.Value = true;
                }
                else {
                    menuControllerTransform.localPosition = new Vector3(0.86f, -0.095f, 0f);
                }

                SetGameLayerRecursive(menuControllerTransform.gameObject, LayerMask.NameToLayer("UI Internal"));
                menuControllerTransform.localRotation = Quaternion.identity;
                _rootRectTransform.transform.localScale = _menuScaleVector;
                _rootRectPickup.enabled = false;
                _rootRectPickupHighlight.enabled = false;
                break;

            case MenuTarget.World:
                var pos = menuControllerTransform.position;
                var rot = menuControllerTransform.rotation;
                menuControllerTransform.SetParent(null, true);
                menuControllerTransform.SetPositionAndRotation(pos, rot);
                SetGameLayerRecursive(menuControllerTransform.gameObject, LayerMask.NameToLayer("UI"));
                break;

            case MenuTarget.HUD:
                var target = MetaPort.Instance.isUsingVr
                    ? PlayerSetup.Instance.vrCamera
                    : PlayerSetup.Instance.desktopCamera;
                menuControllerTransform.SetParent(target.transform, true);
                _rootRectPickup.enabled = false;
                _rootRectPickupHighlight.enabled = false;
                SetGameLayerRecursive(menuControllerTransform.gameObject, LayerMask.NameToLayer("UI Internal"));
                break;
        }

        _currentMenuParent = targetType;
        UpdateWhetherMenuIsShown();
        UpdateButtonStates();
        UpdateBackgroundAndTitleVisibility();
    }

    private void UpdateButtonStates() {
        var attachedToBTKUI = _currentMenuParent == MenuTarget.QuickMenu && ModConfig.MeHistoryWindowOnCenter.Value;
        _powerImage.enabled = !attachedToBTKUI;
        _powerToggle.enabled = !attachedToBTKUI;
        _powerBackground.color = attachedToBTKUI ? ColorBTKUIMenuBackground : ColorBackground;
        _togglesViewBackground.color = attachedToBTKUI ? ColorBTKUIMenuBackground : ColorBackground;
        _powerToggle.SetIsOnWithoutNotify(ModConfig.MeHistoryWindowOpened.Value);
        _powerImage.color = _powerToggle.isOn ? ColorPink : Color.white;

        _pinToggle.SetIsOnWithoutNotify(_currentMenuParent == MenuTarget.World);
        _pinImage.color = _pinToggle.isOn ? ColorOrange : Color.white;

        _hudToggle.SetIsOnWithoutNotify(_currentMenuParent == MenuTarget.HUD);
        _hudImage.color = _hudToggle.isOn ? ColorBlue : Color.white;

        _grabToggle.gameObject.SetActive(_pinToggle.isOn);
        _grabToggle.SetIsOnWithoutNotify(_rootRectPickup.enabled);
        _grabImage.color = _grabToggle.isOn ? ColorGreen : Color.white;
    }

    internal static bool IsBTKUIHistoryPageOpened = false;

    internal void UpdateWhetherMenuIsShown() {
        if (!CVR_MenuManager.Instance.IsViewShown && _currentMenuParent == MenuTarget.QuickMenu) {
            gameObject.SetActive(false);
            CVR_MenuManager.Instance._uiRenderer.sortingOrder = 10;
        }
        else {
            if (!ModConfig.MeHistoryWindowOnCenter.Value) {
                gameObject.SetActive(true);
                CVR_MenuManager.Instance._uiRenderer.sortingOrder = 10;
            }
            else if (ModConfig.MeHistoryWindowOnCenter.Value && IsBTKUIHistoryPageOpened) {
                gameObject.SetActive(true);
                CVR_MenuManager.Instance._uiRenderer.sortingOrder = -1;
            }
            else {
                gameObject.SetActive(false);
                CVR_MenuManager.Instance._uiRenderer.sortingOrder = 10;
            }
        }
    }

    private void UpdateBackgroundAndTitleVisibility() {
        var isShown = ModConfig.MeHistoryWindowOpened.Value && (!ModConfig.MeHistoryWindowOnCenter.Value || _currentMenuParent != MenuTarget.QuickMenu);
        _rootRectImage.enabled = isShown;
        _titleText.alpha = isShown ? 1f : 0f;
    }

    private void ToggleHistoryWindow(bool isOpened) {

        _rootRectPickup.enabled = _grabToggle != null && _grabToggle.isOn;
        _rootRectPickupHighlight.enabled = _rootRectPickup.enabled;
        _rootRectCollider.enabled = isOpened;

        _togglesView.SetActive(isOpened);
        _header.SetActive(isOpened);
        _scrollView.SetActive(isOpened);
        _pickupHighlight.SetActive(isOpened);

        ModConfig.MeHistoryWindowOpened.Value = isOpened;

        ParentTo(MenuTarget.QuickMenu);
    }

    private void Awake() {

        try {
            _quickMenuGo = CVR_MenuManager.Instance.cohtmlView.transform;

            // Main
            _rootRectTransform = GetComponent<RectTransform>();
            _rootRectTransform.gameObject.layer = LayerMask.NameToLayer("UI Internal");
            _rootRectTransform.gameObject.SetActive(ModConfig.MeShowHistoryWindow.Value);
            ModConfig.MeShowHistoryWindow.OnEntryValueChanged.Subscribe((_, currentValue) => {
                _rootRectTransform.gameObject.SetActive(currentValue);
            });
            _rootRectImage = _rootRectTransform.GetComponent<Image>();
            _rootRectCollider = _rootRectTransform.GetComponent<BoxCollider>();

            // Grab Main Sub-Components
            _togglesView = _rootRectTransform.Find("TogglesView").gameObject;
            _togglesViewBackground = _togglesView.GetComponent<Image>();
            _header = _rootRectTransform.Find("Header").gameObject;
            _titleText = _rootRectTransform.Find("Header/Title").GetComponent<TextMeshProUGUI>();
            _scrollView = _rootRectTransform.Find("Scroll View").gameObject;
            if (!MetaPort.Instance.isUsingVr) {
                // In desktop lets increase the scroll sensitivity
                _scrollView.GetComponent<ScrollRect>().scrollSensitivity = 100f;
            }
            _pickupHighlight = _rootRectTransform.Find("PickupHighlight").gameObject;

            // Power Toggle
            _powerToggle = _rootRectTransform.Find("PowerToggle").GetComponent<Toggle>();
            _powerBackground = _powerToggle.transform.Find("Background").GetComponent<Image>();
            _powerImage = _rootRectTransform.Find("PowerToggle/Checkmark").GetComponent<Image>();
            _powerToggle.onValueChanged.AddListener(ToggleHistoryWindow);
            ModConfig.MeHistoryWindowOpened.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
                if (oldValue == newValue) return;
                ToggleHistoryWindow(newValue);
            });

            // Message Button
            _messageButton = _rootRectTransform.Find("TogglesView/Message").GetComponent<Button>();
            _messageButton.onClick.AddListener(() => {
                if (CVR_MenuManager.Instance.IsViewShown) CVR_MenuManager.Instance.ToggleQuickMenu(false);
                ChatBox.OpenKeyboard("");
            });

            // Pin Toggle
            _pinToggle = _rootRectTransform.Find("TogglesView/Pin").GetComponent<Toggle>();
            _pinImage = _rootRectTransform.Find("TogglesView/Pin/Checkmark").GetComponent<Image>();
            _pinToggle.onValueChanged.AddListener(isOn => ParentTo(isOn ? MenuTarget.World : MenuTarget.QuickMenu));

            // Hud Toggle
            _hudToggle = _rootRectTransform.Find("TogglesView/Hud").GetComponent<Toggle>();
            _hudImage = _rootRectTransform.Find("TogglesView/Hud/Checkmark").GetComponent<Image>();
            _hudToggle.onValueChanged.AddListener(isOn => ParentTo(isOn ? MenuTarget.HUD : MenuTarget.QuickMenu));

            // Grab Toggle
            _grabToggle = _rootRectTransform.Find("TogglesView/Grab").GetComponent<Toggle>();
            _grabImage = _rootRectTransform.Find("TogglesView/Grab/Checkmark").GetComponent<Image>();
            _rootRectPickup = _rootRectTransform.GetComponent<CVRPickupObject>();
            _rootRectPickup.enabled = false;
            _rootRectPickup.generateTeleTarget = false;
            _rootRectPickupHighlight = _rootRectTransform.Find("PickupHighlight").GetComponent<MeshRenderer>();
            _rootRectPickupHighlight.enabled = false;
            _grabToggle.onValueChanged.AddListener(isOn => {
                _rootRectPickup.enabled = isOn;
                _rootRectPickupHighlight.enabled = isOn;
                UpdateButtonStates();
            });

            // Content
            _contentRectTransform = _rootRectTransform.Find("Scroll View/Viewport/Content").GetComponent<RectTransform>();

            // Save templates
            _templateChatEntry = _rootRectTransform.Find("Templates/Template_ChatEntry").gameObject;

            // Everything is setup
            ToggleHistoryWindow(ModConfig.MeHistoryWindowOpened.Value);

            // Set the message listeners
            API.OnMessageReceived += chatBoxMessage => {

                // Ignore messages that are not supposed to be displayed
                if (!chatBoxMessage.DisplayOnHistory) return;

                // Handle typing source ignores
                if (ModConfig.MeIgnoreOscMessages.Value && chatBoxMessage.Source == API.MessageSource.OSC) return;
                if (ModConfig.MeIgnoreModMessages.Value && chatBoxMessage.Source == API.MessageSource.Mod) return;

                AddMessage(chatBoxMessage);
            };
            API.OnMessageSent += chatBoxMessage => {

                // Ignore messages that are not supposed to be displayed
                if (!chatBoxMessage.DisplayOnHistory) return;

                // Handle typing source ignores
                if (ModConfig.MeIgnoreOscMessages.Value && chatBoxMessage.Source == API.MessageSource.OSC) return;
                if (ModConfig.MeIgnoreModMessages.Value && chatBoxMessage.Source == API.MessageSource.Mod) return;

                AddMessage(chatBoxMessage);
            };

            // Set font size listener
            ModConfig.MeHistoryFontSize.OnEntryValueChanged.Subscribe((_, newValue) => {
                foreach (var entry in ChatBoxEntries) {
                    entry.UpdateFontSize(newValue);
                }
            });

            // Set message visibility change listener
            MessageVisibilityChanged += () => {
                foreach (var entry in ChatBoxEntries) {
                    entry.UpdateVisibility();
                }
            };

            gameObject.SetActive(false);
            Instance = this;
        }
        catch (Exception ex) {
            MelonLogger.Error(ex);
        }
    }

    private static string CleanTMPString(string input) => string.IsNullOrWhiteSpace(input)
        ? ""
        : input.Replace("<", "&lt;").Replace(">", "&gt;");

    private void AddMessage(API.ChatBoxMessage chatBoxMessage) {

        var isSelf = MetaPort.Instance.ownerId == chatBoxMessage.SenderGuid;

        var chatEntry = Instantiate(_templateChatEntry, _contentRectTransform).transform;
        chatEntry.SetAsFirstSibling();
        var timestampTmp = chatEntry.Find("Header/Timestamp").GetComponent<TextMeshProUGUI>();
#if UNITY_6
        SharedFilter.ProcessTextMeshProUGUI(timestampTmp);
#endif
        timestampTmp.text = $"[{DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}]";

        // Handle OSC/Mod sources
        var modNameTmp = chatEntry.Find("Header/ModName").GetComponent<TextMeshProUGUI>();
#if UNITY_6
        SharedFilter.ProcessTextMeshProUGUI(modNameTmp);
#endif
        if (chatBoxMessage.Source == API.MessageSource.OSC) {
            modNameTmp.text = "[OSC]";
            modNameTmp.color = ChatBoxBehavior.TealTransparency;
        }
        else if (chatBoxMessage.Source == API.MessageSource.Mod) {
            modNameTmp.text = $"[{CleanTMPString(chatBoxMessage.ModName)}]";
            modNameTmp.color = ChatBoxBehavior.PinkTransparency;
        }

        var usernameComponent = chatEntry.Find("Header/Username");
        var usernameButton = usernameComponent.GetComponent<Button>();
        usernameButton.onClick.AddListener(() => ViewManager.Instance.RequestUserDetailsPage(chatBoxMessage.SenderGuid));
        var usernameTmp = usernameComponent.GetComponent<TextMeshProUGUI>();
#if UNITY_6
        SharedFilter.ProcessTextMeshProUGUI(usernameTmp);
#endif
        if (isSelf) {
            usernameTmp.text = CleanTMPString(AuthManager.Username);
            usernameTmp.color = ColorBlue;
        }
        else {
            usernameTmp.text = CleanTMPString(CVRPlayerManager.Instance.TryGetPlayerName(chatBoxMessage.SenderGuid));
            usernameTmp.color = Friends.FriendsWith(chatBoxMessage.SenderGuid) ? ColorGreen : Color.white;
        }

        var msg = chatBoxMessage.Message;
        // Check for profanity and replace if needed
        if (ModConfig.MeProfanityFilter.Value) {
            msg = Regex.Replace(msg, ConfigJson.GetProfanityPattern(), m => new string('*', m.Length), RegexOptions.IgnoreCase);
        }
        var messageTmp = chatEntry.Find("Message").GetComponent<TextMeshProUGUI>();
#if UNITY_6
        SharedFilter.ProcessTextMeshProUGUI(messageTmp);
#endif

        // // Color our own username in messages (I disabled formatting so this won't work ;_;
        // var coloredUsername = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorBlue)}>@{AuthManager.Username}</color>";
        // message = Regex.Replace(message, Regex.Escape("@" + AuthManager.Username), coloredUsername, RegexOptions.IgnoreCase);
        messageTmp.richText = false;
        messageTmp.text = msg;

        var entry = new ChatBoxEntry(chatBoxMessage.SenderGuid, chatEntry.gameObject, usernameTmp, timestampTmp, messageTmp);
        entry.UpdateVisibility();
        entry.UpdateFontSize(ModConfig.MeHistoryFontSize.Value);
        ChatBoxEntries.Add(entry);
    }


    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        private static void After_CVR_MenuManager_Start(ref CVR_MenuManager __instance) {
            try {
                // Instantiate and add the controller script
                Instantiate(ModConfig.ChatBoxHistoryPrefab, __instance.cohtmlView.transform).AddComponent<HistoryBehavior>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        private static void After_CVR_MenuManager_ToggleQuickMenu() {
            try {
                if (!Instance) return;
                Instance.UpdateWhetherMenuIsShown();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_ToggleQuickMenu)}");
                MelonLogger.Error(e);
            }
        }
    }
}
