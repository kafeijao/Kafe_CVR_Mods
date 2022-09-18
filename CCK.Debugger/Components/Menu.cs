using CCK.Debugger.Components.MenuHandlers;
using CCK.Debugger.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CCK.Debugger.Components;


public class Menu : MonoBehaviour {

    // Main
    private RectTransform RootRectTransform;
    private Transform RootQuickMenu;

    // Pin Toggle
    private Toggle PinToggle;
    private Image PinImage;

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

    void Awake() {

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
        PinToggle = RootRectTransform.Find("PinToggle").GetComponent<Toggle>();
        PinImage = RootRectTransform.Find("PinToggle/Checkmark").GetComponent<Image>();
        PinToggle.onValueChanged.AddListener(Events.DebuggerMenu.OnPinned);

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
    }

    private static int _currentHandlerIndex;
    private static IMenuHandler _currentHandler;
    private static readonly List<IMenuHandler> Handlers = new();

    private void ResetToMenu() {
        RootRectTransform.SetParent(RootQuickMenu, true);
        RootRectTransform.transform.localPosition = Vector3.zero;
        RootRectTransform.transform.localRotation = Quaternion.identity;
        RootRectTransform.transform.localScale = new Vector3(0.0004f, 0.0004f, 0.0004f);
        RootRectTransform.anchoredPosition = new Vector2(-0.5f - (RootRectTransform.rect.width*0.0004f/2), 0);
        gameObject.SetActive(Events.QuickMenu.IsQuickMenuOpened);
        PinImage.color = Color.white;
    }

    private void OnDisable() {
        Highlighter.ClearTargetHighlight();
    }

    private void Start() {
        RootQuickMenu = transform.parent;
        ResetToMenu();

        Events.QuickMenu.QuickMenuIsShownChanged += isShown => {
            if (!PinToggle.isOn) return;
            gameObject.SetActive(isShown);
        };

        Events.DebuggerMenu.Pinned += isPinned => {
            if (!isPinned) {
                gameObject.SetActive(true);
                var pos = transform.position;
                var rot = transform.rotation;
                RootRectTransform.transform.SetParent(null, true);
                RootRectTransform.transform.SetPositionAndRotation(pos, rot);
                PinImage.color = Color.green;
            }
            else {
                ResetToMenu();
            }
        };

        void SwitchMenu(bool next) {
            // We can't switch if we only have one handler
            if (Handlers.Count <= 1) return;

            _currentHandlerIndex = (_currentHandlerIndex + (next ? 1 : -1) + Handlers.Count) % Handlers.Count;
            _currentHandler.Unload();
            _currentHandler = Handlers[_currentHandlerIndex];
            _currentHandler.Load(this);
        }

        Events.DebuggerMenu.MainNextPage += () => SwitchMenu(true);
        Events.DebuggerMenu.MainPreviousPage += () => SwitchMenu(false);

        // Add handlers
        var avatarMenuHandler = new AvatarMenuHandler();
        Handlers.Add(avatarMenuHandler);
        Handlers.Add(new SpawnableMenuHandler());

        // Initialize Avatar Handler
        avatarMenuHandler.Load(this);
        _currentHandler = avatarMenuHandler;
    }

    private void Update() {
        _currentHandler?.Update(this);
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
}
