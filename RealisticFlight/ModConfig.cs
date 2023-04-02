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

    internal static MelonPreferences_Entry<bool> MeFlapToFly;

    internal static MelonPreferences_Entry<bool> MeOverrideMaxAppliedVelocity;
    internal static MelonPreferences_Entry<float> MeMaxAppliedVelocity;

    internal static MelonPreferences_Entry<bool> MeOverrideAppliedVelocityFriction;
    internal static MelonPreferences_Entry<float> MeAppliedVelocityFriction;

    internal static MelonPreferences_Entry<float> MeGroundedMultiplier;

    internal static MelonPreferences_Entry<float> MePreClampVelocityMultiplier;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(RealisticFlight));

        MeFlapToFly = _melonCategory.CreateEntry("FlapToFly", true,
            description: "Whether to enable the ability to Fly by flapping your arms (world requires flight allowed).");

        MeOverrideMaxAppliedVelocity = _melonCategory.CreateEntry("OverrideMaxAppliedVelocity", true,
            description: "Whether to override the max applied velocity value or not.");
        MeMaxAppliedVelocity = _melonCategory.CreateEntry("MaxAppliedVelocity", 10000f,
            description: "Maximum value of the magnitude of the velocity a player can have. Game defaults to 10000.");

        MeOverrideAppliedVelocityFriction = _melonCategory.CreateEntry("OverrideAppliedVelocityFriction", true,
            description: "Whether to override the applied velocity friction value or not.");
        MeAppliedVelocityFriction = _melonCategory.CreateEntry("AppliedVelocityFriction", 0.9f,
            description: "Value for the applied velocity friction, the lower this value the faster the velocity will decay. Game defaults to 1.");

        MeGroundedMultiplier = _melonCategory.CreateEntry("GroundedMultiplier", 0.2f,
            description: "Value that will be multiplied to the Applied Velocity Friction to the Axis X and Z when you're grounded. " +
                         "Use this to make the character have more friction when grounded. Game defaults to 1");

        MePreClampVelocityMultiplier = _melonCategory.CreateEntry("PreClampVelocityMultiplier", 0.01f,
            description: "Value to be multiplied with the velocity before clamping the velocity magnitude to MaxAppliedVelocity." +
                         "By having this we allow higher values of velocity to linearly impact the velocity. Defaults to 1.");

    }

    public static void InitializeBTKUI() {
        // BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }
    //
    // private static void SetupBTKUI(CVR_MenuManager manager) {
    //     BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
    //
    //     // BTKUILib.QuickMenuAPI.PrepareIcon(nameof(RealisticFlight), "InstancesIcon",
    //     //     Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));
    //
    //
    //     var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
    //     var miscCategory = miscPage.AddCategory(nameof(RealisticFlight));
    //
    //     var pinButtonBTKUI = miscCategory.AddButton("Pin To Quick Menu", "",
    //         "Pins the Menu back to quick menu. Useful if you lost your menu :)");
    //
    //     var hideMenuToggle = miscCategory.AddToggle("Hide the Menu",
    //         "Whether to completely hide the CCK Debugger Menu or not.",
    //         MeIsHidden.Value);
    //
    //     hideMenuToggle.OnValueUpdated += b => {
    //         if (b != MeIsHidden.Value) MeIsHidden.Value = b;
    //     };
    //
    //     MeIsHidden.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
    //         if (newValue != hideMenuToggle.ToggleValue) hideMenuToggle.ToggleValue = newValue;
    //     });
    //
    //
    //
    //     var page = new BTKUILib.UIObjects.Page(nameof(RealisticFlight), nameof(RealisticFlight), true, "") {
    //         MenuTitle = nameof(RealisticFlight),
    //         MenuSubtitle = "Rejoin previous instances",
    //     };
    //
    //
    //
    //     var categoryInstances = page.AddCategory("Recent Instances");
    //     SetupInstancesButtons(categoryInstances);
    //     RealisticFlight.InstancesConfigChanged += () => SetupInstancesButtons(categoryInstances);
    //
    //     var categorySettings = page.AddCategory("Settings");
    //
    //     var joinLastInstanceButton = categorySettings.AddToggle("Join last instance after Restart",
    //         "Should we attempt to join the last instance you were in upon restarting the game? This takes " +
    //         "priority over starting in an Online Home World.",
    //         MeRejoinLastInstanceOnGameRestart.Value);
    //     joinLastInstanceButton.OnValueUpdated += b => {
    //         if (b == MeRejoinLastInstanceOnGameRestart.Value) return;
    //         MeRejoinLastInstanceOnGameRestart.Value = b;
    //     };
    //     MeRejoinLastInstanceOnGameRestart.OnEntryValueChanged.Subscribe((_, newValue) => {
    //         if (joinLastInstanceButton.ToggleValue == newValue) return;
    //         joinLastInstanceButton.ToggleValue = newValue;
    //     });
    //
    //     var joinInitialOnline = categorySettings.AddToggle("Start on Online Home World",
    //         "Should we create an online instance of your Home World when starting the game? Joining last " +
    //         "instance takes priority if active.",
    //         MeStartInAnOnlineInstance.Value);
    //     joinInitialOnline.OnValueUpdated += b => {
    //         if (b == MeStartInAnOnlineInstance.Value) return;
    //         MeStartInAnOnlineInstance.Value = b;
    //     };
    //     MeStartInAnOnlineInstance.OnEntryValueChanged.Subscribe((_, newValue) => {
    //         if (joinInitialOnline.ToggleValue == newValue) return;
    //         joinInitialOnline.ToggleValue = newValue;
    //     });
    // }
}
