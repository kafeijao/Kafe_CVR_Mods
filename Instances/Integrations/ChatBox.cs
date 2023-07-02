using ABI_RC.Core.Savior;
using MelonLoader;

namespace Kafe.Instances.Integrations;

public static class ChatBox {

    internal static void InitializeChatBox() {

        Kafe.ChatBox.API.AddSendingInterceptor(chatBoxMessage => {
            // Ignore non-restart commands
            if (!chatBoxMessage.Message.StartsWith("/restart")) return Kafe.ChatBox.API.InterceptorResult.Ignore;

            // Send a message saying we're going to restart
            Kafe.ChatBox.API.SendMessage("I'm going to restart my game, brb!", false, false, true);

            // Prevent the /restart message from showing on people's chat box and history
            return new Kafe.ChatBox.API.InterceptorResult(true, true);
        });

        Kafe.ChatBox.API.AddSendingInterceptor(chatBoxMessage => {
            // Ignore non-rejoin commands
            if (!chatBoxMessage.Message.StartsWith("/rejoin")) return Kafe.ChatBox.API.InterceptorResult.Ignore;

            // Send a message saying we're going to restart
            Kafe.ChatBox.API.SendMessage("I'm going to rejoin, brb!", false, false, true);

            // Prevent the /rejoin message from showing on people's chat box and history
            return new Kafe.ChatBox.API.InterceptorResult(true, true);
        });

        Kafe.ChatBox.API.OnMessageSent += msg => {
            if (msg.Message.StartsWith("/restart")) MelonCoroutines.Start(ModConfig.RestartCVR(false));
        };

        Kafe.ChatBox.API.OnMessageSent += msg => {
            if (msg.Message.StartsWith("/rejoin") && !string.IsNullOrEmpty(MetaPort.Instance.CurrentInstanceId)) {
                ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(MetaPort.Instance.CurrentInstanceId,
                    MetaPort.Instance.CurrentWorldId);
            }
        };
    }
}
