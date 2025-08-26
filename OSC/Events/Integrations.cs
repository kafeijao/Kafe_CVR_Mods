namespace Kafe.OSC.Events;

public static class Integrations
{
    public static event Action<bool, bool> ChatBoxTyping;

    public static event Action<string, bool, bool, bool, bool> ChatBoxMessage;

    internal static void OnChatBoxTyping(bool isTyping, bool notify)
    {
        ChatBoxTyping?.Invoke(isTyping, notify);
    }

    internal static void OnChatBoxMessage(string message, bool sendImmediately, bool notify, bool displayInChatBox, bool displayInHistory)
    {
        ChatBoxMessage?.Invoke(message, sendImmediately, notify, displayInChatBox, displayInHistory);
    }
}
