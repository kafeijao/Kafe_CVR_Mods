using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI.CCK.Components;

namespace CCK.Debugger.Events;

internal static class Spawnable {

    public static readonly Dictionary<string, string> SpawnableNamesCache = new();

    public static event Action<Spawnable_t> SpawnableDetailsRecycled;

    public static event Action SpawnableCreated;
    public static event Action SpawnableDeleted;
    public static event Action SpawnablesCleared;


    // Spawnable Triggers
    public static event Action<CVRSpawnableTriggerTask> SpawnableTriggerTriggered;
    public static event Action<CVRSpawnableTriggerTask> SpawnableTriggerExecuted;
    public static event Action<CVRSpawnableTriggerTaskStay> SpawnableStayTriggerTriggered;

    public static void OnSpawnableDetailsRecycled(Spawnable_t details) {
        SpawnableNamesCache[details.SpawnableId] = details.SpawnableName;
        SpawnableDetailsRecycled?.Invoke(details);
    }

    public static void OnSpawnableCreated() {
        SpawnableCreated?.Invoke();
    }
    public static void OnSpawnableDeleted() {
        SpawnableDeleted?.Invoke();
    }
    public static void OnSpawnablesCleared() {
        SpawnablesCleared?.Invoke();
    }


    // Spawnable Triggers
    public static void OnSpawnableTriggerTriggered(CVRSpawnableTriggerTask triggerTask) {
        SpawnableTriggerTriggered?.Invoke(triggerTask);
    }
    public static void OnSpawnableTriggerExecuted(CVRSpawnableTriggerTask triggerTask) {
        SpawnableTriggerExecuted?.Invoke(triggerTask);
    }
    public static void OnSpawnableStayTriggerTriggered(CVRSpawnableTriggerTaskStay triggerTask) {
        SpawnableStayTriggerTriggered?.Invoke(triggerTask);
    }
}
