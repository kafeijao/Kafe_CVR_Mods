using ABI.CCK.Components;
using MelonLoader;
using NAK.Contacts;

namespace Kafe.CCK.Debugger.Events;

public static class TriggerToContactEvents
{
    private static readonly Dictionary<TriggerToContact.ContactTriggerTask, TriggerToContact> TaskToTriggerMap =
        new Dictionary<TriggerToContact.ContactTriggerTask, TriggerToContact>();
    private static readonly Dictionary<TriggerToContact.ContactTriggerStayTask, TriggerToContact> StayTaskToTriggerMap =
        new Dictionary<TriggerToContact.ContactTriggerStayTask, TriggerToContact>();

    public static void OnTriggerToContactStarted(TriggerToContact instance)
    {
        for (var index = 0; index < instance.onEnterTasksCount; index++)
        {
            TriggerToContact.ContactTriggerTask onEnterTask = instance.onEnterTasks[index];
            if (onEnterTask == null) continue;
            TaskToTriggerMap[onEnterTask] = instance;
        }
        for (var index = 0; index < instance.onExitTasksCount; index++)
        {
            TriggerToContact.ContactTriggerTask onExit = instance.onExitTasks[index];
            if (onExit == null) continue;
            TaskToTriggerMap[onExit] = instance;
        }
        for (var index = 0; index < instance.onStayTasksCount; index++)
        {
            TriggerToContact.ContactTriggerStayTask onStayTask = instance.onStayTask[index];
            if (onStayTask == null) continue;
            StayTaskToTriggerMap[onStayTask] = instance;
        }
    }

    public static void OnTriggerToContactDestroyed(TriggerToContact instance)
    {
        for (var index = 0; index < instance.onEnterTasksCount; index++)
        {
            TriggerToContact.ContactTriggerTask onEnterTask = instance.onEnterTasks[index];
            if (onEnterTask == null) continue;
            TaskToTriggerMap.Remove(onEnterTask);
        }
        for (var index = 0; index < instance.onExitTasksCount; index++)
        {
            TriggerToContact.ContactTriggerTask onExitTask = instance.onExitTasks[index];
            if (onExitTask == null) continue;
            TaskToTriggerMap.Remove(onExitTask);
        }
        for (var index = 0; index < instance.onStayTasksCount; index++)
        {
            TriggerToContact.ContactTriggerStayTask onStayTask = instance.onStayTask[index];
            if (onStayTask == null) continue;
            StayTaskToTriggerMap.Remove(onStayTask);
        }
    }

    public static void OnTriggerToContactEntered(TriggerToContact instance)
    {
        if (instance.onEnterTasksCount == 0) return;
        for (var index = 0; index < instance.onEnterTasksCount; index++)
        {
            var task = instance.onEnterTasks[index];
            if (task == null) continue;
            switch (task.triggerType)
            {
                case TriggerToContact.TriggerType.LocalAvatar:
                    Avatar.OnAasTriggerCollision(instance, task);
                    break;
                case TriggerToContact.TriggerType.Spawnable:
                    Spawnable.OnSpawnableTriggerCollision(instance, task);
                    break;
            }
        }
    }

    public static void OnTriggerToContactExited(TriggerToContact instance)
    {
        if (instance.onExitTasksCount == 0) return;
        for (var index = 0; index < instance.onExitTasksCount; index++)
        {
            var task = instance.onExitTasks[index];
            if (task == null) continue;
            switch (task.triggerType)
            {
                case TriggerToContact.TriggerType.LocalAvatar:
                    Avatar.OnAasTriggerCollision(instance, task);
                    break;
                case TriggerToContact.TriggerType.Spawnable:
                    Spawnable.OnSpawnableTriggerCollision(instance, task);
                    break;
            }
        }
    }

    public static void OnTriggerExecuted(TriggerToContact.ContactTriggerTask instance)
    {
        if (!TaskToTriggerMap.TryGetValue(instance, out var trigger))
        {
            MelonLogger.Error("A trigger task was executed but we didn't cache it's trigger reference. This should never happen...");
            return;
        }
        switch (instance.triggerType)
        {
            case TriggerToContact.TriggerType.LocalAvatar:
                Avatar.OnAasTriggerExecuted(trigger, instance);
                break;
            case TriggerToContact.TriggerType.Spawnable:
                Spawnable.OnSpawnableTriggerExecuted(trigger, instance);
                break;
        }
    }

    public static void OnTriggerExecuted(TriggerToContact.ContactTriggerStayTask instance)
    {
        if (!StayTaskToTriggerMap.TryGetValue(instance, out var trigger))
        {
            MelonLogger.Error("A trigger stay task was executed but we didn't cache it's trigger reference. This should never happen...");
            return;
        }
        switch (instance.triggerType)
        {
            case TriggerToContact.TriggerType.LocalAvatar:
                Avatar.OnAasStayTriggerExecuted(trigger, instance);
                break;
            case TriggerToContact.TriggerType.Spawnable:
                Spawnable.OnSpawnableStayTriggerExecuted(trigger, instance);
                break;
        }
    }
}
