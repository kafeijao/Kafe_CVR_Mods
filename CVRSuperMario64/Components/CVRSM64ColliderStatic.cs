using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSM64ColliderStatic : MonoBehaviour {

    [SerializeField] SM64TerrainType terrainType = SM64TerrainType.Grass;
    [SerializeField] SM64SurfaceType surfaceType = SM64SurfaceType.Default;

    public SM64TerrainType TerrainType {
        get { return terrainType; }
    }

    public SM64SurfaceType SurfaceType {
        get { return surfaceType; }
    }
}
