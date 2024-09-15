using System.Diagnostics;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AnimatorManager;
using Kafe.OSC.Utils;
using MelonLoader;
using UnityEngine;

namespace Kafe.OSC.Events;

public static class Avatar {

    // Data
    private static readonly Dictionary<string, string> AvatarsNamesCache = new();
    private static AvatarAnimatorManager _localPlayerAnimatorManager;

    // Caches for osc input
    private static readonly Dictionary<string, float> ParameterCacheInFloat = new();
    private static readonly Dictionary<string, int> ParameterCacheInInt = new();
    private static readonly Dictionary<string, bool> ParameterCacheInBool = new();

    // Caches for osc output (because some parameters get spammed like hell)
    private static readonly Dictionary<string, float> ParameterCacheOutFloat = new();
    private static readonly Dictionary<string, int> ParameterCacheOutInt = new();
    private static readonly Dictionary<string, bool> ParameterCacheOutBool = new();

    // Configs cache
    private static bool _triggersEnabled;
    private static bool _setAvatarEnabled;
    private static bool _debugConfigWarnings;

    // Misc
    private static readonly Stopwatch AvatarSetStopwatch = new();

    // Actions
    public static event Action<AvatarAnimatorManager> AnimatorManagerUpdated;

    // Actions parameters changed
    public static event Action<string, float> ParameterChangedFloat;
    public static event Action<string, int> ParameterChangedInt;
    public static event Action<string, bool> ParameterChangedBool;
    public static event Action<string> ParameterChangedTrigger;

    static Avatar() {

        // Handle the triggers enabled configuration
        _triggersEnabled = OSC.Instance.meOSCAvatarModuleTriggers.Value;
        OSC.Instance.meOSCAvatarModuleTriggers.OnEntryValueChanged.Subscribe((_, enabled) => _triggersEnabled = enabled);

        // Handle the set avatar enabled configuration
        _setAvatarEnabled = OSC.Instance.meOSCAvatarModuleSetAvatar.Value;
        OSC.Instance.meOSCAvatarModuleSetAvatar.OnEntryValueChanged.Subscribe((_, enabled) => _setAvatarEnabled = enabled);

        // Handle the warning when blocked osc command by config
        _debugConfigWarnings = OSC.Instance.meOSCDebugConfigWarnings.Value;
        OSC.Instance.meOSCDebugConfigWarnings.OnEntryValueChanged.Subscribe((_, enabled) => _debugConfigWarnings = enabled);
    }

    // Callers
    internal static void OnAvatarDetailsReceived(string guid, string name) {
        AvatarsNamesCache[guid] = name;
    }

    internal static void Reset() {
        if (_localPlayerAnimatorManager == null) return;
        OnAnimatorManagerUpdate(_localPlayerAnimatorManager);
    }

    internal static async void OnAnimatorManagerUpdate(AvatarAnimatorManager animatorManager) {
        _localPlayerAnimatorManager = animatorManager;

        // Clear caches
        ParameterCacheInFloat.Clear();
        ParameterCacheInInt.Clear();
        ParameterCacheInBool.Clear();
        ParameterCacheOutFloat.Clear();
        ParameterCacheOutInt.Clear();
        ParameterCacheOutBool.Clear();

        // Handle json configs
        var userGuid = MetaPort.Instance.ownerId;
        var avatarGuid = MetaPort.Instance.currentAvatarGuid;

        if (!Guid.TryParse(avatarGuid, out Guid _))
        {
            MelonLogger.Msg($"Found an invalid avatar ID {avatarGuid}, ignoring the setup...");
            return;
        }

        string avatarName = null;

        // Look in cache for the avatar name
        if (AvatarsNamesCache.ContainsKey(avatarGuid)) avatarName = AvatarsNamesCache[avatarGuid];

        // Attempt to Fetch the avatar name from an existing config
        if (avatarName == null) {
            var existingConfig = JsonConfigOsc.GetConfig(userGuid, avatarGuid);

            // Attempt to get the avatar name from the config
            if (existingConfig != null) avatarName = existingConfig.name;
        }

        // Request the avatar name from the API
        if (avatarName == null && AuthManager.IsAuthenticated) {
            avatarName = await ApiRequests.RequestAvatarDetailsPageTask(avatarGuid);
        }

        // If the avatar name is still null, just give up
        if (avatarName == null) {
            JsonConfigOsc.ClearCurrentAvatarConfig();
            MelonLogger.Msg($"Failed to get the avatar name. The config for the Avatar ID {avatarGuid} won't be generated.");
        }
        // Otherwise create the config! (if needed)
        else {
            JsonConfigOsc.CreateConfig(userGuid, avatarGuid, avatarName, animatorManager);
        }

        AnimatorManagerUpdated?.Invoke(animatorManager);
    }

    internal static void OnAvatarSet(string guid) {
        if (!_setAvatarEnabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg("[Config] Attempted to change the avatar via OSC, but that option is disabled on the mod configuration.");
            }
            return;
        }

        // Ignore malformed guids
        if (!Guid.TryParse(guid, out var guidValue)) return;
        var parsedGuid = guidValue.ToString("D");

        // Timer to prevent spamming this (since it's an API call
        if (!AvatarSetStopwatch.IsRunning) AvatarSetStopwatch.Start();
        else if (AvatarSetStopwatch.Elapsed < TimeSpan.FromSeconds(10)) {
            MelonLogger.Msg($"[Info] Attempted to change avatar to {parsedGuid}, but changing avatar is still on cooldown " +
                            $"(10 secs)...");
            return;
        }

        AvatarSetStopwatch.Restart();
        MelonLogger.Msg($"[Command] Received OSC command to change avatar to {parsedGuid}. Changing...");
        AssetManagement.Instance.LoadLocalAvatar(parsedGuid);
    }

    // Callers parameters changed
    internal static void OnParameterChangedFloat(CVRAnimatorManager animatorManager, string name, float value) {
        if (animatorManager != _localPlayerAnimatorManager) return;
        if (ParameterCacheInFloat.ContainsKey(name) && Mathf.Approximately(ParameterCacheInFloat[name], value)) return;
        ParameterCacheInFloat[name] = value;
        ParameterChangedFloat?.Invoke(name, value);
    }

    internal static void OnParameterChangedInt(CVRAnimatorManager animatorManager, string name, int value) {
        if (animatorManager != _localPlayerAnimatorManager) return;
        if (ParameterCacheInInt.ContainsKey(name) && ParameterCacheInInt[name] == value) return;
        ParameterCacheInInt[name] = value;
        ParameterChangedInt?.Invoke(name, value);
    }

    internal static void OnParameterChangedBool(CVRAnimatorManager animatorManager, string name, bool value) {
        if (animatorManager != _localPlayerAnimatorManager) return;
        if (ParameterCacheInBool.ContainsKey(name) && ParameterCacheInBool[name] == value) return;
        ParameterCacheInBool[name] = value;
        ParameterChangedBool?.Invoke(name, value);
    }

    internal static void OnParameterChangedTrigger(CVRAnimatorManager animatorManager, string name) {
        if (animatorManager != _localPlayerAnimatorManager || !_triggersEnabled) return;
        ParameterChangedTrigger?.Invoke(name);
    }

    // Callers parameters set
    internal static void OnParameterSetFloat(string name, float value) {
        if (ParameterCacheOutFloat.ContainsKey(name) && Mathf.Approximately(ParameterCacheOutFloat[name], value)) return;
        ParameterCacheOutFloat[name] = value;
        _localPlayerAnimatorManager?.SetParameter(name, value);
    }

    internal static void OnParameterSetInt(string name, int value) {
        if (ParameterCacheOutInt.ContainsKey(name) && ParameterCacheOutInt[name] == value) return;
        ParameterCacheOutInt[name] = value;
        _localPlayerAnimatorManager?.SetParameter(name, value);
    }

    internal static void OnParameterSetBool(string name, bool value) {
        if (ParameterCacheOutBool.ContainsKey(name) && ParameterCacheOutBool[name] == value) return;
        ParameterCacheOutBool[name] = value;
        _localPlayerAnimatorManager?.SetParameter(name, value);
    }

    internal static void OnParameterSetTrigger(string name) {
        if (!_triggersEnabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg("[Config] Attempted to set a trigger parameter, but that option is disabled in " +
                                "the mod configuration.");
            }
            return;
        }
        _localPlayerAnimatorManager?.SetParameter(name, true);
    }
}
