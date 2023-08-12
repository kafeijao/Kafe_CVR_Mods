namespace Kafe.OSC.Integrations;

public static class ChatBox {

    internal static void InitializeChatBox() {

        Handlers.OscModules.ChatBox.Available = true;

        Events.Integrations.ChatBoxTyping += Kafe.ChatBox.API.SetIsTyping;

        Events.Integrations.ChatBoxMessage += (msg, sendImmediately, notify, displayInChatBox, displayInHistory) => {
            if (sendImmediately) {
                Kafe.ChatBox.API.SendMessage(msg, notify, displayInChatBox, displayInHistory);
            }
            else {
                Kafe.ChatBox.API.OpenKeyboard(msg);
            }
        };
    }
    
}
