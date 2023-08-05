using System.Text.RegularExpressions;
using ABI_RC.Core.InteractionSystem;

namespace Kafe.QRCode.ResultHandlers;

public class InstanceResultHandler : ResultHandler {

    private const string Name = "CVR Instance";
    private const string Prefix = "instance:i+";
    private static readonly Regex IdFormat = new Regex("^([0-9a-f]{16})-([0-9a-f]{6})-([0-9a-f]{6})-([0-9a-f]{8})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    protected override bool HandleResult(string text, out Result result) {
        result = null;

        var processedText = text.Trim().ToLower();
        if (!processedText.StartsWith(Prefix)) return false;

        var match = IdFormat.Match(processedText[Prefix.Length..]);
        if (!match.Success) return false;

        var id = match.Value;
        result = new Result(Name, ModConfig.ImageSprites[ModConfig.ImageType.Instance], text, () => ViewManager.Instance.RequestInstanceDetailsPage("i+" + id));
        return true;
    }

}
