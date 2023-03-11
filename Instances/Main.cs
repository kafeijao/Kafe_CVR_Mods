using System.Collections;
using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace Kafe.Instances;

public class Instances : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeRejoinLastInstanceOnGameRestart;

    internal static JsonConfig Config;

    private const string InstancesConfigFile = "InstancesModConfig.json";
    private const int CurrentConfigVersion = 1;

    private const int MaxInstanceHistoryLimit = 12;

    public static event Action InstancesConfigChanged;
    public static event Action<JsonConfigInstance, bool> InstanceSelected;

    private static bool _isChangingInstance;

    internal static void OnInstanceSelected(JsonConfigInstance instanceInfo, bool isInitial = false) {
        InstanceSelected?.Invoke(instanceInfo, isInitial);
    }

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(Instances));

        MeRejoinLastInstanceOnGameRestart = _melonCategory.CreateEntry("RejoinLastInstanceOnRestart", true,
            description: "Whether to join the last instance (if still available) when restarting the game or not.");

        // Load The config
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", InstancesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();

        // Create default config
        if (!instancesConfigFile.Exists) {
            var config = new JsonConfig();
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {instancesConfigFile.FullName}...");
            File.WriteAllText(instancesConfigFile.FullName, jsonContent);
            Config = config;
        }
        // Load the previous config
        else {
            try {
                Config = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(instancesConfigFile.FullName));
            }
            catch (Exception e) {
                MelonLogger.Error($"Something went wrong when to load the {instancesConfigFile.FullName} config! " +
                                  $"You might want to delete/fix the file and try again...");
                MelonLogger.Error(e);
                throw;
            }
        }

        // Check for BTKUILib
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == "BTKUILib") != null) {
            MelonLogger.Msg($"Detected BTKUILib mod, we're adding the integration!");
            var btkuiLib = new InstancesBTKUI();
        }
        else {
            MelonLogger.Warning($"BTKUILib mod NOT detected! You won't have access to the Instances History feature!");
        }

        InstanceSelected += (instanceInfo, isInitial) => {
            // Lets wait for the previous attempt to succeed/fail
            if (_isChangingInstance) return;
            MelonCoroutines.Start(LoadIntoInstance(instanceInfo, isInitial));
        };

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    private static void SaveConfig() {
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", InstancesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(instancesConfigFile.FullName, jsonContent);
        InstancesConfigChanged?.Invoke();
    }

    private static void InvalidateInstance(JsonConfigInstance instanceInfo) {
        #if DEBUG
        MelonLogger.Msg($"[InvalidateInstance] Invalidating instance {instanceInfo.InstanceName}...");
        #endif
        if (Config.LastInstance?.InstanceId == instanceInfo.InstanceId) {
            Config.LastInstance = null;
        }
        Config.RecentInstances.RemoveAll(i => i.InstanceId == instanceInfo.InstanceId);
        SaveConfig();
    }

    private static IEnumerator UpdateLastInstance(string worldId, string instanceId, string instanceName) {

        // Already have this instance saved
        if (Config.LastInstance?.InstanceId == instanceId) yield break;

        // Grab the image url of the world, if we already had it
        var worldImageUrl = Config.RecentInstances.FirstOrDefault(i => i.WorldId == worldId && i.WorldImageUrl != null)
            ?.WorldImageUrl;

        // Get the instance's world image url
        if (worldImageUrl == null) {
            var task = ApiConnection.MakeRequest<InstanceDetailsResponse>(ApiConnection.ApiOperation.InstanceDetail, new { instanceID = instanceId });
            yield return new WaitUntil(() => task.IsCompleted);
            var instanceDetails = task.Result;
            if (instanceDetails?.Data != null) {
                worldImageUrl = instanceDetails.Data.World.ImageUrl;
            }
        }

        var instanceInfo = new JsonConfigInstance {
            WorldId = worldId,
            WorldImageUrl = worldImageUrl,
            InstanceId = instanceId,
            InstanceName = instanceName,
        };

        Config.LastInstance = instanceInfo;

        // Remove duplicates and Prepend to the list, while maintaining the list on the max limit
        Config.RecentInstances.RemoveAll(i => i.InstanceId == instanceId);
        Config.RecentInstances.Insert(0, instanceInfo);
        if (Config.RecentInstances.Count > MaxInstanceHistoryLimit) {
            Config.RecentInstances.RemoveRange(MaxInstanceHistoryLimit, Config.RecentInstances.Count - MaxInstanceHistoryLimit);
        }

        SaveConfig();
    }

    private static void UpdateWorldImageUrl(string worldId, string worldImageUrl) {
        if (Config.LastInstance.WorldId == worldId) {
            Config.LastInstance.WorldImageUrl = worldImageUrl;
        }
        foreach (var recentInstance in Config.RecentInstances) {
            if (recentInstance.WorldId == worldId) {
                recentInstance.WorldImageUrl = worldImageUrl;
            }
        }
        SaveConfig();
    }

    public record JsonConfig {
        public int ConfigVersion = CurrentConfigVersion;
        public JsonConfigInstance LastInstance = null;
        public readonly List<JsonConfigInstance> RecentInstances = new();
    }

    public record JsonConfigInstance {
        public string WorldId;
        public string WorldImageUrl = null;
        public string InstanceId;
        public string InstanceName;
    }

    private static IEnumerator LoadIntoInstance(JsonConfigInstance instanceInfo, bool isInitial) {

        _isChangingInstance = true;

        var task = ApiConnection.MakeRequest<InstanceDetailsResponse>(ApiConnection.ApiOperation.InstanceDetail, new { instanceID = instanceInfo.InstanceId });
        yield return new WaitUntil(() => task.IsCompleted);
        var instanceDetails = task.Result;

        if (instanceDetails?.Data == null) {
            if (isInitial) {
                // If failed to find the instance -> Load the offline instance home world
                MelonLogger.Msg($"The previous instance {instanceInfo.InstanceName} can't be found :( Sending you to your offline home world.");
                Content.LoadIntoWorld(MetaPort.Instance.homeWorldGuid);
            }
            else {
                // User clicked on an instance that is no longer valid
                CohtmlHud.Instance.ViewDropTextImmediate("", "Instance not Available",
                    "The instance you're trying to join doesn't was deleted or you don't have permission...");
            }
            // Let's invalidate the attempted instance
            InvalidateInstance(instanceInfo);
        }
        else {
            // Update the world image url
            instanceInfo.WorldImageUrl = instanceDetails.Data.World.ImageUrl;
            UpdateWorldImageUrl(instanceInfo.WorldImageUrl, instanceDetails.Data.World.ImageUrl);

            // Load into the instance
            MelonLogger.Msg($"The previous instance {instanceInfo.InstanceName} is still up! Attempting to join...");
            ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(instanceDetails.Data.Id, instanceDetails.Data.World.Id);
        }

        _isChangingInstance = false;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HQTools), "Start")]
        public static bool Before_HQTools_Start() {
            try {
                CVRTools.ConfigureHudAffinity();
                AssetManagement.Instance.LoadLocalAvatar(MetaPort.Instance.currentAvatarGuid);

                // Let's attempt to join the last instance
                if (MeRejoinLastInstanceOnGameRestart.Value && Config.LastInstance != null) {
                    OnInstanceSelected(Config.LastInstance, true);
                    return false;
                }

                Content.LoadIntoWorld(MetaPort.Instance.homeWorldGuid);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_HQTools_Start)}");
                MelonLogger.Error(e);
                throw;
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RichPresence), "PopulateLastMessage")]
        public static void After_RichPresence_PopulateLastMessage() {
            try {
                MelonCoroutines.Start(UpdateLastInstance(MetaPort.Instance.CurrentWorldId,MetaPort.Instance.CurrentInstanceId, MetaPort.Instance.CurrentInstanceName));
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_RichPresence_PopulateLastMessage)}");
                MelonLogger.Error(e);
                throw;
            }
        }
    }
}
