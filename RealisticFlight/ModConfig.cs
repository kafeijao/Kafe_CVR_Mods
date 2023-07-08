using System.Collections;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace Kafe.RealisticFlight;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeCustomFlightInVR;
    internal static MelonPreferences_Entry<bool> MeCustomFlightInDesktop;

    internal static MelonPreferences_Entry<bool> MeOverrideMaxAppliedVelocity;
    internal static MelonPreferences_Entry<float> MeMaxAppliedVelocity;

    internal static MelonPreferences_Entry<bool> MeOverrideAppliedVelocityFriction;
    internal static MelonPreferences_Entry<float> MeAppliedVelocityFriction;

    internal static MelonPreferences_Entry<float> MeGroundedMultiplier;

    internal static MelonPreferences_Entry<float> MePreClampVelocityMultiplier;

    internal static MelonPreferences_Entry<float> MeFlapMultiplier;
    internal static MelonPreferences_Entry<float> MeFlapMultiplierHorizontal;

    internal static MelonPreferences_Entry<bool> MeGlidingRotationLikeAirfoils;
    internal static MelonPreferences_Entry<float> MeRotationAirfoilsSensitivity;

    internal static MelonPreferences_Entry<bool> MeUseAvatarOverrides;

    internal static MelonPreferences_Entry<bool> MeBothArmsDownToStoGliding;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(RealisticFlight));

        MeCustomFlightInVR = _melonCategory.CreateEntry("CustomFlightInVR", true,
            description: "Whether to enable the ability to use the custom flight in VR or not (world requires flight allowed).");

        MeCustomFlightInDesktop = _melonCategory.CreateEntry("CustomFlightInDesktop", true,
            description: "Whether to enable the ability to use the custom flight in Desktop or not (world requires flight allowed).");

        MeOverrideMaxAppliedVelocity = _melonCategory.CreateEntry("OverrideMaxAppliedVelocity", true,
            description: "Whether to override the max applied velocity value or not.");

        MeFlapMultiplier = _melonCategory.CreateEntry("FlapMultiplier", 1.0f,
            description: "Intensity of the Flap, 1 should be the default, higher values will yield stronger flaps and vice-versa.");

        MeFlapMultiplierHorizontal = _melonCategory.CreateEntry("FlapMultiplierHorizontal", 2.0f,
            description: "Intensity of the Flap Horizontally, 2 should be the default, higher values will yield stronger flings forward/sides and vice-versa.");

        MeGlidingRotationLikeAirfoils = _melonCategory.CreateEntry("GlidingRotationLikeAirfoils", false,
            description: "Whether rotate by rotating your wrists like airfoils or not.");

        MeRotationAirfoilsSensitivity = _melonCategory.CreateEntry("RotationAirfoilsSensitivity", 1f,
            description: "Rotation sensitivity of the Airfoils, 1 should be the default.");

        MeBothArmsDownToStoGliding = _melonCategory.CreateEntry("BothArmsDownToStoGliding", false,
            description: "Whether you need to put both arms down to stop flying or not. Might make it harder to unintentional gliding disable.");

        // Max Applied Velocity Settings
        MeMaxAppliedVelocity = _melonCategory.CreateEntry("MaxAppliedVelocity", 10000f,
            description: "Maximum value of the magnitude of the velocity a player can have. Game defaults to 10000.",
            is_hidden: true);
        MeOverrideAppliedVelocityFriction = _melonCategory.CreateEntry("OverrideAppliedVelocityFriction", true,
            description: "Whether to override the applied velocity friction value or not.",
            is_hidden: true);
        MeAppliedVelocityFriction = _melonCategory.CreateEntry("AppliedVelocityFriction", 0.9f,
            description: "Value for the applied velocity friction, the lower this value the faster the velocity will decay. Game defaults to 1.",
            is_hidden: true);
        MeGroundedMultiplier = _melonCategory.CreateEntry("GroundedMultiplier", 0.2f,
            description: "Value that will be multiplied to the Applied Velocity Friction to the Axis X and Z when you're grounded. " +
                         "Use this to make the character have more friction when grounded. Game defaults to 1",
            is_hidden: true);
        MePreClampVelocityMultiplier = _melonCategory.CreateEntry("PreClampVelocityMultiplier", 0.01f,
            description: "Value to be multiplied with the velocity before clamping the velocity magnitude to MaxAppliedVelocity." +
                         "By having this we allow higher values of velocity to linearly impact the velocity. Defaults to 1.",
            is_hidden: true);

        // BTKUI menu settings
        MeUseAvatarOverrides = _melonCategory.CreateEntry("UseAvatarOverrides", false,
            description: "Whether to have setting overrides for specific avatars.",
            is_hidden: true);
    }
}
