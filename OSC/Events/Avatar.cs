using System;
using System.Collections.Generic;
using System.Diagnostics;
using ABI_RC.Core;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Savior;
using MelonLoader;
using OSC.Utils;
using UnityEngine;

namespace OSC.Events; 

public static class Avatar {

    // Data
    internal static readonly Dictionary<string, string> AvatarsNamesCache = new();
    internal static CVRAnimatorManager LocalPlayerAnimatorManager { get; private set; }
    
    // Caches for osc input
    public static readonly Dictionary<string, float> ParameterCacheInFloat = new();
    public static readonly Dictionary<string, int> ParameterCacheInInt = new();
    public static readonly Dictionary<string, bool> ParameterCacheInBool = new();
    
    // Caches for osc output (because some parameters get spammed like hell)
    public static readonly Dictionary<string, float> ParameterCacheOutFloat = new();
    public static readonly Dictionary<string, int> ParameterCacheOutInt = new();
    public static readonly Dictionary<string, bool> ParameterCacheOutBool = new();
    
    // Misc
    private static readonly Stopwatch AvatarSetStopwatch = new Stopwatch();
    
    // Actions
    public static event Action<string, string> AvatarDetailsReceived;
    public static event Action<CVRAnimatorManager> AnimatorManagerUpdated;
    public static event Action<string> AvatarSet;
    
    // Actions parameters changed
    public static event Action<string, float> ParameterChangedFloat;
    public static event Action<string, int> ParameterChangedInt;
    public static event Action<string, bool> ParameterChangedBool;
    public static event Action<string> ParameterChangedTrigger;
    
    // Actions parameters set
    public static event Action<string, float> ParameterSetFloat;
    public static event Action<string, int> ParameterSetInt;
    public static event Action<string, bool> ParameterSetBool;
    public static event Action<string> ParameterSetTrigger;
    

    // Callers
    internal static void OnAvatarDetailsReceived(string guid, string name) {
        AvatarsNamesCache[guid] = name;
        AvatarDetailsReceived?.Invoke(guid, name);
    }
    
    internal static async void OnAnimatorManagerUpdate(CVRAnimatorManager animatorManager) {
        LocalPlayerAnimatorManager = animatorManager;
        
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
        if (avatarName == null) {
            avatarName = await ApiRequests.RequestAvatarDetailsPageTask(avatarGuid);
        }

        // If the avatar name is still null, just give up
        if (avatarName == null) {
            JsonConfigOsc.ClearCurrentAvatarConfig();
            MelonLogger.Msg($"[Error] The config for the avatar {avatarGuid} won't be generated.");
        }
        // Otherwise create the config! (if needed)
        else {
            JsonConfigOsc.CreateConfig(userGuid, avatarGuid, avatarName, animatorManager);
        }
        
        AnimatorManagerUpdated?.Invoke(animatorManager);
    }
    
    internal static void OnAvatarSet(string uuid) {
        if (!OSC.Instance.meOSCEnableSetAvatar.Value) {
            MelonLogger.Msg("[Info] Attempted to set the avatar via OSC, but that option is disabled on the mod configuration.");
            return;
        }
        
        // Ignore malformed guids
        if (!Guid.TryParse(uuid, out _)) return;

        // Timer to prevent spamming this (since it's an API call
        if (!AvatarSetStopwatch.IsRunning) AvatarSetStopwatch.Start();
        else if (AvatarSetStopwatch.Elapsed < TimeSpan.FromSeconds(30)) {
            MelonLogger.Msg($"[Info] Attempted to change avatar to {uuid}, but changing avatar is still on cooldown (30 secs)...");
            return;
        }
        
        AvatarSetStopwatch.Restart();
        MelonLogger.Msg($"[Command] Received OSC command to change avatar to {uuid}. Changing...");
        AssetManagement.Instance.LoadLocalAvatar(uuid);
        AvatarSet?.Invoke(uuid);
    }
    
    // Callers parameters changed
    internal static void OnParameterChangedFloat(CVRAnimatorManager animatorManager, string name, float value) {
        if (animatorManager != LocalPlayerAnimatorManager) return;
        if (ParameterCacheInFloat.ContainsKey(name) && Mathf.Approximately(ParameterCacheInFloat[name], value)) return;
        ParameterCacheInFloat[name] = value;
        ParameterChangedFloat?.Invoke(name, value);
    }

    internal static void OnParameterChangedInt(CVRAnimatorManager animatorManager, string name, int value) {
        if (animatorManager != LocalPlayerAnimatorManager) return;
        if (ParameterCacheInInt.ContainsKey(name) && ParameterCacheInInt[name] == value) return;
        ParameterCacheInInt[name] = value;
        ParameterChangedInt?.Invoke(name, value);
    }

    internal static void OnParameterChangedBool(CVRAnimatorManager animatorManager, string name, bool value) {
        if (animatorManager != LocalPlayerAnimatorManager) return;
        if (ParameterCacheInBool.ContainsKey(name) && ParameterCacheInBool[name] == value) return;
        ParameterCacheInBool[name] = value;
        ParameterChangedBool?.Invoke(name, value);
    }

    internal static void OnParameterChangedTrigger(CVRAnimatorManager animatorManager, string name) {
        if (animatorManager != LocalPlayerAnimatorManager) return;
        if (!OSC.Instance.meOSCEnableTriggers.Value) return;
        ParameterChangedTrigger?.Invoke(name);
    }

    // Callers parameters set
    internal static void OnParameterSetFloat(string name, float value) {
        if (ParameterCacheOutFloat.ContainsKey(name) && Mathf.Approximately(ParameterCacheOutFloat[name], value)) return;
        ParameterCacheOutFloat[name] = value;
        LocalPlayerAnimatorManager?.SetAnimatorParameterFloat(name, value);
        ParameterSetFloat?.Invoke(name, value);
    }

    internal static void OnParameterSetInt(string name, int value) {
        if (ParameterCacheOutInt.ContainsKey(name) && ParameterCacheOutInt[name] == value) return;
        ParameterCacheOutInt[name] = value;
        LocalPlayerAnimatorManager?.SetAnimatorParameterInt(name, value);
        ParameterSetInt?.Invoke(name, value);
    }

    internal static void OnParameterSetBool(string name, bool value) {
        if (ParameterCacheOutBool.ContainsKey(name) && ParameterCacheOutBool[name] == value) return;
        ParameterCacheOutBool[name] = value;
        LocalPlayerAnimatorManager?.SetAnimatorParameterBool(name, value);
        ParameterSetBool?.Invoke(name, value);
    }

    internal static void OnParameterSetTrigger(string name) {
        if (!OSC.Instance.meOSCEnableTriggers.Value) {
            MelonLogger.Msg("[Info] Attempted to set a trigger parameter, but that option is disabled in the mod configuration.");
            return;
        }
        LocalPlayerAnimatorManager?.SetAnimatorParameterTrigger(name);
        ParameterSetTrigger?.Invoke(name);
    }
}