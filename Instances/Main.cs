using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Savior.SceneManagers;
using ABI_RC.Core.UI;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.Movement;
using ABI_RC.Systems.UI;
using ABI.CCK.Components;
using Assets.ABI_RC.Systems.Safety.AdvancedSafety;
using HarmonyLib;
using Kafe.Instances.Properties;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using CVRInstances = ABI_RC.Core.Networking.IO.Instancing.Instances;

namespace Kafe.Instances;

public class Instances : MelonMod {

    internal static JsonConfig AllConfig;
    internal static JsonConfigPlayer PlayerConfig;

    private const string InstancesConfigFile = "InstancesModConfig.json";
    private const string InstancesTempConfigFile = "InstancesModConfig.temp.json";
    public const string InstancesPowerShellLog = "InstancesMod.ps1.log";
    private const int CurrentConfigVersion = 2;

    public static event Action RecentInstancesChanged;
    public static event Action<string, bool> InstanceSelected;

    private static bool _isChangingInstance;
    private static bool _consumedTeleport;

    private const string GroupId = "SFW";

    public const int TeleportToLocationTimeout = 5;

    public const string InstanceRestartConfigArg = "--instances-owo-what-is-dis";


    // Config File Saving
    private static readonly BlockingCollection<string> JsonContentQueue = new(new ConcurrentQueue<string>());
    private static Thread _saveConfigThread;
    private static bool _startedJob;

    internal static void OnInstanceSelected(string instanceId, bool isInitial = false) {
        InstanceSelected?.Invoke(instanceId, isInitial);
    }

    public override void OnInitializeMelon() {

        CVRGameEventSystem.Authentication.OnLogin.AddListener(InitializeAfterAuthentication);
        CVRGameEventSystem.Authentication.OnLogout.AddListener(() => {
            if (_startedJob) {
                // Stop the job that updates the rejoin location
                SchedulerSystem.RemoveJob(UpdateRejoinLocation);
                _startedJob = false;
            }
            _skipInitialLoad = false;
            AllConfig = null;
            PlayerConfig = null;
        });

        // Initialize BTKUI
        ModConfig.InitializeBTKUI();

        // Start Saving Config Thread
        _saveConfigThread = new Thread(SaveJsonConfigsThread);
        _saveConfigThread.Start();

        // Check for ChatBox
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.ChatBoxName) != null) {
            MelonLogger.Msg($"Detected ChatBox mod, we're adding the integration! You can use " +
                            $"{Integrations.ChatBoxIntegration.RestartCommandPrefix} and " +
                            $"{Integrations.ChatBoxIntegration.RejoinCommandPrefix} commands.");
            Integrations.ChatBoxIntegration.InitializeChatBox();
        }
        else {
            MelonLogger.Msg($"You can optionally install ChatBox mod to enable " +
                            $"{Integrations.ChatBoxIntegration.RestartCommandPrefix} and " +
                            $"{Integrations.ChatBoxIntegration.RejoinCommandPrefix} commands.");
        }

        // Setup the instances selected listener
        InstanceSelected += (instanceId, isInitial) => {
            // Lets wait for the previous attempt to succeed/fail
            if (_isChangingInstance) return;
            MelonCoroutines.Start(LoadIntoLastInstance(instanceId, isInitial));
        };

        // Update the player counts to dictate whether we should re-join or not an instance
        // Joining an empty instance results in weird stuff happening
        CVRGameEventSystem.Player.OnJoinEntity.AddListener(_ => {
            if (CommonTools.IsQuitting) return;
            if (PlayerConfig?.LastInstance != null) {
                #if DEBUG
                MelonLogger.Msg($"Instance player count updated! Player Count: {CVRPlayerManager.Instance.NetworkPlayers.Count}");
                #endif
                PlayerConfig.LastInstance.RemotePlayersCount = CVRPlayerManager.Instance.NetworkPlayers.Count;
                SaveConfig(false);
            }
        });
        CVRGameEventSystem.Player.OnLeaveEntity.AddListener(_ => {
            if (CommonTools.IsQuitting) return;
            if (PlayerConfig?.LastInstance != null) {
                #if DEBUG
                MelonLogger.Msg($"Instance player count updated! Player Count: {CVRPlayerManager.Instance.NetworkPlayers.Count}");
                #endif
                PlayerConfig.LastInstance.RemotePlayersCount = CVRPlayerManager.Instance.NetworkPlayers.Count;
                SaveConfig(false);
            }
        });

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    private static void InitializeAfterAuthentication(UserAuthResponse loginInfo) {

        ModConfig.InitializeOrUpdateMelonPrefs();

        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(Instances), InstancesConfigFile));
        var instancesConfigFile = new FileInfo(instancesConfigPath);
        instancesConfigFile.Directory?.Create();

        var instancesTempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(Instances), InstancesTempConfigFile));
        var instancesTempConfigFile = new FileInfo(instancesTempConfigPath);

        var resetConfigFiles = false;

        void AttemptToLoadConfig(string filePath) {
            var fileContents = File.ReadAllText(filePath);
            if (fileContents.All(c => c == '\0')) {
                throw new Exception($"\tThe file {filePath} existed but is binary zero-filled file");
            }
            AllConfig = JsonConvert.DeserializeObject<JsonConfig>(fileContents);

            // Reset the config files when there's a config version update
            if (AllConfig.ConfigVersion != CurrentConfigVersion) {
                resetConfigFiles = true;
                MelonLogger.Warning("\tThe configuration version was updated! The instances configs are going to be reset...");
            }
            // If the config exists and didn't update, just create the player config if doesn't exist
            else {
                AllConfig.PlayerConfigs.TryAdd(MetaPort.Instance.ownerId, new JsonConfigPlayer());
                PlayerConfig = AllConfig.PlayerConfigs[MetaPort.Instance.ownerId];
            }
        }

        // Load the previous config
        if (instancesConfigFile.Exists) {
            try {
                AttemptToLoadConfig(instancesConfigFile.FullName);
            }
            catch (Exception errMain) {
                try {
                    // Attempt to read from the temp file instead
                    MelonLogger.Warning($"Something went wrong when to load the {instancesConfigFile.FullName}. Checking the backup...");
                    AttemptToLoadConfig(instancesTempConfigFile.FullName);
                    MelonLogger.Msg("\tLoaded from the backup config successfully!");
                }
                catch (Exception errTemp) {
                    resetConfigFiles = true;
                    MelonLogger.Error($"\tSomething went wrong when to load the {instancesTempConfigFile.FullName} config! Resetting config...");
                    MelonLogger.Error(errMain);
                    MelonLogger.Error(errTemp);
                }
            }
        }

        // Create default config files
        if (!instancesConfigFile.Exists || resetConfigFiles) {
            var config = new JsonConfig();
            config.PlayerConfigs.TryAdd(MetaPort.Instance.ownerId, new JsonConfigPlayer());
            var jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
            MelonLogger.Msg($"Initializing the config file on {instancesConfigFile.FullName}...");
            File.WriteAllText(instancesConfigFile.FullName, jsonContent);
            File.Copy(instancesConfigPath, instancesTempConfigFile.FullName, true);
            AllConfig = config;
            PlayerConfig = config.PlayerConfigs[MetaPort.Instance.ownerId];
        }
    }

    private static void UpdateRejoinLocation() {

        if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value
            && ModConfig.MeRejoinPreviousLocation.Value
            && MetaPort.Instance.CurrentInstanceId != ""
            && PlayerConfig?.LastInstance?.InstanceId != null
            && PlayerConfig.LastInstance.InstanceId == MetaPort.Instance.CurrentInstanceId
            && BetterBetterCharacterController.Instance.CanFly()) {

            var pos = PlayerSetup.Instance.GetPlayerPosition();
            // Don't save the rejoining location if the location is invalid...
            if (pos.IsBad()) return;
            if (Vector3.Distance(Vector3.zero, pos) > 200000.0 || Mathf.Abs(pos.x) > 200000.0 || Mathf.Abs(pos.y) > 200000.0 || Mathf.Abs(pos.z) > 200000.0) return;
            var rot = PlayerSetup.Instance.GetPlayerRotation();

            PlayerConfig.RejoinLocation = new JsonRejoinLocation {
                InstanceId = MetaPort.Instance.CurrentInstanceId,
                AttemptToTeleport = true,
                ClosedDateTime = DateTime.UtcNow,
                Position = new JsonVector3(pos),
                RotationEuler = new JsonVector3(rot),
            };

            SaveConfig(false);
        }
    }

    public override void OnApplicationQuit() {

        // Stop the job that updates the rejoin location
        if (_startedJob) SchedulerSystem.RemoveJob(UpdateRejoinLocation);

        MelonLogger.Msg($"Saving current location to teleport when rejoining...You need to rejoin within {TeleportToLocationTimeout} minutes!");
        UpdateRejoinLocation();

        // Mark thread as done and join it
        JsonContentQueue.CompleteAdding();
        _saveConfigThread.Join();
    }

    private static void SaveConfig(bool recentInstancesChanged) {
        if (AllConfig == null) return;
        var jsonContent = JsonConvert.SerializeObject(AllConfig, Formatting.Indented);
        // Queue the json to be saved on a thread
        JsonContentQueue.Add(jsonContent);
        if (recentInstancesChanged) {
            RecentInstancesChanged?.Invoke();
        }
    }

    private static void SaveJsonConfigsThread() {

        var instancesConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(Instances), InstancesConfigFile));
        var instancesTempConfigPath = Path.GetFullPath(Path.Combine("UserData", nameof(Instances), InstancesTempConfigFile));

        foreach (var jsonContent in JsonContentQueue.GetConsumingEnumerable()) {

            // Save the temp file first
            var instancesTempConfigFile = new FileInfo(instancesTempConfigPath);
            var instancesConfigFile = new FileInfo(instancesConfigPath);
            instancesTempConfigFile.Directory?.Create();
            File.WriteAllText(instancesTempConfigFile.FullName, jsonContent);

            // Copy the temporary onto the actual file
            File.Copy(instancesTempConfigPath, instancesConfigFile.FullName, true);
        }
    }

    private static void InvalidateInstance(string instanceId) {
        #if DEBUG
        MelonLogger.Msg($"[InvalidateInstance] Invalidating instance {instanceId}...");
        #endif
        if (PlayerConfig.LastInstance?.InstanceId == instanceId) {
            PlayerConfig.LastInstance = null;
        }
        PlayerConfig.RecentInstances.RemoveAll(i => i.InstanceId == instanceId);
        SaveConfig(true);
    }

    private static void UpdateInstanceToken(JsonConfigInstance configLastInstance, JsonConfigJoinToken token = null) {

        // Ignore if the token to be update is the same
        var newToken = token?.Token ?? CVRInstances.InstanceJoinJWT;
        var isSameToken = newToken == configLastInstance.JoinToken?.Token;

        if (configLastInstance.InstanceId != GetJWTInstanceId(newToken)) {
            MelonLogger.Warning($"[UpdateInstanceToken] The provided token doesn't match the current instances... Ignoring... " +
                                $"Current: {configLastInstance.InstanceId}, Token: {GetJWTInstanceId(newToken)}");
            return;
        }

        try {
            if (!isSameToken) {
                configLastInstance.JoinToken = token ?? new JsonConfigJoinToken(CVRInstances.InstanceJoinJWT, CVRInstances.Fqdn, CVRInstances.Port);
                #if DEBUG
                MelonLogger.Msg($"Instance token was updated! Expiration: {configLastInstance.JoinToken.ExpirationDate.ToLocalTime()}");
                #endif
            }
            SaveConfig(false);
        }
        catch (Exception ex) {
            MelonLogger.Error(ex);
            throw;
        }
    }

    private static IEnumerator UpdateLastInstance(string worldId, string instanceId, string instanceName, int remotePlayersCount) {

        // Already have this instance saved, let's just check for the token
        if (PlayerConfig.LastInstance?.InstanceId == instanceId) {
            UpdateInstanceToken(PlayerConfig.LastInstance);
            yield break;
        }

        // Grab the image url of the world, if we already had it
        var worldImageUrl = PlayerConfig.RecentInstances.FirstOrDefault(i => i.WorldId == worldId && i.WorldImageUrl != null)
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
            RemotePlayersCount = remotePlayersCount,
        };
        PlayerConfig.LastInstance = instanceInfo;

        // Save the current token
        UpdateInstanceToken(PlayerConfig.LastInstance);

        // Remove duplicates and Prepend to the list, while maintaining the list on the max limit
        PlayerConfig.RecentInstances.RemoveAll(i => i.InstanceId == instanceId);
        PlayerConfig.RecentInstances.Insert(0, instanceInfo);
        ApplyInstanceHistoryLimit(false);

        SaveConfig(true);
    }

    public static void ApplyInstanceHistoryLimit(bool saveConfig) {
        if (PlayerConfig.RecentInstances.Count > ModConfig.MeInstancesHistoryCount.Value) {
            PlayerConfig.RecentInstances.RemoveRange(ModConfig.MeInstancesHistoryCount.Value, PlayerConfig.RecentInstances.Count - ModConfig.MeInstancesHistoryCount.Value);
        }
        if (saveConfig) SaveConfig(true);
    }

    private static void UpdateWorldImageUrl(string worldId, string worldImageUrl) {
        if (PlayerConfig.LastInstance != null && PlayerConfig.LastInstance.WorldId == worldId) {
            PlayerConfig.LastInstance.WorldImageUrl = worldImageUrl;
        }
        foreach (var recentInstance in PlayerConfig.RecentInstances) {
            if (recentInstance.WorldId == worldId) {
                recentInstance.WorldImageUrl = worldImageUrl;
            }
        }
        SaveConfig(true);
    }

    public record JsonConfig {
        // Can't be readonly, otherwise it won't load from the json
        public int ConfigVersion = CurrentConfigVersion;
        public readonly Dictionary<string, JsonConfigPlayer> PlayerConfigs = new();
    }

    public record JsonConfigPlayer {
        public JsonConfigInstance LastInstance = null;
        public JsonRejoinLocation RejoinLocation = null;
        public readonly List<JsonConfigInstance> RecentInstances = new();
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

    private static JObject GetPayload(string token) {
        var parts = token.Split('.');
        if (parts.Length != 3) {
            throw new Exception($"[GetJWTInstanceId] Invalid JWT token: wrong number of parts ({parts.Length})!");
        }
        var payload = parts[1];
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        return JObject.Parse(payloadJson);
    }

    private static string GetJWTInstanceId(string token) {
        var payloadData = GetPayload(token);
        return payloadData["InstanceId"]!.Value<string>();
    }

    private static DateTime GetJWTExpirationDateTime(string token) {
        var payloadData = GetPayload(token);
        var exp = payloadData["exp"]!.Value<long>();
        return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
    }

    private static byte[] Base64UrlDecode(string input) {
        var output = input;
        output = output.Replace('-', '+'); // 62nd char of encoding
        output = output.Replace('_', '/'); // 63rd char of encoding
        switch (output.Length % 4) { // Pad with trailing '='s
            case 0:
                break; // No pad chars in this case
            case 2:
                output += "==";
                break; // Two pad chars
            case 3:
                output += "=";
                break; // One pad char
            default:
                throw new Exception("[Base64UrlDecode] Illegal base64url string!");
        }
        return Convert.FromBase64String(output);
    }

    public record JsonConfigJoinToken(string Token, string FQDN, short Port) {
        public string Token = Token;
        public string FQDN = FQDN;
        public short Port = Port;
        public DateTime ExpirationDate = GetJWTExpirationDateTime(Token);
    }

    public record JsonConfigInstance {
        public string WorldId;
        public string WorldImageUrl = null;
        public string InstanceId;
        public string InstanceName;
        public int RemotePlayersCount = 0;
        public JsonConfigJoinToken JoinToken = null;
    }

    private static IEnumerator LoadIntoLastInstance(string instanceId, bool isInitial) {

        if (isInitial) {
            // I don't know why, but on r173 vivox won't connect when joining using the token... prob race condition
            yield return new WaitForSeconds(2f);
        }

        _isChangingInstance = true;

        var task = ApiConnection.MakeRequest<InstanceDetailsResponse>(ApiConnection.ApiOperation.InstanceDetail, new { instanceID = instanceId });
        yield return new WaitUntil(() => task.IsCompleted);
        var instanceDetails = task.Result;

        var hasOtherUsers = instanceDetails?.Data?.Members?.Exists(u => u.Name != AuthManager.Username);

        if (instanceDetails?.Data == null || (hasOtherUsers.HasValue && !hasOtherUsers.Value)) {

            if (ModConfig.MePreventRejoiningEmptyInstances.Value && hasOtherUsers.HasValue && !hasOtherUsers.Value) {
                MelonLogger.Msg($"Attempted to join the previous Instance, but there's no-one in the instance. " +
                                    $"This might result in joining a closing instance, skipping... You CAN disable this " +
                                    $"behavior in Melon Prefs.");
            }

            else if (instanceDetails != null) {
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

    internal static async Task SaveCurrentInstanceToken() {
        var baseResponse = await ApiConnection.MakeRequest<InstanceJoinResponse>(ApiConnection.ApiOperation.InstanceJoin, new {
            instanceID = MetaPort.Instance.CurrentInstanceId,
        });
        if (baseResponse is { Data: not null }) {
            // Check for token updates
            UpdateInstanceToken(PlayerConfig.LastInstance, new JsonConfigJoinToken(baseResponse.Data.Jwt, baseResponse.Data.Host.Fqdn, baseResponse.Data.Host.Port));
            MelonLogger.Msg($"Successfully grabbed an instance token! Expires at: {PlayerConfig.LastInstance.JoinToken?.ExpirationDate.ToLocalTime()}");
            SaveConfig(false);
        }
    }

    private static IEnumerator LoadWorldUsingToken(JsonConfigInstance instanceInfo) {

        // I don't know why, but on r173 vivox won't connect when joining using the token... prob race condition
        yield return new WaitForSeconds(2f);

        CVRInstances.RequestedInstance = instanceInfo.InstanceId;
        Content.LoadIntoWorld(instanceInfo.WorldId);
        // This fixes more bs race conditions
        yield return new WaitForSeconds(0.2f);
        CVRInstances.RequestedInstance = instanceInfo.InstanceId;
        CVRInstances.InstanceJoinJWT = instanceInfo.JoinToken.Token;
        CVRInstances.Fqdn = instanceInfo.JoinToken.FQDN;
        CVRInstances.Port = instanceInfo.JoinToken.Port;
    }


    private static IEnumerator CreateAndJoinOnlineInstance() {

        // I don't know why, but on r173 vivox won't connect when joining using the token... prob race condition
        yield return new WaitForSeconds(2f);

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
                    yield return new WaitForSeconds(ModConfig.MeInstanceCreationJoinAttemptInterval.Value * attemptNum);

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

                        // Wait for the instance to warmup (reduces failed connections)
                        yield return new WaitForSeconds(1f);

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

    private static bool _skipInitialLoad;

    public override void OnUpdate() {
        // Already ran the initialization or already pressed to skip!
        if (_skipInitialLoad) return;
        // If presses left shift or left ctrl when starting the game, prevent the initial Instances Initialize
        if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.LeftShift].isPressed) {
            _skipInitialLoad = true;
            MelonLogger.Warning($"Detected {nameof(Key.LeftCtrl)} or {nameof(Key.LeftShift)} key pressed!" +
                                $"Instances will skip the initial world joining...");
        }
    }

    internal static bool AttemptToUseTicked(JsonConfigInstance instanceInfo) {
        // Attempt to join with an instance token (needs to have more than 1 minute time left)
        if (instanceInfo?.JoinToken != null && ModConfig.MeAttemptToSaveAndLoadToken.Value) {
            if (instanceInfo.JoinToken.ExpirationDate - DateTime.UtcNow > TimeSpan.FromMinutes(1)) {
                MelonLogger.Msg($"[AttemptToUseTicked] Attempting to join instance using the join token... Expire Date: {instanceInfo.JoinToken.ExpirationDate.ToLocalTime()}");
                MelonCoroutines.Start(LoadWorldUsingToken(instanceInfo));
                return true;
            }
            else {
                MelonLogger.Msg($"[AttemptToUseTicked] Skip attempting to rejoin last instance using a token, because the token expired or is about to. " +
                                $"Expire Date: {instanceInfo.JoinToken.ExpirationDate.ToLocalTime()}");
            }
        }
        return false;
    }

    private static void Initialize() {

        // Let's attempt to join the last instance
        if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value && PlayerConfig.LastInstance != null) {

            // Attempt to join with an instance token
            if (ModConfig.MeAttemptToSaveAndLoadToken.Value && PlayerConfig.LastInstance.JoinToken != null) {
                if (!ModConfig.MePreventRejoiningEmptyInstances.Value || PlayerConfig.LastInstance.RemotePlayersCount > 0) {
                    if (AttemptToUseTicked(PlayerConfig.LastInstance)) return;
                }
                else {
                    MelonLogger.Msg($"Skipping rejoining using token, because the instance is probably closing/closed. [MePreventRejoiningEmptyInstances=true]");
                }
            }

            // Check if joining last instance timed out
            if (ModConfig.MeJoiningLastInstanceMinutesTimeout.Value >= 0 && DateTime.UtcNow - PlayerConfig.RejoinLocation.ClosedDateTime > TimeSpan.FromMinutes(ModConfig.MeJoiningLastInstanceMinutesTimeout.Value)) {
                MelonLogger.Msg($"Skip attempting to join the last instance, because it has been over {ModConfig.MeJoiningLastInstanceMinutesTimeout.Value} minutes...");
            }
            // Otherwise just attempt to join the last instance
            else {
                OnInstanceSelected(PlayerConfig.LastInstance.InstanceId, true);
                return;
            }
        }

        // Reset the last instance, there was a reason we didn't use it
        PlayerConfig.LastInstance = null;

        // Otherwise let's join our home world, but in an online instance
        if (ModConfig.MeStartInAnOnlineInstance.Value) {
            MelonCoroutines.Start(CreateAndJoinOnlineInstance());
            return;
        }

        // Join our offline world
        Content.LoadIntoWorld(MetaPort.Instance.homeWorldGuid);
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LoginRoom), nameof(LoginRoom.LoadPlayerChosenContent))]
        public static bool Before_LoginRoom_LoadPlayerChosenContent() {
            // Prevent the initial joining to the offline home world
            try {
                if (!_skipInitialLoad) {
                    _skipInitialLoad = true;
                    // Still load the avatar
                    AssetManagement.Instance.LoadLocalAvatar(MetaPort.Instance.currentAvatarGuid);
                    // We're assuming we already authenticated (we wouldn't be reaching here otherwise)
                    Initialize();
                    return false;
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(Before_LoginRoom_LoadPlayerChosenContent)}");
                MelonLogger.Error(e);
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.Start))]
        public static void Before_CVRWorld_Start() {
            _consumedTeleport = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RichPresence), nameof(RichPresence.ReadPresenceUpdateFromNetwork))]
        public static void After_RichPresence_ReadPresenceUpdateFromNetwork() {
            try {
                #if DEBUG
                MelonLogger.Msg($"[After_RichPresence_ReadPresenceUpdateFromNetwork] CurrentWorldId: {MetaPort.Instance.CurrentWorldId}, CurrentInstanceId: {MetaPort.Instance.CurrentInstanceId}, Name: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                #endif

                // Update current instance
                MelonCoroutines.Start(UpdateLastInstance(
                    MetaPort.Instance.CurrentWorldId,
                    MetaPort.Instance.CurrentInstanceId,
                    MetaPort.Instance.CurrentInstanceName,
                    CVRPlayerManager.Instance.NetworkPlayers.Count));

                // Initialize the save config job when we reach an online instance
                if (!_startedJob) {
                    SchedulerSystem.AddJob(UpdateRejoinLocation, 10f);
                    _startedJob = true;
                }

                if (!_consumedTeleport &&
                    ModConfig.MeRejoinLastInstanceOnGameRestart.Value
                    && ModConfig.MeRejoinPreviousLocation.Value
                    && PlayerConfig.RejoinLocation is { AttemptToTeleport: true }
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.X) <= 200000.0
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.Y) <= 200000.0
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.Z) <= 200000.0
                    && PlayerConfig.RejoinLocation.InstanceId == MetaPort.Instance.CurrentInstanceId
                    && BetterBetterCharacterController.Instance.CanFly()) {


                    var timeSinceClosed = DateTime.UtcNow - PlayerConfig.RejoinLocation.ClosedDateTime;
                    if (timeSinceClosed > TimeSpan.FromMinutes(TeleportToLocationTimeout)) {
                        MelonLogger.Msg($"Skip attempting to Teleport to the previous location of this Instance, because it has been over {TeleportToLocationTimeout} minutes...");
                    }
                    else {
                        MelonLogger.Msg("Attempting to Teleport to the previous location of this Instance...");
                        BetterBetterCharacterController.Instance.TeleportPlayerTo(PlayerConfig.RejoinLocation.Position.GetPosition(), PlayerConfig.RejoinLocation.RotationEuler.GetRotation().eulerAngles, false, true);
                    }
                }

                // Mark the teleport as consumed. We don't want to mistakenly teleport
                _consumedTeleport = true;

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_RichPresence_ReadPresenceUpdateFromNetwork)}");
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
