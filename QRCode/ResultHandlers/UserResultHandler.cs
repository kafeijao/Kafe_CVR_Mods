using ABI_RC.Core.InteractionSystem;

namespace Kafe.QRCode.ResultHandlers;

public class UserResultHandler : ResultHandler {

    private const string Name = "CVR Player Details";
    private const string Prefix = "user:";

    protected override bool HandleResult(string text, out Result result) {
        result = null;

        var processedText = text.Trim().ToLower();
        if (!processedText.StartsWith(Prefix)) return false;
        if (!Guid.TryParse(processedText[Prefix.Length..].Trim(), out var guid)) return false;
        result = new Result(Name, ModConfig.ImageSprites[ModConfig.ImageType.User], text, () => ViewManager.Instance.RequestUserDetailsPage(guid.ToString("D")));
        return true;
    }
}
