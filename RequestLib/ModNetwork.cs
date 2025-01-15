using System.Collections;
using System.Text;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using DarkRift;
using DarkRift.Client;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RequestLib;

internal static class ModNetwork {

    private const uint Version = 1;
    private static readonly HashSet<string> PlayerOldVersionWarned = new();

    private enum Tag : ushort {
        Subscribe = 13997,
        Unsubscribe = 13998,
        Message = 13999,
    }

    private const string ModId = $"MelonMod.Kafe.{nameof(RequestLib)}";

    private const int ModNameMaxCharCount = 200;
    private const int MessageMaxCharCount = 200;
    private const int MetaMaxCharCount = 200;

    private const string GuidFormat = "D";

    private static readonly Dictionary<string, API.Request> PendingRequests = new();
    private static readonly Dictionary<string, API.Request> PendingResponses = new();

    private static readonly TimeSpan RequestTimeoutOffset = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeoutMax = TimeSpan.FromHours(1);

    private enum SeedPolicy {
        ToSpecific = 0,
        ToAll = 1,
    }

    private enum MessageType : byte {
        SyncRequest,
        SyncUpdate,
        Request,
        Response,
    }

    internal static HashSet<API.Request> GetPendingSentRequests(string modName) {
        return PendingRequests.Values.Where(r => r.ModName == modName).ToHashSet();
    }

    internal static void CancelSentRequest(API.Request request) {
        var keysToRemove = PendingRequests.Where(pair => pair.Value == request).Select(pair => pair.Key).ToList();
        foreach (var key in keysToRemove) {
            PendingRequests.Remove(key);
        }
    }

    internal static HashSet<API.Request> GetPendingReceivedRequests(string modName) {
        return PendingResponses.Values.Where(r => r.ModName == modName).ToHashSet();
    }

    internal static void ResolveReceivedRequest(API.Request request, API.RequestResult result, string metadata = "") {
        var keysToResolve = PendingResponses.Where(pair => pair.Value == request).Select(pair => pair.Key).ToList();
        foreach (var key in keysToResolve) {
            CohtmlPatches.Request.DeleteRequest(key);
            if (result != API.RequestResult.TimedOut) {
                API.SendResponse(request, new API.Response(result, metadata), key);
            }
            PendingResponses.Remove(key);
        }
    }

    private static void SendSyncUpdate(string requesterGuid) {
        SendMsgToSpecificPlayers(new[] { requesterGuid }, MessageType.SyncUpdate, writer => {
            writer.Write(API.RegisteredMods.Keys.ToArray());
        });
    }

    private static void SendSyncUpdateToAll() {
        var playersToSend = CVRPlayerManager.Instance.NetworkPlayers.Select(p => p.Uuid).ToArray();
        if (playersToSend.Length == 0) return;
        SendMsgToSpecificPlayers(playersToSend, MessageType.SyncUpdate, writer => {
            writer.Write(API.RegisteredMods.Keys.ToArray());
        });
    }

    internal static bool IsOfflineInstance() {
        return NetworkManager.Instance == null || NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected;
    }

    private static bool IsPlayerInInstance(string playerGuid) {
        return CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(p => p.Uuid == playerGuid) != null;
    }

    internal static void SendRequest(API.Request request) {

        if (IsOfflineInstance()) {
            MelonLogger.Warning($"Attempted to {nameof(SendRequest)} but you're not connected an an instance...");
            return;
        }

        if (!IsPlayerInInstance(request.TargetPlayerGuid)) {
            MelonLogger.Warning($"There is no player with the guid {request.TargetPlayerGuid} in the current instance!");
            return;
        }

        if (request.Metadata.Length > MetaMaxCharCount) {
            MelonLogger.Warning($"Metadata of requests can have a maximum of {MetaMaxCharCount} characters. Current: {request.Metadata.Length}");
            return;
        }

        var timeoutInSeconds = request.Timeout - DateTime.UtcNow;
        if (timeoutInSeconds < RequestTimeoutOffset || timeoutInSeconds > RequestTimeoutMax) {
            MelonLogger.Warning($"The timeout of requests needs to be between ${RequestTimeoutOffset.TotalSeconds} and ${RequestTimeoutMax.TotalSeconds}! Current: {timeoutInSeconds.TotalSeconds}");
            return;
        }

        if (request.ModName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Mod Name of requests can have a maximum of {ModNameMaxCharCount} characters. Current: {request.ModName.Length}");
            return;
        }

        if (request.Message.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Messages of requests can have a maximum of {MessageMaxCharCount} characters. Current: {request.Message.Length}");
            return;
        }

        var guid = Guid.NewGuid().ToString(GuidFormat);

        PendingRequests.Add(guid, request);

        SendMsgToSpecificPlayers(new[] { request.TargetPlayerGuid }, MessageType.Request, writer => {
            writer.Write(guid, Encoding.UTF8);
            writer.Write(request.ModName, Encoding.UTF8);
            writer.Write(request.Metadata, Encoding.UTF8);
            writer.Write((uint) timeoutInSeconds.TotalSeconds);
            writer.Write(request.Message, Encoding.UTF8);
        });
    }

    private static void OnRequest(string senderGuid, string possibleRequestGuid, string requestModName, string requestMeta, uint requestTimeoutSeconds, string requestMessage) {

        if (!Guid.TryParseExact(possibleRequestGuid, GuidFormat, out var requestGuid)) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the request GUID is not not valid ({possibleRequestGuid}). This should never happen...");
            return;
        }

        if (requestMeta.Length > MetaMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because Metadata of requests can have a maximum of {MetaMaxCharCount} characters, sent: {requestMeta.Length}. This should never happen...");
            return;
        }

        if (requestTimeoutSeconds < RequestTimeoutOffset.TotalSeconds || requestTimeoutSeconds > RequestTimeoutMax.TotalSeconds) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Timeout of requests ({requestTimeoutSeconds}) needs to be between ${RequestTimeoutOffset.TotalSeconds} and ${RequestTimeoutMax.TotalSeconds}. This should never happen...");
            return;
        }

        if (requestModName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Mod Name is over {ModNameMaxCharCount} characters, sent: {requestModName.Length}. This should never happen...");
            return;
        }

        if (!API.RemotePlayerMods.TryGetValue(senderGuid, out var registeredMods) || !registeredMods.Contains(requestModName)) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Request mod name {requestModName} was not registered. This should never happen...");
            return;
        }

        if (requestMessage.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Request message is over {MessageMaxCharCount} characters, sent: {requestMessage.Length}. This should never happen...");
            return;
        }

        // Sanitize input that will be shown on the hud
        string modNameClean = requestModName.SanitizeForHtml();
        string playerName = CVRPlayerManager.Instance.TryGetPlayerName(senderGuid);
        string playerNameClean = playerName.SanitizeForHtml();

        var pendingResponseGuid = requestGuid.ToString(GuidFormat);
        var timeoutDate = DateTime.UtcNow + TimeSpan.FromSeconds(requestTimeoutSeconds) - RequestTimeoutOffset;
        var request = new API.Request(requestModName, timeoutDate, senderGuid, MetaPort.Instance.ownerId, requestMessage, requestMeta);

        PendingResponses.Add(pendingResponseGuid, request);

        // Check the settings for how to handle the request
        switch (ConfigJson.GetUserOverride(senderGuid, requestModName)) {

            case ConfigJson.UserOverride.AutoAccept:
                MelonLogger.Msg($"[{requestModName}] Auto-Accepted a request from {playerName}. Message: {requestMessage}");
                API.SendResponse(request, new API.Response(API.RequestResult.Accepted, ""), pendingResponseGuid);
                if (ModConfig.MeHudNotificationOnAutoAccept.Value) {
                    CohtmlHud.Instance.ViewDropText(nameof(RequestLib), $"<span>[{modNameClean}] <span style=\"color:green; display:inline\">Auto-Accepted</span> a request from {playerNameClean}.</span>", string.Empty, true);
                }
                return;

            case ConfigJson.UserOverride.AutoDecline:
                MelonLogger.Msg($"[{requestModName}] Auto-Declined a request from {playerName}. Message: {requestMessage}");
                API.SendResponse(request, new API.Response(API.RequestResult.Declined, ""), pendingResponseGuid);
                if (ModConfig.MeHudNotificationOnAutoAccept.Value) {
                    CohtmlHud.Instance.ViewDropText(nameof(RequestLib), $"<span>[{modNameClean}] <span style=\"color:red; display:inline\">Auto-Declined</span> a request from {playerNameClean}.</span>", string.Empty, true);
                }
                return;

            case ConfigJson.UserOverride.Default:
            case ConfigJson.UserOverride.LetMeDecide:
                // Let the request run its normal course
                break;
        }

        // Check whether there is an interceptor blocking the displaying of the request or not
        var interceptorResult = API.RunInterceptor(request);

        // We're displaying the request, so we'll leave the the user to reply to it
        if (interceptorResult.ShouldDisplayRequest) {
            CohtmlPatches.Request.CreateRequest(request, pendingResponseGuid, senderGuid, requestModName, requestMessage);
        }

        // We're not displaying the request, so we need to decide which result to send.
        else {
            switch (interceptorResult.ResponseResult) {
                case API.RequestResult.Accepted:
                    MelonLogger.Msg($"[Interceptor] [{requestModName}] Auto-Accepted a request from {playerName}. Message: {requestMessage}");
                    API.SendResponse(request, new API.Response(API.RequestResult.Accepted, interceptorResult.ResponseMetadata), pendingResponseGuid);
                    if (ModConfig.MeHudNotificationOnAutoAccept.Value) {
                        CohtmlHud.Instance.ViewDropText(nameof(RequestLib), $"<span>[{modNameClean}] <span style=\"color:green; display:inline\">Auto-Accepted</span> a request from {playerNameClean}.</span>", string.Empty, true);
                    }
                    break;
                case API.RequestResult.Declined:
                    MelonLogger.Msg($"[Interceptor] [{requestModName}] Auto-Declined a request from {playerName}. Message: {requestMessage}");
                    API.SendResponse(request, new API.Response(API.RequestResult.Declined, interceptorResult.ResponseMetadata), pendingResponseGuid);
                    if (ModConfig.MeHudNotificationOnAutoAccept.Value) {
                        CohtmlHud.Instance.ViewDropText(nameof(RequestLib), $"<span>[{modNameClean}] <span style=\"color:red; display:inline\">Auto-Declined</span> a request from {playerNameClean}.</span>", string.Empty, true);
                    }
                    break;
                case API.RequestResult.TimedOut:
                    MelonLogger.Msg($"[Interceptor] [{requestModName}] Ignored a request from {playerName}. Message: {requestMessage}");
                    break;
            }
        }
    }

    internal static void SendResponse(string guid, bool accepted, string responseMetadata) {

        if (!PendingResponses.Remove(guid, out var request)) return;

        // If the player is not in the instance anymore ignore
        if (!IsPlayerInInstance(request.SourcePlayerGuid)) {
            MelonLogger.Warning($"[SendResponse] The player {request.SourcePlayerGuid} is not longer in the instance...");
            return;
        }

        if (responseMetadata.Length > MetaMaxCharCount) {
            MelonLogger.Warning($"[SendResponse] Ignored request from {request.SourcePlayerGuid} because the Response Metadata is over {MetaMaxCharCount} characters, sent: {responseMetadata.Length}. This should never happen...");
            return;
        }

        SendMsgToSpecificPlayers(new [] { request.SourcePlayerGuid }, MessageType.Response, writer => {
            writer.Write(guid, Encoding.UTF8);
            writer.Write(accepted);
            writer.Write(responseMetadata, Encoding.UTF8);
        });
    }

    private static void OnResponse(string senderGuid, string possibleResponseGuid, bool accepted, string responseMetadata) {

        if (!Guid.TryParseExact(possibleResponseGuid, GuidFormat, out var responseGuid)) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the request GUID is not not valid ({possibleResponseGuid}). This should never happen...");
            return;
        }

        if (responseMetadata.Length > MetaMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Response Metadata is over {MetaMaxCharCount} characters, sent: {responseMetadata.Length}. This should never happen...");
            return;
        }

        var pendingRequestGuid = responseGuid.ToString(GuidFormat);
        if (PendingRequests.Remove(pendingRequestGuid, out var request)) {
            var response = new API.Response(accepted ? API.RequestResult.Accepted : API.RequestResult.Declined, responseMetadata);
            try {
                request.OnResponse?.Invoke(request, response);
            }
            catch (Exception e) {
                MelonLogger.Error($"The response handler function has errored. This is a problem with the mod {request.ModName}", e);
            }
        }
    }

    private static void OnMessage(object _, MessageReceivedEventArgs e) {

        // Ignore Messages that are not Mod Network Messages
        if (e.Tag != (ushort) Tag.Message) return;

        using var message = e.GetMessage();
        using var reader = message.GetReader();
        var modId = reader.ReadString();

        // Ignore Messages not for our mod
        if (modId != ModId) return;

        var senderGuid = reader.ReadString();

        // Ignore messages from blocked people
        if (MetaPort.Instance.blockedUserIds.Contains(senderGuid)) return;

        // Ignore messages from non-friends
        if (ModConfig.MeOnlyReceiveFromFriends.Value && !Friends.FriendsWith(senderGuid)) return;

        try {

            // Read the version of the message
            var msgVersion = reader.ReadUInt32();
            if (msgVersion != Version) {
                if (PlayerOldVersionWarned.Contains(senderGuid)) return;
                var isNewer = msgVersion > Version;
                var playerName = CVRPlayerManager.Instance.TryGetPlayerName(senderGuid);
                MelonLogger.Warning($"Received a msg from {playerName} with a {(isNewer ? "newer" : "older")} version of the {nameof(RequestLib)} mod." +
                                    $"Please {(isNewer ? "update your mod" : "ask them to update their mod")} if you want to see their requests.");
                PlayerOldVersionWarned.Add(senderGuid);
                return;
            }

            var msgTypeRaw = reader.ReadByte();

            // Ignore wrong msg types
            if (!Enum.IsDefined(typeof(MessageType), msgTypeRaw)) return;

            var msgType = (MessageType) msgTypeRaw;

            // Process the rate limiter
            if (RateLimiter.IsRateLimited(msgType, senderGuid)) return;

            switch (msgType) {

                case MessageType.SyncRequest:
                    SendSyncUpdate(senderGuid);
                    break;

                case MessageType.SyncUpdate:
                    var playerMods = reader.ReadStrings();
                    API.RemotePlayerMods[senderGuid] = playerMods;
                    API.PlayerInfoUpdate?.Invoke(new API.PlayerInfo(senderGuid));
                    break;

                case MessageType.Request:
                    var possibleRequestGuid = reader.ReadString(Encoding.UTF8);
                    var requestModName = reader.ReadString(Encoding.UTF8);
                    var requestMeta = reader.ReadString(Encoding.UTF8);
                    var requestTimeoutSeconds = reader.ReadUInt32();
                    var requestMessage = reader.ReadString(Encoding.UTF8);
                    OnRequest(senderGuid, possibleRequestGuid, requestModName, requestMeta, requestTimeoutSeconds, requestMessage);
                    break;

                case MessageType.Response:
                    var possibleResponseGuid = reader.ReadString(Encoding.UTF8);
                    var accepted = reader.ReadBoolean();
                    var responseMetadata = reader.ReadString(Encoding.UTF8);
                    OnResponse(senderGuid, possibleResponseGuid, accepted, responseMetadata);
                    break;
            }
        }
        catch (Exception ex) {
            MelonLogger.Warning($"Received a malformed message from {CVRPlayerManager.Instance.TryGetPlayerName(senderGuid)}, " +
                                $"they might be running an outdated version of the mod, or I broke something, or they're trying to do something funny.");
            MelonLogger.Error(ex);
        }
    }

    private static bool SendMsgToAllPlayers(MessageType msgType, Action<DarkRiftWriter> msgDataAction = null) {

        if (IsOfflineInstance()) {
            MelonLogger.Warning($"Attempted to {nameof(SendMsgToAllPlayers)} but the Game Network is Down...");
            return false;
        }

        // Mandatory message parameters
        using var writer = DarkRiftWriter.Create();
        writer.Write(ModId);
        writer.Write((int) SeedPolicy.ToAll);

        // Set the message version
        writer.Write(Version);

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create((ushort) Tag.Message, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
        return true;
    }

    private static void SendMsgToSpecificPlayers(string[] playerGuids, MessageType msgType, Action<DarkRiftWriter> msgDataAction = null) {

        if (IsOfflineInstance()) {
            MelonLogger.Warning($"Attempted to {nameof(SendMsgToSpecificPlayers)} but the Game Network is Down...");
            return;
        }

        if (playerGuids.Length == 0 || !playerGuids.All(IsPlayerInInstance)) {
            MelonLogger.Warning($"No players requested, or some players are not in the current instance!");
            return;
        }

        // Mandatory message parameters
        using var writer = DarkRiftWriter.Create();
        writer.Write(ModId);
        writer.Write((int) SeedPolicy.ToSpecific);
        writer.Write(playerGuids.Length);
        foreach (var playerGuid in playerGuids) {
            writer.Write(playerGuid);
        }

        // Set the message version
        writer.Write(Version);

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create((ushort) Tag.Message, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
    }

    private static void HandleTimeouts() {
        try {
            // Check timeouts for pending requests (on the requester)
            var timedOutRequests = PendingRequests.Where(pair => pair.Value.Timeout < DateTime.UtcNow).ToList();
            foreach (var timedOutRequest in timedOutRequests) {
                var response = new API.Response(API.RequestResult.TimedOut, "");
                timedOutRequest.Value.OnResponse?.Invoke(timedOutRequest.Value, response);
                PendingRequests.Remove(timedOutRequest.Key);
            }

            // Check timeouts for pending response (on the requester target)
            var timedOutResponses = PendingResponses.Where(pair => pair.Value.Timeout < DateTime.UtcNow).ToList();
            foreach (var timedOutResponse in timedOutResponses) {
                CohtmlPatches.Request.DeleteRequest(timedOutResponse.Key);
                PendingResponses.Remove(timedOutResponse.Key);
            }
        }
        catch (Exception e) {
            MelonLogger.Error("Error during HandleTimeouts()");
            MelonLogger.Error(e);
        }
    }

    private static void SendGuid(ushort msgTag) {

        if (IsOfflineInstance()) {
            MelonLogger.Warning($"Attempted to {nameof(SendGuid)} but the Game Network is Down...");
            return;
        }

        // Mandatory message parameters
        using var writer = DarkRiftWriter.Create();
        writer.Write((ushort) 1);
        writer.Write(ModId);

        using var message = Message.Create(msgTag, writer);

        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
    }

    private static void Subscribe() => SendGuid((ushort) Tag.Subscribe);

    private static void Unsubscribe() => SendGuid((ushort) Tag.Unsubscribe);


    private static object _sendSyncRequestCoroutine;
    private static IEnumerator HandleInitialSync() {
        if (_sendSyncRequestCoroutine != null) MelonCoroutines.Stop(_sendSyncRequestCoroutine);
        yield return new WaitForSeconds(0.5f);

        // Request sync from all
        SendMsgToAllPlayers(MessageType.SyncRequest);

        // Send our sync to all
        SendSyncUpdateToAll();

        _sendSyncRequestCoroutine = null;
    }

    private class RateLimiter {

        private const int MaxMessagesFallback = 5;
        private const int TimeWindowSecondsFallback = 10;
        private const bool WarnUserFallback = true;

        public static readonly Dictionary<(MessageType, string), RateLimiter> UserRateLimits = new();
        private static readonly Dictionary<MessageType, (int maxMessages, int timeWindowSeconds, bool warnUser)> UserRateMessageLimits = new();

        static RateLimiter() {
            SetupMessageType(MessageType.Request, 3, 10, true);
        }

        private DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
        private int MessageCount { get; set; } = 1;
        private bool Notified { get; set; }

        private static void SetupMessageType(MessageType msgType, int maxMessages, int timeWindowSeconds, bool warnUser) {
            UserRateMessageLimits.Add(msgType, (maxMessages, timeWindowSeconds, warnUser));
        }

        public static bool IsRateLimited(MessageType msgType, string senderGuid) {
            if (!UserRateMessageLimits.TryGetValue(msgType, out var msgInfo)) {
                msgInfo.maxMessages = MaxMessagesFallback;
                msgInfo.timeWindowSeconds = TimeWindowSecondsFallback;
                msgInfo.warnUser = WarnUserFallback;
            }
            if (UserRateLimits.TryGetValue((msgType, senderGuid), out var userState)) {
                if ((DateTime.UtcNow - userState.LastMessageTime).TotalSeconds <= msgInfo.timeWindowSeconds) {
                    // The user is above the rate limit
                    if (userState.MessageCount >= msgInfo.maxMessages) {
                        if (!msgInfo.warnUser || userState.Notified) return true;
                        userState.Notified = true;
                        MelonLogger.Warning($"The player {CVRPlayerManager.Instance.TryGetPlayerName(senderGuid)} " +
                                            $"send over {msgInfo.maxMessages} messages within {msgInfo.timeWindowSeconds} " +
                                            $"seconds. To prevent crashing/lag it's going to be rate limited.");
                        return true;
                    }
                    userState.MessageCount++;
                }
                else {
                    // Reset their count if it's been more than the time window.
                    userState.MessageCount = 1;
                    userState.LastMessageTime = DateTime.UtcNow;
                    userState.Notified = false;
                }
            }
            else {
                // If the sender is not in the dictionary, add them.
                UserRateLimits[(msgType, senderGuid)] = new RateLimiter();
            }
            return false;
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
        public static void After_NetworkManager_Awake(NetworkManager __instance) {
            try {
                MelonLogger.Msg($"Started the Game Server Messages Listener...");
                __instance.GameNetwork.MessageReceived += OnMessage;
                SchedulerSystem.AddJob(HandleTimeouts, 1f, 1f, -1);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_Awake)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.ReceiveReconnectToken))]
        public static void After_NetworkManager_ReceiveReconnectToken() {
            // A new connection to the game server was made
            try {
                #if DEBUG
                MelonLogger.Msg($"Reclaim Token Assigned... Subscribing to {ModId} on the Mod Network...");
                #endif

                // Subscribe to the mod network
                Subscribe();

                // Clear previous info
                API.RemotePlayerMods.Clear();
                RateLimiter.UserRateLimits.Clear();

                // Wait some time and send the sync request to everyone
                _sendSyncRequestCoroutine = MelonCoroutines.Start(HandleInitialSync());
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_ReceiveReconnectToken)}");
                MelonLogger.Error(e);
            }
        }
    }
}
