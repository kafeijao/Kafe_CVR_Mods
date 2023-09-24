using ABI_RC.Core.Networking.IO.UserGeneratedContent;

namespace Kafe.CCK.Debugger.Events;

internal static class World {

    public static readonly Dictionary<string, string> WorldNamesCache = new();

    public static void OnWorldDetailsRecycled(World_t details) {
        WorldNamesCache[details.WorldId] = details.WorldName;
    }

    public static event Action WorldFinishedConfiguration;
    public static void OnWorldFinishedConfiguration() {
        WorldFinishedConfiguration?.Invoke();
    }
}
