using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.UI.UIRework.Managers;
using ABI_RC.Systems.InputManagement;
using MelonLoader;
using UnityEngine;

namespace MenuCSSLoader;

public class MenuCSSLoader : MelonMod
{

    public static readonly HashSet<string> MainMenuCSSFilePaths = new HashSet<string>();
    public static readonly HashSet<string> QuickMenuCSSFilePaths = new HashSet<string>();
    public static readonly HashSet<string> KeyboardViewCSSFilePaths = new HashSet<string>();

    private MelonPreferences_Category _menuCSSLoaderPreferenceCategory;
    private MelonPreferences_Entry<string> _currentTheme;

    public override void OnInitializeMelon() {

        _menuCSSLoaderPreferenceCategory = MelonPreferences.CreateCategory("MenuCSSLoader");
        _currentTheme = MelonPreferences.CreateEntry("MenuCSSLoader", "CurrentTheme", "Default", "Current Theme", "The theme to use. Press shift+F5 to reload.");

        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);
        BTKUIIntegration.CreateIcons(MelonAssembly.Assembly);

        ModConfig.LoadCSSFilePaths();
        BTKUIIntegration.CreateCategory();

        CohtmlPatches.MainMenuInitialized += OnMMInitialized;
        CohtmlPatches.QuickMenuInitialized += OnQMInitialized;
        CohtmlPatches.KeyboardViewInitialized += OnKeyboardInitialized;
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F5) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            MelonLogger.Msg("Detected Shift + F5 press, reloading the css file paths...");
            ModConfig.LoadCSSFilePaths();
        }
    }

    public static void ReloadUI()
    {
        if (ViewManager.Instance.IsViewShown)
            ViewManager.Instance.UiStateToggle(false);
        if (CVR_MenuManager.Instance.IsViewShown)
            CVR_MenuManager.Instance.ToggleQuickMenu(false);
        if (KeyboardManager.Instance.IsViewShown)
            KeyboardManager.Instance.ToggleView(false);
        CVRInputManager.Instance.reload = true;
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

    private static void OnKeyboardInitialized()
    {
        MelonLogger.Msg($"KeyboardView has initialized, loading {KeyboardViewCSSFilePaths.Count} css files");
        foreach (string cssFilePath in KeyboardViewCSSFilePaths)
            CohtmlPatches.LoadCSSFileKeyboard(cssFilePath);
    }

}
