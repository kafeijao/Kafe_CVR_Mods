using System.Collections.ObjectModel;
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

        // Create buttons
        AddButton(new Button { Type = Button.ButtonType.Bone, IsOn = false, IsVisible = true });
        AddButton(new Button { Type = Button.ButtonType.Grab, IsOn = false, IsVisible = false });
        AddButton(new Button { Type = Button.ButtonType.Hud, IsOn = false, IsVisible = true });
        AddButton(new Button { Type = Button.ButtonType.Pin, IsOn = false, IsVisible = true });
        AddButton(new Button { Type = Button.ButtonType.Pointer, IsOn = false, IsVisible = true });
        AddButton(new Button { Type = Button.ButtonType.Reset, IsOn = true, IsVisible = false });
        AddButton(new Button { Type = Button.ButtonType.Tracker, IsOn = false, IsVisible = true });
        AddButton(new Button { Type = Button.ButtonType.Trigger, IsOn = false, IsVisible = true });

        _instance = this;
    }

    [JsonProperty("Sections")] private List<Section> Sections { get; } = new();
    [JsonProperty("Buttons")] private List<Button> Buttons { get; } = new();

    public Section AddSection(string title, bool collapsable = false) {
        var section = new Section(this) { Title = title, Collapsable = collapsable };
        Sections.Add(CacheSection(section));
        return section;
    }

    private void AddButton(Button button) => Buttons.Add(CacheButton(button));

    public static bool GetButton(Button.ButtonType type, out Button button) {
        button = _instance?._buttons[type];
        return button != null;
    }
    public static Button GetButton(Button.ButtonType type) => _instance?._buttons[type];
    public static ReadOnlyCollection<Button> GetButtons() => _instance?.Buttons.AsReadOnly();


    public void UpdateCore(bool showControls, string controlsInfo, bool showSections) {
        if (showControls == _info.ShowControls && controlsInfo.Equals(_info.ControlsInfo) && showSections == _info.ShowSections) return;
        _info.ShowControls = showControls;
        _info.ControlsInfo = controlsInfo;
        _info.ShowSections = showSections;
        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreInfoUpdate(_info);
    }

    public static void ClickButton(Button.ButtonType buttonType) {
        _instance?._buttons[buttonType]?.Click();
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

    [JsonIgnore] private static Core _instance;
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
    public ButtonType Type { get; set; }
    public bool IsOn { get; set; }
    public bool IsVisible { get; set; }

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

    public void Click() {
        Events.DebuggerMenuCohtml.OnCohtmlMenuButtonClick(this);
    }
}
