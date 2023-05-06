using System.Text;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using DarkRift;
using DarkRift.Client;
using HarmonyLib;
using MelonLoader;

namespace Kafe.RequestLib;

internal static class ModNetwork {

    private const ushort ModMsgTag = 13999;
    private const string ModId = $"MelonMod.Kafe.{nameof(RequestLib)}";

    private const int ModNameMaxCharCount = 200;
    private const int MessageMaxCharCount = 200;

    private const string GuidFormat = "D";

    private static readonly Dictionary<string, Tuple<DateTime, Action<API.RequestResult>>> PendingRequests = new();
    private static readonly Dictionary<string, DateTime> PendingResponses = new();
    private static readonly Dictionary<string, string[]> ModsUsed = new();

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

    internal static void SendSyncRequest() => SendMsgToAllPlayers(MessageType.SyncRequest);

    internal static void SendSyncUpdate() {
        SendMsgToAllPlayers(MessageType.SyncUpdate, writer => {
            writer.Write(API.RegisteredMods.ToArray());
        });
    }

    internal static void SendRequest(string modName, string message, Action<API.RequestResult> onResponse) {

        if (modName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Mod Name of requests can have a maximum of {ModNameMaxCharCount} characters.");
            return;
        }

        if (message.Length > MessageMaxCharCount) {
            MelonLogger.Warning($"Messages of requests can have a maximum of {MessageMaxCharCount} characters.");
            return;
        }

        var guid = Guid.NewGuid().ToString(GuidFormat);

        PendingRequests.Add(guid, Tuple.Create(DateTime.UtcNow + RequestTimeout, onResponse));

        SendMsgToAllPlayers(MessageType.Request, writer => {
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
        PendingResponses.Add(pendingResponseGuid, DateTime.UtcNow + RequestTimeout - RequestTimeoutOffset);
        CohtmlPatches.Request.CreateRequest(pendingResponseGuid, senderGuid, requestModName, requestMessage);
    }

    internal static void SendResponse(string guid, string modName, bool accepted) {

        if (!PendingRequests.ContainsKey(guid)) return;
        PendingRequests.Remove(guid);

        if (modName.Length > ModNameMaxCharCount) {
            MelonLogger.Warning($"Mod Name of requests can have a maximum of {ModNameMaxCharCount} characters.");
            return;
        }

        SendMsgToAllPlayers(MessageType.Response, writer => {
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
            requestData.Item2?.Invoke(accepted ? API.RequestResult.Accepted : API.RequestResult.Declined);
            PendingRequests.Remove(pendingRequestGuid);
        }
    }

    private static void OnMessage(object _, MessageReceivedEventArgs e) {

        // Ignore Messages that are not Mod Network Messages
        if (e.Tag != ModMsgTag) return;

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
                    SendSyncUpdate();
                    break;

                case MessageType.SyncUpdate:
                    ModsUsed[senderGuid] = reader.ReadStrings();
                    // Todo: Do something with the info
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

        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected) {
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

        // Actually send the message
        using var message = Message.Create(ModMsgTag, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);

        return true;
    }

    private static void HandleTimeouts() {

        // Check timeouts for pending requests (on the requester)
        var timedOutRequests = PendingRequests.Where(pair => pair.Value.Item1 < DateTime.UtcNow).ToList();
        foreach (var timedOutRequest in timedOutRequests) {
            timedOutRequest.Value.Item2?.Invoke(API.RequestResult.TimedOut);
            PendingRequests.Remove(timedOutRequest.Key);
        }

        // Check timeouts for pending response (on the requester target)
        var timedOutResponses = PendingResponses.Where(pair => pair.Value < DateTime.UtcNow).ToList();
        foreach (var timedOutResponse in timedOutResponses) {
            CohtmlPatches.Request.DeleteRequest(timedOutResponse.Key);
            PendingResponses.Remove(timedOutResponse.Key);
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
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_Awake)}");
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
