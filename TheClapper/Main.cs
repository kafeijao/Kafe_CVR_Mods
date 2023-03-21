using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace TheClapper;

public class TheClapper : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> PreventClappingFriends;
    private static MelonPreferences_Entry<bool> _showVisualizerAfterOpenHands;
    private static MelonPreferences_Entry<float> _showVisualizerDelay;

    private static ParticleSystem _particleSystem;
    private static ParticleSystemRenderer _particleSystemRenderer;
    private static Vector3 _baseScale;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public override void OnInitializeMelon() {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(TheClapper));

        PreventClappingFriends = _melonCategory.CreateEntry("PreventClappingFriendAvatars", true,
            description: "Whether or not to ignore friend's avatars when clapping.");

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
    private static Traverse<CVRGestureStep.Gesture> _leftGesture;
    private static Traverse<CVRGestureStep.Gesture> _rightGesture;

    public override void OnUpdate() {
        if (!_isInitialized || !_showVisualizerAfterOpenHands.Value) return;

        var isPreparingClap = _leftGesture.Value == CVRGestureStep.Gesture.Open &&
                              _rightGesture.Value == CVRGestureStep.Gesture.Open;

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
        var main = _particleSystem.main;

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
        [HarmonyPatch(typeof(GesturePlaneTest), "Start")]
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

            // Save gesture references
            var gmTraverse = Traverse.Create(CVRGestureRecognizer.Instance);
            _leftGesture = gmTraverse.Field<CVRGestureStep.Gesture>("currentGestureLeft");
            _rightGesture = gmTraverse.Field<CVRGestureStep.Gesture>("currentGestureRight");

            _isInitialized = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PuppetMaster), nameof(PuppetMaster.AvatarInstantiated))]
        private static void After_PuppetMaster_AvatarInstantiated(PuppetMaster __instance, PlayerDescriptor ____playerDescriptor, Animator ____animator) {
            if (__instance.avatarObject == null) return;
            ClappableAvatar.Create(__instance, ____playerDescriptor.ownerId, ____playerDescriptor.userName, ____animator);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn))]
        private static void After_CVRSyncHelper_ApplyPropValuesSpawn(CVRSyncHelper.PropData propData) {
            ClappableSpawnable.Create(propData);
        }
    }
}
