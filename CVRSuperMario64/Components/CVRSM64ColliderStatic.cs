using ABI_RC.Core.Util;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64ColliderStatic : MonoBehaviour {

    [SerializeField] private SM64TerrainType terrainType = SM64TerrainType.Grass;
    [SerializeField] private SM64SurfaceType surfaceType = SM64SurfaceType.Default;

    public SM64TerrainType TerrainType => terrainType;
    public SM64SurfaceType SurfaceType => surfaceType;

    // private bool _initialized;
    // private uint _surfaceObjectId;
    //
    // private void OnEnable() {
    //     CVRSM64CContext.EnsureInstanceExists();
    //     var col = GetComponent<Collider>();
    //     var surfaces = Utils.GetScaledSurfaces(col, new List<Interop.SM64Surface>(), surfaceType, terrainType, true).ToArray();
    //     _surfaceObjectId = Interop.SurfaceObjectCreate(transform.position, transform.rotation, surfaces.ToArray());
    //     _initialized = true;
    //
    //     SetTransforms();
    //
    //     #if DEBUG
    //     MelonLogger.Msg($"[CVRSM64ColliderStatic] [{_surfaceObjectId}] {gameObject.name} Enabled! Surface Count: {surfaces.Length}");
    //     #endif
    // }
    //
    // private void Start() {
    //     // We're setting the transforms here as well, because the first OnEnable won't have the position properly set yet
    //     SetTransforms();
    // }
    //
    // private void SetTransforms() {
    //     if (!_initialized) return;
    //     // Move the surface to the correct place
    //     Interop.SurfaceObjectMove(_surfaceObjectId, transform.position, transform.rotation);
    // }
    //
    // private void OnDisable() {
    //
    //     _initialized = false;
    //
    //     if (Interop.isGlobalInit) {
    //         Interop.SurfaceObjectDelete(_surfaceObjectId);
    //     }
    //
    //     #if DEBUG
    //     MelonLogger.Msg($"[CVRSM64ColliderStatic] [{_surfaceObjectId}] {gameObject.name} Disabled!");
    //     #endif
    // }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSyncHelper), nameof(CVRSyncHelper.ApplyPropValuesSpawn))]
        private static void After_CVRSyncHelper_ApplyPropValuesSpawn(CVRSyncHelper.PropData propData) {
            try {
                // If we load a prop with static colliders, let's queue a static colliders refresh!
                if (propData.Spawnable.GetComponentInChildren<CVRSM64ColliderStatic>(true) != null) {
                    #if DEBUG
                    MelonLogger.Msg($"A prop with {nameof(CVRSM64ColliderStatic)} has been spawned, we need to reload " +
                                    $"all static colliders. You might notice some lag spike...");
                    #endif
                    CVRSM64CContext.QueueStaticSurfacesUpdate();
                }
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.OnDestroy))]
        private static void Before_CVRSpawnable_OnDestroy(CVRSpawnable __instance) {
            try {
                // If we delete a prop with static colliders, let's queue a static colliders refresh!
                if (__instance.GetComponentInChildren<CVRSM64ColliderStatic>(true) != null) {
                    #if DEBUG
                    MelonLogger.Msg($"A prop with {nameof(CVRSM64ColliderStatic)} has been deleted, we need to reload " +
                                    $"all static colliders. You might notice some lag spike...");
                    #endif
                    CVRSM64CContext.QueueStaticSurfacesUpdate();
                }
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }
    }
}
