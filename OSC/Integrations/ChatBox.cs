namespace Kafe.OSC.Integrations;

public static class ChatBox {

    internal static void InitializeChatBox() {

        Handlers.OscModules.ChatBox.Available = true;

        Events.Integrations.ChatBoxTyping += Kafe.ChatBox.API.SetIsTyping;

        Events.Integrations.ChatBoxMessage += (msg, sendImmediately, notify) => {
            if (sendImmediately) {
                Kafe.ChatBox.API.SendMessage(msg, notify, true);
            }
            else {
                Kafe.ChatBox.API.OpenKeyboard(msg);
            }
        };
    }
    
}
