using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

internal static class Utils {

    private static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, Mesh mesh,
        SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<Vector3, Vector3> transformFunc) {
        for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
            var tris = mesh.GetTriangles(subMeshIndex);
            var vertices = mesh.vertices.Select(transformFunc).ToArray();

            for (int i = 0; i < tris.Length; i += 3) {
                outSurfaces.Add(new Interop.SM64Surface {
                    force = 0,
                    type = (short)surfaceType,
                    terrain = (ushort)terrainType,
                    v0x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i]].x),
                    v0y = (short)(Interop.SCALE_FACTOR * vertices[tris[i]].y),
                    v0z = (short)(Interop.SCALE_FACTOR * vertices[tris[i]].z),
                    v1x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i + 2]].x),
                    v1y = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 2]].y),
                    v1z = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 2]].z),
                    v2x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i + 1]].x),
                    v2y = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 1]].y),
                    v2z = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 1]].z)
                });
            }
        }
    }

    public static Interop.SM64Surface[] GetSurfacesForMesh(Vector3 scale, Mesh mesh, SM64SurfaceType surfaceType,
        SM64TerrainType terrainType) {
        var surfaces = new List<Interop.SM64Surface>();
        TransformAndGetSurfaces(surfaces, mesh, surfaceType, terrainType, x => Vector3.Scale(scale, x));
        return surfaces.ToArray();
    }

    private static readonly Dictionary<PrimitiveType, Mesh> MeshesCache = new();

    // Get mesh primitives (cached)
    public static Mesh GetPrimitiveMesh(PrimitiveType type) {
        if (MeshesCache.ContainsKey(type)) return MeshesCache[type];

        var go = GameObject.CreatePrimitive(type);
        var mesh = go.GetComponent<MeshFilter>().mesh;
        mesh.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        MeshesCache.Add(type, mesh);
        UnityEngine.Object.Destroy(go);

        return mesh;
    }

    private static readonly int UILayer = LayerMask.NameToLayer("UI");
    private static readonly int UIInternalLayer = LayerMask.NameToLayer("UI Internal");
    private static readonly int PlayerLocalLayer = LayerMask.NameToLayer("PlayerLocal");
    private static readonly int PlayerNetworkLayer = LayerMask.NameToLayer("PlayerNetwork");
    private static readonly int IgnoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    private static readonly int MirrorReflectionLayer = LayerMask.NameToLayer("MirrorReflection");

    internal static bool IsGoodCollider(Collider col) {
        return
            // Ignore disabled
            col.enabled
            && col.gameObject.activeInHierarchy
            // Ignore other mario's colliders
            && col.GetComponentInChildren<CVRSM64CMario>() == null
            // Ignore the some layers
            && col.gameObject.layer != PlayerLocalLayer
            && col.gameObject.layer != PlayerNetworkLayer
            && col.gameObject.layer != IgnoreRaycastLayer
            && col.gameObject.layer != MirrorReflectionLayer
            && col.gameObject.layer != UILayer
            && col.gameObject.layer != UIInternalLayer
            // Ignore triggers
            && !col.isTrigger;
    }


    internal static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, BoxCollider b,
        SM64SurfaceType surfaceType, SM64TerrainType terrainType) {
        Vector3[] vertices = {
            b.transform.TransformPoint(b.center + new Vector3(-b.size.x, -b.size.y, -b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(b.size.x, -b.size.y, -b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(b.size.x, b.size.y, -b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(-b.size.x, b.size.y, -b.size.z) * 0.5f),

            b.transform.TransformPoint(b.center + new Vector3(-b.size.x, b.size.y, b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(b.size.x, b.size.y, b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(b.size.x, -b.size.y, b.size.z) * 0.5f),
            b.transform.TransformPoint(b.center + new Vector3(-b.size.x, -b.size.y, b.size.z) * 0.5f),
        };

        int[] tris = {
            0, 2, 1, //face front
            0, 3, 2,
            2, 3, 4, //face top
            2, 4, 5,
            1, 2, 5, //face right
            1, 5, 6,
            0, 7, 4, //face left
            0, 4, 3,
            5, 4, 7, //face back
            5, 7, 6,
            0, 6, 7, //face bottom
            0, 1, 6
        };

        for (var i = 0; i < tris.Length; i += 3) {
            outSurfaces.Add(new Interop.SM64Surface {
                force = 0,
                type = (short)surfaceType,
                terrain = (ushort)terrainType,
                v0x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i]].x),
                v0y = (short)(Interop.SCALE_FACTOR * vertices[tris[i]].y),
                v0z = (short)(Interop.SCALE_FACTOR * vertices[tris[i]].z),
                v1x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i + 2]].x),
                v1y = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 2]].y),
                v1z = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 2]].z),
                v2x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i + 1]].x),
                v2y = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 1]].y),
                v2z = (short)(Interop.SCALE_FACTOR * vertices[tris[i + 1]].z)
            });
        }
    }

    internal static Vector3 TransformPoint(SphereCollider s, Vector3 point) {
        return s.transform.TransformPoint(point * s.radius * 2 + s.center);
    }

    internal static Vector3 TransformPoint(CapsuleCollider c, Vector3 point) {
        // Calculate the height and radius as if we were on the direction y (i == 1)
        for (int i = 0; i < 3; i++) {
            point[i] *= i == 1 ? Mathf.Max(c.height, c.radius * 2) / 2 : c.radius * 2;
        }

        // Janky way to rotate the whole vertexes for the proper direction
        var direction = Vector3.zero;
        if (c.direction == 0) {
            direction.x = 90f;
            direction.y = -90f;
        }

        if (c.direction == 2) {
            direction.z = 90f;
            direction.y = -90f;
        }

        var alignedCapsule = Quaternion.Euler(direction) * point;

        return c.transform.TransformPoint(alignedCapsule + c.center);
    }


    internal static Interop.SM64Surface[] GetAllStaticSurfaces() {
        var surfaces = new List<Interop.SM64Surface>();

        foreach (var obj in UnityEngine.Object.FindObjectsOfType<Collider>()) {
            // Ignore bad colliders
            if (!IsGoodCollider(obj)) continue;


            #if DEBUG
            //MelonLogger.Msg($"[GoodCollider] {obj.name}");
            #endif

            switch (obj) {
                case BoxCollider boxCollider:
                    // surfaces.AddRange(Utils.GetSurfacesForMesh(boxCollider.transform.lossyScale, GetPrimitiveMesh(PrimitiveType.Cube), SM64SurfaceType.Default, SM64TerrainType.Grass));
                    // Utils.transformAndGetSurfaces( surfaces, GetPrimitiveMesh(PrimitiveType.Cube), SM64SurfaceType.Default, SM64TerrainType.Grass, x => boxCollider.transform.TransformPoint( x ) * boxCollider.size);
                    //Utils.transformAndGetSurfaces( surfaces, GetPrimitiveMesh(PrimitiveType.Cube), SM64SurfaceType.Default, SM64TerrainType.Grass, x => TransformPoint( boxCollider, x ));
                    TransformAndGetSurfaces(surfaces, boxCollider, SM64SurfaceType.Default, SM64TerrainType.Grass);
                    break;
                case CapsuleCollider capsuleCollider:
                    // surfaces.AddRange(Utils.GetSurfacesForMesh(capsuleCollider.transform.lossyScale, GetPrimitiveMesh(PrimitiveType.Capsule), SM64SurfaceType.Default, SM64TerrainType.Grass));
                    // Utils.transformAndGetSurfaces( surfaces, GetPrimitiveMesh(PrimitiveType.Capsule), SM64SurfaceType.Default, SM64TerrainType.Grass, x => capsuleCollider.transform.TransformPoint( x ));
                    Utils.TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Capsule),
                        SM64SurfaceType.Default, SM64TerrainType.Grass, x => TransformPoint(capsuleCollider, x));
                    break;
                case MeshCollider meshCollider:
                    // Skip meshes we can't read the information! ;_;
                    #if DEBUG
                    MelonLogger.Msg(
                        $"[MeshCollider] {meshCollider.name} Readable: {meshCollider.sharedMesh.isReadable}, SubMeshCount: {meshCollider.sharedMesh.subMeshCount}, TrisCount: {meshCollider.sharedMesh.triangles.Length}");
                    #endif
                    if (!meshCollider.sharedMesh.isReadable) {
                        continue;
                    }

                    Utils.TransformAndGetSurfaces(surfaces, meshCollider.sharedMesh, SM64SurfaceType.Default,
                        SM64TerrainType.Grass, x => meshCollider.transform.TransformPoint(x));
                    break;
                case SphereCollider sphereCollider:
                    // surfaces.AddRange(Utils.GetSurfacesForMesh(sphereCollider.transform.lossyScale, GetPrimitiveMesh(PrimitiveType.Sphere), SM64SurfaceType.Default, SM64TerrainType.Grass));
                    // Utils.transformAndGetSurfaces( surfaces, GetPrimitiveMesh(PrimitiveType.Sphere), SM64SurfaceType.Default, SM64TerrainType.Grass, x => sphereCollider.transform.TransformPoint( x ));
                    Utils.TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Sphere),
                        SM64SurfaceType.Default, SM64TerrainType.Grass, x => TransformPoint(sphereCollider, x));
                    break;
                // Ignore other colliders as they would need more handling
                // case TerrainCollider terrainCollider:
                //     terrainCollider.terrainData
            }
        }

        return surfaces.ToArray();
    }
}
