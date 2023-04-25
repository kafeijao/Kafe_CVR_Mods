#if DEBUG
using MelonLoader;
#endif

namespace Kafe.ChatBox;

public static class API {

    private const string OSCNamespace = "Kafe.OSC.Integrations";

    /// <summary>
    /// This action is triggered whenever the local player sends a message to the Network.
    /// </summary>
    /// <param name="source">The source of the message, can be Internal, OSC, or external Mod..</param>
    /// <param name="msg">The raw message text that was sent.</param>
    /// <param name="notification">A boolean indicating if a notification should be sent for the message.</param>
    /// <param name="displayMessage">A boolean indicating if the message should be displayed.</param>
    public static Action<MessageSource, string, bool, bool> OnMessageSent;

    /// <summary>
    /// This action is triggered whenever the local player receives a ChatBox message from the Network.
    /// </summary>
    /// <param name="source">The source of the message, can be Internal, OSC, or external Mod..</param>
    /// <param name="senderGuid">The guid of the player that sent the message.</param>
    /// <param name="msg">The raw message text that was received.</param>
    /// <param name="sendNotification">A boolean indicating if a notification should be sent for the message.</param>
    /// <param name="displayMessage">A boolean indicating if the message should be displayed.</param>
    public static Action<MessageSource, string, string, bool, bool> OnMessageReceived;

    /// <summary>
    /// This action is triggered whenever the local player sends a is typing event to the Network.
    /// </summary>
    /// <param name="source">The source of the message, can be Internal, OSC, or external Mod..</param>
    /// <param name="isTyping">A boolean indicating the typing status (true if typing, false otherwise).</param>
    /// <param name="notification">Whether the event will perform a sound or not.</param>
    public static Action<MessageSource, bool, bool> OnIsTypingSent;

    /// <summary>
    /// This action is triggered whenever the local player receives a is typing event from the Network.
    /// </summary>
    /// <param name="source">The source of the message, can be Internal, OSC, or external Mod..</param>
    /// <param name="senderGuid">The guid of the player that sent the event.</param>
    /// <param name="isTyping">A boolean indicating the typing status (true if typing, false otherwise).</param>
    /// <param name="notification">Whether the event will perform a sound or not.</param>
    public static Action<MessageSource, string, bool, bool> OnIsTypingReceived;


    /// <summary>
    /// Where was the message sent from.
    /// </summary>
    public enum MessageSource : byte {
        Internal,
        OSC,
        Mod,
    }

    /// <summary>
    /// Sends a message through the ChatBox.
    /// </summary>
    /// <param name="message">The message to be sent through the ChatBox.</param>
    /// <param name="sendSoundNotification">Whether to send a sounds notification or not.</param>
    /// <param name="displayMessage">Whether to display the message on the ChatBox and History or not.</param>
    public static void SendMessage(string message, bool sendSoundNotification, bool displayMessage) {
        ModNetwork.SendMessage(GetModName() == OSCNamespace ? MessageSource.OSC : MessageSource.Mod, message, sendSoundNotification, displayMessage);
    }

    /// <summary>
    /// Sets the typing status of the local player.
    /// </summary>
    /// <param name="isTyping">A boolean value indicating whether the local player is typing. Set to true if the player is typing, and false if the player is not typing.</param>
    /// <param name="sendSoundNotification">Whether to send a sounds notification or not.</param>
    public static void SetIsTyping(bool isTyping, bool sendSoundNotification) {
        ChatBox.SetIsTyping(GetModName() == OSCNamespace ? MessageSource.OSC : MessageSource.Mod, isTyping, sendSoundNotification);
    }

    /// <summary>
    /// Opens the in-game keyboard, with an optional initial message.
    /// </summary>
    /// <param name="initialMessage">The initial message to be displayed on the keyboard when it is opened. This can be an empty string.</param>
    public static void OpenKeyboard(string initialMessage = "") {
        ChatBox.OpenKeyboard(false, initialMessage);
    }

    private static string GetModName() {
        // Auto detect namespace of caller, Thanks Daky
        // https://github.com/dakyneko/DakyModsCVR/blob/11386c4b83a6292a277e9c73ad50322abbffe28b/ActionMenu/ActionMenu.cs#L44
        var stackTrace = new System.Diagnostics.StackTrace();
        var modName = stackTrace.GetFrame(2).GetMethod().DeclaringType!.Namespace;
        #if DEBUG
        MelonLogger.Msg($"[GetModName] Mod Name: {modName}");
        #endif
        return modName;
    }
}
