using ABI_RC.Core.Networking.IO.UserGeneratedContent;

namespace CCK.Debugger.Events;

internal static class Spawnable {
    
    public static readonly Dictionary<string, string> SpawnableNamesCache = new();
    
    public static event Action<Spawnable_t> SpawnableDetailsRecycled;
    
    public static event Action SpawnableCreated;
    public static event Action SpawnableDeleted;
    public static event Action SpawnablesCleared;

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
}
