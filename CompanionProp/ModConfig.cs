using MelonLoader;

namespace Kafe.CompanionProp;

public static class ModConfig {

    private static MelonPreferences_Category _melonCategory;

    internal static MelonPreferences_Entry<bool> MeEnabled;
    internal static MelonPreferences_Entry<string> MePropGuid;

    internal static MelonPreferences_Entry<bool> SpawnPropOnJoin;
    internal static MelonPreferences_Entry<int> MeSpawnDelay;

    internal static MelonPreferences_Entry<bool> MePreventRemoveAllProps;
    internal static MelonPreferences_Entry<bool> MePreventRemoveAllMyProps;

    internal static MelonPreferences_Entry<bool> MeSendHudSpawnNotification;
    internal static MelonPreferences_Entry<bool> MeSendHudSpawnNotAllowedNotification;

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(nameof(CompanionProp));

        MeEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether all the mod functionality should be enabled or not.");

        MePropGuid = _melonCategory.CreateEntry("PropGuid", "",
            description: "The guid of the prop we're settings as our companion.");
        MePropGuid.OnEntryValueChanged.Subscribe((_, newValue) => {
            CompanionProp.CheckGuid(newValue, true);
        });


        SpawnPropOnJoin = _melonCategory.CreateEntry("SpawnPropOnJoin", true,
            description: "Whether to spawn the prop on joining an instance that allows props or not.");

        MeSpawnDelay = _melonCategory.CreateEntry("SpawnDelay", 5,
            description: "Time in seconds to spawn the prop after joining the instance.");


        MePreventRemoveAllProps = _melonCategory.CreateEntry("PreventRemoveAllProps", true,
            description: "Whether to prevent removing our prop when pressing the remove all props button.");

        MePreventRemoveAllMyProps = _melonCategory.CreateEntry("PreventRemoveAllMyProps", true,
            description: "Whether to prevent removing our prop when pressed the remove my props button.");


        MeSendHudSpawnNotification = _melonCategory.CreateEntry("SendHudSpawnNotification", true,
            description: "Whether to send a notification to the hud when the prop is spawned or not.");

        MeSendHudSpawnNotAllowedNotification = _melonCategory.CreateEntry("SendHudSpawnNotAllowedNotification", false,
            description: "Whether to send a notification to the hud when the prop is was not allowed to be spawned.");
    }

}
