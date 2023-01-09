using System.Collections.ObjectModel;
using ABI_RC.Core.Savior;
using CCK.Debugger.Components.CohtmlMenuHandlers;
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
        public bool MenuEnabled { get; set; }
        public bool ShowControls { get; set; }
        public string ControlsInfo { get; set; }
        public bool ShowSections { get; set; }
    }

    [JsonIgnore] internal static Core Instance;
    [JsonIgnore] private int _indexToAdd = 0;

    [JsonProperty("Info")] private Info _info;

    public Core(string menuName, bool showControls = false, string controlsInfo = "", bool showSections = true, bool menuEnabled = true) {
        _info = new Info {
            MenuName = menuName,
            ShowControls = showControls,
            ControlsInfo = controlsInfo,
            ShowSections = showSections,
            MenuEnabled = menuEnabled,
        };

        // Buttons initialization
        var powerButton = AddButton(new Button(Button.ButtonType.Power, false, true));
        var grabButton = AddButton(new Button(Button.ButtonType.Grab, false, false));
        var hud = AddButton(new Button(Button.ButtonType.Hud, false, true));
        var pin = AddButton(new Button(Button.ButtonType.Pin, false, true));
        var reset = AddButton(new Button(Button.ButtonType.Reset, true, false, false), true);

        // Power Button Handlers
        powerButton.StateUpdater = button => {
            button.IsOn = CohtmlMenuController.Instance.Enabled;
        };
        powerButton.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            if (button.IsOn) {
                ICohtmlHandler.Reload();
            }
            else {
                ICohtmlHandler.Shutdown();
            }
        };

        // Grab Button Handlers
        grabButton.StateUpdater = button => {
            var canBeOn = CohtmlMenuController.Instance.CurrentMenuParent == CohtmlMenuController.MenuTarget.World;
            var isOn = CohtmlMenuController.Instance.Pickup.enabled;
            CohtmlMenuController.Instance.Pickup.enabled = canBeOn && isOn;
            button.IsOn = isOn;
            button.IsVisible = canBeOn;
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
            var hasActive = PointerVisualizer.HasActive() ||
                            TriggerVisualizer.HasActive() ||
                            GameObjectVisualizer.HasActive() ||
                            TrackerVisualizer.HasTrackersActive() && MetaPort.Instance.isUsingVr;
            button.IsOn = hasActive || ICohtmlHandler.Crashed;
        };
        reset.ClickHandler = button => {
            button.IsOn = !button.IsOn;

            // If the menu is crashed, attempt to reload
            if (ICohtmlHandler.Crashed) {
                ICohtmlHandler.Reload();
                return;
            }

            ICohtmlHandler.DisableEverything();
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

    internal Button AddButton(Button button, bool atTheEnd = false) {
        // Insert the button, using an _indexToAdd to enable adding stuff to the end or not
        Buttons.Insert(atTheEnd ? Buttons.Count : _indexToAdd++, CacheButton(button));
        return button;
    }

    internal static bool GetButton(Button.ButtonType type, out Button button) {
        button = null;
        var found = Instance?._buttons?.TryGetValue(type, out button);
        return found.HasValue && found.Value;
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

    public static void UpdateButtonsVisibilityTo(bool isVisible) {
        Instance?.Buttons.ForEach(button => button.IsVisible = isVisible);
    }

    public static void UpdateSectionsFromGetters() {
        Instance?.Sections.ForEach(section => section.UpdateFromGetter(true));
    }

    public static void ClickButton(Button.ButtonType buttonType) {
        Button button = null;
        Instance?._buttons?.TryGetValue(buttonType, out button);
        button?.Click();
    }

    // Internal
    [JsonIgnore] private readonly Dictionary<int, Section> _sections = new();
    [JsonIgnore] private int _sectionCurrentID = 0;
    [JsonIgnore] private readonly Queue<int> _sectionFreeIds = new();
    public Section CacheSection(Section section) {
        lock (_sections) {
            section.Id = _sectionFreeIds.TryDequeue(out var reusableID) ? reusableID : _sectionCurrentID++;
            _sections[section.Id] = section;
            return section;
        }
    }
    public void RemoveCacheSection(Section section) {
        lock (_sections) {
            _sections.Remove(section.Id);
            _sectionFreeIds.Enqueue(section.Id);
        }
    }
    [JsonIgnore] private readonly Dictionary<Button.ButtonType, Button> _buttons = new();
    private Button CacheButton(Button button) {
        lock (_buttons) {
            _buttons[button.Type] = button;
            return button;
        }
    }
}

public class Section {

    public int Id { get; set; }
    public string Title { get; set; }
    public string Value { get; set; }
    public bool Collapsable { get; set; }
    public bool DynamicSubsections { get; set; }
    [JsonProperty("OldSubSectionIDs")] private List<long> OldSubSectionIDs { get; } = new();
    [JsonProperty("SubSections")] public List<Section> SubSections { get; } = new();


    // Internal
    [JsonIgnore] private Core _core;
    [JsonIgnore] private bool _dynamicSubsectionsUpdated;
    [JsonIgnore] private List<Section> _dynamicSubsectionsLatest;
    public Section(Core core) => _core = core;
    public Section AddSection(string title, string value = "", bool collapsable = false, bool dynamicSubsections = false) {
        var section = new Section(_core) { Title = title, Collapsable = collapsable, Value = value, DynamicSubsections = dynamicSubsections};
        SubSections.Add(_core.CacheSection(section));
        return section;
    }
    private bool AreSubsectionsEqual(List<Section> newUncachedSections) {
        return newUncachedSections.Count == SubSections.Count &&
               newUncachedSections.Zip(SubSections, (item1, item2) => (item1, item2))
                   .All(x =>
                       x.item1.Title == x.item2.Title &&
                       x.item1.Value == x.item2.Value &&
                       x.item1.AreSubsectionsEqual(x.item2.SubSections));
    }
    public void QueueDynamicSectionsUpdate(List<Section> newUncachedSections) {
        if (AreSubsectionsEqual(_dynamicSubsectionsUpdated ? _dynamicSubsectionsLatest : newUncachedSections)) return;
        _dynamicSubsectionsLatest = newUncachedSections;
        _dynamicSubsectionsUpdated = true;
    }

    private void AddNewSubsections(List<Section> newUncachedSections) {
        foreach (var newUncachedSection in newUncachedSections) {
            SubSections.Add(_core.CacheSection(newUncachedSection));
            newUncachedSection.AddNewSubsections(newUncachedSection.SubSections);
        }
    }
    private void ClearSubSections(List<long> oldSectionIds) {
        OldSubSectionIDs.Clear();
        foreach (var section in SubSections) {
            section.ClearSubSections(oldSectionIds);
            _core.RemoveCacheSection(section);
            oldSectionIds.Add(section.Id);
        }
        SubSections.Clear();
    }
    public void Update(string value) {
        if (value == Value && !_dynamicSubsectionsUpdated) return;

        // Spread updates over 10 frames
        if (Time.frameCount % 10 != Id % 10) return;

        // If we're going to consume the dynamic subsection update, lets commit the latest update
        if (_dynamicSubsectionsUpdated) {
            ClearSubSections(OldSubSectionIDs);
            AddNewSubsections(_dynamicSubsectionsLatest);
        }

        Value = value;
        _dynamicSubsectionsUpdated = false;
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
        Eye,
        Power,
    }

    public void Click() => ClickHandler?.Invoke(this);
    public void UpdateState() => StateUpdater?.Invoke(this);
}
