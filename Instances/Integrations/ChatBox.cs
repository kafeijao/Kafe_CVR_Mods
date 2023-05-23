using ABI_RC.Core.Savior;
using MelonLoader;

namespace Kafe.Instances.Integrations;

public static class ChatBox {

    internal static void InitializeChatBox() {

        Kafe.ChatBox.API.OnMessageSent += (_, s, _, _) => {
            if (s.StartsWith("/restart")) MelonCoroutines.Start(ModConfig.RestartCVR(false));
        };

        Kafe.ChatBox.API.OnMessageSent += (_, s, _, _) => {
            if (s.StartsWith("/rejoin") && !string.IsNullOrEmpty(MetaPort.Instance.CurrentInstanceId)) {
                ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(MetaPort.Instance.CurrentInstanceId,
                    MetaPort.Instance.CurrentWorldId);
            }
        };
    }
}
