using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.Camera;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Kafe.AMDQuestWirelessStutterFix;

public enum FixMode
{
    SDR,
    HDR,
    Disabled,
}

public class AMDQuestWirelessStutterFix : MelonMod {

    // Layer Indices and Masks
    private const int PPLayer = 4;
    private const int PPLayerMask = 1 << PPLayer;

    private static CVRWorld _currentWorld;
    private static string _currentWorldGuid;
    private static Camera _currentWorldCamera;
    private static PostProcessLayer _currentWorldLayer;
    private static PostProcessVolume _currentWorldModVolume;
    private static readonly PostProcessProfile ModProfile = ScriptableObject.CreateInstance<PostProcessProfile>();


    // Current world original settings
    private static bool _originalEnabled;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    internal static void AndApplyChanges() {
        // Apply the current config to the world
        _currentWorld.UpdatePostProcessing();
    }

    private static void UpdatePostProcessing() {

        if (ModConfig.MeFixMode.Value == FixMode.Disabled)
        {
            _currentWorldModVolume.enabled = false;
            _currentWorldLayer.enabled = _originalEnabled;
            PortableCamera.Instance.OnWorldLoaded(_currentWorldCamera);
            // We're done here, the original behavior was loaded by CVR before this was called
            return;
        }

        // Set the PP overrides state
        _currentWorldModVolume.enabled = true;
        _currentWorldLayer.enabled = true;
        PortableCamera.Instance.OnWorldLoaded(_currentWorldCamera);

        // ColorGrading enabled
        if (!ModProfile.TryGetSettings(out ColorGrading colorGrading)) {
            colorGrading = ModProfile.AddSettings<ColorGrading>();
        }

        // Override the active (this is set by the CVR Settings for Post Processing, we want to ignore the setting)
        colorGrading.active = true;

        colorGrading.enabled.value = true;
        colorGrading.enabled.overrideState = true;

        // ColorGrading Mode
        colorGrading.gradingMode.value = ModConfig.MeFixMode.Value == FixMode.HDR ? GradingMode.HighDefinitionRange : GradingMode.LowDefinitionRange;
        colorGrading.gradingMode.overrideState = true;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static void SetupPostProcessing(CVRWorld world) {

            // Save current world info
            _currentWorld = world;
            _currentWorldGuid = MetaPort.Instance.CurrentWorldId;

            _currentWorldCamera = PlayerSetup.Instance.activeCam;
            _currentWorldLayer = _currentWorldCamera.GetComponent<PostProcessLayer>();

            // Enforce the PPLayer on the Post Processing layer
            if (world.referenceCamera != null && world.referenceCamera.TryGetComponent(out PostProcessLayer refCameraPostProcessLayer)) {
                _currentWorldLayer.volumeLayer = refCameraPostProcessLayer.volumeLayer;
                _originalEnabled = refCameraPostProcessLayer.enabled;
            }
            else {
                _originalEnabled = false;
            }

            // Add our PP layer to the Mask
            _currentWorldLayer.volumeLayer |= PPLayerMask;

            // Create the mod override volume for the world
            var modPPVolumeGo = new GameObject($"[{nameof(AMDQuestWirelessStutterFix)} Mod] OverrideVolume") {
                layer = PPLayer
            };
            modPPVolumeGo.transform.SetParent(world.transform);
            _currentWorldModVolume = modPPVolumeGo.AddComponent<PostProcessVolume>();
            _currentWorldModVolume.profile = ModProfile;
            _currentWorldModVolume.isGlobal = true;
            _currentWorldModVolume.priority = float.MaxValue - 1;
            _currentWorldModVolume.weight = 1f;
            _currentWorldModVolume.enabled = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.SetDefaultCamValues))]
        public static void After_CVRWorld_SetDefaultCamValues(CVRWorld __instance) {
            try {
                // Setup when there is no reference camera
                SetupPostProcessing(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_CopyRefCamValues)}");
                MelonLogger.Error(e);
                throw;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.CopyRefCamValues))]
        public static void After_CVRWorld_CopyRefCamValues(CVRWorld __instance) {
            try {
                // Setup when there is a reference camera
                SetupPostProcessing(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_CopyRefCamValues)}");
                MelonLogger.Error(e);
                throw;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.UpdatePostProcessing))]
        public static void After_CVRWorld_UpdatePostProcessing(CVRWorld __instance) {
            try {
                // Everytime CVR Updates the post processing, we're going to catch it and do our own business
                UpdatePostProcessing();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_UpdatePostProcessing)}");
                MelonLogger.Error(e);
                throw;
            }
        }

    }
}
