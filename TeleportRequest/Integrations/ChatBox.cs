using System.Text.RegularExpressions;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Kafe.RequestLib;
using MelonLoader;

namespace Kafe.TeleportRequest.Integrations;

public static class ChatBox {

    internal static void InitializeChatBox() {

        ModConfig.HasChatBoxMod = true;

        Kafe.ChatBox.API.AddSendingInterceptor(chatBoxMessage => {

            // We only want the messages we sent
            if (chatBoxMessage.SenderGuid != MetaPort.Instance.ownerId) return Kafe.ChatBox.API.InterceptorResult.Ignore;

            var isOurCommand = false;

            // Prevent the command messages from showing on people's chat box and history
            if (chatBoxMessage.Message.StartsWith(ModConfig.MeCommandTeleportRequest.Value, StringComparison.OrdinalIgnoreCase)) {
                HandleTeleportRequest(chatBoxMessage.Message);
                isOurCommand = true;
            }
            else if (chatBoxMessage.Message.StartsWith(ModConfig.MeCommandTeleportAccept.Value, StringComparison.OrdinalIgnoreCase)) {
                HandleTeleportResponse(chatBoxMessage.Message, true);
                isOurCommand = true;
            }
            else if (chatBoxMessage.Message.StartsWith(ModConfig.MeCommandTeleportDecline.Value, StringComparison.OrdinalIgnoreCase)) {
                HandleTeleportResponse(chatBoxMessage.Message, false);
                isOurCommand = true;
            }
            else if (chatBoxMessage.Message.Equals(ModConfig.MeCommandTeleportBack.Value, StringComparison.OrdinalIgnoreCase)) {
                TeleportRequest.TeleportBack();
                isOurCommand = true;
            }

            // We received a Command, let's prevent the ChatBox from displaying the message
            if (isOurCommand && !ModConfig.MeShowCommandsOnChatBox.Value) return new Kafe.ChatBox.API.InterceptorResult(true, true);

            // Ignore everything else
            return Kafe.ChatBox.API.InterceptorResult.Ignore;
        });
    }

    private static bool CheckMatchingUsernames(string usernamePrefix, out CVRPlayerEntity matchedUsername) {
        var matchedUsernames = CVRPlayerManager.Instance.NetworkPlayers.FindAll(p => p.Username.StartsWith(usernamePrefix, StringComparison.OrdinalIgnoreCase));
        if (matchedUsernames.Count == 1) {
            matchedUsername = matchedUsernames.First();
            return true;
        }
        MelonLogger.Warning(matchedUsernames.Count == 0
            ? $"The command didn't match any user in the instance, attempted username prefix: {usernamePrefix}"
            : $"The command matched more than one username, it can only match one. Matched: {string.Join(", ", matchedUsernames.Select(p => p.Username))}");
        matchedUsername = null;
        return false;
    }

    private static void HandleTeleportRequest(string commandMsg) {
        var regex = new Regex($@"{Regex.Escape(ModConfig.MeCommandTeleportRequest.Value)} @(\w+)", RegexOptions.IgnoreCase);
        var match = regex.Match(commandMsg);
        if (match.Success) {
            var usernamePrefix = match.Groups[1].Value;
            if (CheckMatchingUsernames(usernamePrefix, out var matchedUser)) {
                TeleportRequest.RequestToTeleport(matchedUser.Username, matchedUser.Uuid);
            }
        }
        else {
            MelonLogger.Warning($"The command must match {ModConfig.MeCommandTeleportRequest.Value} @username_start, you sent: {commandMsg}");
        }
    }

    private static void HandleTeleportResponse(string commandMsg, bool accepted) {
        var command = accepted ? ModConfig.MeCommandTeleportAccept.Value : ModConfig.MeCommandTeleportDecline.Value;
        var regex = new Regex($@"^{Regex.Escape(command)}( @(\w+))?$", RegexOptions.IgnoreCase);
        var match = regex.Match(commandMsg);
        if (match.Success) {
            var usernamePrefix = match.Groups[2].Value;

            // If no username is provided let's accept all
            if (usernamePrefix == string.Empty) {
                foreach (var pendingReceivedRequest in API.GetPendingReceivedRequests()) {
                    API.ResolveReceivedRequest(pendingReceivedRequest, accepted ? API.RequestResult.Accepted : API.RequestResult.Declined);
                }
            }
            // If username is provided accept all requests from that players
            else {
                if (!CheckMatchingUsernames(usernamePrefix, out var matchedUser)) return;
                foreach (var pendingReceivedRequest in API.GetPendingReceivedRequests()) {
                    if (pendingReceivedRequest.SourcePlayerGuid != matchedUser.Uuid) continue;
                    API.ResolveReceivedRequest(pendingReceivedRequest, accepted ? API.RequestResult.Accepted : API.RequestResult.Declined);
                }
            }
        }
        else {
            MelonLogger.Warning($"The command must match {command} or {command} @username_start, you sent: {commandMsg}");
        }
    }

}
