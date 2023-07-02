using System.Diagnostics;
using MelonLoader;

namespace Kafe.ChatBox;

public static class API {

    private const string OSCModName = "OSC";

    /// <summary>
    /// The ChatBoxMessage class encapsulates details about a chat message received by the local player.
    /// This can be triggered by various sources such as Internal, OSC, or external Mods.
    /// </summary>
    public class ChatBoxMessage {

        /// <summary>
        /// The source of the chat message, which can be Internal, OSC, or external Mod.
        /// </summary>
        public readonly MessageSource Source;

        /// <summary>
        /// The unique identifier (GUID) of the player who sent the chat message.
        /// </summary>
        public readonly string SenderGuid;

        /// <summary>
        /// The raw text content of the chat message that was received.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// A flag indicating whether a notification should be triggered for this chat message.
        /// </summary>
        public readonly bool TriggerNotification;

        /// <summary>
        /// A flag indicating whether this chat message should be displayed in the chat box.
        /// </summary>
        public readonly bool DisplayOnChatBox;

        /// <summary>
        /// A flag indicating whether this chat message should be displayed in the history window.
        /// </summary>
        public readonly bool DisplayOnHistory;

        /// <summary>
        /// The name of the mod in case it was sent via Mod.
        /// </summary>
        public readonly string ModName;

        internal ChatBoxMessage(MessageSource source, string senderGuid, string message, bool triggerNotification, bool displayOnChatBox, bool displayOnHistory, string modName) {
            Source = source;
            SenderGuid = senderGuid;
            Message = message;
            TriggerNotification = triggerNotification;
            DisplayOnChatBox = displayOnChatBox;
            DisplayOnHistory = displayOnHistory;
            ModName = modName;
        }
    }

    /// <summary>
    /// The ChatBoxTyping class encapsulates details about a typing event received by the local player.
    /// This can be triggered by various sources such as Internal, OSC, or external Mods.
    /// </summary>
    public class ChatBoxTyping {

        /// <summary>
        /// The source of the typing event, which can be Internal, OSC, or external Mod.
        /// </summary>
        public readonly MessageSource Source;

        /// <summary>
        /// The unique identifier (GUID) of the player who triggered the typing event.
        /// </summary>
        public readonly string SenderGuid;

        /// <summary>
        /// A flag indicating whether the player is currently typing. True if typing, false otherwise.
        /// </summary>
        public readonly bool IsTyping;

        /// <summary>
        /// A flag indicating whether a notification (e.g., a sound) should be triggered for this typing event.
        /// </summary>
        public readonly bool TriggerNotification;

        internal ChatBoxTyping(MessageSource source, string senderGuid, bool isTyping, bool triggerNotification) {
            Source = source;
            SenderGuid = senderGuid;
            IsTyping = isTyping;
            TriggerNotification = triggerNotification;
        }
    }

    /// <summary>
    /// This action is triggered whenever the local player sends a message to the Network.
    /// </summary>
    public static Action<ChatBoxMessage> OnMessageSent;

    /// <summary>
    /// This action is triggered whenever the local player receives a ChatBox message from the Network.
    /// </summary>
    public static Action<ChatBoxMessage> OnMessageReceived;

    /// <summary>
    /// This action is triggered whenever the local player sends a is typing event to the Network.
    /// </summary>
    public static Action<ChatBoxTyping> OnIsTypingSent;

    /// <summary>
    /// This action is triggered whenever the local player receives a is typing event from the Network.
    /// </summary>
    public static Action<ChatBoxTyping> OnIsTypingReceived;


    /// <summary>
    /// Where the message was sent from.
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
    /// <param name="displayInChatBox">Whether to display the message on the ChatBox or not.</param>
    /// <param name="displayInHistory">Whether to display the message on the History Window or not.</param>
    public static void SendMessage(string message, bool sendSoundNotification, bool displayInChatBox, bool displayInHistory) {
        var modName = GetModName();
        var source = modName == OSCModName ? MessageSource.OSC : MessageSource.Mod;
        ModNetwork.SendMessage(source, modName, message, sendSoundNotification, displayInChatBox, displayInHistory);
    }

    /// <summary>
    /// Sets the typing status of the local player.
    /// </summary>
    /// <param name="isTyping">A boolean value indicating whether the local player is typing. Set to true if the player is typing, and false if the player is not typing.</param>
    /// <param name="sendSoundNotification">Whether to send a sounds notification or not.</param>
    public static void SetIsTyping(bool isTyping, bool sendSoundNotification) {
        ChatBox.SetIsTyping(GetModName() == OSCModName ? MessageSource.OSC : MessageSource.Mod, isTyping, sendSoundNotification);
    }

    /// <summary>
    /// Opens the in-game keyboard, with an optional initial message.
    /// </summary>
    /// <param name="initialMessage">The initial message to be displayed on the keyboard when it is opened. This can be an empty string.</param>
    public static void OpenKeyboard(string initialMessage = "") {
        ChatBox.OpenKeyboard(false, initialMessage);
    }

    internal static readonly HashSet<Func<ChatBoxMessage, InterceptorResult>> ReceivingInterceptors = new();
    internal static readonly HashSet<Func<ChatBoxMessage, InterceptorResult>> SendingInterceptors = new();

    /// <summary>
    /// Allows adding an interceptor to RECEIVED messages to override the display of the messages.
    /// </summary>
    /// <param name="interceptor">The handler that receives a message and is waiting for a result. You need to create one.
    /// For the result you can use `InterceptorResult.Ignore` if you don't want to override the message.</param>
    public static void AddReceivingInterceptor(Func<ChatBoxMessage, InterceptorResult> interceptor) => ReceivingInterceptors.Add(interceptor);


    /// <summary>
    /// Allows removing a previously added interceptor to RECEIVED messages to override the display of the messages.
    /// </summary>
    /// <param name="interceptor">A reference for the interceptor previously added.</param>
    public static void RemoveReceivingInterceptor(Func<ChatBoxMessage, InterceptorResult> interceptor) => ReceivingInterceptors.Remove(interceptor);


    /// <summary>
    /// Allows adding an interceptor to SENT messages to override the display of the messages.
    /// </summary>
    /// <param name="interceptor">The handler that receives a message and is waiting for a result. You need to create one.
    /// For the result you can use `InterceptorResult.Ignore` if you don't want to override the message.</param>
    public static void AddSendingInterceptor(Func<ChatBoxMessage, InterceptorResult> interceptor) => SendingInterceptors.Add(interceptor);


    /// <summary>
    /// Allows removing a previously added interceptor to RECEIVED messages to override the display of the messages.
    /// </summary>
    /// <param name="interceptor">A reference for the interceptor previously added.</param>
    public static void RemoveSendingInterceptor(Func<ChatBoxMessage, InterceptorResult> interceptor) => SendingInterceptors.Remove(interceptor);

    /// <summary>
    /// Class representing the result of an Interception. Use `InterceptorResult.Ignore` as a result if you don't want to affect the message.
    /// </summary>
    public class InterceptorResult {

        public readonly string ModName;
        public readonly bool PreventDisplayOnChatBox;
        public readonly bool PreventDisplayOnHistory;

        /// <summary>
        /// Instance of an InterceptorResult that does not affect the message. You should use this when you want the interceptor to be ignored.
        /// </summary>
        public static readonly InterceptorResult Ignore = new(false, false);

        /// <summary>
        /// Create an interceptor to prevent certain messages from being displayed either on the ChatBox or History Window.
        /// </summary>
        /// <param name="preventDisplayOnChatBox"></param> Whether it should prevent displaying on ChatBox or not.
        /// <param name="preventDisplayOnHistory"></param> Whether it should prevent displaying on History Window or not.
        public InterceptorResult(bool preventDisplayOnChatBox, bool preventDisplayOnHistory) {
            PreventDisplayOnChatBox = preventDisplayOnChatBox;
            PreventDisplayOnHistory = preventDisplayOnHistory;
            ModName = GetModName();
        }
    }

    private static string GetModName() {
        try {
            var callingFrame = new StackTrace().GetFrame(2);
            var callingAssembly = callingFrame.GetMethod().Module.Assembly;
            var callingMelonAttr = callingAssembly.CustomAttributes.FirstOrDefault(
                attr => attr.AttributeType == typeof(MelonInfoAttribute));
            return (string) callingMelonAttr!.ConstructorArguments[1].Value;
        }
        catch (Exception ex) {
            MelonLogger.Error("[GetModName] Attempted to get a mod's name...");
            MelonLogger.Error(ex);
        }
        return "N/A";
    }
}
