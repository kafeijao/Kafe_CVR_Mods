using System.IO.Compression;
using MelonLoader;

namespace Kafe.YoutubeFixSABR;

public static class DenoDownloader
{
    private const string Target = "x86_64-pc-windows-msvc";


    public static async Task EnsureDenoAsync()
    {
        if (File.Exists(YoutubeFixSABR.DenoExePath))
        {
            MelonLogger.Msg($"deno.exe already exists on: {YoutubeFixSABR.DenoExePath}");
            return;
        }

        Directory.CreateDirectory(YoutubeFixSABR.UserDataFolderPath);

        using var http = new HttpClient();

        // Get latest version string
        var version = (await http.GetStringAsync("https://dl.deno.land/release-latest.txt")).Trim();

        MelonLogger.Msg($"Found latest deno version: {version}");

        // Download zip
        var downloadUrl = $"https://dl.deno.land/release/{version}/deno-{Target}.zip";

        var zipPath = Path.Combine(YoutubeFixSABR.UserDataFolderPath, "deno.zip");

        MelonLogger.Msg($"Downloading {downloadUrl} to {zipPath}");

        await using (var zipStream = await http.GetStreamAsync(downloadUrl))
        await using (var fileStream = File.Create(zipPath))
            await zipStream.CopyToAsync(fileStream);

        MelonLogger.Msg($"Looking for deno.exe in the {zipPath} to extract it...");

        // Extract ONLY deno.exe
        bool foundDeno = false;
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.Name, "deno.exe", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(YoutubeFixSABR.DenoExePath, overwrite: true);
                    foundDeno = true;
                    break;
                }
            }
        }

        if (foundDeno)
            MelonLogger.Msg($"Downloaded deno.exe into: {YoutubeFixSABR.DenoExePath}");
        else
            MelonLogger.Error("Failed to find deno.exe in the downloaded zip :(");

        File.Delete(zipPath);
    }
}
