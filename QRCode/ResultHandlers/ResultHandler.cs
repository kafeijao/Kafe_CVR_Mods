using MelonLoader;
using UnityEngine;

namespace Kafe.QRCode.ResultHandlers;

public abstract class ResultHandler {
    public class Result {
        internal readonly string Type;
        internal readonly Sprite Sprite;
        internal readonly string Message;
        internal readonly Action Handler;

        public Result(string type, Sprite sprite, string message, Action handler) {
            Type = type;
            Sprite = sprite;
            Message = message;
            Handler = handler;
        }
    }

    private static readonly List<ResultHandler> Handlers = new ();

    internal static void RegisterHandler(ResultHandler handler) {
        Handlers.Add(handler);
    }

    protected abstract bool HandleResult(string text, out Result result);

    public static void ProcessText(string text) {
        foreach (var handler in Handlers) {
            if (handler.HandleResult(text, out var result)) {
                QRCodeBehavior.BarcodeParsedResults.Enqueue(result);
                return;
            }
        }
        MelonLogger.Msg($"Failed to find a handler, this should never happen... Text:\n{text}.");
    }
}
