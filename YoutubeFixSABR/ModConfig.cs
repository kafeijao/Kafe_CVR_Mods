using MelonLoader;

namespace Kafe.YoutubeFixSABR;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(YoutubeFixSABR));
    }

    public static void InitializeUILibMenu()
    {

    }
}
