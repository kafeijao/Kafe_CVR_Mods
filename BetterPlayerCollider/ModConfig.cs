using MelonLoader;

namespace Kafe.BetterPlayerCollider;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MePlaceColliderOnFeet;
    internal static MelonPreferences_Entry<bool> MePreventWallPushback;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterPlayerCollider));

        MePlaceColliderOnFeet = _melonCategory.CreateEntry("PlaceColliderOnFeet", true,
            description: "Whether to place the player collider on the feet or not.");

        MePreventWallPushback = _melonCategory.CreateEntry("PreventWallPushback", false,
            description: "Whether to ignore the wall pushbacks completely or not.");

    }

}
