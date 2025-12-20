using System.Reflection;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using ABI_RC.Systems.UI.UILib.UIObjects.Objects;
using MelonLoader;

namespace MenuCSSLoader
{
    internal static class BTKUIIntegration
    {
        private const string ModName = "MenuCSSLoader";
        private static readonly string ModDataFolder = Path.GetFullPath(Path.Combine("UserData", nameof(MenuCSSLoader)));

        private static readonly MelonPreferences_Entry<string> CurrentTheme = MelonPreferences.GetEntry<string>(ModName, "CurrentTheme");

        private static readonly List<string> AvailableThemes = new List<string>();

        public static void CreateIcons(Assembly assembly)
        {
            QuickMenuAPI.PrepareIcon(ModName, "MenuCSSIcon", assembly.GetManifestResourceStream("MenuCSSLoader.Pallete.png"));
            QuickMenuAPI.PrepareIcon(ModName, "MenuCSS.ReloadIcon", assembly.GetManifestResourceStream("MenuCSSLoader.Reload.png"));

        }
        public static void CreateCategory()
        {
            Category obligatory = QuickMenuAPI.MiscTabPage.AddCategory("MenuCSSLoader", ModName);
            Button selector = obligatory.AddButton("Select Theme", "MenuCSSIcon", "Select a theme.");
            selector.OnPress += OpenThemeSelector;
            Button reloadUI = obligatory.AddButton("Reload UI", "MenuCSS.ReloadIcon", "Close Menus and reload UI.");
            reloadUI.OnPress += MenuCSSLoader.ReloadUI;
            
        }

        private static void OpenThemeSelector()
        {
            AvailableThemes.Clear();
            foreach (string themePath in Directory.GetDirectories(ModDataFolder))
            {
                AvailableThemes.Add(themePath.Split("\\").Last());
            }
            MultiSelection themeSelector = new MultiSelection("Select a theme.",
                                               AvailableThemes.ToArray<string>(),
                                               AvailableThemes.FindIndex(a => a.Equals(CurrentTheme.Value)));
            themeSelector.OnOptionUpdated += SelectTheme;
            QuickMenuAPI.OpenMultiSelect(themeSelector);
        }

        private static void SelectTheme(int themeIndex)
        {
            CurrentTheme.Value = AvailableThemes[themeIndex];
            ModConfig.LoadCSSFilePaths();
            QuickMenuAPI.ShowConfirm("Reload UI?", "Would you like to reload the UI now?", MenuCSSLoader.ReloadUI);
        }
    }
}
