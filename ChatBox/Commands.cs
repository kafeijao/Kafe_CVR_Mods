using System.Reflection;
using MelonLoader;

namespace Kafe.ChatBox;

public static class Commands {

    private const string Character = "/";

    private static readonly List<Command> CommandList = new();
    internal static readonly Dictionary<string, List<string>> ModCommands = new();

    /// <summary>
    /// Registers a new command with a specified prefix and associated actions for handling command sent and command
    /// received events. The messages will contain the / and the prefix of the command.
    /// </summary>
    /// <param name="prefix">The prefix for the command. This is used to identify the command when a message starts with
    /// the specified prefix.</param>
    /// <param name="onCommandSent">Optional. The action to be executed when the command is sent by the local user. The
    /// action takes the full message (including the prefix) as its first argument, and the second one whether it was
    /// specified to do a sound notification or not. If not provided, no action will be executed when the command is
    /// sent.</param>
    /// <param name="onCommandReceived">Optional. The action to be executed when the command is received from another
    /// user. The action takes the sender's GUID as the first argument, the full message (including the prefix) as
    /// the second argument, and a third argument of whether it was specified to do a sound notification or not. If not
    /// provided, no action will be executed when the command is received.</param>
    public static void RegisterCommand(string prefix, Action<string, bool> onCommandSent = null, Action<string, string, bool> onCommandReceived = null) {
        // Auto detect namespace of caller, Thanks Daky
        // https://github.com/dakyneko/DakyModsCVR/blob/11386c4b83a6292a277e9c73ad50322abbffe28b/ActionMenu/ActionMenu.cs#L44
        var stackTrace = new System.Diagnostics.StackTrace();
        var modName = stackTrace.GetFrame(1).GetMethod().DeclaringType!.Namespace;
        CommandList.Add(new Command { Prefix = prefix, OnCommandSent = onCommandSent, OnCommandReceived = onCommandReceived});
        if (!ModCommands.ContainsKey(modName!)) ModCommands[modName] = new List<string>();
        ModCommands[modName].Add(prefix);
    }

    private class Command {

        internal string Prefix;

        // Command Sent (message)
        internal Action<string, bool> OnCommandSent;

        // Command Sent (sender guid, message)
        internal Action<string, string, bool> OnCommandReceived;
    }

    internal static void HandleSentCommand(string message, bool notification) {
        if (!message.StartsWith(Character)) return;
        foreach (var command in CommandList.Where(command => message.StartsWith(Character + command.Prefix))) {
            command.OnCommandSent?.Invoke(message, notification);
        }
    }

    internal static void HandleReceivedCommand(string sender, string message, bool notification) {
        if (!message.StartsWith(Character)) return;
        foreach (var command in CommandList.Where(command => message.StartsWith(Character + command.Prefix))) {
            command.OnCommandReceived?.Invoke(sender, message, notification);
        }
    }
}
