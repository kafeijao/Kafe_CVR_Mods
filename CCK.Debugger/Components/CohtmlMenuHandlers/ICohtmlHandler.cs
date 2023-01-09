using CCK.Debugger.Components.GameObjectVisualizers;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using UnityEngine;

namespace CCK.Debugger.Components.CohtmlMenuHandlers;

public abstract class ICohtmlHandler {

    protected abstract void Load();
    protected abstract void Unload();
    public abstract void Update();
    protected abstract void Reset();

    internal static bool Crashed;

    private static int _currentHandlerIndex;
    internal static ICohtmlHandler CurrentHandler;
    internal static readonly List<ICohtmlHandler> Handlers = new();

    public static void DisableEverything() {

        // Reset all buttons (if available)
        if (Core.GetButton(Button.ButtonType.Pointer, out var pointerButton) && pointerButton.IsOn) {
            pointerButton.IsOn = false;
        }
        if (Core.GetButton(Button.ButtonType.Trigger, out var triggerButton) && triggerButton.IsOn) {
            triggerButton.IsOn = false;
        }
        if (Core.GetButton(Button.ButtonType.Bone, out var boneButton) && boneButton.IsOn) {
            boneButton.IsOn = false;
        }
        if (Core.GetButton(Button.ButtonType.Tracker, out var trackerButton) && trackerButton.IsOn) {
            trackerButton.IsOn = false;
        }
        if (Core.GetButton(Button.ButtonType.Eye, out var eyeButton) && eyeButton.IsOn) {
            eyeButton.IsOn = false;
        }

        // Disable all visualizers
        PointerVisualizer.DisableAll();
        TriggerVisualizer.DisableAll();
        GameObjectVisualizer.DisableAll();

        // Disable trackers
        TrackerVisualizer.ToggleTrackers(false);
    }

    public static void Shutdown() {

        // Create disabled menu
        var core = new Core("Disabled", false, "", true, false);
        core.AddSection("The CCK.Debugger menu is disabled")
            .AddSection("Press the power button to activate!");
        core.AddSection("While in this state the menu doesn't butcher")
            .AddSection("performance while the menu is opened")
            .AddSection("c:");
        CohtmlMenuController.Instance.Enabled = false;
        DisableEverything();
        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);

        // Update buttons to reflect the shutdown
        Core.UpdateButtonsVisibilityTo(false);
        if (Core.GetButton(Button.ButtonType.Power, out var powerButton)) {
            powerButton.IsVisible = true;
            powerButton.IsOn = false;
        }

        // Update cached stuff
        CohtmlMenuController.ConsumeCachedUpdates();
    }

    internal static void Crash() {

        // Create the error menu
        var core = new Core("Error", false, "", true, false);
        core.AddSection("The CCK.Debugger menu crashed :(");
        core.AddSection("To report this crash check the Console for instructions");
        core.AddSection("To reload the menu press the Reset button");
        Crashed = true;
        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);

        // Update buttons to reflect the crash
        Core.UpdateButtonsVisibilityTo(false);
        if (Core.GetButton(Button.ButtonType.Reset, out var resetButton)) resetButton.IsOn = true;

        // Update cached stuff
        CohtmlMenuController.ConsumeCachedUpdates();
    }

    public static void Reload() {
        if (CurrentHandler == null) {
            CurrentHandler = Handlers[0];
            Shutdown();
            return;
        }

        CurrentHandler.Unload();
        CurrentHandler.Load();

        // Recover from a crash
        CurrentHandler.Reset();
        Crashed = false;

        // Turn on
        CohtmlMenuController.Instance.Enabled = true;
    }

    internal static void ResetCurrentEntities() {

        // Cleaning up caches, since started changing entity
        CurrentEntityPointerList.Clear();
        CurrentEntityTriggerList.Clear();
        CurrentEntityBoneList.Clear();
    }

    public static void SwitchMenu(bool next) {
        // We can't switch if we only have one handler
        if (Handlers.Count <= 1) return;

        _currentHandlerIndex = (_currentHandlerIndex + (next ? 1 : -1) + Handlers.Count) % Handlers.Count;
        CurrentHandler.Unload();

        // Reset inspected entity (since we're changing menu)
        ResetCurrentEntities();

        // Hide the controls (they'll be shown in the handler if they need
        if (CohtmlMenuController.Initialized && Core.Instance != null) {
            Core.Instance.UpdateCore(false, "N/A", false);
        }

        CurrentHandler = Handlers[_currentHandlerIndex];
        CurrentHandler.Load();

        // Recover from a crash
        Crashed = false;
    }

    protected static string GetUsername(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        return Events.Player.PlayersUsernamesCache.ContainsKey(guid) ? Events.Player.PlayersUsernamesCache[guid] : $"Unknown [{guid}]";
    }

    protected static string GetSpawnableName(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        var croppedGuid = guid.Length == 36 ? guid.Substring(guid.Length - 12) : guid;
        return Events.Spawnable.SpawnableNamesCache.ContainsKey(guid) ? Events.Spawnable.SpawnableNamesCache[guid] : $"Unknown [{croppedGuid}]";
    }

    protected static string GetAvatarName(string guid) {
        if (string.IsNullOrEmpty(guid)) return "N/A";
        var croppedGuid = guid.Length == 36 ? guid.Substring(guid.Length - 12) : guid;
        return Events.Avatar.AvatarsNamesCache.ContainsKey(guid) ? Events.Avatar.AvatarsNamesCache[guid] : $"Unknown [{croppedGuid}]";
    }

    protected static string GetTimeDifference(float time) {
        var timeDiff = Time.time - time;
        return timeDiff > 10f ? "10.00+" : timeDiff.ToString("0.00");
    }

    protected const string Na = "-none-";

    protected static string ToString(bool value) => value ? "yes" : "no";

    // Pointers, Triggers, Bones, and Trackers
    protected static readonly List<PointerVisualizer> CurrentEntityPointerList = new();
    protected static readonly List<TriggerVisualizer> CurrentEntityTriggerList = new();
    protected static readonly List<BoneVisualizer> CurrentEntityBoneList = new();
}
