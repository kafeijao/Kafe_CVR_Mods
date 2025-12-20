#nullable enable
using ABI_RC.Core.Savior;
using ABI_RC.Systems.ChatBox;
using MelonLoader;

namespace Kafe.Instances.Integrations;

public static class ChatBoxIntegration
{
    public const string RestartCommandPrefix = "/restart";
    public const string RejoinCommandPrefix = "/rejoin";

    private static ChatBoxAPI.InterceptorResult RestartInterceptor(ChatBoxAPI.ChatBoxMessage chatBoxMessage)
    {
        // Ignore non-restart commands
        if (!chatBoxMessage.Message.StartsWith(RestartCommandPrefix)) return ChatBoxAPI.InterceptorResult.Ignore;

        // Send a message saying we're going to restart
        ChatBoxAPI.SendMessage("I'm going to restart my game, brb!", false, false, true);

        // Prevent the /restart message from showing on people's chat box and history
        return new ChatBoxAPI.InterceptorResult(true, true);
    }

    private static ChatBoxAPI.InterceptorResult RejoinInterceptor(ChatBoxAPI.ChatBoxMessage chatBoxMessage)
    {
        // Ignore non-rejoin commands
        if (!chatBoxMessage.Message.StartsWith(RejoinCommandPrefix)) return ChatBoxAPI.InterceptorResult.Ignore;

        // Send a message saying we're going to rejoin
        ChatBoxAPI.SendMessage("I'm going to rejoin, brb!", false, false, true);

        // Prevent the /rejoin message from showing on people's chat box and history
        return new ChatBoxAPI.InterceptorResult(true, true);
    }

    private static void HandleRestart(ChatBoxAPI.ChatBoxMessage chatBoxMessage)
    {
        if (chatBoxMessage.Message.StartsWith(RestartCommandPrefix)) MelonCoroutines.Start(ModConfig.RestartCVR(false));
    }

    private static void HandleRejoin(ChatBoxAPI.ChatBoxMessage chatBoxMessage)
    {
        if (chatBoxMessage.Message.StartsWith(RejoinCommandPrefix) &&
            !string.IsNullOrEmpty(ABI_RC.Core.Networking.IO.Instancing.Instances.CurrentInstanceId))
        {
            ABI_RC.Core.Networking.IO.Instancing.Instances.TryJoinInstance(ABI_RC.Core.Networking.IO.Instancing.Instances.CurrentInstanceId,
                ABI_RC.Core.Networking.IO.Instancing.Instances.JoinInstanceSource.Mod);
        }
    }


    internal static void InitializeChatBox()
    {
        ChatBoxAPI.AddSendingInterceptor(RestartInterceptor);
        ChatBoxAPI.OnMessageSent += HandleRestart;

        ChatBoxAPI.AddSendingInterceptor(RejoinInterceptor);
        ChatBoxAPI.OnMessageSent += HandleRejoin;
    }
}
