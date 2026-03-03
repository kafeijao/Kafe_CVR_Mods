using ABI_RC.Core;
using ABI_RC.Systems.UI.UILib;
using MelonLoader;

namespace Kafe.Captions;

using System.Security.Cryptography;

public static class WhisperModelDownloader
{
    public const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/";
    private const string BaseUrlResolve = BaseUrl + "resolve/main";

    private static readonly HttpClient HttpClient = new HttpClient();

    private static bool _isDownloading;

    public sealed class WhisperModelInfo(string fileName, string sizeText, string sha1, bool isRecommended = false)
    {
        public readonly string FileName = fileName;
        public readonly string SizeText = sizeText;
        public readonly string Sha1 = sha1;
        public readonly bool IsRecommended = isRecommended;

        public override string ToString()
        {
            return $"{FileName} [{SizeText}]{(IsRecommended ? " *recommended*" : "")}";
        }
    }

    private static readonly Dictionary<string, WhisperModelInfo> Models =
        CreateModels();

    private static Dictionary<string, WhisperModelInfo> CreateModels()
    {
        var modelInfos = new[]
        {
            // Recommended (same speed as the smaller models, but waaaay better)
            new WhisperModelInfo("ggml-large-v3-turbo.bin", "1.5 GiB", "4af2b29d7ec73d781377bfd1758ca957a807e941", true),
            new WhisperModelInfo("ggml-large-v3-turbo-q5_0.bin", "547 MiB", "e050f7970618a659205450ad97eb95a18d69c9ee", true),

            new WhisperModelInfo("ggml-tiny.bin", "75 MiB", "bd577a113a864445d4c299885e0cb97d4ba92b5f"),
            new WhisperModelInfo("ggml-tiny.en.bin", "75 MiB", "c78c86eb1a8faa21b369bcd33207cc90d64ae9df"),

            new WhisperModelInfo("ggml-base.bin", "142 MiB", "465707469ff3a37a2b9b8d8f89f2f99de7299dac"),
            new WhisperModelInfo("ggml-base.en.bin", "142 MiB", "137c40403d78fd54d454da0f9bd998f78703390c"),

            new WhisperModelInfo("ggml-small.bin", "466 MiB", "55356645c2b361a969dfd0ef2c5a50d530afd8d5"),
            new WhisperModelInfo("ggml-small.en.bin", "466 MiB", "db8a495a91d927739e50b3fc1cc4c6b8f6c2d022"),

            new WhisperModelInfo("ggml-medium.bin", "1.5 GiB", "fd9727b6e1217c2f614f9b698455c4ffd82463b4"),
            new WhisperModelInfo("ggml-medium.en.bin", "1.5 GiB", "8c30f0e44ce9560643ebd10bbe50cd20eafd3723"),
        };

        return modelInfos.ToDictionary(m => m.ToString());
    }

    public static readonly string[] ModelOptions = Models.Keys.ToArray();

    public static WhisperModelInfo GetModelInfo(string key) => Models[key];

    public static string GetModelDownloadUrl(string modelFileName) => $"{BaseUrlResolve}/{modelFileName}";

    public static async Task DownloadModelAsync(string modelOptionKey, string destinationFolder)
    {
        try
        {
            if (_isDownloading)
            {
                _ = RootLogic.RunInMainThread(() =>
                {
                    MelonLogger.Warning("Attempted to download a model while a model download is already in progress.");
                    QuickMenuAPI.ShowAlertToast("Already downloading a model, please wait...");
                });
                return;
            }

            // Unavailable model name, how did you even get here?
            if (!Models.TryGetValue(modelOptionKey, out WhisperModelInfo? model))
            {
                MelonLogger.Error($"The model {modelOptionKey} is not in our available models, how did you even get here?");
                _ = RootLogic.RunInMainThread(() =>
                {
                    QuickMenuAPI.ShowNotice("Model not found",
                        $"The model {modelOptionKey} is not present in our available models.",
                        okText: "This is so sad :(");
                });
                return;
            }

            _isDownloading = true;

            string modelFileName = model.FileName;

            MelonLogger.Msg($"Started attempt of download the model {modelFileName} into {destinationFolder}");
            _ = RootLogic.RunInMainThread(() =>
            {
                QuickMenuAPI.ShowAlertToast("The download has started, please wait...");
            });

            Directory.CreateDirectory(destinationFolder);

            string outputPath = Path.Combine(destinationFolder, modelFileName);

            // Check if the file already exists
            if (File.Exists(outputPath))
            {
                var matchHash = VerifySha1(outputPath, model.Sha1);
                string msg = $"The model {modelFileName} is already present in the models folder at {outputPath}.";
                MelonLogger.Warning(msg);
                _ = RootLogic.RunInMainThread(() =>
                {
                    if (matchHash)
                    {
                        QuickMenuAPI.ShowNotice("Model already Exist",
                            $"{msg} The existing file is exactly the same as the one that would've been downloaded.",
                            okText: "Aight, thanks");
                    }
                    else
                    {
                        QuickMenuAPI.ShowConfirm("Model already Exist",
                            $"{msg} The existing file is different than the one we would've downloaded. Either you have a custom model or it is corrupted, do you want to keep or delete it?",
                            onYes: () => File.Delete(outputPath),
                            yesText: "Delete it",
                            noText: "Keep it");
                    }
                });
                return;
            }

            string url = GetModelDownloadUrl(model.FileName);

            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                string msg = $"The download the of {modelFileName} model has failed, Status Code: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                MelonLogger.Warning(msg);
                _ = RootLogic.RunInMainThread(() =>
                {
                    QuickMenuAPI.ShowNotice(
                        "Download has Failed",
                        msg,
                        okText: "Awesome, thanks!");
                });
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            await stream.CopyToAsync(fileStream);
            fileStream.Close(); // We need to manually close, so we can then check the sha from the file

            // Verify SHA1
            if (!VerifySha1(outputPath, model.Sha1))
            {
                string msg = $"SHA1 mismatch for {modelFileName}. The downloaded file at {outputPath} is corrupted or there was an update... Do you want to keep it?";
                MelonLogger.Error(msg);
                _ = RootLogic.RunInMainThread(() =>
                {
                    QuickMenuAPI.ShowConfirm(
                        "Download mismatch",
                        msg,
                        onYes: () => File.Delete(outputPath),
                        yesText: "No, Delete it!",
                        noText: "Yes, keep it!");
                });
                return;
            }

            string finishedMsg = $"The model {modelFileName} has been successfully downloaded onto: {outputPath}";
            MelonLogger.Msg(finishedMsg);
            _ = RootLogic.RunInMainThread(() =>
            {
                QuickMenuAPI.ShowNotice(
                    "Download finished",
                    finishedMsg,
                    okText: "Yay!");
            });
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Something went wrong in {nameof(DownloadModelAsync)}", e);
            _ = RootLogic.RunInMainThread(() =>
            {
                QuickMenuAPI.ShowNotice(
                    "Download Failed",
                    $"The {modelOptionKey} model download has failed, check the logs for more information. {e.Message}");
            });
        }
        finally
        {
            _isDownloading = false;
        }
    }

    private static bool VerifySha1(string filePath, string expectedSha1)
    {
        using var sha1 = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha1.ComputeHash(stream);
        string actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return string.Equals(actual, expectedSha1, StringComparison.OrdinalIgnoreCase);
    }
}
