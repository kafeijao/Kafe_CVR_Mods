using MelonLoader;

namespace Kafe.CVRSuperMario64;

public static class Config {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeDisableAudio;
    internal static MelonPreferences_Entry<float> MeAudioPitch;
    internal static MelonPreferences_Entry<float> MeAudioVolume;
    internal static MelonPreferences_Entry<int> MeGameTickMs;
    internal static MelonPreferences_Entry<bool> MePlayRandomMusicOnMarioJoin;
    internal static MelonPreferences_Entry<float> MeSkipFarMarioDistance;
    internal static MelonPreferences_Entry<int> MeMaxMariosAnimatedPerPerson;
    internal static MelonPreferences_Entry<int> MeMaxMeshColliderTotalTris;
    internal static MelonPreferences_Entry<bool> MeDeleteMarioAfterDead;

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(nameof(CVRSuperMario64));

        MeDisableAudio = _melonCategory.CreateEntry("DisableAudio", false,
            description: "Whether to disable the game audio or not.");

        MeAudioVolume = _melonCategory.CreateEntry("AudioVolume", 0.1f,
            description: "The audio volume.");

        MeAudioPitch = _melonCategory.CreateEntry("AudioPitch", 0.74f,
            description: "The audio pitch of the game sounds.");

        MeGameTickMs = _melonCategory.CreateEntry("GameTickMs", 25,
            description: "The game ticks frequency in Milliseconds.");

        MePlayRandomMusicOnMarioJoin = _melonCategory.CreateEntry("PlayRandomMusicOnMarioJoin", true,
            description: "Whether to play a random music when a mario joins or not.");
        MePlayRandomMusicOnMarioJoin.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!newValue) Interop.StopMusic();
        });

        MeMaxMariosAnimatedPerPerson = _melonCategory.CreateEntry("MaxMariosAnimatedPerPerson", 1,
            description: "The max number of marios other people can control at the same time.");

        MeSkipFarMarioDistance = _melonCategory.CreateEntry("SkipFarMarioDistance", 5f,
            description: "The max distance that we're going to calculate the mario animations for other people.");

        MeMaxMeshColliderTotalTris = _melonCategory.CreateEntry("MaxMeshColliderTotalTris", 50000,
            description: "The max total number of collision tris loaded from automatically generated static mesh colliders.");

        MeDeleteMarioAfterDead = _melonCategory.CreateEntry("DeleteMarioAfterDead", true,
            description: "Whether to automatically delete our marios after 15 seconds of being dead or not.");
    }

}
