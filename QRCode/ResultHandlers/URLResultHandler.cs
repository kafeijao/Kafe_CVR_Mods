using System.Diagnostics;

namespace Kafe.QRCode.ResultHandlers;

public class URLResultHandler : ResultHandler {

    private const string Name = "Url";

    protected override bool HandleResult(string text, out Result result) {
        result = null;

        // Ignore non-http(s) urls
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uriResult) || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)) return false;
        result = new Result(Name, ModConfig.ImageSprites[ModConfig.ImageType.Url], text, () => Process.Start("explorer", uriResult.AbsoluteUri));
        return true;
    }
}
