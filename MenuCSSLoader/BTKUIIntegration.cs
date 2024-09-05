using System.Reflection;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using BTKUILib.UIObjects.Objects;
using MelonLoader;

namespace MenuCSSLoader
{
    internal static class BTKUIIntegration
    {
        private static readonly string ModName = "MenuCSSLoader";
        private static readonly string ModDataFolder = Path.GetFullPath(Path.Combine("UserData", nameof(MenuCSSLoader)));
        private static MelonPreferences_Entry<string> CurentTheme = MelonPreferences.GetEntry<string>(ModName, "CurrentTheme");

        private static List<string> AvailableThemes = new List<string>();
        //private static MultiSelection ThemeSelector;

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

        public static void OpenThemeSelector()
        {
            AvailableThemes.Clear();
            foreach (string themePath in Directory.GetDirectories(ModDataFolder))
            {
                AvailableThemes.Add(themePath.Split("\\").Last());
            }
            MultiSelection ThemeSelector = new MultiSelection("Select a theme.",
                                               AvailableThemes.ToArray<string>(),
                                               AvailableThemes.FindIndex(a => a.Equals(CurentTheme.Value)));
            ThemeSelector.OnOptionUpdated += SelectTheme;
            QuickMenuAPI.OpenMultiSelect(ThemeSelector);
        }

        public static void SelectTheme(int themeIndex)
        {
            CurentTheme.Value = AvailableThemes[themeIndex];
            ModConfig.LoadCSSFilePaths();
            QuickMenuAPI.ShowConfirm("Reload UI?", "Would you like to reload the UI now?", MenuCSSLoader.ReloadUI);
        }
    }
}
