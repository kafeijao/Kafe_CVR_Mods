using ABI.CCK.Components;

namespace Kafe.CCK.Debugger.Events;

internal static class Spawnable {

    public static readonly Dictionary<string, string> SpawnableNamesCache = new();

    // Spawnable Triggers
    public static event Action<CVRSpawnableTriggerTask> SpawnableTriggerTriggered;
    public static void OnSpawnableTriggerTriggered(CVRSpawnableTriggerTask triggerTask) {
        SpawnableTriggerTriggered?.Invoke(triggerTask);
    }
    public static event Action<CVRSpawnableTriggerTask> SpawnableTriggerExecuted;
    public static void OnSpawnableTriggerExecuted(CVRSpawnableTriggerTask triggerTask) {
        SpawnableTriggerExecuted?.Invoke(triggerTask);
    }
    public static event Action<CVRSpawnableTriggerTaskStay> SpawnableStayTriggerTriggered;
    public static void OnSpawnableStayTriggerTriggered(CVRSpawnableTriggerTaskStay triggerTask) {
        SpawnableStayTriggerTriggered?.Invoke(triggerTask);
    }

    public static void OnCVRSpawnableStarted(CVRSpawnable spawnable) => DebuggerMenu.OnSpawnableLoad(spawnable, true);

    public static void OnCVRSpawnableDestroyed(CVRSpawnable spawnable) => DebuggerMenu.OnSpawnableLoad(spawnable, false);
}
