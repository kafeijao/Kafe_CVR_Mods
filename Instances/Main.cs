using System.Collections;
using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace Kafe.Instances;

public class Instances : MelonMod {

    internal static JsonConfig Config;

    public const string InstancesConfigFile = "InstancesModConfig.json";
    public const string InstancesPowerShellLog = "InstancesMod.ps1.log";
    private const int CurrentConfigVersion = 1;

    public static event Action InstancesConfigChanged;
    public static event Action<string, bool> InstanceSelected;

    private static bool _isChangingInstance;

    private const string GroupId = "SFW";

    public const int TeleportToLocationTimeout = 5;

    public const string InstanceRestartConfigArg = "--instances-owo-what-is-dis";

    internal static void OnInstanceSelected(string instanceId, bool isInitial = false) {
        InstanceSelected?.Invoke(instanceId, isInitial);
    }

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

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
            ModConfig.InitializeBTKUI();
        }
        else {
            MelonLogger.Warning($"BTKUILib mod NOT detected! You won't have access to the Instances History feature!");
        }

        InstanceSelected += (instanceId, isInitial) => {
            // Lets wait for the previous attempt to succeed/fail
            if (_isChangingInstance) return;
            MelonCoroutines.Start(LoadIntoLastInstance(instanceId, isInitial));
        };

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    public override void OnApplicationQuit() {
        if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value
            && ModConfig.RejoinPreviousLocation.Value
            && MetaPort.Instance.CurrentInstanceId != ""
            && Config.LastInstance.InstanceId == MetaPort.Instance.CurrentInstanceId
            && MovementSystem.Instance.canFly) {

            var movementSystemTransform = MovementSystem.Instance.transform;
            var pos = MovementSystem.Instance.rotationPivot.position;
            pos.y = movementSystemTransform.position.y;
            var rot = MovementSystem.Instance.rotationPivot.eulerAngles;
            rot.x = 0;
            rot.z = 0;

            MelonLogger.Msg($"Saving current location to teleport when rejoining... You need to rejoin withing 5 minutes!");
            Config.RejoinLocation = new JsonRejoinLocation {
                InstanceId = MetaPort.Instance.CurrentInstanceId,
                AttemptToTeleport = true,
                ClosedDateTime = DateTime.UtcNow,
                Position = new JsonVector3(pos),
                RotationEuler = new JsonVector3(rot),
            };
            SaveConfig();
        }
    }

    private static void SaveConfig() {
        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", InstancesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(instancesConfigFile.FullName, jsonContent);
        InstancesConfigChanged?.Invoke();
    }

    private static void InvalidateInstance(string instanceId) {
        #if DEBUG
        MelonLogger.Msg($"[InvalidateInstance] Invalidating instance {instanceId}...");
        #endif
        if (Config.LastInstance?.InstanceId == instanceId) {
            Config.LastInstance = null;
        }
        Config.RecentInstances.RemoveAll(i => i.InstanceId == instanceId);
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
        ApplyInstanceHistoryLimit(false);

        SaveConfig();
    }

    public static void ApplyInstanceHistoryLimit(bool saveConfig) {
        if (Config.RecentInstances.Count > ModConfig.MeInstancesHistoryCount.Value) {
            Config.RecentInstances.RemoveRange(ModConfig.MeInstancesHistoryCount.Value, Config.RecentInstances.Count - ModConfig.MeInstancesHistoryCount.Value);
        }
        if (saveConfig) SaveConfig();
    }

    private static void UpdateWorldImageUrl(string worldId, string worldImageUrl) {
        if (Config.LastInstance != null && Config.LastInstance.WorldId == worldId) {
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
        public JsonRejoinLocation RejoinLocation = null;
    }

    public record JsonRejoinLocation {
        public string InstanceId;
        public bool AttemptToTeleport;
        public DateTime ClosedDateTime = DateTime.UtcNow;
        public JsonVector3 Position = null;
        public JsonVector3 RotationEuler = null;
    }

    public record JsonVector3 {
        public float X;
        public float Y;
        public float Z;
        public JsonVector3() { }
        public JsonVector3(Vector3 vector) { X = vector.x; Y = vector.y; Z = vector.z; }
        public JsonVector3(Quaternion quaternion) { var vector = quaternion.eulerAngles; X = vector.x; Y = vector.y; Z = vector.z; }
        public Vector3 GetPosition() => new Vector3(X, Y, Z);
        public Quaternion GetRotation() => Quaternion.Euler(X, Y, Z);
    }

    public record JsonConfigInstance {
        public string WorldId;
        public string WorldImageUrl = null;
        public string InstanceId;
        public string InstanceName;
    }

    private static IEnumerator LoadIntoLastInstance(string instanceId, bool isInitial) {

        _isChangingInstance = true;

        var task = ApiConnection.MakeRequest<InstanceDetailsResponse>(ApiConnection.ApiOperation.InstanceDetail, new { instanceID = instanceId });
        yield return new WaitUntil(() => task.IsCompleted);
        var instanceDetails = task.Result;

        if (instanceDetails?.Data == null) {

            if (instanceDetails != null) {
                MelonLogger.Warning($"Attempted to join the previous Instance, but {instanceId} can't be found, " +
                                    $"Reason: {instanceDetails.Message}");
            }

            if (isInitial) {

                // We failed to get the last instance, let's give it a try to loading into an online initial instance
                if (ModConfig.MeStartInAnOnlineInstance.Value) {
                    yield return CreateAndJoinOnlineInstance();
                }
                // Otherwise let's just load to our offline world
                else {
                    // If failed to find the instance -> Load the offline instance home world
                    MelonLogger.Msg($"Failed to join the previous Instance... Sending you to your offline home world.");
                    Content.LoadIntoWorld(MetaPort.Instance.homeWorldGuid);
                }
            }
            else {
                // User clicked on an instance that is no longer valid, or the initial instance we created borked
                MelonLogger.Msg($"Instance {instanceId} has not been found.");
                CohtmlHud.Instance.ViewDropTextImmediate("", "Instance not Available",
                    "The instance you're trying to join doesn't was deleted or you don't have permission...");
            }
            // Let's invalidate the attempted instance
            InvalidateInstance(instanceId);
        }
        else {
            // Update the world image url
            UpdateWorldImageUrl(instanceDetails.Data.World.Id, instanceDetails.Data.World.ImageUrl);

            // Load into the instance
            MelonLogger.Msg($"The previous instance {instanceDetails.Data.Name} is still up! Attempting to join...");
            ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(instanceDetails.Data.Id, instanceDetails.Data.World.Id);
        }

        _isChangingInstance = false;
    }

    private static IEnumerator CreateAndJoinOnlineInstance() {

        var createInstanceTask = ApiConnection.MakeRequest<InstanceCreateResponse>(ApiConnection.ApiOperation.InstanceCreate, new {
            worldId = MetaPort.Instance.homeWorldGuid,
            privacy = ModConfig.MeStartingInstancePrivacyType.Value.ToString(),
            region = ((int) ModConfig.MeStartingInstanceRegion.Value).ToString(),
            groupId = GroupId,
        });
        yield return new WaitUntil(() => createInstanceTask.IsCompleted);

        var createInstancedInfo = createInstanceTask.Result;

        if (createInstancedInfo != null) {

            // Failed to create the instance
            if (createInstancedInfo.Data == null) {
                MelonLogger.Warning($"Failed to create an Initial Online instance. Reason: {createInstancedInfo.Message}");
            }
            // Created the instance, let's join it!
            else {
                #if DEBUG
                MelonLogger.Warning($"Created an Online Instance: [{createInstancedInfo.Data.Region}][{createInstancedInfo.Data.Id}] {createInstancedInfo.Data.Name}");
                #endif

                var instanceId = createInstancedInfo.Data.Id;

                // Let's get the instance details to see if it's up (we'll try 3 times and then give up)
                for (var attemptNum = 1; attemptNum <= 3; attemptNum++) {

                    // They also wait on the UI for 300 ms... It seems it takes a while before we can request the instance details
                    yield return new WaitForSeconds(0.3f*attemptNum);

                    var task = ApiConnection.MakeRequest<InstanceDetailsResponse>(ApiConnection.ApiOperation.InstanceDetail, new { instanceID = instanceId });
                    yield return new WaitUntil(() => task.IsCompleted);
                    var instanceDetails = task.Result;

                    if (instanceDetails?.Data == null) {
                        if (instanceDetails != null) {
                            MelonLogger.Warning($"[Attempt {attemptNum}/3]The created Instance {instanceId} is still not up... Message: {instanceDetails.Message}");
                        }
                    }
                    else {
                        // Update the world image url
                        UpdateWorldImageUrl(instanceDetails.Data.World.Id, instanceDetails.Data.World.ImageUrl);

                        // Load into the instance
                        MelonLogger.Msg($"The created {instanceDetails.Data.Name} is {(attemptNum > 1 ? "finally" : "")} up! Attempting to join...");
                        ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(instanceDetails.Data.Id, instanceDetails.Data.World.Id);

                        // We succeeded so lets break the coroutine here
                        yield break;
                    }
                }
            }
        }

        // If failed to create the instance -> Load the offline instance home world
        MelonLogger.Warning($"Failed to create an Initial Online Instance :( Sending you to your offline home world.");
        Content.LoadIntoWorld(MetaPort.Instance.homeWorldGuid);
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HQTools), nameof(HQTools.Start))]
        public static bool Before_HQTools_Start() {
            try {

                CVRTools.ConfigureHudAffinity();
                AssetManagement.Instance.LoadLocalAvatar(MetaPort.Instance.currentAvatarGuid);

                // If shift is hold, let's just go to our offline instance
                if (!Input.GetKey(KeyCode.LeftShift)) {

                    // Let's attempt to join the last instance
                    if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value && Config.LastInstance != null) {
                        OnInstanceSelected(Config.LastInstance.InstanceId, true);
                        return false;
                    }

                    // Otherwise let's join our home world, but in an online instance
                    if (ModConfig.MeStartInAnOnlineInstance.Value) {
                        MelonCoroutines.Start(CreateAndJoinOnlineInstance());
                        return false;
                    }
                }
                else {
                    MelonLogger.Msg("Since you pressed Left Shift during the game start, you'll be thrown into an offline Instance!");
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
        [HarmonyPatch(typeof(RichPresence), nameof(RichPresence.PopulateLastMessage))]
        public static void After_RichPresence_PopulateLastMessage() {
            try {
                MelonCoroutines.Start(UpdateLastInstance(MetaPort.Instance.CurrentWorldId,MetaPort.Instance.CurrentInstanceId, MetaPort.Instance.CurrentInstanceName));

                if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value
                    && ModConfig.RejoinPreviousLocation.Value
                    && Config.RejoinLocation is { AttemptToTeleport: true }
                    && Config.RejoinLocation.InstanceId == MetaPort.Instance.CurrentInstanceId
                    && MovementSystem.Instance.canFly) {

                    var timeSinceClosed = DateTime.UtcNow - Config.RejoinLocation.ClosedDateTime;
                    if (timeSinceClosed > TimeSpan.FromMinutes(TeleportToLocationTimeout)) {
                        MelonLogger.Msg($"Skip attempting to Teleport to the previous location of this Instance, because it has been over {TeleportToLocationTimeout} minutes...");
                    }
                    else {
                        MelonLogger.Msg("Attempting to Teleport to the previous location of this Instance...");
                        MovementSystem.Instance.TeleportToPosRot(Config.RejoinLocation.Position.GetPosition(), Config.RejoinLocation.RotationEuler.GetRotation());
                    }
                }

                // Delete the rejoining location
                Config.RejoinLocation = null;
                SaveConfig();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_RichPresence_PopulateLastMessage)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MetaPort), nameof(MetaPort.Start))]
        public static void After_MetaPort_Start() {
            // Look for arguments set by a previous restart of the game by the Instances Mod
            foreach (var commandLineArg in Environment.GetCommandLineArgs()) {
                if (!commandLineArg.Contains(InstanceRestartConfigArg)) continue;
                MetaPort.Instance.matureContentAllowed = true;
                break;
            }
        }
    }
}
