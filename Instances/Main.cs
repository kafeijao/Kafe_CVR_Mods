using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Savior.SceneManagers;
using ABI_RC.Core.UI;
using ABI_RC.Core.Util;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.Movement;
using ABI_RC.Systems.Safety.AdvancedSafety;
using ABI.CCK.Components;
using HarmonyLib;
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

    public const int TeleportToLocationTimeout = 5;

    public const string RestartedWithInstancesMod = "--restarted-with-instances-mod";

    // Config File Saving
    private static readonly BlockingCollection<string> JsonContentQueue = new(new ConcurrentQueue<string>());
    private static Thread _saveConfigThread;
    private static bool _startedJob;
    private static ScheduledJob _startedJobInstance = null;

    internal static void OnInstanceSelected(string instanceId, bool isInitial = false) {
        InstanceSelected?.Invoke(instanceId, isInitial);
    }

    public override void OnInitializeMelon() {

        CVRGameEventSystem.Authentication.OnLogin.AddListener(InitializeAfterAuthentication);
        CVRGameEventSystem.Authentication.OnLogout.AddListener(() => {
            if (_startedJob) {
                // Stop the job that updates the rejoin location
                BetterScheduleSystem.RemoveJob(_startedJobInstance);
                _startedJobInstance = null;
                _startedJob = false;
            }
            _skipInitialLoad = false;
            AllConfig = null;
            PlayerConfig = null;
        });

        ModConfig.InitializeBTKUI();

        // Start Saving Config Thread
        _saveConfigThread = new Thread(SaveJsonConfigsThread);
        _saveConfigThread.Start();

        MelonLogger.Msg($"Detected ChatBox mod, we're adding the integration! You can use " +
                        $"{Integrations.ChatBoxIntegration.RestartCommandPrefix} and " +
                        $"{Integrations.ChatBoxIntegration.RejoinCommandPrefix} commands.");
        Integrations.ChatBoxIntegration.InitializeChatBox();

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
            && CVRInstances.CurrentInstanceId != ""
            && PlayerConfig?.LastInstance?.InstanceId != null
            && PlayerConfig.LastInstance.InstanceId == CVRInstances.CurrentInstanceId
            && BetterBetterCharacterController.Instance.CanFly()) {

            var pos = PlayerSetup.Instance.GetPlayerPosition();
            // Don't save the rejoining location if the location is invalid...
            if (pos.IsBad()) return;
            if (Vector3.Distance(Vector3.zero, pos) > 200000.0 || Mathf.Abs(pos.x) > 200000.0 || Mathf.Abs(pos.y) > 200000.0 || Mathf.Abs(pos.z) > 200000.0) return;
            var rot = PlayerSetup.Instance.GetPlayerRotation();

            PlayerConfig.RejoinLocation = new JsonRejoinLocation {
                InstanceId = CVRInstances.CurrentInstanceId,
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
        if (_startedJob) BetterScheduleSystem.RemoveJob(_startedJobInstance);

        MelonLogger.Msg($"Saving current location to teleport when rejoining...You need to rejoin within {TeleportToLocationTimeout} minutes!");
        UpdateRejoinLocation();

        // Mark thread as done and join it
        JsonContentQueue.CompleteAdding();
        _saveConfigThread.Join();
    }

    private static void SaveConfig(bool recentInstancesChanged) {
        if (AllConfig == null) return;

        if (JsonContentQueue.IsAddingCompleted)
        {
            MelonLogger.Msg("Unable to save the current config since the saving thread already closed. " +
                            "This is normal if the game is closing. " +
                            "On application quit the config is saved somewhere else.");
            return;
        }

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

        Task<BaseResponse<InstanceDetailsResponse>> task = CVRInstances.GetInstanceDetailsAsync(instanceId);
        yield return new WaitUntil(() => task.IsCompleted);
        BaseResponse<InstanceDetailsResponse> instanceDetails = task.Result;

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
                // If failed to find the instance -> Fallback to the original behaviour
                MelonLogger.Msg("Failed to join the previous Instance during initial join... fallback to the original initial instance join behaviour");
                yield return LoginRoom.LoadPlayerChosenContent();
            }

            else {
                // User clicked on an instance that is no longer valid, or the initial instance we created borked
                MelonLogger.Msg($"Instance {instanceId} has not been found.");
                CohtmlHud.Instance.ViewDropTextImmediate("", "Instance not Available",
                    "The instance you're trying to join doesn't was deleted or you don't have permission...", "", false);
            }

            // Let's invalidate the attempted instance
            InvalidateInstance(instanceId);
        }
        else {
            // Update the world image url
            UpdateWorldImageUrl(instanceDetails.Data.World.Id, instanceDetails.Data.World.ImageUrl);

            // Load into the instance
            MelonLogger.Msg($"The previous instance {instanceDetails.Data.Name} is still up! Attempting to join...");
            Task<bool> joinTask = CVRInstances.TryJoinInstanceAsync(instanceDetails.Data.Id, CVRInstances.JoinInstanceSource.Mod);
            yield return new WaitUntil(() => joinTask.IsCompleted);
            MelonLogger.Msg($"The previous instance join {(joinTask.Result ? "was successful" : "has failed")}");

            // Fallback to the original behaviour if it's the initial join
            if (!joinTask.Result && isInitial)
            {
                MelonLogger.Msg("Since it was the initial join, we're falling back to the original initial instance join behaviour");
                yield return LoginRoom.LoadPlayerChosenContent();
            }
        }

        _isChangingInstance = false;
    }

    internal static async Task SaveCurrentInstanceToken() {
        var baseResponse = await ApiConnection.MakeRequest<InstanceJoinResponse>(ApiConnection.ApiOperation.InstanceJoin, new {
            instanceID = CVRInstances.CurrentInstanceId,
        });
        if (baseResponse is { Data: not null }) {
            // Check for token updates
            UpdateInstanceToken(PlayerConfig.LastInstance, new JsonConfigJoinToken(baseResponse.Data.Jwt, baseResponse.Data.Host.Fqdn, baseResponse.Data.Host.Port));
            MelonLogger.Msg($"Successfully grabbed an instance token! Expires at: {PlayerConfig.LastInstance.JoinToken?.ExpirationDate.ToLocalTime()}");
            SaveConfig(false);
        }
    }

    private static IEnumerator LoadWorldUsingToken(JsonConfigInstance instanceInfo) {

        // // I don't know why, but on r173 vivox won't connect when joining using the token... prob race condition
        // yield return new WaitForSeconds(2f);

        // Get instance details
        Task<BaseResponse<InstanceDetailsResponse>> getInstanceDetailsTask = CVRInstances.GetInstanceDetailsAsync(instanceInfo.InstanceId);
        yield return new WaitUntil(() => getInstanceDetailsTask.IsCompleted);
        BaseResponse<InstanceDetailsResponse> instanceDetails = getInstanceDetailsTask.Result;

        CVRInstances.RequestedInstance = instanceInfo.InstanceId;
        Content.LoadIntoWorld(instanceInfo.WorldId);
        CVRInstances.OverrideWorldId = string.Empty;
        CVRInstances.InstanceJoinJWT = instanceInfo.JoinToken.Token;
        CVRInstances.Fqdn = instanceInfo.JoinToken.FQDN;
        CVRInstances.Port = instanceInfo.JoinToken.Port;

        if (instanceDetails.HasValidData && instanceDetails.Data != null)
        {
            CVRInstances.InstancesJoinInfo[instanceInfo.InstanceId] = new CVRInstances.JoinInfo(instanceDetails.Data, DateTime.UtcNow, true);
        }
        else
        {
            MelonLogger.Warning($"Failed to fetch the instance info when joining using the token, " +
                                $"some game functionality might break (for example instance type param stream)");
        }
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

    public static bool HasCommandLineArg(string arg)
    {
        foreach (string commandLineArg in Environment.GetCommandLineArgs()) {
            if (commandLineArg.Contains(arg))
            {
                return true;
            }
        }
        return false;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LoginRoom), nameof(LoginRoom.LoadPlayerChosenContent))]
        public static bool Before_LoginRoom_LoadPlayerChosenContent()
        {
            // Prevent the initial joining to the offline home world
            try {
                if (!_skipInitialLoad) {
                    _skipInitialLoad = true;

                    if (HasCommandLineArg(RestartedWithInstancesMod))
                    {
                        MelonLogger.Msg("Skipping the Deeplink, DiscordJoin, and LocalTesting because the game is is starting from an Instances Restart");
                    }
                    // Check if we should let the game do its thing
                    else
                    {
                        // Prevent running our stuff if the game is starting with a DeepLink Url
                        if (CheckVR.Instance.hasDeepLinkUrl && !DeepLinkHelper._consumedJoinInstanceLaunchArg)
                        {
                            MelonLogger.Msg("Skipping Instances initialize because the game is starting with a DeepLink Url");
                            return true;
                        }

                        // Prevent running our stuff if the game is starting with discord join
                        if (RichPresence.HasDiscordJoinId && !string.IsNullOrWhiteSpace(RichPresence.JoinInstanceId))
                        {
                            MelonLogger.Msg("Skipping Instances initialize because the game is starting with a Discord Join");
                            return true;
                        }
                    }

                    // Let's attempt to join the last instance
                    if (ModConfig.MeRejoinLastInstanceOnGameRestart.Value && PlayerConfig.LastInstance != null) {

                        // Attempt to join with an instance token
                        if (ModConfig.MeAttemptToSaveAndLoadToken.Value && PlayerConfig.LastInstance.JoinToken != null) {
                            if (!ModConfig.MePreventRejoiningEmptyInstances.Value || PlayerConfig.LastInstance.RemotePlayersCount > 0) {
                                if (AttemptToUseTicked(PlayerConfig.LastInstance))
                                {
                                    // We used the join ticket to join, lets load our avatar since we're skipping normal init
                                    AssetManagement.Instance.LoadLocalAvatar(MetaPort.Instance.currentAvatarGuid);
                                    return false;
                                }
                            }
                            else {
                                MelonLogger.Msg($"Skipping rejoining using token, because the instance is probably closing/closed. [MePreventRejoiningEmptyInstances=true]");
                            }
                        }

                        // Check if joining last instance timed out
                        var timeSinceLeavingInstance = DateTime.UtcNow - PlayerConfig.RejoinLocation.ClosedDateTime;
                        var timeToTimeoutJoiningLastInstance = TimeSpan.FromMinutes(ModConfig.MeJoiningLastInstanceMinutesTimeout.Value);
                        if (ModConfig.MeJoiningLastInstanceMinutesTimeout.Value >= 0 && timeSinceLeavingInstance > timeToTimeoutJoiningLastInstance) {
                            MelonLogger.Msg($"Skip attempting to join the last instance, because it has been over {ModConfig.MeJoiningLastInstanceMinutesTimeout.Value} minutes...");
                        }

                        // Otherwise just attempt to join the last instance
                        else {
                            // We're manually joining an instance, lets load our avatar since we're skipping normal init
                            AssetManagement.Instance.LoadLocalAvatar(MetaPort.Instance.currentAvatarGuid);
                            OnInstanceSelected(PlayerConfig.LastInstance.InstanceId, true);
                            return false;
                        }
                    }

                    // Reset the last instance, there was a reason we didn't use it
                    PlayerConfig.LastInstance = null;

                    // We didn't use our initialization, let's let the game do its thing
                    return true;
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
                MelonLogger.Msg($"[After_RichPresence_ReadPresenceUpdateFromNetwork] " +
                                $"CurrentWorldId: {ABI_RC.Core.Networking.IO.Instancing.Instances.CurrentWorldId}, " +
                                $"CurrentInstanceId: {ABI_RC.Core.Networking.IO.Instancing.Instances.CurrentInstanceId}, " +
                                $"Name: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                #endif

                // Update current instance
                MelonCoroutines.Start(UpdateLastInstance(
                    CVRInstances.CurrentWorldId,
                    CVRInstances.CurrentInstanceId,
                    CVRInstances.CurrentInstanceName,
                    CVRPlayerManager.Instance.NetworkPlayers.Count));

                // Initialize the save config job when we reach an online instance
                if (!_startedJob) {
                    _startedJobInstance = BetterScheduleSystem.AddJob(UpdateRejoinLocation, 10f);
                    _startedJob = true;
                }

                if (!_consumedTeleport &&
                    ModConfig.MeRejoinLastInstanceOnGameRestart.Value
                    && ModConfig.MeRejoinPreviousLocation.Value
                    && PlayerConfig.RejoinLocation is { AttemptToTeleport: true }
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.X) <= 200000.0
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.Y) <= 200000.0
                    && Mathf.Abs(PlayerConfig.RejoinLocation.Position.Z) <= 200000.0
                    && PlayerConfig.RejoinLocation.InstanceId == CVRInstances.CurrentInstanceId
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

        /// <summary>
        /// Accept the group instance joint prompt (to be here we must have accepted it before)
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.ShowGroupModeratedInstanceAlert))]
        public static bool Before_ViewManager_ShowGroupModeratedInstanceAlert(ref Task<bool> __result) {
            try
            {
                if (_isChangingInstance)
                {
                    MelonLogger.Msg("Got a group alert during initial instance join, accepting the prompt...");

                    // Force the async method to immediately succeed
                    __result = Task.FromResult(true);

                    // Skip original method entirely
                    return false;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
            return true;
        }
    }
}
