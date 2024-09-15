#nullable enable
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;

namespace Kafe.Instances.Integrations;

public static class ChatBoxIntegration {

    public const string RestartCommandPrefix = "/restart";
    public const string RejoinCommandPrefix = "/rejoin";

    private static void SafeStaticInvokeFunc<TInstance, TResult>(Type ourClassType, string ourMethodName, Type libClassType, string libMethodName) {
        // Crappy handler to ensure delegates don't get referenced in the static constructor in the IL code ;_;
        var ourMethodInfo = ourClassType.GetMethod(ourMethodName, AccessTools.all);
        var delegateType = typeof(Func<,>).MakeGenericType(typeof(TInstance), typeof(TResult));
        var genericDelegate = Delegate.CreateDelegate(delegateType, ourMethodInfo!);
        var libMethodInfo = libClassType.GetMethod(libMethodName, AccessTools.all);
        // If not static we need to send the libClassInstance instead of null
        libMethodInfo!.Invoke(null, new object[] { genericDelegate });
    }

    private static void SafeStaticInvokeAction<TInstance>(Type ourClassType, string ourMethodName, Type libClassType, string libFieldName) {
        // Crappy handler to ensure delegates don't get referenced in the static constructor in the IL code ;_;
        var outMethodInfo = ourClassType.GetMethod(ourMethodName, AccessTools.all);
        var delegateType = typeof(Action<>).MakeGenericType(typeof(TInstance));
        var genericDelegate = outMethodInfo!.CreateDelegate(delegateType);
        var libFieldInfo = libClassType.GetField(libFieldName, AccessTools.all);
        var existingDelegate = (Delegate?)libFieldInfo!.GetValue(null);
        // We need to combine because the += we want to preserve the other ones
        var newDelegate = Delegate.Combine(existingDelegate, genericDelegate);
        // If not static we need to send the libClassInstance instead of null
        libFieldInfo.SetValue(null, newDelegate);
    }

    private static ChatBox.API.InterceptorResult RestartInterceptor(ChatBox.API.ChatBoxMessage chatBoxMessage) {
        // Ignore non-restart commands
        if (!chatBoxMessage.Message.StartsWith(RestartCommandPrefix)) return ChatBox.API.InterceptorResult.Ignore;

        // Send a message saying we're going to restart
        ChatBox.API.SendMessage("I'm going to restart my game, brb!", false, false, true);

        // Prevent the /restart message from showing on people's chat box and history
        return new ChatBox.API.InterceptorResult(true, true);
    }

    private static ChatBox.API.InterceptorResult RejoinInterceptor(ChatBox.API.ChatBoxMessage chatBoxMessage) {
        // Ignore non-rejoin commands
        if (!chatBoxMessage.Message.StartsWith(RejoinCommandPrefix)) return ChatBox.API.InterceptorResult.Ignore;

        // Send a message saying we're going to rejoin
        ChatBox.API.SendMessage("I'm going to rejoin, brb!", false, false, true);

        // Prevent the /rejoin message from showing on people's chat box and history
        return new ChatBox.API.InterceptorResult(true, true);
    }

    private static void HandleRestart(ChatBox.API.ChatBoxMessage chatBoxMessage) {
        if (chatBoxMessage.Message.StartsWith(RestartCommandPrefix)) MelonCoroutines.Start(ModConfig.RestartCVR(false));
    }

    private static void HandleRejoin(ChatBox.API.ChatBoxMessage chatBoxMessage) {
        if (chatBoxMessage.Message.StartsWith(RejoinCommandPrefix) && !string.IsNullOrEmpty(MetaPort.Instance.CurrentInstanceId)) {
            ABI_RC.Core.Networking.IO.Instancing.Instances.SetJoinTarget(MetaPort.Instance.CurrentInstanceId);
        }
    }


    internal static void InitializeChatBox() {

        // Setup the restart interceptor and handler
        SafeStaticInvokeFunc<ChatBox.API.ChatBoxMessage, ChatBox.API.InterceptorResult>(typeof(ChatBoxIntegration), nameof(RestartInterceptor), typeof(ChatBox.API), nameof(ChatBox.API.AddSendingInterceptor));
        SafeStaticInvokeAction<ChatBox.API.ChatBoxMessage>(typeof(ChatBoxIntegration), nameof(HandleRestart), typeof(ChatBox.API), nameof(ChatBox.API.OnMessageSent));

        // Setup the rejoin interceptor and handler
        SafeStaticInvokeFunc<ChatBox.API.ChatBoxMessage, ChatBox.API.InterceptorResult>(typeof(ChatBoxIntegration), nameof(RejoinInterceptor), typeof(ChatBox.API), nameof(ChatBox.API.AddSendingInterceptor));
        SafeStaticInvokeAction<ChatBox.API.ChatBoxMessage>(typeof(ChatBoxIntegration), nameof(HandleRejoin), typeof(ChatBox.API), nameof(ChatBox.API.OnMessageSent));
    }
}
