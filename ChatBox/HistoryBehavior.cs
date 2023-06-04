using System.Globalization;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
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
    private GameObject _header;
    private GameObject _scrollView;
    private GameObject _pickupHighlight;

    // Power Toggle
    private Toggle _powerToggle;
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
    private readonly List<Tuple<TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI>> _messagesTMPComponents = new();

    internal enum MenuTarget {
        QuickMenu,
        World,
        HUD,
    }

    internal void ParentTo(MenuTarget targetType) {

        var menuControllerTransform = (RectTransform) transform;

        switch (targetType) {
            case MenuTarget.QuickMenu:
                menuControllerTransform.SetParent(_quickMenuGo.transform, true);
                menuControllerTransform.localPosition = new Vector3(0.86f, -0.095f, 0f);
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
                break;

            case MenuTarget.HUD:
                var target = MetaPort.Instance.isUsingVr
                    ? PlayerSetup.Instance.vrCamera
                    : PlayerSetup.Instance.desktopCamera;
                menuControllerTransform.SetParent(target.transform, true);
                _rootRectPickup.enabled = false;
                _rootRectPickupHighlight.enabled = false;
                break;
        }

        _currentMenuParent = targetType;
        UpdateWhetherMenuIsShown();
        UpdateButtonStates();
    }

    private void UpdateButtonStates() {
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

    private void UpdateWhetherMenuIsShown() {
        if (!CVR_MenuManager.Instance._quickMenuOpen && _currentMenuParent == MenuTarget.QuickMenu) {
            gameObject.SetActive(false);
        }
        else {
            gameObject.SetActive(true);
        }
    }

    private void ToggleHistoryWindow(bool isOpened) {

        _rootRectImage.enabled = isOpened;
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
            _quickMenuGo = CVR_MenuManager.Instance.quickMenu.transform;

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
            _header = _rootRectTransform.Find("Header").gameObject;
            _scrollView = _rootRectTransform.Find("Scroll View").gameObject;
            if (!MetaPort.Instance.isUsingVr) {
                // In desktop lets increase the scroll sensitivity
                _scrollView.GetComponent<ScrollRect>().scrollSensitivity = 100f;
            }
            _pickupHighlight = _rootRectTransform.Find("PickupHighlight").gameObject;

            // Power Toggle
            _powerToggle = _rootRectTransform.Find("PowerToggle").GetComponent<Toggle>();
            _powerImage = _rootRectTransform.Find("PowerToggle/Checkmark").GetComponent<Image>();
            _powerToggle.onValueChanged.AddListener(ToggleHistoryWindow);
            ModConfig.MeHistoryWindowOpened.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
                if (oldValue == newValue) return;
                ToggleHistoryWindow(newValue);
            });

            // Message Button
            _messageButton = _rootRectTransform.Find("TogglesView/Message").GetComponent<Button>();
            _messageButton.onClick.AddListener(() => ChatBox.OpenKeyboard(true, ""));

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
            API.OnMessageReceived += (source, senderGuid, msg, _, _) => {
                if (source != API.MessageSource.Internal) return;
                AddMessage(DateTime.Now, senderGuid, CVRPlayerManager.Instance.TryGetPlayerName(senderGuid), MetaPort.Instance.ownerId == senderGuid, Friends.FriendsWith(senderGuid), msg);
            };
            API.OnMessageSent += (source, msg, _, _) => {
                if (source != API.MessageSource.Internal) return;
                AddMessage(DateTime.Now, MetaPort.Instance.ownerId, MetaPort.Instance.username, true, false, msg);
            };

            // Set font size listeners
            ModConfig.MeHistoryFontSize.OnEntryValueChanged.Subscribe((_, newValue) => {
                foreach (var components in _messagesTMPComponents) {
                    components.Item1.fontSize = newValue * TimestampFontSizeModifier;
                    components.Item2.fontSize = newValue * UsernameFontSizeModifier;
                    components.Item3.fontSize = newValue;
                }
            });

            gameObject.SetActive(false);
            Instance = this;
        }
        catch (Exception ex) {
            MelonLogger.Error(ex);
        }
    }

    private void AddMessage(DateTime date, string senderGuid, string senderUsername, bool isSelf, bool isFriend, string message) {
        var chatEntry = Instantiate(_templateChatEntry, _contentRectTransform).transform;
        chatEntry.SetAsFirstSibling();
        var timestampTmp = chatEntry.Find("Header/Timestamp").GetComponent<TextMeshProUGUI>();
        timestampTmp.text = $"[{date.ToString("T", CultureInfo.CurrentCulture)}]";
        timestampTmp.fontSize = ModConfig.MeHistoryFontSize.Value * TimestampFontSizeModifier;
        var usernameComponent = chatEntry.Find("Header/Username");
        var usernameButton = usernameComponent.GetComponent<Button>();
        usernameButton.onClick.AddListener(() => ViewManager.Instance.RequestUserDetailsPage(senderGuid));
        var usernameTmp = usernameComponent.GetComponent<TextMeshProUGUI>();
        usernameTmp.text = senderUsername;
        usernameTmp.fontSize = ModConfig.MeHistoryFontSize.Value * UsernameFontSizeModifier;
        if (isSelf) usernameTmp.color = ColorBlue;
        else usernameTmp.color = isFriend ? ColorGreen : Color.white;

        var messageTmp = chatEntry.Find("Message").GetComponent<TextMeshProUGUI>();
        messageTmp.text = message;
        messageTmp.fontSize = ModConfig.MeHistoryFontSize.Value;

        _messagesTMPComponents.Add(new Tuple<TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI>(timestampTmp, usernameTmp, messageTmp));
    }


    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        private static void After_CVR_MenuManager_Start(ref CVR_MenuManager __instance) {
            try {
                // Instantiate and add the controller script
                Instantiate(ModConfig.ChatBoxHistoryPrefab, __instance.quickMenu.transform).AddComponent<HistoryBehavior>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        private static void AfterMenuToggle() {
            if (!Instance) return;
            Instance.UpdateWhetherMenuIsShown();
        }
    }
}
