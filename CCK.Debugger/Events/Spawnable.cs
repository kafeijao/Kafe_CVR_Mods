using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI.CCK.Components;

namespace CCK.Debugger.Events;

internal static class Spawnable {

    public static readonly Dictionary<string, string> SpawnableNamesCache = new();

    public static void OnSpawnableDetailsRecycled(Spawnable_t details) {
        SpawnableNamesCache[details.SpawnableId] = details.SpawnableName;
    }

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


    public static event Action<CVRSpawnable> CVRSpawnableDestroyed;
    public static void OnCVRSpawnableDestroyed(CVRSpawnable spawnable) => CVRSpawnableDestroyed?.Invoke(spawnable);

    public static event Action<CVRSpawnable> CVRSpawnableStarted;
    public static void OnCVRSpawnableStarted(CVRSpawnable spawnable) => CVRSpawnableStarted?.Invoke(spawnable);
}
