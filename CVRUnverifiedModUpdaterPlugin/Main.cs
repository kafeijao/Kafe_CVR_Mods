using System.Net;
using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json.Linq;

namespace Kafe.CVRUnverifiedModUpdaterPlugin;

public class CVRUnverifiedModUpdaterPlugin : MelonPlugin {

    public override void OnApplicationEarlyStart() {

        ModConfig.InitializeJsonConfig();

        var repoCount = ModConfig.Config.RepoConfigs.Count;

        if (repoCount == 0) {
            MelonLogger.Msg($"There are no Repos in the config to check for updates. Skipping...");
            return;
        }

        if (repoCount > 30) {
            MelonLogger.Msg($"There are over 30 repos to check. Due the github api Limits we can't check that many repos. Skipping...");
            return;
        }

        if (ModConfig.Config.NextCheck > DateTime.UtcNow) {
            var timeLeft = ModConfig.Config.NextCheck - DateTime.UtcNow;
            MelonLogger.Msg($"Next update check in {(int) timeLeft.TotalMinutes} minutes... Skipping for now...");
            return;
        }

        // Check the repos and update if necessary
        foreach (var repo in ModConfig.Config.RepoConfigs) {
            DownloadLatestReleaseAssets(repo);
        }

        // Update the next update timout, let's make it 10 minutes per repo
        ModConfig.Config.NextCheck = DateTime.UtcNow.AddMinutes(repoCount >= 6 ? 60 : repoCount * 10);
        ModConfig.SaveJsonConfig();
    }

    private static void ClearSillyNames(string folderToCheck, string downloadedFilePath) {

        var assembly = AssemblyDefinition.ReadAssembly(downloadedFilePath);
        var attribute = assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");
        assembly.Dispose();
        if (attribute == null || attribute.ConstructorArguments.Count < 2 ||
            attribute.ConstructorArguments[1].Value is not string modName) {
            MelonLogger.Warning($"The mod at {downloadedFilePath} is not a MelonLoader mod... Skipping attempting to remove duplicates...");
            return;
        }

        // Get all dll files from the folder (not recursive)
        var dllFiles = Directory.GetFiles(folderToCheck, "*.dll");

        foreach (var dllFile in dllFiles) {
            // Skip the downloaded file
            if (dllFile.Equals(downloadedFilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            using var dllAssembly = AssemblyDefinition.ReadAssembly(dllFile);
            var dllAttribute = dllAssembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");

            // If it's not a melon loader mod or the name is different keep looking...
            if (dllAttribute == null ||
                dllAttribute.ConstructorArguments.Count <= 1 ||
                dllAttribute.ConstructorArguments[1].Value is not string dllModName ||
                dllModName != modName) continue;

            MelonLogger.Msg($"Found a duplicate mod at {dllFile}. Probably because it had a funny name and we downloaded with the correct name. Deleting it...");

            // If the mod names match, delete the file (we need to dispose the file first otherwise it's in use)
            dllAssembly.Dispose();
            File.Delete(dllFile);
        }
    }

    private static void DownloadLatestReleaseAssets(ModConfig.JsonConfigRepo repo) {

        var request = WebRequest.Create(repo.GetUrl()) as HttpWebRequest;
        request!.Method = "GET";
        request.UserAgent = $"CVRUnverifiedModUpdaterPlugin/{Properties.AssemblyInfoParams.Version}";

        try {
            using var response = request.GetResponse() as HttpWebResponse;
            using var reader = new StreamReader(response!.GetResponseStream()!);
            var json = reader.ReadToEnd();
            var release = JObject.Parse(json);

            foreach (var asset in release["assets"]) {
                var name = asset["name"].ToString();
                var updatedAt = asset["updated_at"].ToString();
                var downloadUrl = asset["browser_download_url"].ToString();

                var matchedFile = repo.Files.FirstOrDefault(f => f.Name == name);
                // Ignore if we don't have the asset name on our config, or if the updated at date is the same
                if (matchedFile == null) continue;
                if (matchedFile.UpdatedAt == updatedAt) {
                    MelonLogger.Msg($"{repo.Owner}/{repo.Repo}/{name} is already up to date!");
                    continue;
                }

                MelonLogger.Msg($"Found a newer version of {repo.Owner}/{repo.Repo}/{name}! Updating...");

                // Download the mod
                var destinationFilePath = matchedFile.GetDestinationFilePath(name);
                using (var client = new WebClient()) {
                    client.DownloadFile(downloadUrl, destinationFilePath);
                }

                // Delete current mods (this is to prevent when silly people have silly names like OSC (1).dll)
                ClearSillyNames(matchedFile.GetDestinationFolderPath(), destinationFilePath);

                matchedFile.UpdatedAt = updatedAt;
                ModConfig.SaveJsonConfig();
            }
        }
        catch (WebException e) {
            MelonLogger.Error($"Error fetching: {repo.GetUrl()}, make sure the git owner ({repo.Owner}) and repo ({repo.Repo}) names are properly set!");
            MelonLogger.Error(e);
            throw;
        }
    }
}
