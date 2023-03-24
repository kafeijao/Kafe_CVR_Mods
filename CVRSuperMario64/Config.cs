using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;

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

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += LoadBTKUILib;
    }

    private static void LoadBTKUILib(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= LoadBTKUILib;

        // Load the Mario Icon
        using (var stream = new MemoryStream(CVRSuperMario64.GetMarioSprite().texture.EncodeToPNG())) {
            BTKUILib.QuickMenuAPI.PrepareIcon(nameof(CVRSuperMario64), "MarioIcon", stream);
        }

        var page = new BTKUILib.UIObjects.Page(nameof(CVRSuperMario64), nameof(CVRSuperMario64), true, "MarioIcon") {
            MenuTitle = nameof(CVRSuperMario64),
            MenuSubtitle = "Configure CVR Super Mario 64 Mod",
        };

        var audioCategory = page.AddCategory("Audio");

        var disableAudio = audioCategory.AddToggle("Disable ALL Audio",
            "Whether to disable all Super Mario 64 Music/Sounds or not.",
            MeDisableAudio.Value);
        disableAudio.OnValueUpdated += b => {
            if (b != MeDisableAudio.Value) MeDisableAudio.Value = b;
        };
        MeDisableAudio.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) disableAudio.ToggleValue = newValue;
        });

        var playRandomMusicOnMarioStart = audioCategory.AddToggle("Play music on Mario Join",
            "Whether to play a random music on Mario Join or not.",
            MePlayRandomMusicOnMarioJoin.Value);
        playRandomMusicOnMarioStart.OnValueUpdated += b => {
            if (b != MePlayRandomMusicOnMarioJoin.Value) MePlayRandomMusicOnMarioJoin.Value = b;
        };
        MePlayRandomMusicOnMarioJoin.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) playRandomMusicOnMarioStart.ToggleValue = newValue;
        });

        var audioVolume = page.AddSlider(
            "Volume",
            "The volume for all the Super Mario 64 sounds.",
            MeAudioVolume.Value, 0f, 1f, 3);
        audioVolume.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeAudioVolume.Value)) MeAudioVolume.Value = newValue;
        };
        MeAudioVolume.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, audioVolume.SliderValue)) audioVolume.SetSliderValue(newValue);
        });

        // Performance Category
        var performanceCategory = page.AddCategory("Performance");

        var deleteMarioAfterDead = performanceCategory.AddToggle("Delete Mario 15s after Dead",
            "Whether to delete our Marios 15 seconds after Dead or not.",
            MeDeleteMarioAfterDead.Value);
        deleteMarioAfterDead.OnValueUpdated += b => {
            if (b != MeDeleteMarioAfterDead.Value) MeDeleteMarioAfterDead.Value = b;
        };
        MeDeleteMarioAfterDead.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != disableAudio.ToggleValue) deleteMarioAfterDead.ToggleValue = newValue;
        });

        var skipFarMarioDistance = page.AddSlider(
            "Skip Mario Engine Updates Distance",
            "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.",
            MeSkipFarMarioDistance.Value, 0f, 50f, 2);
        skipFarMarioDistance.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeSkipFarMarioDistance.Value)) MeSkipFarMarioDistance.Value = newValue;
        };
        MeSkipFarMarioDistance.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, skipFarMarioDistance.SliderValue)) skipFarMarioDistance.SetSliderValue(newValue);
        });

        var maxMariosAnimatedPerPerson = page.AddSlider(
            "Max Marios per Player",
            "Max number of Marios per player that will be animated using the Super Mario 64 Engine.",
            MeMaxMariosAnimatedPerPerson.Value, 0, 20, 0);
        maxMariosAnimatedPerPerson.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeMaxMariosAnimatedPerPerson.Value) MeMaxMariosAnimatedPerPerson.Value = Mathf.RoundToInt(newValue);
        };
        MeMaxMariosAnimatedPerPerson.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(maxMariosAnimatedPerPerson.SliderValue)) maxMariosAnimatedPerPerson.SetSliderValue(newValue);
        });

        var maxMeshColliderTotalTris = page.AddSlider(
            "Max Mesh Collider Total Triangles",
            "Maximum total number of triangles of automatically generated mesh colliders allowed.",
            MeMaxMeshColliderTotalTris.Value, 0, 250000, 0);
        maxMeshColliderTotalTris.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeMaxMeshColliderTotalTris.Value) MeMaxMeshColliderTotalTris.Value = Mathf.RoundToInt(newValue);
        };
        MeMaxMeshColliderTotalTris.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(maxMeshColliderTotalTris.SliderValue)) maxMeshColliderTotalTris.SetSliderValue(newValue);
        });

        // Engine Category
        var engineCategory = page.AddCategory("SM64 Engine");

        var audioPitch = page.AddSlider(
            "Pitch of the Audio [Default: 0.74]",
            "The audio pitch of the game sounds. You can use this to fine tune the Engine Sounds",
            MeAudioPitch.Value, 0f, 1f, 2);
        audioPitch.OnValueUpdated += newValue => {
            if (!Mathf.Approximately(newValue, MeAudioPitch.Value)) MeAudioPitch.Value = newValue;
        };
        MeAudioPitch.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (!Mathf.Approximately(newValue, audioPitch.SliderValue)) audioPitch.SetSliderValue(newValue);
        });

        var gameTicksMs = page.AddSlider(
            "Game Ticks Interval [Default: 25]",
            "The game ticks Interval in Milliseconds. This will directly impact the speed of Mario's behavior.",
            MeGameTickMs.Value, 1, 100, 0);
        gameTicksMs.OnValueUpdated += newValue => {
            if (Mathf.RoundToInt(newValue) != MeGameTickMs.Value) MeGameTickMs.Value = Mathf.RoundToInt(newValue);
        };
        MeGameTickMs.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue != Mathf.RoundToInt(gameTicksMs.SliderValue)) gameTicksMs.SetSliderValue(newValue);
        });
    }

}
