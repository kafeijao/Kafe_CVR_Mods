using ABI_RC.Core.Player;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Kafe.NavMeshTools;

public static class Utils {
    
    public static bool ClampBounds(ref Bounds bounds) {
        
        var maxBoundsSize = ModConfig.MeMaxBakeBounds.Value;

        var adjusted = false;
        var center = bounds.center;
        var adjustedSize = bounds.size;

        if (bounds.size.x > maxBoundsSize) {
            adjustedSize.x = maxBoundsSize;
            adjusted = true;
        }
        if (bounds.size.y > maxBoundsSize / 5f) {
            adjustedSize.y = maxBoundsSize / 5f;
            adjusted = true;
        }
        if (bounds.size.z > maxBoundsSize) {
            adjustedSize.z = maxBoundsSize;
            adjusted = true;
        }

        if (adjusted) {
            // Update the bounds with the new size and move the center to the player.
            bounds.size = adjustedSize;
            var playerCameraTransform = PlayerSetup.Instance.activeCam.transform;
            bounds.center = playerCameraTransform.position;
        }

        return adjusted;
    }

    public static void CallResultsAction(Action<int, bool> onResults, int agentTypeID, bool result) {
        try {
            onResults?.Invoke(agentTypeID, result);
        }
        catch (Exception e) {
            MelonLogger.Error($"Error during the callback for finishing a bake... Check the StackTrace to see who's the culprit.");
            MelonLogger.Error(e);
        }
    }

    public static void ShowNavMeshVisualizer(GameObject gameObjectHolder, NavMeshTriangulation? navMeshTriangulation = null) {

        // Get the current mesh if not provided
        navMeshTriangulation ??= NavMesh.CalculateTriangulation();

        var meshToVisualize = new Mesh() {
            vertices = navMeshTriangulation.Value.vertices,
            triangles = navMeshTriangulation.Value.indices,
        };

        // Create navmesh visualization
        if (!gameObjectHolder.TryGetComponent<MeshFilter>(out var meshFilter)) {
            meshFilter = gameObjectHolder.AddComponent<MeshFilter>();
        }

        if (!gameObjectHolder.TryGetComponent<MeshRenderer>(out var meshRenderer)) {
            meshRenderer = gameObjectHolder.AddComponent<MeshRenderer>();
        }

        meshFilter.mesh = meshToVisualize;

        // Setup the material
        var noachiWireFrameMat = new Material(ModConfig.NoachiWireframeShader);
        noachiWireFrameMat.SetColor(ModConfig.MatWireColor, ModConfig.ColorBlue);
        noachiWireFrameMat.SetColor(ModConfig.MatFaceColor, ModConfig.ColorPinkTransparent);
        noachiWireFrameMat.SetFloat(ModConfig.MatWireThickness, 0.03f);
        meshRenderer.materials = new []{ noachiWireFrameMat };
    }

    public static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh) {

        var meshCopy = new Mesh {
            indexFormat = nonReadableMesh.indexFormat
        };

        // Handle vertices
        var verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
        var totalSize = verticesBuffer.stride * verticesBuffer.count;
        var data = new byte[totalSize];
        verticesBuffer.GetData(data);
        meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
        meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
        verticesBuffer.Release();

        // Handle triangles
        meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
        var indexesBuffer = nonReadableMesh.GetIndexBuffer();
        var tot = indexesBuffer.stride * indexesBuffer.count;
        var indexesData = new byte[tot];
        indexesBuffer.GetData(indexesData);
        meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
        meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
        indexesBuffer.Release();

        // Restore sub-mesh structure
        uint currentIndexOffset = 0;
        for (var i = 0; i < meshCopy.subMeshCount; i++) {
            var subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
            meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
            currentIndexOffset += subMeshIndexCount;
        }

        // Recalculate normals and bounds
        meshCopy.RecalculateNormals();
        meshCopy.RecalculateBounds();

        return meshCopy;
    }

}
