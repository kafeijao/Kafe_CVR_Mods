using CCK.Debugger.Components.GameObjectVisualizers;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace CCK.Debugger.Components;

public class Core {

    public class Info {
        public string MenuName { get; set; }
        public bool ShowControls { get; set; }
        public string ControlsInfo { get; set; }
        public bool ShowSections { get; set; }
    }

    [JsonProperty("Info")] private Info _info;

    public Core(string menuName, bool showControls = false, string controlsInfo = "", bool showSections= true) {
        _info = new Info {
            MenuName = menuName,
            ShowControls = showControls,
            ControlsInfo = controlsInfo,
            ShowSections = showSections,
        };

        // Buttons initialization
        var grabButton = AddButton(new Button(Button.ButtonType.Grab, false, false));
        var hud = AddButton(new Button(Button.ButtonType.Hud, false, true));
        var pin = AddButton(new Button(Button.ButtonType.Pin, false, true));
        var reset = AddButton(new Button(Button.ButtonType.Reset, true, false, false));

        // Grab Button Handlers
        grabButton.StateUpdater = button => {
            button.IsOn = CohtmlMenuController.Instance.Pickup.enabled;
            button.IsVisible = CohtmlMenuController.Instance.CurrentMenuParent == CohtmlMenuController.MenuTarget.World;
        };
        grabButton.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            CohtmlMenuController.Instance.Pickup.enabled = button.IsOn;
        };

        // HUD Button Handlers
        hud.StateUpdater = button => {
            button.IsOn = CohtmlMenuController.Instance.CurrentMenuParent == CohtmlMenuController.MenuTarget.HUD;
        };
        hud.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            pin.IsOn = false;
            CohtmlMenuController.Instance.ParentTo(button.IsOn ? CohtmlMenuController.MenuTarget.HUD : CohtmlMenuController.MenuTarget.QuickMenu);
        };

        // Pin Button Handlers
        pin.StateUpdater = button => {
            button.IsOn = CohtmlMenuController.Instance.CurrentMenuParent == CohtmlMenuController.MenuTarget.World;
        };
        pin.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            hud.IsOn = false;
            // Disable the grab if it's on
            if (!button.IsOn && grabButton.IsOn) grabButton.Click();
            CohtmlMenuController.Instance.ParentTo(button.IsOn ? CohtmlMenuController.MenuTarget.World : CohtmlMenuController.MenuTarget.QuickMenu);
        };

        // Reset Button Handlers
        reset.StateUpdater = button => {
            var hasActive = PointerVisualizer.HasActive() || TriggerVisualizer.HasActive() || GameObjectVisualizer.HasActive();
            button.IsOn = hasActive;
        };
        reset.ClickHandler = button => {
            button.IsOn = !button.IsOn;

            // Reset all buttons (if available)
            if (GetButton(Button.ButtonType.Pointer, out var pointerButton) && pointerButton.IsOn) {
                pointerButton.IsOn = false;
            }
            if (GetButton(Button.ButtonType.Trigger, out var triggerButton) && triggerButton.IsOn) {
                triggerButton.IsOn = false;
            }
            if (GetButton(Button.ButtonType.Bone, out var boneButton) && boneButton.IsOn) {
                boneButton.IsOn = false;
            }
            if (GetButton(Button.ButtonType.Tracker, out var trackerButton) && trackerButton.IsOn) {
                trackerButton.IsOn = false;
            }

            // Disable all visualizers
            PointerVisualizer.DisableAll();
            TriggerVisualizer.DisableAll();
            GameObjectVisualizer.DisableAll();
        };

        Instance = this;
    }

    [JsonProperty("Sections")] private List<Section> Sections { get; } = new();
    [JsonProperty("Buttons")] private List<Button> Buttons { get; } = new();

    public Section AddSection(string title, bool collapsable = false) {
        var section = new Section(this) { Title = title, Collapsable = collapsable };
        Sections.Add(CacheSection(section));
        return section;
    }

    internal Button AddButton(Button button) {
        Buttons.Add(CacheButton(button));
        return button;
    }

    private static bool GetButton(Button.ButtonType type, out Button button) {
        button = Instance?._buttons?.GetValueOrDefault(type, null);
        return button != null;

    }

    public void UpdateCore(bool showControls, string controlsInfo, bool showSections) {
        if (showControls == _info.ShowControls && controlsInfo.Equals(_info.ControlsInfo) && showSections == _info.ShowSections) return;
        _info.ShowControls = showControls;
        _info.ControlsInfo = controlsInfo;
        _info.ShowSections = showSections;
        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreInfoUpdate(_info);
    }

    public static void UpdateButtonsState() {
        Instance?.Buttons.ForEach(button => button.UpdateState());
    }

    public static void ClickButton(Button.ButtonType buttonType) {
        Instance?._buttons?.GetValueOrDefault(buttonType, null)?.Click();
    }

    // Internal
    [JsonIgnore] private readonly Dictionary<int, Section> _sections = new();
    public Section CacheSection(Section section) {
        lock (_sections) {
            section.Id = _sections.Count;
            _sections[section.Id] = section;
            return section;
        }
    }
    [JsonIgnore] private readonly Dictionary<Button.ButtonType, Button> _buttons = new();
    private Button CacheButton(Button button) {
        lock (_buttons) {
            _buttons[button.Type] = button;
            return button;
        }
    }

    [JsonIgnore] internal static Core Instance;
}

public class Section {

    public int Id { get; set; }
    public string Title { get; set; }
    public string Value { get; set; }
    public bool Collapsable { get; set; }
    [JsonProperty("SubSections")] public List<Section> SubSections { get; } = new();


    // Internal
    [JsonIgnore] private Core _core;
    public Section(Core core) {
        _core = core;
    }
    public Section AddSection(string title, string value = "", bool collapsable = false) {
        var section = new Section(_core) { Title = title, Collapsable = collapsable, Value = value};
        SubSections.Add(_core.CacheSection(section));
        return section;
    }
    public void Update(string value) {
        if (value == Value) return;

        // Spread updates over 10 frames
        if (Time.frameCount % 10 != Id % 10) return;

        Value = value;
        Events.DebuggerMenuCohtml.OnCohtmlMenuSectionUpdate(this);
    }
    [JsonIgnore] private Func<string> _valueGetter;
    public Section AddValueGetter(Func<string> valueGetter) {
        _valueGetter = valueGetter;
        _hasValueGetter = true;
        return this;
    }
    [JsonIgnore] private bool _hasValueGetter;
    public void UpdateFromGetter(bool recursive = false) {
        if (_hasValueGetter) Update(_valueGetter());

        if (!recursive) return;
        // Iterate all sub-sections and also update them if recursive
        foreach (var subSection in SubSections) {
            subSection.UpdateFromGetter(true);
        }
    }
}

public class Button {

    public Button(ButtonType buttonType, bool initialIsOn, bool initialIsVisible, bool isVisibleWhenOff = true) {
        Type = buttonType;
        _isOn = initialIsOn;
        _isVisible = initialIsVisible;
        _isVisibleWhenOff = isVisibleWhenOff;
    }

    public ButtonType Type { get; }

    [JsonProperty("IsOn")] private bool _isOn;
    [JsonIgnore] public bool IsOn {
        get => _isOn;
        set {
            _isOn = value;
            if (!_isVisibleWhenOff) _isVisible = _isOn;
            Events.DebuggerMenuCohtml.OnCohtmlMenuButtonUpdate(this);
        }
    }

    [JsonProperty("IsVisible")] private bool _isVisible;
    [JsonIgnore]
    public bool IsVisible {
        get => _isVisible;
        set {
            if (value == _isVisible) return;
            _isVisible = value;
            Events.DebuggerMenuCohtml.OnCohtmlMenuButtonUpdate(this);
        }
    }

    [JsonIgnore] private readonly bool _isVisibleWhenOff;

    [JsonIgnore] public Action<Button> StateUpdater;
    [JsonIgnore] public Action<Button> ClickHandler;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ButtonType {
        Bone,
        Grab,
        Hud,
        Pin,
        Pointer,
        Reset,
        Tracker,
        Trigger,
    }

    public void Click() => ClickHandler?.Invoke(this);
    public void UpdateState() => StateUpdater?.Invoke(this);
}
