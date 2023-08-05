using MelonLoader;

namespace Kafe.QRCode.ResultHandlers;

public class DefaultResultHandler : ResultHandler {

    private const string Name = "Text";

    protected override bool HandleResult(string text, out Result result) {
        result = null;

        if (string.IsNullOrWhiteSpace(text)) return false;
        result = new Result(Name, ModConfig.ImageSprites[ModConfig.ImageType.Clipboard], text, () => {
            MelonLogger.Msg($"Copied the scanned content to the Clipboard. Content:\n {text}");
            TextCopy.ClipboardService.SetText(text);
        });
        return true;
    }
}
