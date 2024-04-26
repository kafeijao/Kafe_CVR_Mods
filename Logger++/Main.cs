using System.Collections.Concurrent;
using CustomWebSocketSharp;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.LoggerPlusPlus;

public class LoggerPlusPlus : MelonPlugin {

    public override void OnApplicationEarlyStart() {

        ModConfig.InitializeMelonPrefs();

        var defaultStackLogTypeError = Application.GetStackTraceLogType(LogType.Error);
        if (ModConfig.MeFullStackForErrors.Value) Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.Full);
        ModConfig.MeFullStackForErrors.OnEntryValueChanged.Subscribe((_, newValue) => {
            Application.SetStackTraceLogType(LogType.Error, newValue ? StackTraceLogType.Full : defaultStackLogTypeError);
        });

        var defaultStackLogTypeAssert = Application.GetStackTraceLogType(LogType.Assert);
        if (ModConfig.MeFullStackForAsserts.Value) Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.Full);
        ModConfig.MeFullStackForAsserts.OnEntryValueChanged.Subscribe((_, newValue) => {
            Application.SetStackTraceLogType(LogType.Assert, newValue ? StackTraceLogType.Full : defaultStackLogTypeAssert);
        });

        var defaultStackLogTypeException = Application.GetStackTraceLogType(LogType.Exception);
        if (ModConfig.MeFullStackForExceptions.Value) Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full);
        ModConfig.MeFullStackForExceptions.OnEntryValueChanged.Subscribe((_, newValue) => {
            Application.SetStackTraceLogType(LogType.Exception, newValue ? StackTraceLogType.Full : defaultStackLogTypeException);
        });

        var defaultStackLogTypeWarning = Application.GetStackTraceLogType(LogType.Warning);
        if (ModConfig.MeFullStackForWarnings.Value) Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.Full);
        ModConfig.MeFullStackForWarnings.OnEntryValueChanged.Subscribe((_, newValue) => {
            Application.SetStackTraceLogType(LogType.Warning, newValue ? StackTraceLogType.Full : defaultStackLogTypeWarning);
        });

        var defaultStackLogTypeLog = Application.GetStackTraceLogType(LogType.Log);
        if (ModConfig.MeFullStackForLogs.Value) Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.Full);
        ModConfig.MeFullStackForLogs.OnEntryValueChanged.Subscribe((_, newValue) => {
            Application.SetStackTraceLogType(LogType.Log, newValue ? StackTraceLogType.Full : defaultStackLogTypeLog);
        });

        Application.logMessageReceived += LogMessageReceived;
        Application.logMessageReceivedThreaded += LogMessageReceived;

        HarmonyInstance.Patch(
            AccessTools.Method(AccessTools.TypeByName("DebugLogHandler"), "Internal_Log", new[] {
                typeof(LogType),
                typeof(LogOption),
                typeof(string),
                typeof(UnityEngine.Object)
            }),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(LoggerPlusPlus), nameof(InternalLog)))
        );

        HarmonyInstance.Patch(
            AccessTools.Method(AccessTools.TypeByName("DebugLogHandler"), "Internal_LogException", new[] {
                typeof(LogType),
                typeof(LogOption),
                typeof(string),
                typeof(UnityEngine.Object)
            }),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(LoggerPlusPlus), nameof(InternalLogException)))
        );
    }

    private static void ActualLogMessage(LogType type, string message, bool isStackTrace, Exception exception) {

        // Check if it's a spam log
        if (LogParser.IsSpamMessage(message)) {
            if (!ModConfig.MeShowSpamMessages.Value) return;
        }
        // Check if it's a useless log
        else if (LogParser.IsUselessMessage(message)) {
            if (!ModConfig.MeShowUselessMessages.Value) return;
        }
        // Check Cohtml logs
        else if (LogParser.TryParseCohtml(ref message, ref type)) {
            if (!ModConfig.MeShowCohtmlInfo.Value && type == LogType.Log) return;
            if (!ModConfig.MeShowCohtmlWarning.Value && type == LogType.Warning) return;
            if (!ModConfig.MeShowCohtmlError.Value && type is LogType.Error or LogType.Assert) return;
        }
        // Check Missing Scripts logs
        else if (LogParser.TryParseMissingScript(ref message)) {
            if (!ModConfig.MeShowMissingScripts.Value) return;
        }
        // Check CVR Game logs
        else if (LogParser.TryParseGame(ref message, ref type)) {
            if (!ModConfig.MeShowCVRInfo.Value && type == LogType.Log) return;
            if (!ModConfig.MeShowCVRWarning.Value && type == LogType.Warning) return;
            if (!ModConfig.MeShowCVRError.Value && type is LogType.Error) return;
        }
        // Check AV Pro logs
        else if (LogParser.IsAvPro(message)) {
            if (!ModConfig.MeShowAvPro.Value) return;
        }
        // Check Stack Traces
        else if (isStackTrace) {
            if (!ModConfig.MeShowStackTraces.Value) return;
        }
        // Check Unknown logs
        else {
            if (!ModConfig.MeShowUnknownInfo.Value) return;
            if (!ModConfig.MeShowUnknownWarning.Value) return;
            if (!ModConfig.MeShowUnknownError.Value) return;
        }

        switch (type) {
            case LogType.Log:
                MelonLogger.Msg(message);
                break;
            case LogType.Warning:
                MelonLogger.Warning(message);
                break;
            case LogType.Error:
                MelonLogger.Error(message);
                break;
            case LogType.Assert:
                MelonLogger.Error(message);
                break;
            case LogType.Exception:
                MelonLogger.Error(message);
                break;
            default:
                MelonLogger.Msg(message);
                break;
        }
    }

    private static class RateLimitedLogger {

        private static readonly ConcurrentDictionary<string, DateTime> MessageTimestamps = new();
        private static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(5);

        private static DateTime _lastCleanup = DateTime.UtcNow;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
        private static readonly object CleanupLock = new();

        public static void Log(string message, LogType type, bool isStacktrace, Exception exception) {
            var now = DateTime.UtcNow;
            var lastLogged = MessageTimestamps.GetOrAdd(message, now);
            if (now - lastLogged < RateLimit) {
                // Update with the most recent attempt
                MessageTimestamps.TryUpdate(message, now, lastLogged);
                return;
            }
            MessageTimestamps[message] = now;
            ActualLogMessage(type, message, isStacktrace, exception);

            // Cleanup the cache
            lock (CleanupLock) {
                if (now - _lastCleanup > CleanupInterval) {
                    var keysToRemove = (from kvp in MessageTimestamps where now - kvp.Value > RateLimit select kvp.Key).ToList();
                    foreach (var key in keysToRemove) {
                        MessageTimestamps.TryRemove(key, out _);
                    }
                    _lastCleanup = now;
                }
            }
        }
    }

    private static void LogMessageReceived(string condition, string stackTrace, LogType type) {
        try {
            RateLimitedLogger.Log($"{condition}{(stackTrace.IsNullOrEmpty() ? "" : $"\n{stackTrace}")}", type, false, null);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
        }
    }

    private static void InternalLog(LogType level, LogOption options, string msg, UnityEngine.Object obj) {
        try {
            RateLimitedLogger.Log($"{msg}{(obj == null || obj.ToString().IsNullOrEmpty() ? "" : $"\n{obj}")}", level, false, null);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
        }
    }

    private static void InternalLogException(Exception exception, UnityEngine.Object obj) {
        try {
            RateLimitedLogger.Log($"{exception}{(obj == null || obj.ToString().IsNullOrEmpty() ? "" : $"\n{obj}")}", LogType.Exception, true, exception);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
        }
    }
}
