using System.Collections;
using System.Text;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using DarkRift;
using DarkRift.Client;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RequestLib;

internal static class ModNetwork {

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

        if (requestMessage.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the Request message is over {MessageMaxCharCount} characters, sent: {requestMessage.Length}. This should never happen...");
            return;
        }

        var pendingResponseGuid = requestGuid.ToString(GuidFormat);
        var timeoutDate = DateTime.UtcNow + TimeSpan.FromSeconds(requestTimeoutSeconds) - RequestTimeoutOffset;
        var request = new API.Request(requestModName, timeoutDate, senderGuid, MetaPort.Instance.ownerId, requestMessage, requestMeta);

        PendingResponses.Add(pendingResponseGuid, request);

        // Check whether there is an interceptor blocking the displaying of the request or not
        var interceptorResult = API.RunInterceptor(request);

        // We're displaying the request, so we'll leave the the user to reply to it
        if (interceptorResult.ShouldDisplayRequest) {
            CohtmlPatches.Request.CreateRequest(pendingResponseGuid, senderGuid, requestModName, requestMessage);
        }

        // We're not displaying the request, so we need to decide which result to send.
        else {
            switch (interceptorResult.ResponseResult) {
                case API.RequestResult.Accepted: SendResponse(pendingResponseGuid, true, interceptorResult.ResponseMetadata);
                    break;
                case API.RequestResult.Declined: SendResponse(pendingResponseGuid, false, interceptorResult.ResponseMetadata);
                    break;
            }
        }
    }

    internal static void SendResponse(string guid, bool accepted, string responseMetadata) {

        if (!PendingResponses.TryGetValue(guid, out var request)) return;
        PendingResponses.Remove(guid);

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
        if (PendingRequests.TryGetValue(pendingRequestGuid, out var request)) {
            var response = new API.Response(accepted ? API.RequestResult.Accepted : API.RequestResult.Declined, responseMetadata);
            request.OnResponse?.Invoke(request, response);
            PendingRequests.Remove(pendingRequestGuid);
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

        try {

            // Ignore messages from non-friends
            if (ModConfig.MeOnlyReceiveFromFriends.Value && !Friends.FriendsWith(senderGuid)) return;

            var msgTypeRaw = reader.ReadByte();

            // Ignore wrong msg types
            if (!Enum.IsDefined(typeof(MessageType), msgTypeRaw)) return;

            switch ((MessageType) msgTypeRaw) {

                case MessageType.SyncRequest:
                    SendSyncUpdate(senderGuid);
                    break;

                case MessageType.SyncUpdate:
                    var playerMods = reader.ReadStrings();
                    API.RemotePlayerMods[senderGuid] = playerMods;
                    API.PlayerInfoUpdate?.Invoke(senderGuid);
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
        catch (Exception) {
            MelonLogger.Warning($"Received a malformed message from {CVRPlayerManager.Instance.TryGetPlayerName(senderGuid)}, " +
                                $"they might be running an outdated version of the mod, or I broke something, or they're trying to do something funny.");
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

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create((ushort) Tag.Message, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
    }

    private static void HandleTimeouts() {

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

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
        public static void After_NetworkManager_Awake(NetworkManager __instance) {
            try {
                MelonLogger.Msg($"Started the Game Server Messages Listener...");
                __instance.GameNetwork.MessageReceived += OnMessage;
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

                // Wait some time and send the sync request to everyone
                _sendSyncRequestCoroutine = MelonCoroutines.Start(HandleInitialSync());
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_ReceiveReconnectToken)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.PeriodicJobs))]
        public static void After_NetworkManager_PeriodicJobs() {
            try {
                HandleTimeouts();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_PeriodicJobs)}");
                MelonLogger.Error(e);
            }
        }
    }
}
