using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using ABI.CCK.Components;
using CCK.Debugger.Components.GameObjectVisualizers;
using CCK.Debugger.Components.MenuHandlers;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using CCK.Debugger.Utils;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CCK.Debugger.Components;

public class Menu : MonoBehaviour {

    // Genera
    private bool crashed;

    // Main
    private RectTransform RootRectTransform;
    private Transform RootQuickMenu;

    // Pin Toggle
    private Toggle PinToggle;
    private Image PinImage;

    // Hud Toggle
    private Toggle HudToggle;
    private Image HudImage;

    // Grab Toggle
    private Toggle GrabToggle;
    private Image GrabImage;
    private BoxCollider GrabPickupCollider;
    private CVRPickupObject GrabPickupScript;

    // Pointer Toggle
    internal Toggle PointerToggle;
    private Image PointerImage;

    // Trigger Toggle
    internal Toggle TriggerToggle;
    private Image TriggerImage;

    // Bone Toggle
    internal Toggle BoneToggle;
    private Image BoneImage;

    // Tracker Toggle
    internal Toggle TrackerToggle;
    private Image TrackerImage;

    // Reset Toggle
    internal Toggle ResetToggle;
    private Image ResetImage;

    // Title
    private TextMeshProUGUI TitleText;
    private Button MainPrevious;
    private Button MainNext;

    // Controls
    private GameObject Controls;
    private TextMeshProUGUI ControlsExtra;
    private Button ControlPrevious;
    private Button ControlNext;

    // Content
    private RectTransform ContentRectTransform;

    // Templates
    private GameObject TemplateCategory;
    private GameObject TemplateCategoryEntry;

    // Pointers, Triggers, Bones, and Trackers
    internal List<PointerVisualizer> CurrentEntityPointerList;
    internal List<TriggerVisualizer> CurrentEntityTriggerList;
    internal List<GameObjectVisualizer> CurrentEntityBoneList;
    internal List<GameObjectVisualizer> CurrentEntityTrackerList;

    // TMPTextProCaches
    private static Dictionary<TextMeshProUGUI, object> TextMeshProUGUIParam = new();
    private static Dictionary<TextMeshProUGUI, object[]> TextMeshProUGUIParams = new();

    // Secondary Menu Pins
    private GameObject AuxMenuGameObject;
    private RectTransform AuxMenuRootRectTransform;
    private Button AuxMenuPinButton;
    private Button AuxMenuHudButton;

    public void AddMenuAux(GameObject menuPins) {
        AuxMenuGameObject = menuPins;
        AuxMenuRootRectTransform = menuPins.GetComponent<RectTransform>();
        AuxMenuPinButton = AuxMenuRootRectTransform.Find("TogglesView/Pin").GetComponent<Button>();
        AuxMenuPinButton.onClick.AddListener(() => PinToggle.isOn = false);
        AuxMenuPinButton.gameObject.SetActive(false);
        AuxMenuHudButton = AuxMenuRootRectTransform.Find("TogglesView/Hud").GetComponent<Button>();
        AuxMenuHudButton.onClick.AddListener(() => HudToggle.isOn = false);
        AuxMenuHudButton.gameObject.SetActive(false);
    }

    private void StartMenuAux() {
        // Setup aux menu
        AuxMenuRootRectTransform.SetParent(RootQuickMenu, true);
        AuxMenuRootRectTransform.transform.localPosition = Vector3.zero;
        AuxMenuRootRectTransform.transform.localRotation = Quaternion.identity;
        AuxMenuRootRectTransform.transform.localScale = new Vector3(0.0004f, 0.0004f, 0.0004f);
        AuxMenuRootRectTransform.anchoredPosition = new Vector2(-0.5f - (AuxMenuRootRectTransform.rect.width*0.0004f/2), 0);

        UpdateAuxMenu();
    }

    private void UpdateAuxMenu() {
        AuxMenuPinButton.gameObject.SetActive(PinToggle.isOn);
        AuxMenuHudButton.gameObject.SetActive(HudToggle.isOn);
        var menuDistance = Vector3.Distance(RootRectTransform.position, AuxMenuRootRectTransform.position);
        AuxMenuGameObject.SetActive(menuDistance > .5f && Events.QuickMenu.IsQuickMenuOpened && (PinToggle.isOn || HudToggle.isOn));
    }

    private void Awake() {

        // Main
        RootRectTransform = GetComponent<RectTransform>();
        TitleText = RootRectTransform.Find("Header/Title").GetComponent<TextMeshProUGUI>();
        MainPrevious = RootRectTransform.Find("Header/Previous").GetComponent<Button>();
        MainPrevious.gameObject.SetActive(true);
        MainPrevious.onClick.AddListener(Events.DebuggerMenu.OnMainPrevious);
        MainNext = RootRectTransform.Find("Header/Next").GetComponent<Button>();
        MainNext.gameObject.SetActive(true);
        MainNext.onClick.AddListener(Events.DebuggerMenu.OnMainNextPage);

        // Pin Toggle
        PinToggle = RootRectTransform.Find("TogglesView/Pin").GetComponent<Toggle>();
        PinImage = RootRectTransform.Find("TogglesView/Pin/Checkmark").GetComponent<Image>();
        PinToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnPinned);
        PinToggle.gameObject.SetActive(true);

        // Hud Toggle
        HudToggle = RootRectTransform.Find("TogglesView/Hud").GetComponent<Toggle>();
        HudImage = RootRectTransform.Find("TogglesView/Hud/Checkmark").GetComponent<Image>();
        HudToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnHudToggled);
        HudToggle.gameObject.SetActive(true);

        // Grab Toggle
        GrabToggle = RootRectTransform.Find("TogglesView/Grab").GetComponent<Toggle>();
        GrabImage = RootRectTransform.Find("TogglesView/Grab/Checkmark").GetComponent<Image>();
        GrabToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnGrabToggle);
        GrabToggle.gameObject.SetActive(false);
        GrabPickupScript = RootRectTransform.GetComponent<CVRPickupObject>();
        GrabPickupCollider = RootRectTransform.GetComponent<BoxCollider>();
        GrabPickupCollider.enabled = false;

        // Pointer Toggle
        PointerToggle = RootRectTransform.Find("TogglesView/Pointer").GetComponent<Toggle>();
        PointerImage = RootRectTransform.Find("TogglesView/Pointer/Checkmark").GetComponent<Image>();
        PointerToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnPointerToggle);
        PointerToggle.gameObject.SetActive(false);
        PointerImage.color = Color.gray;

        // Trigger Toggle
        TriggerToggle = RootRectTransform.Find("TogglesView/Trigger").GetComponent<Toggle>();
        TriggerImage = RootRectTransform.Find("TogglesView/Trigger/Checkmark").GetComponent<Image>();
        TriggerToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnTriggerToggle);
        TriggerToggle.gameObject.SetActive(false);
        TriggerImage.color = Color.gray;

        // Reset Toggle
        ResetToggle = RootRectTransform.Find("TogglesView/Reset").GetComponent<Toggle>();
        ResetImage = RootRectTransform.Find("TogglesView/Reset/Checkmark").GetComponent<Image>();
        ResetToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnResetToggle);
        ResetToggle.gameObject.SetActive(false);
        ResetImage.color = Color.gray;

        // Bone Toggle
        BoneToggle = RootRectTransform.Find("TogglesView/Bone").GetComponent<Toggle>();
        BoneImage = RootRectTransform.Find("TogglesView/Bone/Checkmark").GetComponent<Image>();
        BoneToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnBoneToggle);
        BoneToggle.gameObject.SetActive(false);
        BoneImage.color = Color.gray;

        // Tracker Toggle
        TrackerToggle = RootRectTransform.Find("TogglesView/Tracker").GetComponent<Toggle>();
        TrackerImage = RootRectTransform.Find("TogglesView/Tracker/Checkmark").GetComponent<Image>();
        TrackerToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnTrackerToggle);
        TrackerToggle.gameObject.SetActive(false);
        TrackerImage.color = Color.gray;

        // Controls
        Controls = RootRectTransform.Find("Controls").gameObject;
        ControlsExtra = RootRectTransform.Find("Controls/Extra").GetComponent<TextMeshProUGUI>();
        ControlPrevious = RootRectTransform.Find("Controls/Previous").GetComponent<Button>();
        ControlPrevious.onClick.AddListener(Events.DebuggerMenu.OnControlsPrevious);
        ControlNext = RootRectTransform.Find("Controls/Next").GetComponent<Button>();
        ControlNext.onClick.AddListener(Events.DebuggerMenu.OnControlsNextPage);

        // Content
        ContentRectTransform = RootRectTransform.Find("Scroll View/Viewport/Content").GetComponent<RectTransform>();

        // Save templates
        TemplateCategory = RootRectTransform.Find("Templates/Template_Category").gameObject;
        TemplateCategoryEntry = RootRectTransform.Find("Templates/Template_CategoryEntry").gameObject;

        // Visualizers
        CurrentEntityPointerList = new List<PointerVisualizer>();
        CurrentEntityTriggerList = new List<TriggerVisualizer>();
        CurrentEntityBoneList = new List<GameObjectVisualizer>();
        CurrentEntityTrackerList = new List<GameObjectVisualizer>();
    }

    private static int _currentHandlerIndex;
    private static MenuHandler _currentHandler;
    private static readonly List<MenuHandler> Handlers = new();

    private void ResetToMenu() {
        RootRectTransform.SetParent(RootQuickMenu, true);
        RootRectTransform.transform.localPosition = Vector3.zero;
        RootRectTransform.transform.localRotation = Quaternion.identity;
        RootRectTransform.transform.localScale = new Vector3(0.0004f, 0.0004f, 0.0004f);
        RootRectTransform.anchoredPosition = new Vector2(-0.5f - (RootRectTransform.rect.width*0.0004f/2), 0);
        gameObject.SetActive(Events.QuickMenu.IsQuickMenuOpened);
    }

    private void OnDisable() {
        Highlighter.ClearTargetHighlight();
    }

    private void Start() {
        RootQuickMenu = transform.parent;
        ResetToMenu();
        StartMenuAux();

        Events.DebuggerMenu.TextMeshProUGUIDestroyed += tmpText => {
            // Remove destroyed tmp texts from our caches
            TextMeshProUGUIParam.Remove(tmpText);
            TextMeshProUGUIParams.Remove(tmpText);
        };

        Events.QuickMenu.QuickMenuIsShownChanged += isShown => {
            UpdateAuxMenu();

            if (PinToggle.isOn || HudToggle.isOn) return;
            gameObject.SetActive(isShown);
        };

        void SwitchMenu(bool next) {
            // We can't switch if we only have one handler
            if (Handlers.Count <= 1) return;

            _currentHandlerIndex = (_currentHandlerIndex + (next ? 1 : -1) + Handlers.Count) % Handlers.Count;
            _currentHandler.Unload();

            // Reset inspected entity (since we're changing menu)
            Events.DebuggerMenu.OnSwitchInspectedEntity(false);
            Events.DebuggerMenu.OnSwitchInspectedEntity(true);

            // Hide the controls (they'll be shown in the handler if they need
            ShowControls(false);

            _currentHandler = Handlers[_currentHandlerIndex];
            _currentHandler.Load(this);

            // Recover from a crash
            crashed = false;
        }

        Events.DebuggerMenu.MainNextPage += () => SwitchMenu(true);
        Events.DebuggerMenu.MainPreviousPage += () => SwitchMenu(false);

        // Recover from a crash
        Events.DebuggerMenu.ControlsNextPage += () => crashed = false;
        Events.DebuggerMenu.ControlsPreviousPage += () => crashed = false;

        // Add Toggle handlers
        void UpdatePinAndHud() {
            if (PinToggle.isOn) {
                gameObject.SetActive(true);
                var pos = transform.position;
                var rot = transform.rotation;
                RootRectTransform.transform.SetParent(null, true);
                RootRectTransform.transform.SetPositionAndRotation(pos, rot);
            }
            else if (HudToggle.isOn) {
                gameObject.SetActive(true);
                var target = MetaPort.Instance.isUsingVr
                    ? PlayerSetup.Instance.vrCamera
                    : PlayerSetup.Instance.desktopCamera;
                RootRectTransform.transform.SetParent(target.transform, true);
            }
            else {
                ResetToMenu();
            }
            UpdateAuxMenu();
        }
        void UpdateGrabToggle() {
            GrabToggle.gameObject.SetActive(PinToggle.isOn);
            GrabPickupCollider.enabled = GrabToggle.isOn && PinToggle.isOn;
        }
        void UpdateResetToggle() {
            // Check if has nothing to reset -> disable
            if (ResetToggle.isOn
                && !PointerVisualizer.HasActive()
                && !TriggerVisualizer.HasActive()
                && !GameObjectVisualizer.HasActive()
                && !TrackerToggle.isOn) {
                ResetToggle.isOn = false;
            }
            // Check if has anything to reset -> enable
            else if (!ResetToggle.isOn && (
                         PointerVisualizer.HasActive() ||
                         TriggerVisualizer.HasActive() ||
                         GameObjectVisualizer.HasActive() ||
                         TrackerToggle.isOn)) {
                ResetToggle.isOn = true;
            }
        }
        void UpdatePointerToggle() {
            var hasPointers = CurrentEntityPointerList.Count > 0;
            // Hide icon if current entity has no pointers
            PointerToggle.gameObject.SetActive(hasPointers);
            // Check where has any pointer disabled -> disable
            if (PointerToggle.isOn && CurrentEntityPointerList.Any(vis => !vis.enabled)) PointerToggle.isOn = false;
            // Check if has nothing to reset -> enable
            else if (!ResetToggle.isOn && hasPointers && CurrentEntityPointerList.All(vis => vis.enabled)) PointerToggle.isOn = true;
        }
        void UpdateTriggerToggle() {
            var hasTriggers = CurrentEntityTriggerList.Count > 0;
            // Hide icon if current entity has no triggers
            TriggerToggle.gameObject.SetActive(hasTriggers);
            // Check where has any trigger disabled hasTriggers disable
            if (TriggerToggle.isOn && CurrentEntityTriggerList.Any(vis => !vis.enabled)) TriggerToggle.isOn = false;
            // Check if has nothing to reset -> enable
            else if (!ResetToggle.isOn && hasTriggers && CurrentEntityTriggerList.All(vis => vis.enabled)) TriggerToggle.isOn = true;
        }
        void UpdateBoneToggle() {
            var hasBones = CurrentEntityBoneList.Count > 0;
            // Hide icon if current entity has no bones
            BoneToggle.gameObject.SetActive(hasBones);
            // Check where has any bone disabled hasTriggers disable
            if (BoneToggle.isOn && CurrentEntityBoneList.Any(vis => !vis.enabled)) BoneToggle.isOn = false;
            // Check if has nothing to reset -> enable
            else if (!ResetToggle.isOn && hasBones && CurrentEntityBoneList.All(vis => vis.enabled)) BoneToggle.isOn = true;
        }
        void UpdateTrackerToggle() {
            var displayToggle = MetaPort.Instance.isUsingVr
                && ((_currentHandler.GetType() == typeof(AvatarMenuHandler) && ((AvatarMenuHandler)_currentHandler).IsLocalAvatar())
                    || _currentHandler.GetType() == typeof(MiscHandler));
            // Hide icon if current entity has no trackers
            TrackerToggle.gameObject.SetActive(displayToggle);
        }
        Events.DebuggerMenu.Pinned += isPinned => {
            PinImage.color = isPinned ? Color.green : Color.white;
            if (isPinned) HudToggle.isOn = false;
            UpdatePinAndHud();
            UpdateGrabToggle();
        };
        Events.DebuggerMenu.HudToggled += isHudToggled => {
            HudImage.color = isHudToggled ? Color.green : Color.white;
            if (isHudToggled) PinToggle.isOn = false;
            UpdatePinAndHud();
        };
        Events.DebuggerMenu.GrabToggled += isGrabToggled => {
            GrabImage.color = isGrabToggled ? Color.green : Color.gray;
            UpdateGrabToggle();
        };
        Events.DebuggerMenu.PointerToggled += isToggled => {
            PointerImage.color = isToggled ? Misc.ColorBlue : Color.gray;
            CurrentEntityPointerList.ForEach(vis => vis.enabled = isToggled);
            UpdateResetToggle();
        };
        Events.DebuggerMenu.TriggerToggled += isToggled => {
            TriggerImage.color = isToggled ? Misc.ColorYellow : Color.gray;
            CurrentEntityTriggerList.ForEach(vis => vis.enabled = isToggled);
            UpdateResetToggle();
        };
        Events.DebuggerMenu.BoneToggled += isToggled => {
            BoneImage.color = isToggled ? Misc.ColorIvory : Color.gray;
            CurrentEntityBoneList.ForEach(vis => vis.enabled = isToggled);
            UpdateResetToggle();
        };
        Events.DebuggerMenu.TrackerToggled += isToggled => {
            TrackerImage.color = isToggled ? Misc.ColorBlue : Color.gray;

            // If is toggled -> Setup trackers and show the models
            if (isToggled) {
                var avatarHeight = Traverse.Create(PlayerSetup.Instance).Field("_avatarHeight").GetValue<float>();
                var trackers = IKSystem.Instance.AllTrackingPoints.FindAll((t) => t.isActive && t.isValid && t.suggestedRole != TrackingPoint.TrackingRole.Invalid);
                foreach (var tracker in trackers) {
                    if (TrackerVisualizer.Create(tracker, out var trackerVisualizer, avatarHeight)) {
                        CurrentEntityTrackerList.Add(trackerVisualizer);
                    }
                }
            }

            // Enable controller models
            IKSystem.Instance.leftHandModel.SetActive(isToggled);
            IKSystem.Instance.rightHandModel.SetActive(isToggled);

            CurrentEntityTrackerList.ForEach(vis => vis.enabled = isToggled);
            UpdateResetToggle();
        };
        Events.DebuggerMenu.ResetToggled += isToggled => {
            ResetToggle.gameObject.SetActive(isToggled);
            ResetImage.color = isToggled ? Misc.ColorOrange : Color.gray;
            if (!isToggled) {
                PointerVisualizer.DisableAll();
                TriggerVisualizer.DisableAll();
                GameObjectVisualizer.DisableAll();
                if (PointerToggle.isOn) PointerToggle.isOn = false;
                if (TriggerToggle.isOn) TriggerToggle.isOn = false;
                if (BoneToggle.isOn) BoneToggle.isOn = false;
                if (TrackerToggle.isOn) TrackerToggle.isOn = false;
            }
        };

        // Handle entity switch events
        Events.DebuggerMenu.SwitchedInspectedEntity += finishedInitializing => {

            // Cleaning up caches, since started changing entity
            if (!finishedInitializing) {
                CurrentEntityPointerList.Clear();
                CurrentEntityTriggerList.Clear();
                CurrentEntityBoneList.Clear();
            }

            // The change entity has finished, lets update the toggle states
            else {
                UpdatePointerToggle();
                UpdateTriggerToggle();
                UpdateBoneToggle();
                UpdateTrackerToggle();
                UpdateResetToggle();
            }
        };

        // Add handlers
        var avatarMenuHandler = new AvatarMenuHandler();
        Handlers.Add(avatarMenuHandler);
        Handlers.Add(new SpawnableMenuHandler());
        Handlers.Add(new MiscHandler());

        // Initialize Avatar Handler
        avatarMenuHandler.Load(this);
        _currentHandler = avatarMenuHandler;
    }

    private void Update() {

        // Update aux menu pins state (to allow call the menu back)
        if (Events.QuickMenu.IsQuickMenuOpened && (PinToggle.isOn || HudToggle.isOn)) UpdateAuxMenu();

        // Prevent updates until we swap entity
        if (crashed) return;

        // Crash wrapper to avoid giga lag
        try {
            _currentHandler?.Update(this);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
            MelonLogger.Error($"Something BORKED really bad, and to prevent lagging we're going to stop the debugger menu updates until you swap entity.");
            MelonLogger.Error($"Feel free to ping kafeijao#8342 in the #bug-reports channel of ChilloutVR Modding Group discord with the error message.");
            crashed = true;
        }
    }

    public void AddNewDebugger(string debuggerName) {
        TitleText.SetText(debuggerName);
        for (var i = 0; i < ContentRectTransform.childCount; i++) {
            // Clean all categories
            Destroy(ContentRectTransform.GetChild(i).gameObject);
        }
    }

    public void ToggleCategories(bool isShown) {
        for (var i = 0; i < ContentRectTransform.childCount; i++) {
            // Toggle all categories
            var go = ContentRectTransform.GetChild(i).gameObject;
            if (go.activeSelf != isShown) go.SetActive(isShown);
        }
    }

    public GameObject AddCategory(string categoryName) {
        GameObject newCategory = Instantiate(TemplateCategory, ContentRectTransform);
        newCategory.transform.Find("Header").GetComponent<TextMeshProUGUI>().SetText(categoryName);
        newCategory.SetActive(true);
        return newCategory;
    }

    public TextMeshProUGUI AddCategoryEntry(GameObject category, string entryName) {
        // Add category entry with fixed name
        var categoryEntries = category.transform.Find("Entries");
        GameObject newEntry = Instantiate(TemplateCategoryEntry, categoryEntries.transform);
        newEntry.SetActive(true);
        newEntry.transform.Find("Key").GetComponent<TextMeshProUGUI>().SetText(entryName);
        return newEntry.transform.Find("Value").GetComponent<TextMeshProUGUI>();
    }

    public (TextMeshProUGUI, TextMeshProUGUI) AddCategoryEntry(GameObject category) {
        // Add category entry with variable name
        var categoryEntries = category.transform.Find("Entries");
        GameObject newEntry = Instantiate(TemplateCategoryEntry, categoryEntries.transform);
        newEntry.SetActive(true);
        var value = newEntry.transform.Find("Value").GetComponent<TextMeshProUGUI>();
        value.text = "";
        return (newEntry.transform.Find("Key").GetComponent<TextMeshProUGUI>(), value);
    }

    public void ClearCategory(GameObject category) {
        var entriesTransform = category.transform.Find("Entries").transform;
        for (var i = 0; i < entriesTransform.childCount; i++) {
            Destroy(entriesTransform.GetChild(i).gameObject);
        }
    }

    public void ShowControls(bool show) {
        if (Controls.activeSelf != show) Controls.SetActive(show);
    }

    public void SetControlsExtra(string extra) {
        ControlsExtra.SetText(extra);
    }

    public string GetUsername(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        return Events.Player.PlayersUsernamesCache.ContainsKey(guid) ? Events.Player.PlayersUsernamesCache[guid] : $"Unknown [{guid}]";
    }

    public string GetSpawnableName(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        var croppedGuid = guid.Length == 36 ? guid.Substring(guid.Length - 12) : guid;
        return Events.Spawnable.SpawnableNamesCache.ContainsKey(guid) ? Events.Spawnable.SpawnableNamesCache[guid] : $"Unknown [{croppedGuid}]";
    }

    public string GetAvatarName(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        var croppedGuid = guid.Length == 36 ? guid.Substring(guid.Length - 12) : guid;
        return Events.Avatar.AvatarsNamesCache.ContainsKey(guid) ? Events.Avatar.AvatarsNamesCache[guid] : $"Unknown [{croppedGuid}]";
    }

    public static string GetTimeDifference(float time) {
        var timeDiff = Time.time - time;
        return timeDiff > 10f ? "10.00+" : timeDiff.ToString("0.00");
    }

    private static bool _warnedMissingTypeValueChange;
    private static bool HasValueChanged(object value, object cached) {

        switch (cached) {
            case float cachedFloat when value is float floatValue:
                if (Mathf.Approximately(cachedFloat, floatValue)) {
                    // MelonLogger.Msg($"\t[HAS NOT CHANGED] [Floats] {cachedFloat} +/- {floatValue}");
                    return false;
                }
                break;
            case bool cachedBool when value is bool boolValue:
                if (cachedBool == boolValue) {
                    // MelonLogger.Msg($"\t[HAS NOT CHANGED] [Booleans] {cachedBool} == {boolValue}");
                    return false;
                }
                break;
            case int cachedInt when value is int intValue:
                if (cachedInt == intValue) {
                    // MelonLogger.Msg($"\t[HAS NOT CHANGED] [Ints] {cachedInt} == {intValue}");
                    return false;
                }
                break;
            case string cachedString when value is string stringValue:
                if (cachedString == stringValue) {
                    // MelonLogger.Msg($"\t[HAS NOT CHANGED] [Strings] {cachedString} == {stringValue}");
                    return false;
                }
                break;
            default: {
                // If the type hasn't changed, warn one time. This should never happen.
                if (!_warnedMissingTypeValueChange && cached?.GetType() == value?.GetType()) {
                    MelonLogger.Error($"Attempted to check if a value changed between unsupported values...\n\t\t" +
                                      $"Values compared: {cached} ({cached?.GetType()}) == {value} ({value?.GetType()}");
                    _warnedMissingTypeValueChange = true;
                }
                break;
            }
        }
        // MelonLogger.Msg($"\t[HAS CHANGED] {cached} ({cached.GetType()}) != {value} ({value.GetType()})");
        return true;
    }

    internal static bool HasValueChanged(TextMeshProUGUI tmpText, object value) {
        if (TextMeshProUGUIParam.ContainsKey(tmpText) && !HasValueChanged(value, TextMeshProUGUIParam[tmpText])) {
            // MelonLogger.Msg($"[Single] [HAS NOT CHANGED]:");
            return false;
        }
        TextMeshProUGUIParam[tmpText] = value;
        //MelonLogger.Msg($"[Single] [HAS CHANGED]: [{TextMeshProUGUIParam[tmpText].GetType()}]{TextMeshProUGUIParam[tmpText]} -> [{value}]{value}");
        return true;
    }

    internal static bool HasValueChanged(TextMeshProUGUI tmpText, object[] values) {
        if (TextMeshProUGUIParams.ContainsKey(tmpText)) {
            var cachedValues = TextMeshProUGUIParams[tmpText];
            if (cachedValues.Length == values.Length) {
                // If there are no parameters that changed -> return false
                if (!values.Where((t, i) => HasValueChanged(t, cachedValues[i])).Any()) {
                    // MelonLogger.Msg($"[Multi] [HAS NOT CHANGED]:");
                    return false;
                }
            }
        }
        TextMeshProUGUIParams[tmpText] = values;
        //MelonLogger.Msg($"[Single] [HAS CHANGED]");
        return true;
    }
}
