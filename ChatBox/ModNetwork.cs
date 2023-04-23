using System.Text;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Savior;
using DarkRift;
using DarkRift.Client;
using HarmonyLib;
using MelonLoader;

namespace Kafe.ChatBox;

public static class ModNetwork {

    private const ushort ModMsgTag = 13999;
    private const string ModId = $"MelonMod.Kafe.{nameof(ChatBox)}";

    private enum SeedPolicy {
        ToSpecific = 0,
        ToAll = 1,
    }

    private enum MessageType : byte {
        Typing = 0,
        SendMessage = 1,
    }

    public static void SendTyping(bool isTyping) {
        SendMsgToAllPlayers(MessageType.Typing, writer => writer.Write(isTyping));
    }

    public static void SendMessage(string msg) {
        SendMsgToAllPlayers(MessageType.SendMessage, writer => writer.Write(msg, Encoding.UTF8));
    }

    private static void OnMessage(object sender, MessageReceivedEventArgs e) {

        // Ignore Messages that are not Mod Network Messages
        if (e.Tag != ModMsgTag) return;

        using var message = e.GetMessage();
        using var reader = message.GetReader();
        var modId = reader.ReadString();

        // Ignore Messages not for our mod
        if (modId != ModId) return;

        var senderGuid = reader.ReadString();

        // Ignore messages from non-friends
        if (ModConfig.MeOnlyViewFriends.Value && !Friends.FriendsWith(senderGuid)) return;

        // Ignore our own messages
        if (senderGuid == MetaPort.Instance.ownerId) return;

        var msgTypeRaw = reader.ReadByte();

        // Ignore wrong msg types
        if (!Enum.IsDefined(typeof(MessageType), msgTypeRaw)) return;

        switch ((MessageType) msgTypeRaw) {

            case MessageType.Typing:
                ChatBox.OnReceivedTyping?.Invoke(senderGuid, reader.ReadBoolean());
                break;

            case MessageType.SendMessage:
                ChatBox.OnReceivedMessage?.Invoke(senderGuid, reader.ReadString(Encoding.UTF8));
                break;
        }
    }

    private static void SendMsgToAllPlayers(MessageType msgType, Action<DarkRiftWriter> msgDataAction = null) {

        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected) {
            MelonLogger.Warning($"Attempted to {nameof(SendMsgToAllPlayers)} but the Game Network is Down...");
        }

        // Mandatory message parameters
        using var writer = DarkRiftWriter.Create();
        writer.Write(ModId);
        writer.Write((int) SeedPolicy.ToAll);

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create(ModMsgTag, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
    }

    private static void SendToSpecificPlayers(List<string> playerGuids, MessageType msgType, Action<DarkRiftWriter> msgDataAction = null) {

        if (NetworkManager.Instance == null ||
            NetworkManager.Instance.GameNetwork.ConnectionState != ConnectionState.Connected) {
            MelonLogger.Warning($"Attempted to {nameof(SendToSpecificPlayers)} but the Game Network is Down...");
        }

        // Mandatory message parameters
        using var writer = DarkRiftWriter.Create();
        writer.Write(ModId);
        writer.Write((int) SeedPolicy.ToSpecific);
        writer.Write(playerGuids.Count);
        foreach (var playerGuid in playerGuids) {
            writer.Write(playerGuid);
        }

        // Set the message type (for our internal behavior)
        writer.Write((byte) msgType);

        // Set the parameters we want to send
        msgDataAction?.Invoke(writer);

        using var message = Message.Create(ModMsgTag, writer);
        NetworkManager.Instance.GameNetwork.SendMessage(message, SendMode.Reliable);
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
    }

}
