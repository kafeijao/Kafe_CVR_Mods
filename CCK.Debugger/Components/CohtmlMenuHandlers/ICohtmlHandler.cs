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

    public static void Reload() {
        if (CurrentHandler == null) {
            CurrentHandler = Handlers[0];
            Core.UpdateButtonsState();
        }
        else {
            CurrentHandler.Unload();
        }

        CurrentHandler.Load();

        // Recover from a crash
        CurrentHandler.Reset();
        Crashed = false;
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
