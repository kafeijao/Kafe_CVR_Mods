using System.Collections;
using System.Text;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
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

    private class PendingResponse {
        internal readonly string SenderGuid;
        internal readonly DateTime Timeout;
        internal PendingResponse(string senderGuid, DateTime timeout) {
            SenderGuid = senderGuid;
            Timeout = timeout;
        }
    }

    private class PendingRequest : PendingResponse {
        internal readonly Action<API.RequestResult> OnResponseAction;
        internal PendingRequest(string senderGuid, DateTime timeout, Action<API.RequestResult> onResponseAction) : base(senderGuid, timeout) {
            OnResponseAction = onResponseAction;
        }
    }

    private const string ModId = $"MelonMod.Kafe.{nameof(RequestLib)}";

    private const int ModNameMaxCharCount = 200;
    private const int MessageMaxCharCount = 200;

    private const string GuidFormat = "D";

    private static readonly Dictionary<string, PendingRequest> PendingRequests = new();
    private static readonly Dictionary<string, PendingResponse> PendingResponses = new();

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RequestTimeoutOffset = TimeSpan.FromSeconds(5);

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
            writer.Write(API.RegisteredMods.ToArray());
        });
    }

    private static void SendSyncUpdateToAll() {
        var playersToSend = CVRPlayerManager.Instance.NetworkPlayers.Select(p => p.Uuid).ToArray();
        if (playersToSend.Length == 0) return;
        SendMsgToSpecificPlayers(playersToSend, MessageType.SyncUpdate, writer => {
            writer.Write(API.RegisteredMods.ToArray());
        });
    }

    internal static bool IsOfflineInstance() {
        return NetworkManager.Instance == null || NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected;
    }

    private static bool IsPlayerInInstance(string playerGuid) {
        return CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(p => p.Uuid == playerGuid) != null;
    }

    internal static void SendRequest(string modName, string playerGuid, string message, Action<API.RequestResult> onResponse) {

        if (IsOfflineInstance()) {
            MelonLogger.Warning($"Attempted to {nameof(SendRequest)} but you're not connected an an instance...");
            return;
        }

        if (!IsPlayerInInstance(playerGuid)) {
            MelonLogger.Warning($"There is no player with the guid {playerGuid} in the current instance!");
            return;
        }

        if (modName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Mod Name of requests can have a maximum of {ModNameMaxCharCount} characters.");
            return;
        }

        if (message.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Messages of requests can have a maximum of {MessageMaxCharCount} characters.");
            return;
        }

        var guid = Guid.NewGuid().ToString(GuidFormat);

        PendingRequests.Add(guid, new PendingRequest(playerGuid, DateTime.UtcNow + RequestTimeout, onResponse));

        SendMsgToSpecificPlayers(new[] { playerGuid }, MessageType.Request, writer => {
            writer.Write(guid, Encoding.UTF8);
            writer.Write(modName, Encoding.UTF8);
            writer.Write(message, Encoding.UTF8);
        });
    }

    internal static void OnRequest(string senderGuid, string possibleRequestGuid, string requestModName, string requestMessage) {

        if (!Guid.TryParseExact(possibleRequestGuid, GuidFormat, out var requestGuid)) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the request GUID is not not valid ({possibleRequestGuid}). This should never happen...");
            return;
        }

        if (requestModName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the mod name is over {ModNameMaxCharCount} characters. This should never happen...");
            return;
        }

        if (requestMessage.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the request message is over {MessageMaxCharCount} characters. This should never happen...");
            return;
        }

        var pendingResponseGuid = requestGuid.ToString(GuidFormat);
        PendingResponses.Add(pendingResponseGuid, new PendingResponse(senderGuid, DateTime.UtcNow + RequestTimeout - RequestTimeoutOffset));
        CohtmlPatches.Request.CreateRequest(pendingResponseGuid, senderGuid, requestModName, requestMessage);
    }

    internal static void SendResponse(string guid, string modName, bool accepted) {

        if (!PendingResponses.ContainsKey(guid)) return;
        var requestingPlayerGuid = PendingResponses[guid].SenderGuid;
        PendingResponses.Remove(guid);

        // If the player is not in the instance anymore ignore
        if (!IsPlayerInInstance(requestingPlayerGuid)) {
            MelonLogger.Warning($"The player {requestingPlayerGuid} is not longer in the instance...");
            return;
        }

        if (modName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Mod Name of requests can have a maximum of {ModNameMaxCharCount} characters.");
            return;
        }

        SendMsgToSpecificPlayers(new [] { requestingPlayerGuid }, MessageType.Response, writer => {
            writer.Write(guid, Encoding.UTF8);
            writer.Write(modName, Encoding.UTF8);
            writer.Write(accepted);
        });

    }

    private static void OnResponse(string senderGuid, string possibleResponseGuid, string responseModName, bool accepted) {

        if (!Guid.TryParseExact(possibleResponseGuid, GuidFormat, out var responseGuid)) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the request GUID is not not valid ({possibleResponseGuid}). This should never happen...");
            return;
        }

        if (responseModName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Ignored request from {senderGuid} because the mod name is over {ModNameMaxCharCount} characters. This should never happen...");
            return;
        }

        var pendingRequestGuid = responseGuid.ToString(GuidFormat);
        if (PendingRequests.TryGetValue(pendingRequestGuid, out var requestData)) {
            requestData.OnResponseAction?.Invoke(accepted ? API.RequestResult.Accepted : API.RequestResult.Declined);
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

            // Todo: Check if blocked people can send us messages. And if they do nuke it

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
                    API.PlayerInfoUpdate?.Invoke(senderGuid, playerMods);
                    break;

                case MessageType.Request:
                    var possibleRequestGuid = reader.ReadString(Encoding.UTF8);
                    var requestModName = reader.ReadString(Encoding.UTF8);
                    var requestMessage = reader.ReadString(Encoding.UTF8);
                    OnRequest(senderGuid, possibleRequestGuid, requestModName, requestMessage);
                    break;

                case MessageType.Response:
                    var possibleResponseGuid = reader.ReadString(Encoding.UTF8);
                    var responseModName = reader.ReadString(Encoding.UTF8);
                    var accepted = reader.ReadBoolean();
                    OnResponse(senderGuid, possibleResponseGuid, responseModName, accepted);
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
            timedOutRequest.Value.OnResponseAction?.Invoke(API.RequestResult.TimedOut);
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
        yield return new WaitForSeconds(10f);

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
