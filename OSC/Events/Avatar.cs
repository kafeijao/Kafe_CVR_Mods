using ABI_RC.Core.Networking;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AnimatorManager;
using Kafe.OSC.Utils;
using MelonLoader;

namespace Kafe.OSC.Events;

public static class Avatar
{
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

    // Callers
    internal static void OnAvatarDetailsReceived(string guid, string name)
    {
        AvatarsNamesCache[guid] = name;
    }

    internal static void Reset()
    {
        if (_localPlayerAnimatorManager == null) return;
        OnAnimatorManagerUpdate(_localPlayerAnimatorManager);
    }

    internal static async void OnAnimatorManagerUpdate(AvatarAnimatorManager animatorManager)
    {
        try
        {
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
            if (avatarName == null)
            {
                var existingConfig = JsonConfigOsc.GetConfig(userGuid, avatarGuid);

                // Attempt to get the avatar name from the config
                if (existingConfig != null) avatarName = existingConfig.name;
            }

            // Request the avatar name from the API
            if (avatarName == null && AuthManager.IsAuthenticated)
            {
                avatarName = await ApiRequests.RequestAvatarDetailsPageTask(avatarGuid);
            }

            // If the avatar name is still null, just give up
            if (avatarName == null)
            {
                JsonConfigOsc.ClearCurrentAvatarConfig();
                MelonLogger.Msg(
                    $"Failed to get the avatar name. The config for the Avatar ID {avatarGuid} won't be generated.");
            }
            // Otherwise create the config! (if needed)
            else
            {
                JsonConfigOsc.CreateConfig(userGuid, avatarGuid, avatarName, animatorManager);
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error(e.Message);
        }
    }
}
