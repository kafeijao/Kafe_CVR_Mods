using MelonLoader;

namespace Kafe.RealisticFlight;

public static class ModConfig
{

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeCustomFlightInVR;
    internal static MelonPreferences_Entry<bool> MeCustomFlightInDesktop;

    internal static MelonPreferences_Entry<float> MeFlapMultiplier;
    internal static MelonPreferences_Entry<float> MeFlapMultiplierHorizontal;

    internal static MelonPreferences_Entry<bool> MeGlidingRotationLikeAirfoils;
    internal static MelonPreferences_Entry<float> MeRotationAirfoilsSensitivity;

    internal static MelonPreferences_Entry<bool> MeUseAvatarOverrides;

    internal static MelonPreferences_Entry<bool> MeBothArmsDownToStopGliding;
    internal static MelonPreferences_Entry<bool> MeBothArmsUpToStopGliding;

    public static void InitializeMelonPrefs()
    {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(RealisticFlight));

        MeCustomFlightInVR = _melonCategory.CreateEntry("CustomFlightInVR", true,
            description: "Whether to enable the ability to use the custom flight in VR or not (world requires flight allowed).");

        MeCustomFlightInDesktop = _melonCategory.CreateEntry("CustomFlightInDesktop", true,
            description: "Whether to enable the ability to use the custom flight in Desktop or not (world requires flight allowed).");

        MeFlapMultiplier = _melonCategory.CreateEntry("FlapMultiplier", 1.0f,
            description: "Intensity of the Flap, 1 should be the default, higher values will yield stronger flaps and vice-versa.");

        MeFlapMultiplierHorizontal = _melonCategory.CreateEntry("FlapMultiplierHorizontal", 2.0f,
            description: "Intensity of the Flap Horizontally, 2 should be the default, higher values will yield stronger flings forward/sides and vice-versa.");

        MeGlidingRotationLikeAirfoils = _melonCategory.CreateEntry("GlidingRotationLikeAirfoils", false,
            description: "Whether rotate by rotating your wrists like airfoils or not.");

        MeRotationAirfoilsSensitivity = _melonCategory.CreateEntry("RotationAirfoilsSensitivity", 1f,
            description: "Rotation sensitivity of the Airfoils, 1 should be the default.");

        MeBothArmsDownToStopGliding = _melonCategory.CreateEntry("BothArmsDownToStopGliding", false,
            description: "Whether you need to put both arms down to stop flying or not. Might make it harder to unintentional gliding disable.");

        MeBothArmsUpToStopGliding = _melonCategory.CreateEntry("BothArmsUpToStopGliding", false,
            description: "Whether you need to put both arms up to stop flying or not. Might make it harder to unintentional gliding disable.");

        // UI menu settings
        MeUseAvatarOverrides = _melonCategory.CreateEntry("UseAvatarOverrides", false,
            description: "Whether to have setting overrides for specific avatars.",
            is_hidden: true);
    }
}
