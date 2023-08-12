using ABI_RC.Core.InteractionSystem;

namespace Kafe.QRCode.ResultHandlers;

public class WorldResultHandler : ResultHandler {

    private const string Name = "CVR World";
    private const string Prefix = "world:";

    protected override bool HandleResult(string text, out Result result) {
        result = null;

        var processedText = text.Trim().ToLower();
        if (!processedText.StartsWith(Prefix)) return false;
        if (!Guid.TryParse(processedText[Prefix.Length..].Trim(), out var guid)) return false;
        result = new Result(Name, ModConfig.ImageSprites[ModConfig.ImageType.World], text, () => ViewManager.Instance.RequestWorldDetailsPage(guid.ToString("D")));
        return true;
    }
}
