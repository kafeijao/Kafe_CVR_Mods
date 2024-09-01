using MelonLoader;
using UnityEngine;

namespace MenuCSSLoader;

public class MenuCSSLoader : MelonMod
{

    public static readonly HashSet<string> MainMenuCSSFilePaths = new HashSet<string>();
    public static readonly HashSet<string> QuickMenuCSSFilePaths = new HashSet<string>();

    public override void OnInitializeMelon() {

        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        ModConfig.LoadCSSFilePaths();

        CohtmlPatches.MainMenuInitialized += OnMMInitialized;
        CohtmlPatches.QuickMenuInitialized += OnQMInitialized;
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F5) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            MelonLogger.Msg("Detected Shift + F5 press, reloading the css file paths...");
            ModConfig.LoadCSSFilePaths();
        }
    }

    private static void OnMMInitialized()
    {
        MelonLogger.Msg($"MainMenu has initialized, loading {MainMenuCSSFilePaths.Count} css files");
        foreach (string cssFilePath in MainMenuCSSFilePaths)
            CohtmlPatches.LoadCSSFileMM(cssFilePath);
    }

    private static void OnQMInitialized()
    {
        MelonLogger.Msg($"QuickMenu has initialized, loading {QuickMenuCSSFilePaths.Count} css files");
        foreach (string cssFilePath in QuickMenuCSSFilePaths)
            CohtmlPatches.LoadCSSFileQM(cssFilePath);
    }

}
