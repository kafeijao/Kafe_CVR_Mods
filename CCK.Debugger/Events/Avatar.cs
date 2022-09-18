using ABI_RC.Core;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI.CCK.Components;
using UnityEngine;

namespace CCK.Debugger.Events;

internal static class Avatar {

    public static readonly Dictionary<string, string> AvatarsNamesCache = new();
    public static readonly Dictionary<string, float> ParameterCache = new();
    public static CVRAnimatorManager LocalPlayerAnimatorManager;

    public static event Action<AvatarDetails_t> AvatarDetailsRecycled;
    public static event Action<string, float> ParameterChanged;
    public static event Action AnimatorManagerUpdated;

    // AAS Triggers
    public static event Action<CVRAdvancedAvatarSettingsTriggerTask> AasTriggerTriggered;
    public static event Action<CVRAdvancedAvatarSettingsTriggerTask> AasTriggerExecuted;
    public static event Action<CVRAdvancedAvatarSettingsTriggerTaskStay> AasStayTriggerTriggered;

    public static void OnAvatarDetailsRecycled(AvatarDetails_t details) {
        AvatarsNamesCache[details.AvatarId] = details.AvatarName;
        AvatarDetailsRecycled?.Invoke(details);
    }

    public static void OnParameterChanged(CVRAnimatorManager animatorManager, string name, float value) {
        if (animatorManager != LocalPlayerAnimatorManager) return;
        if (ParameterCache.ContainsKey(name) && Mathf.Approximately(ParameterCache[name], value)) return;
        ParameterCache[name] = value;
        ParameterChanged?.Invoke(name, value);
    }

    public static void OnAnimatorManagerUpdate(CVRAnimatorManager animatorManager) {
        LocalPlayerAnimatorManager = animatorManager;
        ParameterCache.Clear();
        AnimatorManagerUpdated?.Invoke();
    }

    // Avatar AAS Triggers
    public static void OnAasTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTask triggerTask) {
        AasTriggerTriggered?.Invoke(triggerTask);
    }
    public static void OnAasTriggerExecuted(CVRAdvancedAvatarSettingsTriggerTask triggerTask) {
        AasTriggerExecuted?.Invoke(triggerTask);
    }
    public static void OnAasStayTriggerTriggered(CVRAdvancedAvatarSettingsTriggerTaskStay triggerTask) {
        AasStayTriggerTriggered?.Invoke(triggerTask);
    }
}
