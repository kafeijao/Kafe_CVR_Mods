using UnityEngine;

#if DEBUG
using MelonLoader;
#endif

namespace Kafe.CVRSuperMario64;

public class CVRSM64ColliderDynamic : MonoBehaviour {

    [SerializeField] private SM64TerrainType terrainType = SM64TerrainType.Grass;
    [SerializeField] private SM64SurfaceType surfaceType = SM64SurfaceType.Default;

    public SM64TerrainType TerrainType => terrainType;
    public SM64SurfaceType SurfaceType => surfaceType;

    private uint _surfaceObjectId;

    // Threading
    private readonly object _lock = new();

    private Vector3 LastPosition { get; set; }
    private Quaternion LastRotation { get; set; }

    private bool HasChanges { get; set; }


    private void OnEnable() {
        CVRSM64Context.RegisterSurfaceObject(this);

        LastPosition = transform.position;
        LastRotation = transform.rotation;

        var col = GetComponent<Collider>();
        var surfaces = Utils.GetScaledSurfaces(col, new List<Interop.SM64Surface>(), surfaceType, terrainType, true).ToArray();
        _surfaceObjectId = Interop.SurfaceObjectCreate(transform.position, transform.rotation, surfaces.ToArray());

        #if DEBUG
        MelonLogger.Msg($"[CVRSM64ColliderDynamic] [{_surfaceObjectId}] {gameObject.name} Enabled! Surface Count: {surfaces.Length}");
        #endif
    }

    private void OnDisable() {

        if (Interop.isGlobalInit) {
            CVRSM64Context.UnregisterSurfaceObject(this);
            Interop.SurfaceObjectDelete(_surfaceObjectId);
        }

        #if DEBUG
        MelonLogger.Msg($"[CVRSM64ColliderDynamic] [{_surfaceObjectId}] {gameObject.name} Disabled!");
        #endif
    }

    internal void UpdateCurrentPositionData() {
        lock (_lock) {
            if (transform.position != LastPosition || transform.rotation != LastRotation) {
                LastPosition = transform.position;
                LastRotation = transform.rotation;
                HasChanges = true;
            }
        }
    }

    internal void ConsumeCurrentPosition() {
        lock (_lock) {
            if (HasChanges) {
                Interop.SurfaceObjectMove(_surfaceObjectId, LastPosition, LastRotation);
                HasChanges = false;
            }
        }
    }

    internal void ContextFixedUpdateSynced() {
        if (transform.position != LastPosition || transform.rotation != LastRotation) {
            LastPosition = transform.position;
            LastRotation = transform.rotation;

            Interop.SurfaceObjectMove(_surfaceObjectId, transform.position, transform.rotation);
        }
    }
}
