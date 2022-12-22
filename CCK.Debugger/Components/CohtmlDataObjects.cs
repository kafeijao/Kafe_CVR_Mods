using Newtonsoft.Json;
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

        // Create temp buttons
        var boneButton = AddButton(new Button { Type = "Bone", IsOn = false, IsVisible = true });
        var grabButton = AddButton(new Button { Type = "Grab", IsOn = false, IsVisible = true });
        var hudButton = AddButton(new Button { Type = "Hud", IsOn = false, IsVisible = true });
        var pinButton = AddButton(new Button { Type = "Pin", IsOn = false, IsVisible = true });
        var pointerButton = AddButton(new Button { Type = "Pointer", IsOn = false, IsVisible = true });
        var resetButton = AddButton(new Button { Type = "Reset", IsOn = true, IsVisible = false });
        var trackerButton = AddButton(new Button { Type = "Tracker", IsOn = false, IsVisible = true });
        var triggerButton = AddButton(new Button { Type = "Trigger", IsOn = false, IsVisible = true });

        _instance = this;
    }

    [JsonProperty("Sections")] private List<Section> Sections { get; } = new();
    [JsonProperty("Buttons")] private List<Button> Buttons { get; } = new();

    public Section AddSection(string title) {
        var section = new Section(this) { Title = title };
        Sections.Add(CacheSection(section));
        return section;
    }

    public Button AddButton(Button button) {
        Buttons.Add(CacheButton(button));
        return button;
    }

    public static bool GetButton(string name, out Button button) {
        button = _instance.Buttons.FirstOrDefault(button => button.Type == name);
        return button != null;
    }

    public void UpdateCore(bool showControls, string controlsInfo, bool showSections) {
        if (showControls == _info.ShowControls && controlsInfo.Equals(_info.ControlsInfo) && showSections == _info.ShowSections) return;
        _info.ShowControls = showControls;
        _info.ControlsInfo = controlsInfo;
        _info.ShowSections = showSections;
        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreInfoUpdate(_info);
    }

    // public void UpdateSection(int id, string value) {
    //     lock (_sections) {
    //         if (!_sections.ContainsKey(id)) return;
    //         _sections[id].Value = value;
    //     }
    // }
    //
    // public void UpdateButton(int id, bool isOn, bool isVisible) {
    //     lock (_buttons) {
    //         if (!_buttons.ContainsKey(id)) return;
    //         var button = _buttons[id];
    //         button.IsOn = isOn;
    //         button.IsVisible = isVisible;
    //     }
    // }

    public static void ClickButton(int buttonId) {
        _instance?.Buttons.FirstOrDefault(button => button.Id == buttonId)?.Click();
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
    [JsonIgnore] private readonly Dictionary<int, Button> _buttons = new();
    public Button CacheButton(Button button) {
        lock (_buttons) {
            button.Id = _buttons.Count;
            _buttons[button.Id] = button;
            return button;
        }
    }

    [JsonIgnore] private static Core _instance;
}

public class Section {

    public int Id { get; set; }
    public string Title { get; set; }
    public string Value { get; set; }
    [JsonProperty("SubSections")] public List<Section> SubSections { get; } = new();


    // Internal
    [JsonIgnore] private Core _core;
    public Section(Core core) {
        _core = core;
    }
    public Section AddSection(string title, string value = "") {
        var section = new Section(_core) { Title = title, Value = value};
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
    public int Id { get; set; }
    public string Type { get; set; }
    public bool IsOn { get; set; }
    public bool IsVisible { get; set; }

    public void Click() {
        Events.DebuggerMenuCohtml.OnCohtmlMenuButtonClick(this);
    }
}
