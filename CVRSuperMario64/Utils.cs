using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

internal static class Utils {

    private static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, Mesh mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<Vector3, Vector3> transformFunc) {
        for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++) {
            var tris = mesh.GetTriangles(subMeshIndex);
            var vertices = mesh.vertices.Select(transformFunc).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, tris, vertices, surfaceType, terrainType);
        }
    }

    public static Interop.SM64Surface[] GetSurfacesForMesh(Vector3 scale, Mesh mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType) {
        var surfaces = new List<Interop.SM64Surface>();
        TransformAndGetSurfaces(surfaces, mesh, surfaceType, terrainType, x => Vector3.Scale(scale, x));
        return surfaces.ToArray();
    }

    private static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, TerrainCollider terrain, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<Vector3, Vector3> transformFunc = null) {

        var actualTerrainResolution = terrain.terrainData.heightmapResolution;
        const int maxTerrainResolution = 129;
        if (actualTerrainResolution <= 0) actualTerrainResolution = maxTerrainResolution;

        if (actualTerrainResolution > maxTerrainResolution) {
            MelonLogger.Warning($"[TerrainCollider] {terrain.name} has a resolution of {actualTerrainResolution} " +
                                $"which would result in {Math.Pow(actualTerrainResolution, 2)} Collision Polygons. " +
                                $"We're going to scale down to a resolution of {maxTerrainResolution}. Marios might " +
                                $"clip in the terrain :(");
        }

        var terrainResolution = Math.Min(actualTerrainResolution, maxTerrainResolution);
        var multiplier = terrainResolution / (float)actualTerrainResolution;

        // Get the heightmap data from the terrain
        var heights = terrain.terrainData.GetHeights(0, 0, actualTerrainResolution, actualTerrainResolution);

        // Generate vertices and normals based on the heightmap data
        var vertices = new Vector3[terrainResolution * terrainResolution];
        var index = 0;
        for (var i = 0; i < terrainResolution; i++) {
            for (var j = 0; j < terrainResolution; j++) {
                var jLerp = j + Mathf.InverseLerp(0, terrainResolution, j);
                var iLerp = i + Mathf.InverseLerp(0, terrainResolution, i);

                var pos = new Vector3(
                    jLerp / terrainResolution * terrain.terrainData.size.x,
                    heights[(int)Math.Round(i/multiplier), (int)Math.Round(j/multiplier)] * terrain.terrainData.size.y,
                    iLerp / terrainResolution * terrain.terrainData.size.z);

                vertices[index] = pos;
                index++;
            }
        }

        var triangles = new int[(terrainResolution - 1) * (terrainResolution - 1) * 6];
        index = 0;
        for (var i = 0; i < terrainResolution - 1; i++) {
            for (var j = 0; j < terrainResolution - 1; j++) {
                var a = i * terrainResolution + j;
                var b = (i + 1) * terrainResolution + j;
                var c = (i + 1) * terrainResolution + j + 1;
                var d = i * terrainResolution + j + 1;
                triangles[index++] = a;
                triangles[index++] = b;
                triangles[index++] = c;
                triangles[index++] = c;
                triangles[index++] = d;
                triangles[index++] = a;
            }
        }

        #if DEBUG
            MelonLogger.Msg($"[TerrainCollider] Added {terrain.name} TrisCount: {triangles.Length}");
        #endif

        // Transform if a function is provided
        if (transformFunc != null) {
            vertices = vertices.Select(transformFunc).ToArray();
        }

        Interop.CreateAndAppendSurfaces(outSurfaces, triangles, vertices, surfaceType, terrainType);
    }

    private static readonly Dictionary<PrimitiveType, Mesh> MeshesCache = new();

    private static Mesh GetPrimitiveMesh(PrimitiveType type) {
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
    private static readonly int PlayerCloneLayer = LayerMask.NameToLayer("PlayerClone");
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
            && col.GetComponentInChildren<CVRSM64Mario>() == null
            && col.GetComponentInParent<CVRSM64Mario>() == null
            // Ignore the some layers
            && col.gameObject.layer != PlayerCloneLayer
            && col.gameObject.layer != PlayerLocalLayer
            && col.gameObject.layer != PlayerNetworkLayer
            && col.gameObject.layer != IgnoreRaycastLayer
            && col.gameObject.layer != MirrorReflectionLayer
            && col.gameObject.layer != UILayer
            && col.gameObject.layer != UIInternalLayer
            // Ignore triggers
            && !col.isTrigger;
    }

    internal static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, BoxCollider b, SM64SurfaceType surfaceType, SM64TerrainType terrainType) {
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

        Interop.CreateAndAppendSurfaces(outSurfaces, tris, vertices, surfaceType, terrainType);
    }

    internal static void ScaleAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, BoxCollider b, SM64SurfaceType surfaceType, SM64TerrainType terrainType) {
        Vector3[] vertices = {
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(-b.size.x, -b.size.y, -b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(b.size.x, -b.size.y, -b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(b.size.x, b.size.y, -b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(-b.size.x, b.size.y, -b.size.z) * 0.5f),

            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(-b.size.x, b.size.y, b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(b.size.x, b.size.y, b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(b.size.x, -b.size.y, b.size.z) * 0.5f),
            Vector3.Scale(b.transform.lossyScale, b.center + new Vector3(-b.size.x, -b.size.y, b.size.z) * 0.5f),
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

        Interop.CreateAndAppendSurfaces(outSurfaces, tris, vertices, surfaceType, terrainType);
    }

    internal static Vector3 TransformPoint(SphereCollider s, Vector3 point) {
        return s.transform.TransformPoint(point * s.radius * 2 + s.center);
    }

    internal static Vector3 ScalePoint(SphereCollider s, Vector3 point) {
        return Vector3.Scale(s.transform.lossyScale, point * s.radius * 2 + s.center);
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

    internal static Vector3 ScalePoint(CapsuleCollider c, Vector3 point) {
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

        return Vector3.Scale(c.transform.lossyScale, alignedCapsule + c.center);
    }


    internal static Interop.SM64Surface[] GetAllStaticSurfaces() {
        var surfaces = new List<Interop.SM64Surface>();

        foreach (var obj in UnityEngine.Object.FindObjectsOfType<Collider>()) {

            var cvrSM64ColliderStatic = obj.GetComponent<CVRSM64ColliderStatic>();
            var hasDedicatedComponent = cvrSM64ColliderStatic != null;

            // Ignore bad colliders if we don't have a dedicated component
            if (!hasDedicatedComponent && !IsGoodCollider(obj)) continue;

            // Ignore the dynamic colliders, those will be handled separately
            var dynamicCollider = obj.GetComponent<CVRSM64ColliderDynamic>();
            if (dynamicCollider != null) continue;

            // Check if we have surface and terrain data
            var surfaceType = SM64SurfaceType.Default;
            var terrainType = SM64TerrainType.Grass;

            // Check if we have surface and terrain data
            if (hasDedicatedComponent) {
                surfaceType = cvrSM64ColliderStatic.SurfaceType;
                terrainType = cvrSM64ColliderStatic.TerrainType;
            }

            #if DEBUG
            //MelonLogger.Msg($"[GoodCollider] {obj.name}");
            #endif

            GetTransformedSurfaces(obj, surfaces, surfaceType, terrainType, hasDedicatedComponent);
        }

        return surfaces.ToArray();
    }

    internal static List<Interop.SM64Surface> GetTransformedSurfaces(Collider collider, List<Interop.SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, bool bypassPolygonLimit) {

        switch (collider) {
            case BoxCollider boxCollider:
                TransformAndGetSurfaces(surfaces, boxCollider, surfaceType, terrainType);
                break;
            case CapsuleCollider capsuleCollider:
                TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Capsule), surfaceType, terrainType, x => TransformPoint(capsuleCollider, x));
                break;
            case MeshCollider meshCollider:

                #if DEBUG
                MelonLogger.Msg($"[MeshCollider] {meshCollider.name} Readable: {meshCollider.sharedMesh.isReadable}, SubMeshCount: {meshCollider.sharedMesh.subMeshCount}, TrisCount: {meshCollider.sharedMesh.triangles.Length}");
                #endif

                if (!meshCollider.sharedMesh.isReadable) {
                    MelonLogger.Warning(
                        $"[MeshCollider] {meshCollider.name} Mesh is not readable, so we won't be able to use this as a collider for Mario :(");
                    return surfaces;
                }

                if (!bypassPolygonLimit && meshCollider.sharedMesh.triangles.Length > CVRSuperMario64.MeIgnoreCollidersHigherThanPolygons.Value) {
                    MelonLogger.Warning($"[MeshCollider] {meshCollider.name} has {meshCollider.sharedMesh.triangles.Length} triangles, " +
                                    $"which is more than the configured limit ({CVRSuperMario64.MeIgnoreCollidersHigherThanPolygons.Value}), so this mesh will be ignored!");
                    return surfaces;
                }

                // Todo: Handle when meshes are too big (colliders stop working).
                // Planes scaled to 60 60 60 will break. While 50 50 50 will work.

                TransformAndGetSurfaces(surfaces, meshCollider.sharedMesh, surfaceType, terrainType, x => meshCollider.transform.TransformPoint(x));
                break;
            case SphereCollider sphereCollider:
                TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Sphere), surfaceType, terrainType, x => TransformPoint(sphereCollider, x));
                break;
            case TerrainCollider terrainCollider:
                TransformAndGetSurfaces(surfaces, terrainCollider, surfaceType, terrainType, x => terrainCollider.transform.position + x);
                break;
            // Ignore other colliders as they would need more handling
        }

        return surfaces;
    }

    internal static List<Interop.SM64Surface> GetScaledSurfaces(Collider collider, List<Interop.SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, bool bypassPolygonLimit) {

            switch (collider) {
                case BoxCollider boxCollider:
                    ScaleAndGetSurfaces(surfaces, boxCollider, surfaceType, terrainType);
                    break;
                case CapsuleCollider capsuleCollider:
                    TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Capsule), surfaceType, terrainType, x => ScalePoint(capsuleCollider, x));
                    break;
                case MeshCollider meshCollider:

                    if (!meshCollider.sharedMesh.isReadable) {
                        MelonLogger.Warning($"[MeshCollider] {meshCollider.name} Mesh is not readable, so we won't be able to use this as a collider for Mario :(");
                        return surfaces;
                    }

                    if (!bypassPolygonLimit && meshCollider.sharedMesh.triangles.Length > CVRSuperMario64.MeIgnoreCollidersHigherThanPolygons.Value) {
                        MelonLogger.Warning($"[MeshCollider] {meshCollider.name} has {meshCollider.sharedMesh.triangles.Length} triangles, " +
                                        $"which is more than the configured limit ({CVRSuperMario64.MeIgnoreCollidersHigherThanPolygons.Value}), so this mesh will be ignored!");
                        return surfaces;
                    }

                    TransformAndGetSurfaces(surfaces, meshCollider.sharedMesh, surfaceType, terrainType, x => Vector3.Scale(meshCollider.transform.lossyScale, x));
                    break;
                case SphereCollider sphereCollider:
                    TransformAndGetSurfaces(surfaces, GetPrimitiveMesh(PrimitiveType.Sphere), surfaceType, terrainType, x => ScalePoint(sphereCollider, x));
                    break;

                case TerrainCollider terrainCollider:
                    TransformAndGetSurfaces(surfaces, terrainCollider, surfaceType, terrainType);
                    break;
            }

            return surfaces;
    }

    public enum MarioCapType {
        None,
        VanishCap,
        MetalCap,
        WingCap,
    }

    public static bool HasCapType(uint flags, MarioCapType capType) {
        switch (capType) {
            case MarioCapType.VanishCap: return (flags & (uint)CapFlags.MARIO_VANISH_CAP) != 0;
            case MarioCapType.MetalCap: return (flags & (uint)CapFlags.MARIO_METAL_CAP) != 0;
            case MarioCapType.WingCap: return (flags & (uint)CapFlags.MARIO_WING_CAP) != 0;
        }
        return capType == MarioCapType.None;
    }

    public static readonly Dictionary<SoundBitsKeys, uint> SoundBits = new() {
        { SoundBitsKeys.SOUND_GENERAL_COIN,              SoundArgLoad(3, 8, 0x11, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_WATER,        SoundArgLoad(3, 8, 0x12, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT,        SoundArgLoad(3, 0, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_2,      SoundArgLoad(3, 8, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_EU,     SoundArgLoad(3, 8, 0x30, 0x20, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_DROP,         SoundArgLoad(3, 0, 0x36, 0x40, 8) },
        { SoundBitsKeys.SOUND_GENERAL_RED_COIN,          SoundArgLoad(3, 0, 0x68, 0x90, 8) },
        { SoundBitsKeys.SOUND_MENU_COIN_ITS_A_ME_MARIO,  SoundArgLoad(7, 0, 0x14, 0x00, 8) },
        { SoundBitsKeys.SOUND_MENU_COLLECT_RED_COIN,     SoundArgLoad(7, 8, 0x28, 0x90, 8) },
    };

    private static uint SoundArgLoad(uint bank, uint playFlags, uint soundID, uint priority, uint flags2) {
        // Sound Magic Definition:
        // First Byte (Upper Nibble): Sound Bank (not the same as audio bank!)
        // First Byte (Lower Nibble): Bitflags for audio playback?
        // Second Byte: Sound ID
        // Third Byte: Priority
        // Fourth Byte (Upper Nibble): More bitflags
        // Fourth Byte (Lower Nibble): Sound Status (this is set to SOUND_STATUS_PLAYING when passed to the audio driver.)
        return (bank << 28) | (playFlags << 24) | (soundID << 16) | (priority << 8) | (flags2 << 4) | 1;
    }
}
