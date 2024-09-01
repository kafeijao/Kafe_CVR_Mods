using System.Reflection;
using MelonLoader;

namespace MenuCSSLoader;

public static class ModConfig {

    private static readonly string ModDataFolder = Path.GetFullPath(Path.Combine("UserData", nameof(MenuCSSLoader)));

    private const string MenuJsPatches = "MenuCSSLoader.MenuPatches.js";
    internal static string MenuJsPatchesContent;

    private const string MainMenuCSSFolder = "MainMenu";
    private const string QuickMenuCSSFolder = "QuickMenu";

    public static void LoadAssemblyResources(Assembly assembly) {

        try {
            // Load the Menu js patches
            using var resourceStream = assembly.GetManifestResourceStream(MenuJsPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {MenuJsPatches}!");
                return;
            }
            using var streamReader = new StreamReader(resourceStream);
            MenuJsPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the assembly resource");
            MelonLogger.Error(ex);
        }

    }

    public static void LoadCSSFilePaths()
    {
        try {
            // Clear current css file paths
            MenuCSSLoader.MainMenuCSSFilePaths.Clear();
            MenuCSSLoader.QuickMenuCSSFilePaths.Clear();

            // Create MainMenu directory if it doesn't exist
            var mainMenuCSSFolderPath = Path.GetFullPath(Path.Combine(ModDataFolder, MainMenuCSSFolder));
            if (!Directory.Exists(mainMenuCSSFolderPath))
            {
                Directory.CreateDirectory(mainMenuCSSFolderPath);
                MelonLogger.Msg($"Created directory: {mainMenuCSSFolderPath}");
            }

            // Create QuickMenu directory if it doesn't exist
            var quickMenuCSSFolderPath = Path.GetFullPath(Path.Combine(ModDataFolder, QuickMenuCSSFolder));
            if (!Directory.Exists(quickMenuCSSFolderPath))
            {
                Directory.CreateDirectory(quickMenuCSSFolderPath);
                MelonLogger.Msg($"Created directory: {quickMenuCSSFolderPath}");
            }

            // Load all CSS files in the main menu folder
            var mainMenuCSSFiles = Directory.GetFiles(mainMenuCSSFolderPath, "*.css");
            foreach (var cssFile in mainMenuCSSFiles)
            {
                MenuCSSLoader.MainMenuCSSFilePaths.Add(cssFile);
                MelonLogger.Msg($"Loaded {MainMenuCSSFolder} CSS file: {cssFile}");
            }

            // Load all CSS files in the quick menu folder
            var quickMenuCSSFiles = Directory.GetFiles(quickMenuCSSFolderPath, "*.css");
            foreach (var cssFile in quickMenuCSSFiles)
            {
                MenuCSSLoader.QuickMenuCSSFilePaths.Add(cssFile);
                MelonLogger.Msg($"Loaded {QuickMenuCSSFolder} CSS file: {cssFile}");
            }

            MelonLogger.Msg("Loaded the Menu CSS file paths to use.");
        }
        catch (Exception ex) {
            MelonLogger.Error($"Failed to Load the CSS files from the folders in: {ModDataFolder}");
            MelonLogger.Error(ex);
        }
    }
}
