using ABI_RC.Systems.OSC;
using ABI_RC.Systems.OSC.Jobs;
using Kafe.OSC.Utils;
using LucHeart.CoreOSC;
using MelonLoader;
using Newtonsoft.Json.Linq;

namespace Kafe.OSC.Modules;

public class OSCChatBoxModule : OSCModule
{
    public const string ModulePrefix = "/chatbox";

    private enum ChatBoxOperation
    {
        Input,
        Typing,
    }

    private bool _enabled;
    private bool _debugConfigWarnings;

    private OSCJobQueue<ChatBoxTypingPayload> _chatboxTypingQueue = null!;
    private OSCJobQueue<ChatBoxMessagePayload> _chatboxMessageQueue = null!;

    public OSCChatBoxModule() : base(ModulePrefix)
    {
        // Enable according to the availability and config and setup the config listeners
        UpdateEnabled();
        OSC.Instance.meOSCChatBoxModule.OnEntryValueChanged.Subscribe((_, _) => UpdateEnabled());

        // Handle the warning when blocked osc command by config
        _debugConfigWarnings = OSC.Instance.meOSCDebugConfigWarnings.Value;
        OSC.Instance.meOSCDebugConfigWarnings.OnEntryValueChanged.Subscribe((_, enabled) =>
            _debugConfigWarnings = enabled);

        return;

        void UpdateEnabled() => _enabled = OSC.Instance.meOSCChatBoxModule.Value;
    }

    #region Module Overrides

    public override void Initialize()
    {
        RegisterQueues();
        OSCServer.OSCQueryServer.OnRootResponse += OnOSCQueryResponse;
    }

    public override void Cleanup()
    {
        OSCServer.OSCQueryServer.OnRootResponse -= OnOSCQueryResponse;
        FreeQueues();
    }

    public override bool HandleIncoming(OscMessage packet)
    {
        if (!_enabled)
        {
            if (_debugConfigWarnings)
                MelonLogger.Msg($"[Config] Sent an osc msg to {Prefix}, but this module is disabled in the melon preferences file. Ignoring...");
            return false;
        }

        var addressParts = packet.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3)
        {
            MelonLogger.Msg($"Attempted to interact with the ChatBox but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{packet.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{Prefix}/<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ChatBoxOperation)))}");
            return false;
        }

        Enum.TryParse<ChatBoxOperation>(addressParts[2], true, out var chatBoxOperation);

        switch (chatBoxOperation)
        {
            case ChatBoxOperation.Input:
                return QueueChatBoxMessage(packet);
            case ChatBoxOperation.Typing:
                return QueueChatBoxTyping(packet);
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to interact with the ChatBox but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{packet.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{Prefix}/<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ChatBoxOperation))).ToLowerInvariant()}"
                );
                break;
        }

        return false;
    }

    #endregion Module Overrides

    #region Queues

    private void RegisterQueues()
    {
        _chatboxTypingQueue = OSCJobSystemExtensions.RegisterQueue<ChatBoxTypingPayload>(512, payload =>
        {
            Events.Integrations.OnChatBoxTyping(payload.IsTyping, payload.Notify);
        });

        _chatboxMessageQueue = OSCJobSystemExtensions.RegisterQueue<ChatBoxMessagePayload>(128, payload =>
        {
            Events.Integrations.OnChatBoxMessage(
                payload.Message.ToString(),
                payload.SendImmediately,
                payload.Notify,
                payload.DisplayInChatBox,
                payload.DisplayInHistory);
        });
    }

    private void FreeQueues()
    {
        OSCJobSystem.UnRegisterQueue(_chatboxTypingQueue);
        _chatboxTypingQueue = null!;
        OSCJobSystem.UnRegisterQueue(_chatboxMessageQueue);
        _chatboxMessageQueue = null!;
    }

    #endregion Queues

    private bool QueueChatBoxTyping(OscMessage oscMessage)
    {
        var arguments = oscMessage.Arguments;

        if (arguments.Length != 1 && arguments.Length != 2)
        {
            MelonLogger.Msg(
                $"[Error] Attempted to set the ChatBox isTyping, but provided an invalid number of arguments. " +
                $"Expected 1 or 2 arguments, first for whether isTyping or not, and second for whether it " +
                $"should send a sound notification or not.");
            return false;
        }

        var possibleIsTyping = arguments[0];
        if (possibleIsTyping is not bool isTyping)
        {
            MelonLogger.Msg($"[Error] Attempted to set the ChatBox isTyping, but provided an invalid bool value. " +
                            $"Attempted: \"{possibleIsTyping}\" Type: {possibleIsTyping?.GetType()}" +
                            $"The isTyping value has to be a boolean.");
            return false;
        }

        // Default notify to false
        var notify = false;
        if (arguments.Length == 2)
        {
            var possibleNotify = arguments[1];
            if (possibleNotify is not bool notifyValue)
            {
                MelonLogger.Msg(
                    $"[Error] Attempted to set ChatBox typing notify value, but provided an invalid bool value. " +
                    $"Attempted: \"{possibleNotify}\" Type: {possibleNotify?.GetType()}" +
                    $"The notify value has to be a boolean.");
                return false;
            }

            notify = notifyValue;
        }

        _chatboxTypingQueue.Enqueue(new ChatBoxTypingPayload(isTyping, notify));
        return true;
    }

    private bool QueueChatBoxMessage(OscMessage oscMessage)
    {
        var arguments = oscMessage.Arguments;

        if (arguments.Length is < 2 or > 5)
        {
            MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid number of arguments. " +
                            $"Expected between 2 and 5 arguments, first for the message, the second for whether the message should" +
                            $"be sent immediately or put it on the keyboard, optionally a third for whether it should send a " +
                            $"sound notification or not, optionally a fourth for whether should display in the ChatBox or not, and " +
                            $"lately optionally a fifth for whether it should display in the history window or not.");
            return false;
        }

        var possibleMessage = arguments[0];
        if (possibleMessage is not string message)
        {
            MelonLogger.Msg($"[Error] Attempted to send a ChatBox Msg, but provided an invalid string msg value. " +
                            $"Attempted: \"{possibleMessage}\" Type: {possibleMessage?.GetType()}" +
                            $"The msg value has to be a string.");
            return false;
        }

        var possibleSendImmediately = arguments[1];
        if (possibleSendImmediately is not bool sendImmediately)
        {
            MelonLogger.Msg(
                $"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for send immediately. " +
                $"Attempted: \"{possibleSendImmediately}\" Type: {possibleSendImmediately?.GetType()}" +
                $"The send immediately value has to be a boolean.");
            return false;
        }

        // Default notify to false
        var notify = false;
        if (arguments.Length >= 3)
        {
            var possibleNotify = arguments[2];
            if (possibleNotify is not bool notifyValue)
            {
                MelonLogger.Msg(
                    $"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the notify. " +
                    $"Attempted: \"{possibleNotify}\" Type: {possibleNotify?.GetType()}" +
                    $"The notify value has to be a boolean.");
                return false;
            }

            notify = notifyValue;
        }

        // Default display in ChatBox to true
        var displayInChatBox = true;
        if (arguments.Length >= 4)
        {
            var possibleDisplayInChatBox = arguments[3];
            if (possibleDisplayInChatBox is not bool displayInChatBoxValue)
            {
                MelonLogger.Msg(
                    $"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the display in the ChatBox. " +
                    $"Attempted: \"{possibleDisplayInChatBox}\" Type: {possibleDisplayInChatBox?.GetType()}" +
                    $"The display in ChatBox value has to be a boolean.");
                return false;
            }

            displayInChatBox = displayInChatBoxValue;
        }

        // Default display in History Window to true
        var displayInHistory = false;
        if (arguments.Length >= 5)
        {
            var possibleDisplayInHistory = arguments[4];
            if (possibleDisplayInHistory is not bool displayInHistoryValue)
            {
                MelonLogger.Msg(
                    $"[Error] Attempted to send a ChatBox Msg, but provided an invalid bool value for the display in the History Window. " +
                    $"Attempted: \"{possibleDisplayInHistory}\" Type: {possibleDisplayInHistory?.GetType()}" +
                    $"The display in history window value has to be a boolean.");
                return false;
            }

            displayInHistory = displayInHistoryValue;
        }

        _chatboxMessageQueue.Enqueue(new ChatBoxMessagePayload(message, sendImmediately, notify, displayInChatBox, displayInHistory));
        return true;
    }

    #region OSCQuery

    private static void OnOSCQueryResponse(JToken obj)
    {
        var newStructure = JObject.Parse(
            """
            {
              "CONTENTS": {
                "chatbox": {
                  "FULL_PATH": "/chatbox",
                  "ACCESS": 0,
                  "CONTENTS": {
                    "typing": {
                      "FULL_PATH": "/chatbox/typing",
                      "ACCESS": 2,
                      "TYPE": "TT"
                    },
                    "input": {
                      "FULL_PATH": "/chatbox/input",
                      "ACCESS": 2,
                      "TYPE": "sTTTT"
                    }
                  }
                }
              }
            }
            """);

        if (obj is JObject objAsJObject)
        {
            objAsJObject.Merge(newStructure, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union,
                MergeNullValueHandling = MergeNullValueHandling.Ignore,
            });
        }
    }

    #endregion OSCQuery

    #region Job Payloads

    public readonly struct ChatBoxTypingPayload(bool isTyping, bool notify)
    {
        public readonly bool IsTyping = isTyping;
        public readonly bool Notify = notify;
    }

    public readonly struct ChatBoxMessagePayload
    {
        public ChatBoxMessagePayload(string message, bool sendImmediately, bool notify, bool displayInChatBox, bool displayInHistory)
        {
            Message = new FixedUtf8String4096();
            Message.Set(message);
            SendImmediately = sendImmediately;
            Notify = notify;
            DisplayInChatBox = displayInChatBox;
            DisplayInHistory = displayInHistory;
        }
        public readonly FixedUtf8String4096 Message;
        public readonly bool SendImmediately;
        public readonly bool Notify;
        public readonly bool DisplayInChatBox;
        public readonly bool DisplayInHistory;
    }

    #endregion Job Payloads
}
