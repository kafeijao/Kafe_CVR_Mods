using System.Text;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using DarkRift;
using DarkRift.Client;
using HarmonyLib;
using MelonLoader;

namespace Kafe.ChatBox;

public static class ModNetwork {

    private const string ModId = $"MelonMod.Kafe.{nameof(ChatBox)}";

    private const int CharactersMaxCount = 1000;
    private const int ModNameCharactersMaxCount = 50;

    private enum Tag : ushort {
        Subscribe = 13997,
        Unsubscribe = 13998,
        Message = 13999,
    }

    private enum SeedPolicy {
        ToSpecific = 0,
        ToAll = 1,
    }

    private enum MessageType : byte {
        Typing = 0,
        SendMessage = 1,
    }

    private const uint Version = 2;
    private static readonly HashSet<string> PlayerOldVersionWarned = new();

    internal static void SendTyping(API.MessageSource source, bool isTyping, bool notification) {
        var sent = SendMsgToAllPlayers(MessageType.Typing, writer => {
            writer.Write((byte) source);
            writer.Write(isTyping);
            writer.Write(notification);
        });
        if (sent) API.OnIsTypingSent?.Invoke(new API.ChatBoxTyping(source, MetaPort.Instance.ownerId, isTyping, notification));
    }

    internal static void SendMessage(API.MessageSource source, string modName, string msg, bool notification, bool displayInChatBox, bool displayInHistory) {
        if (string.IsNullOrEmpty(msg)) return;
        if (modName.Length > ModNameCharactersMaxCount) {
            MelonLogger.Warning($"Mod Name can have a maximum of {ModNameCharactersMaxCount} characters.");
            return;
        }
        if (msg.Length > CharactersMaxCount) {
            MelonLogger.Warning($"Messages can have a maximum of {CharactersMaxCount} characters.");
            return;
        }

        var chatBoxMessage = new API.ChatBoxMessage(source, MetaPort.Instance.ownerId, msg, notification, displayInChatBox, displayInHistory, modName);
        // Check Sending Interceptors
        foreach (var interceptor in API.SendingInterceptors) {
            try {
                var interceptorResult = interceptor.Invoke(chatBoxMessage);
                if (interceptorResult.PreventDisplayOnChatBox) displayInChatBox = false;
                if (interceptorResult.PreventDisplayOnHistory) displayInHistory = false;
                chatBoxMessage = new API.ChatBoxMessage(source, MetaPort.Instance.ownerId, msg, notification, displayInChatBox, displayInHistory, modName);
            }
            catch (Exception ex) {
                MelonLogger.Error("An mod's interceptor errored :(");
                MelonLogger.Error(ex);
            }
        }

        var sent = SendMsgToAllPlayers(MessageType.SendMessage, writer => {
            writer.Write((byte) source);
            writer.Write(modName, Encoding.UTF8);
            writer.Write(msg, Encoding.UTF8);
            writer.Write(notification);
            writer.Write(displayInChatBox);
            writer.Write(displayInHistory);
        });

        if (sent) API.OnMessageSent?.Invoke(chatBoxMessage);
    }

    private static void OnMessage(object sender, MessageReceivedEventArgs e) {

        // Ignore Messages that are not Mod Network Messages
        if (e.Tag != (ushort) Tag.Message) return;

        using var message = e.GetMessage();
        using var reader = message.GetReader();
        var modId = reader.ReadString();

        // Ignore Messages not for our mod
        if (modId != ModId) return;

        var senderGuid = reader.ReadString();

        try {

            // Read the version of the message
            var msgVersion = reader.ReadUInt32();
            if (msgVersion != Version) {
                if (PlayerOldVersionWarned.Contains(senderGuid)) return;
                var isNewer = msgVersion > Version;
                var playerName = CVRPlayerManager.Instance.TryGetPlayerName(senderGuid);
                MelonLogger.Warning($"Received a msg from {playerName} with a {(isNewer ? "newer" : "older")} version of the ChatBox mod." +
                                    $"Please {(isNewer ? "update your mod" : "ask them to update their mod")} if you want to see their messages.");
                PlayerOldVersionWarned.Add(senderGuid);
                return;
            }

            var msgTypeRaw = reader.ReadByte();

            // Ignore wrong msg types
            if (!Enum.IsDefined(typeof(MessageType), msgTypeRaw)) return;

            var msgType = (MessageType) msgTypeRaw;

            // Process rate limits
            if (RateLimiter.IsRateLimited(msgType, senderGuid)) return;

            switch (msgType) {

                case MessageType.Typing:

                    var typingSrcByte = reader.ReadByte();

                    if (!Enum.IsDefined(typeof(API.MessageSource), typingSrcByte)) {
                        throw new Exception($"[Typing] Received an invalid source byte ({typingSrcByte}).");
                    }

                    var isTyping = reader.ReadBoolean();
                    var notification = reader.ReadBoolean();

                    var chatBoxTyping = new API.ChatBoxTyping((API.MessageSource)typingSrcByte, senderGuid, isTyping, notification);
                    API.OnIsTypingReceived?.Invoke(chatBoxTyping);
                    break;

                case MessageType.SendMessage:

                    var messageSrcByte = reader.ReadByte();
                    if (!Enum.IsDefined(typeof(API.MessageSource), messageSrcByte)) {
                        throw new Exception($"[SendMessage] Received an invalid source byte ({messageSrcByte}).");
                    }

                    var modName = reader.ReadString(Encoding.UTF8);
                    if (modName.Length > ModNameCharactersMaxCount) {
                        MelonLogger.Warning($"Ignored message from {sender} because the mod name it's over {ModNameCharactersMaxCount} characters.");
                        return;
                    }

                    var msg = reader.ReadString(Encoding.UTF8);
                    if (msg.Length > CharactersMaxCount) {
                        MelonLogger.Warning($"Ignored message from {sender} because it's over {CharactersMaxCount} characters.");
                        return;
                    }

                    var sendNotification = reader.ReadBoolean();
                    var displayInChatBox = reader.ReadBoolean();
                    var displayInHistory = reader.ReadBoolean();

                    var chatBoxMessage = new API.ChatBoxMessage((API.MessageSource)messageSrcByte, senderGuid, msg, sendNotification, displayInChatBox, displayInHistory, modName);
                    // Check Receiving Interceptors
                    foreach (var interceptor in API.ReceivingInterceptors) {
                        try {
                            var interceptorResult = interceptor.Invoke(chatBoxMessage);
                            if (interceptorResult.PreventDisplayOnChatBox) displayInChatBox = false;
                            if (interceptorResult.PreventDisplayOnHistory) displayInHistory = false;
                            chatBoxMessage = new API.ChatBoxMessage((API.MessageSource)messageSrcByte, senderGuid, msg, sendNotification, displayInChatBox, displayInHistory, modName);
                        }
                        catch (Exception ex) {
                            MelonLogger.Error("An mod's interceptor errored :(");
                            MelonLogger.Error(ex);
                        }
                    }

                    API.OnMessageReceived?.Invoke(chatBoxMessage);
                    break;
            }
        }
        catch (Exception) {
            MelonLogger.Warning($"Received a malformed message from {CVRPlayerManager.Instance.TryGetPlayerName(senderGuid)}, " +
                                $"they might be running an outdated/updated version of the mod, or I broke something, or they're trying to do something funny.");
        }
    }

    private static void SendGuid(ushort msgTag) {

        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected) {
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

        // Set the message version
        writer.Write(Version);

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create((ushort) Tag.Message, writer);
        if (message.DataLength > 60000) {
            MelonLogger.Warning($"The limit for data in a single packet should be {ushort.MaxValue} bytes, " +
                                $"so let's limit to 60000 bytes. This message won't be sent");
            return false;
        }
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
        return true;
    }

    private static void SendToSpecificPlayers(string[] playerGuids, MessageType msgType, Action<DarkRiftWriter> msgDataAction = null) {

        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected) {
            MelonLogger.Warning($"Attempted to {nameof(SendToSpecificPlayers)} but the Game Network is Down...");
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

    private class RateLimiter {

        private const int MaxMessagesFallback = 5;
        private const int TimeWindowSecondsFallback = 10;
        private const bool WarnUserFallback = true;

        public static readonly Dictionary<(MessageType, string), RateLimiter> UserRateLimits = new();
        private static readonly Dictionary<MessageType, (int maxMessages, int timeWindowSeconds, bool warnUser)> UserRateMessageLimits = new();

        static RateLimiter() {
            SetupMessageType(MessageType.SendMessage, 5, 10, true);
            SetupMessageType(MessageType.Typing, 1, 3, false);
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
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.ReceiveReconnectToken))]
        public static void After_NetworkManager_ReceiveReconnectToken() {
            try {
                #if DEBUG
                MelonLogger.Msg($"Reclaim Token Assigned... Subscribing to {ModId} on the Mod Network...");
                #endif
                Subscribe();

                // Clear the rate limiters
                RateLimiter.UserRateLimits.Clear();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_ReceiveReconnectToken)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
        public static void After_NetworkManager_Awake(NetworkManager __instance) {
            try {
                #if DEBUG
                MelonLogger.Msg($"Started the Game Server Messages Listener...");
                #endif
                __instance.GameNetwork.MessageReceived += OnMessage;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_NetworkManager_Awake)}");
                MelonLogger.Error(e);
            }
        }
    }

}
