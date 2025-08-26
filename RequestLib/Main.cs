using System.Diagnostics;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.UI;
using ABI_RC.Core.UI.UIMessage;
using MelonLoader;

namespace Kafe.RequestLib;

internal class RequestLib : MelonMod {

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ConfigJson.LoadConfigJson();
        ModConfig.InitializeBTKUI();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    #if DEBUG
    public override void OnUpdate() {
        if (CVRPlayerManager.Instance == null) return;
        if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Period)
            && (AuthManager.Username.StartsWith("kaf", StringComparison.InvariantCultureIgnoreCase)
            || AuthManager.Username.StartsWith("lop", StringComparison.InvariantCultureIgnoreCase))) {
            var player = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault();
            var playerGuid = player?.Uuid;
            var playerName = player?.Username;
            if (playerGuid == null)
                playerGuid = Guid.NewGuid().ToString("D");
            if (playerName == null)
                playerName = "N/A";
            ModNetwork.OnRequest(playerGuid, Guid.NewGuid().ToString("D"), "TeleportRequest",
                "", 30, $"The player {playerName}");
        }
    }
    #endif

    /// <summary>
    /// Clears the message by Reference ID
    /// This is probably not needed since the UI should clea the message on its own
    /// </summary>
    internal static void DeleteRequest(string requestId) {
        UIMessageManager.Instance.ClearMessageByReferenceID(UIMessageCategory.Other, requestId);
    }

    internal static void CreateReceivedRequest(API.Request apiRequest, string requestId, string senderGuid, string modName, string message)
    {
        string playerName = CVRPlayerManager.Instance.TryGetPlayerName(senderGuid);
        var playerEntity = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(p => p.Uuid == senderGuid);
        string playerImageUrl = playerEntity?.ApiProfileImageUrl ?? "";

        var timeout = apiRequest.Timeout - DateTime.UtcNow;
        if (timeout.Ticks <= 0)
        {
            MelonLogger.Warning($"Ignoring creating receive request since the timeout was 0 or less ({timeout.TotalSeconds})");
            return;
        }

        MelonLogger.Msg($"Request {requestId} received from {playerName}: {message}");

        var acceptButton = new UIMessageButton(
            buttonText: "Accept",
            buttonIcon: "gfx/accept.svg",
            onClick: () =>
            {
                MelonLogger.Msg($"[OnAccept] Accepting {requestId} received from {playerName}: {message}");
                // #if DEBUG
                // CreateAnsweredInfo(requestId, senderGuid, playerName, message, API.RequestResult.Accepted);
                // #endif
                API.SendResponse(apiRequest, new API.Response(API.RequestResult.Accepted, ""), requestId);
            },
            shouldSkipDismiss: true,
            clearOnClick: true,
            quickMenuAcceptButton: true);

        var declineButton = new UIMessageButton(
            buttonText: "Deny",
            buttonIcon: "gfx/deny.svg",
            onClick: () =>
            {
                MelonLogger.Msg($"[OnDecline] Declining {requestId} received from {playerName}: {message}");
                // #if DEBUG
                // CreateAnsweredInfo(requestId, senderGuid, playerName, message, API.RequestResult.Declined);
                // #endif
                API.SendResponse(apiRequest, new API.Response(API.RequestResult.Declined, ""), requestId);
            },
            shouldSkipDismiss: true,
            clearOnClick: true,
            quickMenuAcceptButton: false);

        var msg = new CVRUIMessage(
            messageName: $"{modName} from {playerName}",
            messageCategory: UIMessageCategory.Other,
            referenceID: requestId,
            messageText: message,
            messageImageUrl: playerImageUrl,
            canShowQMNotif: true,
            canBeSilenced: true,
            senderUserID: senderGuid,
            senderUsername: playerName,
            buttons: [acceptButton, declineButton]);
        msg.MessageTimeout = timeout;
        msg.DontCallDismissOnTimeout = true;
        msg.OnDismiss = () =>
        {
            MelonLogger.Msg($"[OnDismiss] Timing out {requestId} received from {playerName}: {message}");
            // #if DEBUG
            // CreateAnsweredInfo(requestId, senderGuid, playerName, message, API.RequestResult.TimedOut);
            // #endif
            API.SendResponse(apiRequest, new API.Response(API.RequestResult.TimedOut, ""), requestId);
        };

        UIMessageManager.Instance.PopUIMessage(msg);
    }

    internal static void CreateSentRequest(API.Request apiRequest, string requestId, string receiverGuid, string modName, string message)
    {
        string playerName = CVRPlayerManager.Instance.TryGetPlayerName(receiverGuid);
        var playerEntity = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(p => p.Uuid == receiverGuid);
        string playerImageUrl = playerEntity?.ApiProfileImageUrl ?? "";

        MelonLogger.Msg($"[OnSent] Request {requestId} sent to {playerName}: {message}");

        var timeout = apiRequest.Timeout - DateTime.UtcNow;
        if (timeout.Ticks <= 0)
        {
            MelonLogger.Warning($"Ignoring creating send request since the timeout was 0 or less ({timeout.TotalSeconds})");
            return;
        }

        var msg = new CVRUIMessage(
            messageName: $"{modName} to {playerName}",
            messageCategory: UIMessageCategory.Other,
            referenceID: requestId,
            messageText: message,
            messageImageUrl: playerImageUrl,
            canShowQMNotif: true,
            canBeSilenced: true,
            senderUserID: receiverGuid,
            senderUsername: playerName,
            buttons: []);
        msg.MessageTimeout = timeout;

        UIMessageManager.Instance.PopUIMessage(msg);
    }

    internal static void CreateAnsweredInfo(string requestId, string receiverGuid, string modName, string message, API.RequestResult result)
    {
        // Remove the msg saying it's pending on the sender side
        DeleteRequest(receiverGuid);

        string playerName = CVRPlayerManager.Instance.TryGetPlayerName(receiverGuid);
        var playerEntity = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(p => p.Uuid == receiverGuid);
        string playerImageUrl = playerEntity?.ApiProfileImageUrl ?? "";

        string resultStr = result switch
        {
            API.RequestResult.TimedOut => "Timed Out :(",
            API.RequestResult.Accepted => "Accepted :)",
            API.RequestResult.Declined => "Declined :(",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };

        MelonLogger.Msg($"[OnAnswered] Request {requestId} sent to {playerName} with the message: \"{message}\" was answered: {resultStr}");

        var msg = new CVRUIMessage(
            messageName: $"{modName} to [{playerName}] - {resultStr}",
            messageCategory: UIMessageCategory.Other,
            referenceID: requestId,
            messageText: message,
            messageImageUrl: playerImageUrl,
            canShowQMNotif: true,
            canBeSilenced: true,
            senderUserID: receiverGuid,
            senderUsername: playerName,
            buttons: []);
        msg.MessageTimeout = TimeSpan.FromSeconds(30);

        UIMessageManager.Instance.PopUIMessage(msg);
    }

    internal static string GetModName(int frameIncrement) {
        try {
            var callingFrame = new StackTrace().GetFrame(frameIncrement);
            var callingAssembly = callingFrame.GetMethod().Module.Assembly;
            var callingMelonAttr = callingAssembly.CustomAttributes.FirstOrDefault(
                attr => attr.AttributeType == typeof(MelonInfoAttribute));
            return (string) callingMelonAttr!.ConstructorArguments[1].Value;
        }
        catch (Exception ex) {
            MelonLogger.Error("[GetModName] Attempted to get a mod's name...");
            MelonLogger.Error(ex);
        }
        return null;
    }
}
