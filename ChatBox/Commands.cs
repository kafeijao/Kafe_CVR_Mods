using System.Reflection;
using MelonLoader;

namespace Kafe.ChatBox;

public static class Commands {

    private const string Character = "/";

    private static readonly List<Command> CommandList = new();

    /// <summary>
    /// Registers a new command with a specified prefix and associated actions for handling command sent and command
    /// received events. The messages will contain the / and the prefix of the command.
    /// </summary>
    /// <param name="prefix">The prefix for the command. This is used to identify the command when a message starts with
    /// the specified prefix.</param>
    /// <param name="onCommandSent">Optional. The action to be executed when the command is sent by the local user. The
    /// action takes the full message (including the prefix) as its argument. If not provided, no action will be
    /// executed when the command is sent.</param>
    /// <param name="onCommandReceived">Optional. The action to be executed when the command is received from another
    /// user. The action takes the sender's GUID as the first argument and the full message (including the prefix) as
    /// the second argument. If not provided, no action will be executed when the command is received.</param>
    public static void RegisterCommand(string prefix, Action<string> onCommandSent = null, Action<string, string> onCommandReceived = null) {
        var modName = Assembly.GetExecutingAssembly().GetName().Name;
        MelonLogger.Msg($"[Commands] The mod {modName} registered the command {prefix}.");
        CommandList.Add(new Command { Prefix = prefix, OnCommandSent = onCommandSent, OnCommandReceived = onCommandReceived});
    }

    private class Command {

        internal string Prefix;

        // Command Sent (message)
        internal Action<string> OnCommandSent;

        // Command Sent (sender guid, message)
        internal Action<string, string> OnCommandReceived;
    }

    internal static void HandleSentCommand(string message) {
        if (!message.StartsWith(Character)) return;
        foreach (var command in CommandList.Where(command => message.StartsWith(Character + command.Prefix))) {
            command.OnCommandSent?.Invoke(message);
        }
    }

    internal static void HandleReceivedCommand(string sender, string message) {
        if (!message.StartsWith(Character)) return;
        foreach (var command in CommandList.Where(command => message.StartsWith(Character + command.Prefix))) {
            command.OnCommandReceived?.Invoke(sender, message);
        }
    }
}
