using System.Text.RegularExpressions;
using ABI_RC.Core;
using UnityEngine;

namespace Kafe.LoggerPlusPlus;

public static class LogParser {

    // Game Logs Regex
    private static readonly Regex CVRLogRegex = new(@"\((?<id>[A-Za-z0-9]+) \| (?<severity>[A-Za-z]+)\) \[(?<source>[^\]]+)\] (?<message>.*)", RegexOptions.Compiled);
    private static readonly Regex CVRLogRegexAlt = new(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} \((?<source>[^\|]+)\| (?<severity>[A-Za-z]+)\) (?<message>.*)", RegexOptions.Compiled);

    // Cohtml Logs Regex
    private static readonly Regex CohtmlLogRegex = new(@"\[Cohtml\] \((Info|Warning|Error|AssertFailure)\) ", RegexOptions.Compiled);

    // Missing Script Messages
    private static readonly Regex ScriptMissingPattern1 = new(@"The referenced script \((?<scriptName>.+?)\) on this Behaviour is missing!", RegexOptions.Compiled);
    private static readonly Regex ScriptMissingPattern2 = new(@"The referenced script on this Behaviour \(Game Object '(?<scriptName>.*?)'\) is missing!", RegexOptions.Compiled);

    // AV Pro Messages
    private static readonly HashSet<string> AvProPrefixMessages = new() {
        "[AVProVideo] ",
    };
    private static readonly HashSet<string> AvProInnerMessages = new() {
        "ABI_RC.VideoPlayer.Scripts.Players.AvPro.AvProPlayer.get_Time () (at",
        "ABI_RC.VideoPlayer.Scripts.Players.AvPro.AvProPlayer.CleanupVideoPlayers () (at",
        "ABI_RC.VideoPlayer.Scripts.ViewManagerVideoPlayer.E_NotifyUser (System.String text)",
        "ABI.CCK.Components.CVRVideoPlayer.Pause (System.Boolean broadcast, System.String username) (at",
    };

    // Misc Spam Messages
    private static readonly HashSet<string> MiscSpamMessages = new() {
        "Result from API Request InstanceDetail returned NotFound",
        "Kinematic body only supports Speculative Continuous collision detection",
        "RenderTexture.Create: Depth|ShadowMap RenderTexture requested without a depth buffer. Changing to a 16 bit depth buffer.",
        "Bundle in old format attempting legacy loading!",
    };
    private static readonly HashSet<string> MiscSpamPrefixMessages = new() {
        "IK chain has no Bones.",
        "Screen position out of view frustum ",
        "The character with Unicode value ",
        "Couldn't create a Convex Mesh from source mesh ",
        "Material doesn't have a color property ",
        "Coroutine couldn't be started because the the game object '",
        "Only custom filters can be played. Please add a custom filter or an audioclip to the audiosource (",
    };
    private static readonly HashSet<string> MiscSpamSuffixMessages = new() {
        " is already registered as a write bone.",
    };

    // Misc Useless Messages
    private static readonly HashSet<string> MiscUselessMessages = new() {
        "-------------------------------------------------------------------------------------------------------",
        "~   This Game has been MODIFIED using MelonLoader. DO NOT report any issues to the Game Developers!   ~",
        "Loaded HRTF: default.",
        "Successfully connected to API Websocket",
        "0",
    };
    private static readonly HashSet<string> MiscUselessPrefixesMessages = new() {
        "Non-convex MeshCollider with non-kinematic Rigidbody is no longer supported since Unity 5",
    };


    public static bool TryParseGame(ref string message, ref LogType logType) {
        var isAlt = false;
        var match = CVRLogRegex.Match(message);
        if (!match.Success) {
            match = CVRLogRegexAlt.Match(message);
            isAlt = true;
            if (!match.Success) return false;
        }

        // Attempt to parse the severity
        if (!Enum.TryParse(match.Groups["severity"].Value, out CommonTools.LogLevelType_t cvrLogType)) return false;
        switch (cvrLogType) {
            case CommonTools.LogLevelType_t.Info:
                logType = LogType.Log;
                break;
            case CommonTools.LogLevelType_t.Warning:
                logType = LogType.Warning;
                break;
            case CommonTools.LogLevelType_t.Error:
            case CommonTools.LogLevelType_t.Fatal:
                logType = LogType.Error;
                break;
            default:
                return false;
        }

        var id = isAlt ? "N/A" : match.Groups["id"].Value;
        var source = match.Groups["source"].Value;
        var msg = match.Groups["message"].Value;

        message = $"[CVR] [{id}] [{source}] {msg}";

        return true;
    }

    public static bool TryParseCohtml(ref string message, ref LogType logType) {
        var match = CohtmlLogRegex.Match(message);
        if (!match.Success) return false;
        switch (match.Groups[1].Value) {
            case "Info":
                logType = LogType.Log;
                break;
            case "Warning":
                logType = LogType.Warning;
                break;
            case "Error":
                logType = LogType.Error;
                break;
            case "AssertFailure":
                logType = LogType.Assert;
                break;
            default:
                return false;
        }
        // Remove the matched prefix and prefix Cohtml.
        message = "[Cohtml] " + message.Substring(match.Length);
        return true;
    }

    public static bool TryParseMissingScript(ref string message) {
        var match = ScriptMissingPattern1.Match(message);
        if (match.Success) {
            // scriptName = match.Groups["scriptName"].Value;
            message = "[MissingScript] " + message;
            return true;
        }

        match = ScriptMissingPattern2.Match(message);
        if (match.Success) {
            message = "[MissingScript] " + message;
            // scriptName = match.Groups["scriptName"].Value;
            return true;
        }
        return false;
    }

    public static bool IsSpamMessage(string message) =>
        MiscSpamMessages.Contains(message)
        || MiscSpamPrefixMessages.Any(message.StartsWith)
        || MiscSpamSuffixMessages.Any(message.EndsWith);

    public static bool IsUselessMessage(string message) =>
        MiscUselessMessages.Contains(message)
        || MiscUselessPrefixesMessages.Any(message.StartsWith);

    public static bool IsAvPro(string message) =>
        AvProPrefixMessages.Any(message.StartsWith)
        || AvProInnerMessages.Any(message.Contains);
}
