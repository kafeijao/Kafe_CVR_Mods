using Kafe.CCK.Debugger.Components;
using MelonLoader;

namespace Kafe.CCK.Debugger.Events;

public static class DebuggerMenuCohtml {

    public static event Action CohtmlMenuReloaded;
    public static void OnCohtmlMenuReload() => CohtmlMenuReloaded?.Invoke();

    private static Core _latestCore;
    private static readonly object LatestCoreLock = new();
    private static bool _latestCoreConsumed = true;
    public static void OnCohtmlMenuCoreCreate(Core core) {
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
            LatestSectionUpdates.Clear();
            _latestSectionUpdatesConsumed = true;
            LatestButtonUpdates.Clear();
            _latestButtonUpdatesConsumed = true;

            return true;
        }
    }


    private static Core.Info _latestCoreInfo;
    private static readonly object LatestCoreInfoLock = new();
    private static bool _latestCoreInfoConsumed = true;
    public static void OnCohtmlMenuCoreInfoUpdate(Core.Info coreInfo) {
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

    private static readonly Dictionary<int, Section> LatestSectionUpdates = new();
    private static bool _latestSectionUpdatesConsumed = true;
    private static bool _erroredSections;
    public static void OnCohtmlMenuSectionUpdate(Section section) {
        if (_erroredSections) return;

        lock (LatestSectionUpdates) lock (LatestCoreLock) {
            // Check to prevent memory leaks
            if (LatestSectionUpdates.Count > 10000) {
                _erroredSections = true;
                MelonLogger.Error("We reached over 10000 section updates... We're going to stop tracking the updates." +
                                  "This is to prevent memory leaks, contact the mod creator to fix this issue.");
                return;
            }
            LatestSectionUpdates[section.Id] = section;
            _latestSectionUpdatesConsumed = false;
        }
        //MelonLogger.Msg($"Section Updated\n{JsonConvert.SerializeObject(section, Formatting.Indented)}");
    }
    public static bool GetLatestSectionUpdatesToConsume(out Section[] sectionUpdates) {
        lock (LatestSectionUpdates) lock (LatestCoreLock) {
            sectionUpdates = null;
            if (_latestSectionUpdatesConsumed) return false;

            sectionUpdates = LatestSectionUpdates.Values.ToArray();
            // Reset the list
            LatestSectionUpdates.Clear();
            if (_latestSectionUpdatesConsumed) return false;
            _latestSectionUpdatesConsumed = true;
            return true;
        }
    }

    private static readonly Dictionary<Button.ButtonType, Button> LatestButtonUpdates = new();
    private static bool _latestButtonUpdatesConsumed = true;
    public static void OnCohtmlMenuButtonUpdate(Button button) {
        lock (LatestButtonUpdates) lock (LatestCoreLock) {
            LatestButtonUpdates[button.Type] = button;
            _latestButtonUpdatesConsumed = false;
        }
        //MelonLogger.Msg($"Button Updated\n{JsonConvert.SerializeObject(button, Formatting.Indented)}");
    }
    public static bool GetLatestButtonUpdatesToConsume(out Button[] buttonUpdates) {
        lock (LatestButtonUpdates) lock (LatestCoreLock) {
            buttonUpdates = null;
            if (_latestButtonUpdatesConsumed) return false;

            // Reset the list
            buttonUpdates = LatestButtonUpdates.Values.ToArray();
            LatestButtonUpdates.Clear();
            _latestButtonUpdatesConsumed = true;
            return true;
        }
    }
}
