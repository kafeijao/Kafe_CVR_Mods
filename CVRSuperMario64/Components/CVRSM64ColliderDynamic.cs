using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64ColliderDynamic : MonoBehaviour {

    [SerializeField] SM64TerrainType terrainType = SM64TerrainType.Grass;
    [SerializeField] SM64SurfaceType surfaceType = SM64SurfaceType.Default;

    public SM64TerrainType TerrainType => terrainType;
    public SM64SurfaceType SurfaceType => surfaceType;

    uint _surfaceObjectId;

    private Vector3 LastPosition { get; set; }
    private Quaternion LastRotation { get; set; }


    private void OnEnable() {
        CVRSM64CContext.RegisterSurfaceObject(this);

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
            CVRSM64CContext.UnregisterSurfaceObject(this);
            Interop.SurfaceObjectDelete(_surfaceObjectId);
        }

        #if DEBUG
        MelonLogger.Msg($"[CVRSM64ColliderDynamic] [{_surfaceObjectId}] {gameObject.name} Disabled!");
        #endif
    }

    internal void ContextFixedUpdate() {
        if (transform.position != LastPosition || transform.rotation != LastRotation) {
            LastPosition = transform.position;
            LastRotation = transform.rotation;

            Interop.SurfaceObjectMove(_surfaceObjectId, transform.position, transform.rotation);
        }
    }

    internal void ContextUpdate() {
        // var t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        //
        // transform.position = Vector3.LerpUnclamped(lastPosition, position, t);
        // transform.rotation = Quaternion.SlerpUnclamped(lastRotation, rotation, t);
    }
}
