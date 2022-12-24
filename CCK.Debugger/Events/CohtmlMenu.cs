using CCK.Debugger.Components;
using MelonLoader;

namespace CCK.Debugger.Events;

public static class DebuggerMenuCohtml {

    public static event Action CohtmlMenuReloaded;
    public static void OnCohtmlMenuReload() {
        CohtmlMenuReloaded?.Invoke();
    }


    public static event Action<Core> CohtmlMenuCoreCreated;
    private static Core _latestCore;
    private static readonly object LatestCoreLock = new();
    private static bool _latestCoreConsumed = true;
    public static void OnCohtmlMenuCoreCreate(Core core) {
        CohtmlMenuCoreCreated?.Invoke(core);
        lock (LatestCoreLock) {
            _latestCore = core;
            _latestCoreConsumed = false;
        }
        //MelonLogger.Msg($"Core Created\n{JsonConvert.SerializeObject(core, Formatting.Indented)}");
    }
    public static bool GetLatestCoreToConsume(out Core core) {
        lock (LatestCoreLock) {
            core = _latestCore;
            if (_latestCoreConsumed) return false;

            _latestCoreConsumed = true;

            // Clear/invalidate other caches
            _latestCoreInfoConsumed = true;
            _latestSectionUpdates.Clear();
            _latestSectionUpdatesConsumed = true;
            _latestButtonUpdates.Clear();
            _latestButtonUpdatesConsumed = true;

            return true;
        }
    }


    public static event Action<Core.Info> CohtmlMenuCoreInfoUpdated;
    private static Core.Info _latestCoreInfo;
    private static readonly object LatestCoreInfoLock = new();
    private static bool _latestCoreInfoConsumed = true;
    public static void OnCohtmlMenuCoreInfoUpdate(Core.Info coreInfo) {
        CohtmlMenuCoreInfoUpdated?.Invoke(coreInfo);
        lock (LatestCoreInfoLock) lock (LatestCoreLock) {
            _latestCoreInfo = coreInfo;
            _latestCoreInfoConsumed = false;
        }
        //MelonLogger.Msg($"Core Updated\n{JsonConvert.SerializeObject(coreInfo, Formatting.Indented)}");
    }
    public static bool GetLatestCoreInfoToConsume(out Core.Info coreInfo) {
        lock (LatestCoreInfoLock) lock (LatestCoreLock) {
            coreInfo = _latestCoreInfo;
            if (_latestCoreInfoConsumed) return false;

            _latestCoreInfoConsumed = true;
            return true;
        }
    }

    public static event Action<Section> CohtmlMenuSectionUpdated;
    private static Dictionary<int, Section> _latestSectionUpdates = new();
    private static bool _latestSectionUpdatesConsumed = true;
    private static bool _erroredSections;
    public static void OnCohtmlMenuSectionUpdate(Section section) {

        CohtmlMenuSectionUpdated?.Invoke(section);

        if (_erroredSections) return;

        lock (_latestSectionUpdates) lock (LatestCoreLock) {
            // Check to prevent memory leaks
            if (_latestSectionUpdates.Count > 10000) {
                _erroredSections = true;
                MelonLogger.Error("We reached over 10000 section updates... We're going to stop tracking the updates." +
                                  "This is to prevent memory leaks, contact the mod creator to fix this issue.");
                return;
            }
            _latestSectionUpdates[section.Id] = section;
            _latestSectionUpdatesConsumed = false;
        }
        //MelonLogger.Msg($"Section Updated\n{JsonConvert.SerializeObject(section, Formatting.Indented)}");
    }
    public static bool GetLatestSectionUpdatesToConsume(out Section[] sectionUpdates) {
        lock (_latestSectionUpdates) lock (LatestCoreLock) {
            sectionUpdates = null;
            if (_latestSectionUpdatesConsumed) return false;

            sectionUpdates = _latestSectionUpdates.Values.ToArray();
            // Reset the list
            _latestSectionUpdates.Clear();
            if (_latestSectionUpdatesConsumed) return false;
            _latestSectionUpdatesConsumed = true;
            return true;
        }
    }

    public static event Action<Button> CohtmlMenuButtonUpdated;
    private static Dictionary<Button.ButtonType, Button> _latestButtonUpdates = new();
    private static bool _latestButtonUpdatesConsumed = true;
    public static void OnCohtmlMenuButtonUpdate(Button button) {

        CohtmlMenuButtonUpdated?.Invoke(button);

        lock (_latestButtonUpdates) lock (LatestCoreLock) {
            _latestButtonUpdates[button.Type] = button;
            _latestButtonUpdatesConsumed = false;
        }
        //MelonLogger.Msg($"Button Updated\n{JsonConvert.SerializeObject(button, Formatting.Indented)}");
    }
    public static bool GetLatestButtonUpdatesToConsume(out Button[] buttonUpdates) {
        lock (_latestButtonUpdates) lock (LatestCoreLock) {
            buttonUpdates = null;
            if (_latestButtonUpdatesConsumed) return false;

            // Reset the list
            buttonUpdates = _latestButtonUpdates.Values.ToArray();
            _latestButtonUpdates.Clear();
            _latestButtonUpdatesConsumed = true;
            return true;
        }
    }

    public static event Action<Button> CohtmlMenuButtonClicked;
    public static void OnCohtmlMenuButtonClick(Button button) {
        CohtmlMenuButtonClicked?.Invoke(button);
    }
}
