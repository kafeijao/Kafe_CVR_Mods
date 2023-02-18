using LibSM64;
using UnityEngine;

namespace CVRSuperMario64;

public class CVRSM64DynamicCollider : MonoBehaviour {
    
    [SerializeField] SM64TerrainType terrainType = SM64TerrainType.Grass;
    [SerializeField] SM64SurfaceType surfaceType = SM64SurfaceType.Default;

    public SM64TerrainType TerrainType => terrainType;

    public SM64SurfaceType SurfaceType => surfaceType;

    uint _surfaceObjectId;

    public Vector3 position { get; private set; }

    public Vector3 lastPosition { get; private set; }

    public Quaternion rotation { get; private set; }

    public Quaternion lastRotation { get; private set; }

    void OnEnable() {
        
        CVRSM64CContext.RegisterSurfaceObject(this);

        position = transform.position;
        rotation = transform.rotation;
        lastPosition = position;
        lastRotation = rotation;

        var mc = GetComponent<MeshCollider>();
        var surfaces = Utils.GetSurfacesForMesh(transform.lossyScale, mc.sharedMesh, surfaceType, terrainType);
        _surfaceObjectId = Interop.SurfaceObjectCreate(position, rotation, surfaces.ToArray());
    }

    void OnDisable() {
        if (Interop.isGlobalInit) {
            CVRSM64CContext.UnregisterSurfaceObject(this);
            Interop.SurfaceObjectDelete(_surfaceObjectId);
        }
    }

    internal void contextFixedUpdate() {
        
        if (position != lastPosition || rotation != lastRotation) {
            
            lastPosition = position;
            lastRotation = rotation;
            
            Interop.SurfaceObjectMove(_surfaceObjectId, position, rotation);
        }
    }

    internal void contextUpdate() {
        
        var t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

        transform.position = Vector3.LerpUnclamped(lastPosition, position, t);
        transform.rotation = Quaternion.SlerpUnclamped(lastRotation, rotation, t);
    }
}