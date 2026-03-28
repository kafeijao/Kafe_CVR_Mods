using ABI.CCK.Components;
using NAK.Contacts;

namespace Kafe.CCK.Debugger.Events;

internal static class Spawnable
{
    public static readonly Dictionary<string, string> SpawnableNamesCache = new();

    // Spawnable Triggers
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerTask> SpawnableTriggerCollided;
    public static void OnSpawnableTriggerCollision(TriggerToContact trigger, TriggerToContact.ContactTriggerTask triggerTask) {
        SpawnableTriggerCollided?.Invoke(trigger, triggerTask);
    }
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerTask> SpawnableTriggerExecuted;
    public static void OnSpawnableTriggerExecuted(TriggerToContact trigger, TriggerToContact.ContactTriggerTask triggerTask) {
        SpawnableTriggerExecuted?.Invoke(trigger, triggerTask);
    }
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerStayTask> SpawnableStayTriggerExecuted;
    public static void OnSpawnableStayTriggerExecuted(TriggerToContact trigger, TriggerToContact.ContactTriggerStayTask triggerTask) {
        SpawnableStayTriggerExecuted?.Invoke(trigger, triggerTask);
    }

    public static void OnCVRSpawnableStarted(CVRSpawnable spawnable) => DebuggerMenu.OnSpawnableLoad(spawnable, true);

    public static void OnCVRSpawnableDestroyed(CVRSpawnable spawnable) => DebuggerMenu.OnSpawnableLoad(spawnable, false);
}
