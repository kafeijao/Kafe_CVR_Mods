using MelonLoader;

namespace Kafe.MovementParentFlight;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeEnabled;
    internal static MelonPreferences_Entry<float> MeSpeedPerMetre;
    internal static MelonPreferences_Entry<float> MeMaxSpeed;
    internal static MelonPreferences_Entry<float> MeTurnPerDegree;
    internal static MelonPreferences_Entry<float> MeMaxTurn;

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(MovementParentFlight));

        MeEnabled = _melonCategory.CreateEntry("Enabled", true,
            description: "Whether standing on a movement parent you are holding allows you to fly.");

        MeSpeedPerMetre = _melonCategory.CreateEntry("SpeedPerMetre", 50f,
            description: "Metres per second gained per meter you push or tilt the movement parent.");

        MeMaxSpeed = _melonCategory.CreateEntry("MaxSpeed", 100f,
            description: "Fastest you can fly, in meters per second.");

        MeTurnPerDegree = _melonCategory.CreateEntry("TurnPerDegree", 8f,
            description: "Degrees per second gained per degree you twist the movement parent.");

        MeMaxTurn = _melonCategory.CreateEntry("MaxTurn", 720f,
            description: "Fastest you can turn, in degrees per second.");
    }
}
