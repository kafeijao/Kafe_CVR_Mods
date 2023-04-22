using MelonLoader;

namespace Kafe.ChatBox;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<int> MeStringSize;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(ChatBox));

        MeStringSize = _melonCategory.CreateEntry("StringSizeBytes", 10,
            description: "How many Bytes should we send on the test.");

    }

}
