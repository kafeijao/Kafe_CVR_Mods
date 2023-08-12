using MelonLoader;
using Rug.Osc.Core;

namespace Kafe.OSC.Handlers.OscModules;

public class ChatBox : OscHandler {

    private enum ChatBoxOperation {
        input,
        typing,
    }

    private static ChatBox _instance;

    private static bool _available;
    public static bool Available {
        get => _available;
        set {
            _available = value;
            _instance.UpdateEnabled();
        }
    }


    internal const string AddressPrefixChatBox = "/chatbox/";

    private bool _enabled;
    private bool _debugConfigWarnings;

    public ChatBox() {

        _instance = this;

        // Enable according to the availability and config and setup the config listeners
        UpdateEnabled();
        OSC.Instance.meOSCChatBoxModule.OnEntryValueChanged.Subscribe((_, _) => UpdateEnabled());

        // Handle the warning when blocked osc command by config
        _debugConfigWarnings = OSC.Instance.meOSCDebugConfigWarnings.Value;
        OSC.Instance.meOSCDebugConfigWarnings.OnEntryValueChanged.Subscribe((_, enabled) => _debugConfigWarnings = enabled);
    }

    private void UpdateEnabled() {
        if (OSC.Instance.meOSCChatBoxModule.Value && Available) {
            Enable();
        }
        else {
            Disable();
        }
    }

    internal sealed override void Enable() {
        _enabled = true;
    }

    internal sealed override void Disable() {
        _enabled = false;
    }

    internal sealed override void ReceiveMessageHandler(OscMessage oscMsg) {
        if (!_enabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg($"[Config] Sent an osc msg to {AddressPrefixChatBox}, but this module is disabled " +
                                $"in the configuration file, so this will be ignored.");
            }
            return;
        }

        var addressParts =oscMsg.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to interact with the ChatBox but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{AddressPrefixChatBox}<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
            return;
        }

        Enum.TryParse<ChatBoxOperation>(addressParts[2], true, out var chatBoxOperation);

        switch (chatBoxOperation) {
            case ChatBoxOperation.input:
                ReceivedMessage(oscMsg);
                return;
            case ChatBoxOperation.typing:
                ReceivedTyping(oscMsg);
                return;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to interact with the ChatBox but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{AddressPrefixChatBox}<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ChatBoxOperation)))}"
                    );
                return;
        }
    }

    private static void ReceivedTyping(OscMessage oscMessage) {

        if (oscMessage.Count != 1 && oscMessage.Count != 2) {
            MelonLogger.Msg($"[Error] Attempted to set the ChatBox isTyping, but provided an invalid number of arguments. " +
                            $"Expected 1 or 2 arguments, first for whether isTyping or not, and second for whether it " +
                            $"should send a sound notification or not.");
            return;
        }

        var possibleIsTyping = oscMessage[0];
        if (possibleIsTyping is not bool isTyping) {
            MelonLogger.Msg($"[Error] Attempted to set the ChatBox isTyping, but provided an invalid bool value. " +
                            $"Attempted: \"{possibleIsTyping}\" Type: {possibleIsTyping?.GetType()}" +
                            $"The isTyping value has to be a boolean.");
            return;
        }

        // Default notify to false
        var notify = false;
        if (oscMessage.Count == 2) {
            var possibleNotify = oscMessage[1];
            if (possibleNotify is not bool notifyValue) {
                MelonLogger.Msg($"[Error] Attempted to set ChatBox typing notify value, but provided an invalid bool value. " +
                                $"Attempted: \"{possibleNotify}\" Type: {possibleNotify?.GetType()}" +
                                $"The notify value has to be a boolean.");
                return;
            }
            notify = notifyValue;
        }

        Events.Integrations.OnChatBoxTyping(isTyping, notify);
    }

    private static void ReceivedMessage(OscMessage oscMessage) {

        if (oscMessage.Count is < 2 or > 5) {
            MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid number of arguments. " +
                            $"Expected between 2 and 5 arguments, first for the message, the second for whether the message should" +
                            $"be sent immediately or put it on the keyboard, optionally a third for whether it should send a " +
                            $"sound notification or not, optionally a fourth for whether should display in the ChatBox or not, and " +
                            $"lately optionally a fifth for whether it should display in the history window or not.");
            return;
        }

        var possibleMessage = oscMessage[0];
        if (possibleMessage is not string message) {
            MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid string msg value. " +
                            $"Attempted: \"{possibleMessage}\" Type: {possibleMessage?.GetType()}" +
                            $"The msg value has to be a string.");
            return;
        }

        var possibleSendImmediately = oscMessage[1];
        if (possibleSendImmediately is not bool sendImmediately) {
            MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for send immediately. " +
                            $"Attempted: \"{possibleSendImmediately}\" Type: {possibleSendImmediately?.GetType()}" +
                            $"The send immediately value has to be a boolean.");
            return;
        }

        // Default notify to false
        var notify = false;
        if (oscMessage.Count >= 3) {
            var possibleNotify = oscMessage[2];
            if (possibleNotify is not bool notifyValue) {
                MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the notify. " +
                                $"Attempted: \"{possibleNotify}\" Type: {possibleNotify?.GetType()}" +
                                $"The notify value has to be a boolean.");
                return;
            }
            notify = notifyValue;
        }

        // Default display in ChatBox to true
        var displayInChatBox = true;
        if (oscMessage.Count >= 4) {
            var possibleDisplayInChatBox = oscMessage[3];
            if (possibleDisplayInChatBox is not bool displayInChatBoxValue) {
                MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the display in the ChatBox. " +
                                $"Attempted: \"{possibleDisplayInChatBox}\" Type: {possibleDisplayInChatBox?.GetType()}" +
                                $"The display in ChatBox value has to be a boolean.");
                return;
            }
            displayInChatBox = displayInChatBoxValue;
        }

        // Default display in History Window to true
        var displayInHistory = false;
        if (oscMessage.Count >= 5) {
            var possibleDisplayInHistory = oscMessage[4];
            if (possibleDisplayInHistory is not bool displayInHistoryValue) {
                MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the display in the History Window. " +
                                $"Attempted: \"{possibleDisplayInHistory}\" Type: {possibleDisplayInHistory?.GetType()}" +
                                $"The display in history window value has to be a boolean.");
                return;
            }
            displayInHistory = displayInHistoryValue;
        }

        Events.Integrations.OnChatBoxMessage(message, sendImmediately, notify, displayInChatBox, displayInHistory);
    }

}
