﻿using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
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

    private static ParticleSystem _particleSystem;
    private static ParticleSystemRenderer _particleSystemRenderer;
    private static Vector3 _baseScale;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

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
    }

    private static bool _isInitialized;
    private static bool _wasPreparingClap;
    private static float _preparingTime;

    public override void OnUpdate() {
        if (!_isInitialized || !_showVisualizerAfterOpenHands.Value) return;

        var isPreparingClap = CVRGestureRecognizer.Instance.currentGestureLeft == CVRGestureStep.Gesture.Open &&
                              CVRGestureRecognizer.Instance.currentGestureRight == CVRGestureStep.Gesture.Open;

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

    internal static void EmitParticles(Vector3 pos, Color color, float scale = 1f) {
        var _ = _particleSystem.main;

        var material = _particleSystemRenderer.material;
        material.color = color;
        material.SetColor(EmissionColor, color * 5f);

        _particleSystem.gameObject.transform.localScale = _baseScale * scale;

        _particleSystem.transform.position = pos;
        _particleSystem.Emit(100);
        //_particleSystem.Emit(new ParticleSystem.EmitParams { position = pos }, 100);
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GesturePlaneTest), nameof(GesturePlaneTest.Start))]
        private static void After_GesturePlaneTest_Start(GesturePlaneTest __instance) {

            // Create the Clap gesture and add its handler
            var gesture = new CVRGesture {
                name = "avatarClap",
                type = CVRGesture.GestureType.OneShot,
            };
            gesture.steps.Add(new CVRGestureStep {
                firstGesture = CVRGestureStep.Gesture.Open,
                secondGesture = CVRGestureStep.Gesture.Open,
                startDistance = 0.4f,
                endDistance = 0.25f,
                direction = CVRGestureStep.GestureDirection.MovingIn,
            });
            gesture.onEnd.AddListener(Clappable.OnGestureClapped);
            CVRGestureRecognizer.Instance.gestures.Add(gesture);

            // Copy the camera clap particles
            var particlesGo = UnityEngine.Object.Instantiate(__instance.particles);
            UnityEngine.Object.DontDestroyOnLoad(particlesGo);
            var particleSystem = particlesGo.GetComponent<ParticleSystem>();
            var particleSystemRenderer = particlesGo.GetComponent<ParticleSystemRenderer>();
            var mainModule = particleSystem.main;
            mainModule.playOnAwake = false;
            mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;
            mainModule.startSpeedMultiplier *= 3f;
            particlesGo.SetActive(true);
            _baseScale = particlesGo.transform.localScale;
            _particleSystem = particleSystem;
            _particleSystemRenderer = particleSystemRenderer;

            _isInitialized = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.OnSetupAvatar))]
        private static void After_PuppetMaster_AvatarInstantiated(PuppetMaster __instance) {
            if (__instance.AvatarObject == null) return;
            ClappableAvatar.Create(__instance, __instance.PlayerId, __instance.CVRPlayerEntity.Username, __instance.Animator);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn))]
        private static void After_CVRSyncHelper_ApplyPropValuesSpawn(CVRSyncHelper.PropData propData) {
            ClappableSpawnable.Create(propData);
        }
    }
}
