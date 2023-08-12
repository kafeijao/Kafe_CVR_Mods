using System.Globalization;
using System.Text.RegularExpressions;
using ABI_RC.Core.AudioEffects;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
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

    private static bool _skipNextInteraction;

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
        _skipNextInteraction = true;
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
        if (!CVR_MenuManager.Instance._quickMenuOpen && _currentMenuParent == MenuTarget.QuickMenu) {
            gameObject.SetActive(false);
            CVR_MenuManager.Instance._quickMenuRenderer.sortingOrder = 10;
        }
        else {
            if (!ModConfig.MeHistoryWindowOnCenter.Value) {
                gameObject.SetActive(true);
                CVR_MenuManager.Instance._quickMenuRenderer.sortingOrder = 10;
            }
            else if (ModConfig.MeHistoryWindowOnCenter.Value && IsBTKUIHistoryPageOpened) {
                gameObject.SetActive(true);
                CVR_MenuManager.Instance._quickMenuRenderer.sortingOrder = -1;
            }
            else {
                gameObject.SetActive(false);
                CVR_MenuManager.Instance._quickMenuRenderer.sortingOrder = 10;
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
                if (CVR_MenuManager.Instance._quickMenuOpen) CVR_MenuManager.Instance.ToggleQuickMenu(false);
                ChatBox.OpenKeyboard(true, "");
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

    private void AddMessage(API.ChatBoxMessage chatBoxMessage) {

        var isSelf = MetaPort.Instance.ownerId == chatBoxMessage.SenderGuid;

        var chatEntry = Instantiate(_templateChatEntry, _contentRectTransform).transform;
        chatEntry.SetAsFirstSibling();
        var timestampTmp = chatEntry.Find("Header/Timestamp").GetComponent<TextMeshProUGUI>();
        timestampTmp.text = $"[{DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}]";

        // Handle OSC/Mod sources
        var modNameTmp = chatEntry.Find("Header/ModName").GetComponent<TextMeshProUGUI>();
        if (chatBoxMessage.Source == API.MessageSource.OSC) {
            modNameTmp.text = "[OSC]";
            modNameTmp.color = ChatBoxBehavior.TealTransparency;
        }
        else if (chatBoxMessage.Source == API.MessageSource.Mod) {
            modNameTmp.text = $"[{chatBoxMessage.ModName}]";
            modNameTmp.color = ChatBoxBehavior.PinkTransparency;
        }

        var usernameComponent = chatEntry.Find("Header/Username");
        var usernameButton = usernameComponent.GetComponent<Button>();
        usernameButton.onClick.AddListener(() => ViewManager.Instance.RequestUserDetailsPage(chatBoxMessage.SenderGuid));
        var usernameTmp = usernameComponent.GetComponent<TextMeshProUGUI>();
        if (isSelf) {
            usernameTmp.text = AuthManager.username;
            usernameTmp.color = ColorBlue;
        }
        else {
            usernameTmp.text = CVRPlayerManager.Instance.TryGetPlayerName(chatBoxMessage.SenderGuid);
            usernameTmp.color = Friends.FriendsWith(chatBoxMessage.SenderGuid) ? ColorGreen : Color.white;
        }

        var msg = chatBoxMessage.Message;
        // Check for profanity and replace if needed
        if (ModConfig.MeProfanityFilter.Value) {
            msg = Regex.Replace(msg, ConfigJson.GetProfanityPattern(), m => new string('*', m.Length), RegexOptions.IgnoreCase);
        }
        var messageTmp = chatEntry.Find("Message").GetComponent<TextMeshProUGUI>();

        // // Color our own username in messages (I disabled formatting so this won't work ;_;
        // var coloredUsername = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorBlue)}>@{AuthManager.username}</color>";
        // message = Regex.Replace(message, Regex.Escape("@" + AuthManager.username), coloredUsername, RegexOptions.IgnoreCase);
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
                Instantiate(ModConfig.ChatBoxHistoryPrefab, __instance.quickMenu.transform).AddComponent<HistoryBehavior>();
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ControllerRay), nameof(ControllerRay.LateUpdate))]
        private static void After_ControllerRay_LateUpdate(ControllerRay __instance) {
            // Cancer code for Ui events when our Unity UI is on top of the QM
            try {
                
                if (!Instance) return;

                if (!MetaPort.Instance.isUsingVr) return;

                // Only do the events if
                if (!Instance.gameObject.activeInHierarchy || !ModConfig.MeHistoryWindowOnCenter.Value ||
                    Instance._currentMenuParent != MenuTarget.QuickMenu) return;
            
                var isInteracting = __instance.hand ? CVRInputManager.Instance.interactLeftDown : CVRInputManager.Instance.interactRightDown;

                // Raycast the internal looking for unity UI in the internal UI layer
                if (!Physics.Raycast(__instance.transform.TransformPoint(__instance.RayDirection * -0.15f),
                        __instance.transform.TransformDirection(__instance.RayDirection), out var hitInfo1,
                        float.PositiveInfinity, LayerMask.GetMask("UI Internal"))) return;
                
                var hitTransform = hitInfo1.collider.transform;

                // Only do this for our menu
                if (!hitTransform.IsChildOf(Instance.transform)) return;

                Button component3 = hitTransform.GetComponent<Button>();
                Toggle component4 = hitTransform.GetComponent<Toggle>();
                Slider component5 = hitTransform.GetComponent<Slider>();
                EventTrigger eventTrigger = hitTransform.GetComponent<EventTrigger>();
                InputField component6 = hitTransform.GetComponent<InputField>();
                TMP_InputField component7 = hitTransform.GetComponent<TMP_InputField>();
                Dropdown component8 = hitTransform.GetComponent<Dropdown>();
                ScrollRect component9 = hitTransform.GetComponent<ScrollRect>();


               
                if (component3 != __instance.lastButton) {
                  if (component3 != null) component3.OnPointerEnter(null);
                  if (__instance.lastButton != null) __instance.lastButton.OnPointerExit(null);
                  __instance.lastButton = component3;
                }
                if (component4 != __instance.lastToggle) {
                  if (component4 != null) component4.OnPointerEnter(null);
                  if (__instance.lastToggle != null) __instance.lastToggle.OnPointerExit(null);
                  __instance.lastToggle = component4;
                }
                if (component8 != __instance.lastDropdown) {
                  if (component8 != null) component8.OnPointerEnter(null);
                  if (__instance.lastDropdown != null) __instance.lastDropdown.OnPointerExit(null);
                  __instance.lastDropdown = component8;
                }
                if (component6 != __instance.lastInputField) {
                  if (component6 != null) component6.OnPointerEnter(null);
                  if (__instance.lastInputField != null) __instance.lastInputField.OnPointerExit(null);
                  __instance.lastInputField = component6;
                }
                if (component7 != __instance.lastTMPInputField) {
                  if (component7 != null) component7.OnPointerEnter(null);
                  if (__instance.lastTMPInputField != null) __instance.lastTMPInputField.OnPointerExit(null);
                  __instance.lastTMPInputField = component7;
                }
                if (eventTrigger == null && component3 != null) eventTrigger = component3.GetComponentInParent<EventTrigger>();
                if (eventTrigger == null && component4 != null) eventTrigger = component4.GetComponentInParent<EventTrigger>();
                if (eventTrigger == null && component5 != null) eventTrigger = component5.GetComponentInParent<EventTrigger>();
                if (eventTrigger == null && component6 != null) eventTrigger = component6.GetComponentInParent<EventTrigger>();
                if (eventTrigger == null && component7 != null) eventTrigger = component7.GetComponentInParent<EventTrigger>();
                if (eventTrigger == null && component9 != null) eventTrigger = component9.GetComponentInParent<EventTrigger>();
                if (eventTrigger != null && eventTrigger != __instance.lastEventTrigger) {
                  if (__instance.lastEventTrigger != null) {
                    foreach (EventTrigger.Entry trigger in __instance.lastEventTrigger.triggers) {
                      if (trigger.eventID == EventTriggerType.PointerExit) trigger.callback.Invoke(null);
                    }
                  }
                  __instance.lastEventTrigger = eventTrigger;
                  foreach (EventTrigger.Entry trigger in eventTrigger.triggers) {
                    if (trigger.eventID == EventTriggerType.PointerEnter) {
                      if (CVRWorld.Instance.uiHighlightSoundObjects.Contains(eventTrigger.gameObject)) InterfaceAudio.Play(AudioClipField.Hover);
                      trigger.callback.Invoke(null);
                    }
                  }
                }
                if (component9) {
                  if (Mathf.Abs(CVRInputManager.Instance.scrollValue) > 0.0) {
                    Vector2 anchoredPosition = component9.content.anchoredPosition;
                    if (component9.vertical) anchoredPosition.y -= CVRInputManager.Instance.scrollValue * 1000f;
                    else if (component9.horizontal) anchoredPosition.x -= CVRInputManager.Instance.scrollValue * 1000f;
                    component9.content.anchoredPosition = anchoredPosition;
                  }
                }
                else {
                  ScrollRect componentInParent4 = __instance.hitTransform.GetComponentInParent<ScrollRect>();
                  if (componentInParent4 != null && Mathf.Abs(CVRInputManager.Instance.scrollValue) > 0.0) {
                    Vector2 anchoredPosition = componentInParent4.content.anchoredPosition;
                    if (componentInParent4.vertical) anchoredPosition.y -= CVRInputManager.Instance.scrollValue * 1000f;
                    else if (componentInParent4.horizontal) anchoredPosition.x -= CVRInputManager.Instance.scrollValue * 1000f;
                    componentInParent4.content.anchoredPosition = anchoredPosition;
                  }
                }

                if (isInteracting) {

                    // Skip interaction after parenting
                    if (_skipNextInteraction) {
                        _skipNextInteraction = false;
                        return;
                    }

                    if (component3) {
                        component3.onClick.Invoke();
                    }
                    if (component4) {
                        component4.isOn = !component4.isOn;
                    }
                    if (component5) {
                        __instance.lastSlider = component5;
                        __instance.lastSliderRect = component5.GetComponent<RectTransform>();
                        __instance.SetSliderValueFromRay(component5, hitInfo1, __instance.lastSliderRect);
                    }
                    if (component9 != null) {
                        __instance.lastScrollView = component9;
                        __instance.scrollStartPositionView = __instance.GetScreenPositionFromRaycastHit(hitInfo1, component9.viewport);
                        __instance.scrollStartPositionContent = component9.content.anchoredPosition;
                        __instance.SetScrollViewValueFromRay(component9, hitInfo1);
                    }
                    if (component8 != null) {
                        if (component8.transform.childCount != 3) {
                            component8.Hide();
                        }
                        else {
                            component8.Show();
                        }
                        foreach (Toggle componentsInChild in component8.gameObject.GetComponentsInChildren<Toggle>(true)) {
                            if (componentsInChild.GetComponent<BoxCollider>() == null) {
                                BoxCollider boxCollider = componentsInChild.gameObject.AddComponent<BoxCollider>();
                                boxCollider.isTrigger = true;
                                RectTransform component10 = componentsInChild.gameObject.GetComponent<RectTransform>();
                                boxCollider.size = new Vector3(Mathf.Max(component10.sizeDelta.x, component10.rect.width), component10.sizeDelta.y, 0.1f);
                                boxCollider.center = new Vector3(boxCollider.size.x * (0.5f - component10.pivot.x), boxCollider.size.y * (0.5f - component10.pivot.y), 0.0f);
                            }
                        }
                    }
                    if (component6) {
                        component6.Select();
                        component6.ActivateInputField();
                        ViewManager.Instance.openMenuKeyboard(component6);
                    }
                    if (component7) {
                        component7.Select();
                        component7.ActivateInputField();
                        ViewManager.Instance.openMenuKeyboard(component7);
                    }
                }
                
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ControllerRay_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }
}
