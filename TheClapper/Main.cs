using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.PropManagement;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.GestureRecognition;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.TheClapper;

public class TheClapper : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> DisableClappingProps;
    internal static MelonPreferences_Entry<bool> DisableClappingAvatars;
    internal static MelonPreferences_Entry<bool> AlwaysAllowClappingHiddenAvatars;
    internal static MelonPreferences_Entry<bool> PreventClappingFriendsAvatars;
    internal static MelonPreferences_Entry<bool> PreventClappingFriendsProps;
    internal static MelonPreferences_Entry<bool> PreventClappingMyProps;
    internal static MelonPreferences_Entry<bool> ClappablePropPickups;
    internal static MelonPreferences_Entry<bool> ClappablePropSubSyncs;
    private static MelonPreferences_Entry<bool> _showVisualizerAfterOpenHands;
    private static MelonPreferences_Entry<float> _showVisualizerDelay;

    public override void OnInitializeMelon() {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(TheClapper));

        DisableClappingProps = _melonCategory.CreateEntry("DisableClappingProps", false,
            description: "Whether clapping props is completely disabled or not.");

        DisableClappingAvatars = _melonCategory.CreateEntry("DisableClappingAvatars", false,
            description: "Whether clapping avatars is completely disabled or not.");

        AlwaysAllowClappingHiddenAvatars = _melonCategory.CreateEntry("AlwaysAllowClappingHiddenAvatars", true,
            description: "Whether always allow clapping avatars when they are hidden. (clapping hidden avatars reveals them)");

        PreventClappingFriendsAvatars = _melonCategory.CreateEntry("PreventClappingFriendAvatars", true,
            description: "Whether or not to ignore friend's avatars when clapping.");

        PreventClappingMyProps = _melonCategory.CreateEntry("PreventClappingMyProps", false,
            description: "Whether or not to ignore props spawned by me props when clapping.");

        PreventClappingFriendsProps = _melonCategory.CreateEntry("PreventClappingFriendsProps", false,
            description: "Whether or not to ignore friend's spawned props when clapping.");

        ClappablePropPickups = _melonCategory.CreateEntry("ClappablePropPickups", false,
            description: "Makes all prop's pickup points clappable. Changes only apply to newly spawned props.");

        ClappablePropSubSyncs = _melonCategory.CreateEntry("ClappablePropSubSyncs", false,
            description: "Makes all prop's sub-sync points clappable. Changes only apply to newly spawned props.");

        _showVisualizerAfterOpenHands = _melonCategory.CreateEntry("ShowVisualizersAfterOpenHands", true,
            description: "Whether or not to show visualizers of where to clap. The visualizers appear 1 second " +
                         "after having both hands with the open hand gesture.");

        _showVisualizerDelay = _melonCategory.CreateEntry("ShowVisualizersDelayInSeconds", 1f,
            description: "Delay the visualizers take to show up. Use 0 for instant.");

        _showVisualizerDelay.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (newValue < 0) _showVisualizerDelay.Value = Mathf.Abs(newValue);
            _wasPreparingClap = false;
        });

        CVRGameEventSystem.Avatar.OnRemoteAvatarLoad.AddListener((player, _) =>
        {
            try
            {
                var puppetMaster = player.PuppetMaster;
                if (puppetMaster == null || puppetMaster.AvatarObject == null) return;
                ClappableAvatar.Create(puppetMaster, puppetMaster.PlayerId, player.Username, puppetMaster.Animator);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(CVRGameEventSystem.Avatar.OnRemoteAvatarLoad)} listener", e);
            }
        });

        CVRGameEventSystem.Spawnable.OnPropSpawned.AddListener((_, propSoul) =>
        {
            try
            {
                ClappableSpawnable.Create(propSoul);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during {nameof(CVRGameEventSystem.Spawnable.OnPropSpawned)} listener", e);
            }
        });
    }

    private static bool _isInitialized;
    private static bool _wasPreparingClap;
    private static float _preparingTime;

    public override void OnUpdate() {
        if (!_isInitialized || !_showVisualizerAfterOpenHands.Value) return;

        var inputManager = CVRGestureRecognizer.Instance.inputManager;
        var isPreparingClap = CVRGestureRecognizer.Sign(inputManager.gestureLeftRaw) == CVRGesture.Sign.Open &&
                              CVRGestureRecognizer.Sign(inputManager.gestureRightRaw) == CVRGesture.Sign.Open;

        if (isPreparingClap) {
            if (!_wasPreparingClap) {
                _preparingTime = Time.time + _showVisualizerDelay.Value;
                _wasPreparingClap = true;
            }
            else if (Time.time > _preparingTime) {
                Clappable.UpdateVisualizersShown(true);
            }
        }
        else if (_wasPreparingClap) {
            Clappable.UpdateVisualizersShown(false);
            _preparingTime = -1;
            _wasPreparingClap = false;
        }
    }

    internal static void EmitParticles(Vector3 pos, Color color)
    {
        RootLogic.Instance.SpawnPoof(pos, color, color * 5f);
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRGestureRecognizer), nameof(CVRGestureRecognizer.Start))]
        private static void After_CVRGestureRecognizer_Start(CVRGestureRecognizer __instance) {
            // Start destroys duplicate recognizers, only register on the one that stayed
            if (CVRGestureRecognizer.Instance != __instance) return;

            // Create the Clap gesture and add its handler
            var gesture = new CVRGesture {
                name = "avatarClap",
                type = CVRGesture.GestureType.OneShot,
                firstSign = CVRGesture.Sign.Open,
                secondSign = CVRGesture.Sign.Open,
                startDistance = 0.4f,
                endDistance = 0.25f,
                direction = CVRGesture.Direction.MovingIn,
            };
            gesture.onEnd.AddListener(Clappable.OnGestureClapped);
            __instance.gestures.Add(gesture);

            _isInitialized = true;
        }
    }
}
