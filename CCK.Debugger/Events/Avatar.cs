using ABI_RC.Core;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Util.AnimatorManager;
using ABI.CCK.Components;
using NAK.Contacts;

namespace Kafe.CCK.Debugger.Events;

internal static class Avatar {


    public static readonly Dictionary<string, string> AvatarsNamesCache = new();
    public static void OnAvatarDetailsRecycled(AvatarDetails_t details) {
        AvatarsNamesCache[details.AvatarId] = details.AvatarName;
    }

    public static AvatarAnimatorManager LocalPlayerAvatarAnimatorManager;
    public static void OnAnimatorManagerUpdate(AvatarAnimatorManager animatorManager) {
        LocalPlayerAvatarAnimatorManager = animatorManager;
    }

    // Avatar AAS Triggers
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerTask> AasTriggerCollided;
    public static void OnAasTriggerCollision(TriggerToContact trigger, TriggerToContact.ContactTriggerTask triggerTask) {
        AasTriggerCollided?.Invoke(trigger, triggerTask);
    }
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerTask> AasTriggerExecuted;
    public static void OnAasTriggerExecuted(TriggerToContact trigger, TriggerToContact.ContactTriggerTask triggerTask) {
        AasTriggerExecuted?.Invoke(trigger, triggerTask);
    }
    public static event Action<TriggerToContact, TriggerToContact.ContactTriggerStayTask> AasStayTriggerExecuted;
    public static void OnAasStayTriggerExecuted(TriggerToContact trigger, TriggerToContact.ContactTriggerStayTask triggerTask) {
        AasStayTriggerExecuted?.Invoke(trigger, triggerTask);
    }

    public static void OnCVRAvatarStarted(CVRAvatar avatar) => DebuggerMenu.OnAvatarLoad(avatar, true);

    public static void OnCVRAvatarDestroyed(CVRAvatar avatar) => DebuggerMenu.OnAvatarLoad(avatar, false);
}
