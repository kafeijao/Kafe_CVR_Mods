using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using eDmitriyAssets.NavmeshLinksGenerator;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Kafe.NavMeshTools;

internal class NavMeshTools : MelonMod {

    internal static NavMeshTools Instance;

    private NavMeshBuilderQueue _navMeshBuilderQueue;

    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaked = new();
    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaking = new();
    private readonly HashSet<NavMeshDataInstance> _currentWorldNavMeshDataInstances = new();
    private readonly Dictionary<API.Agent, HashSet<NavMeshLinkInstance>> _currentWorldNavMeshLinkInstances = new();

    private readonly List<Tuple<API.Agent, Action<int, bool>>> _queuedBakesForCurrentWorld = new();

    private NavMeshLinks_AutoPlacer _navMeshLinkAutoPlacer;

    public override void OnInitializeMelon() {

        _navMeshBuilderQueue = new NavMeshBuilderQueue();

        CVRGameEventSystem.World.OnLoad.AddListener(_ => {

            _currentWorldNavMeshAgentsBaked.Clear();
            _currentWorldNavMeshAgentsBaking.Clear();

            // Since we changed world, lets invalidate pending bakes
            foreach (var queuedBake in _queuedBakesForCurrentWorld) {
                CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, false);
            }
            _queuedBakesForCurrentWorld.Clear();

            var navMeshLinkAutoPlacerGo = new GameObject($"[{nameof(NavMeshTools)} Mod] NavMeshLinkAutoPlacer");
            _navMeshLinkAutoPlacer = navMeshLinkAutoPlacerGo.AddComponent<NavMeshLinks_AutoPlacer>();
        });

        CVRGameEventSystem.World.OnUnload.AddListener(_ => {
            // Clear all instances of nav mesh upon leaving the world
            foreach (var instance in _currentWorldNavMeshDataInstances) {
                NavMesh.RemoveNavMeshData(instance);
            }
            _currentWorldNavMeshDataInstances.Clear();

            // Clear all instances of nav mesh links upon leaving the world
            foreach (var (_, instances) in _currentWorldNavMeshLinkInstances) {
                foreach (var instance in instances) {
                    NavMesh.RemoveLink(instance);
                }
            }
            _currentWorldNavMeshLinkInstances.Clear();
        });

        Instance = this;
    }

    internal void RequestWorldBake(API.Agent agent, Action<int, bool> onBakeFinish, bool force) {

        // If this world was already baked and we're not forcing, tell the bake is done
        if (_currentWorldNavMeshAgentsBaked.Contains(agent) && !force) {
            CallResultsAction(onBakeFinish, agent.AgentTypeID, true);
            return;
        }

        // If is currently baking, make it wait for the current bake
        if (_currentWorldNavMeshAgentsBaking.Contains(agent)) {
            _queuedBakesForCurrentWorld.Add(new Tuple<API.Agent, Action<int, bool>>(agent, onBakeFinish));
            return;
        }

        // Otherwise just bake it!
        _currentWorldNavMeshAgentsBaking.Add(agent);

        var allSources = new List<NavMeshBuildSource>();
        var bounds = new Bounds(Vector3.zero, new Vector3(20000f, 20000f, 20000f));

        // Get the colliders we want to bake
        var allowedColliders = FixAndGetColliders();

        // This will collect all the sources in the bounds, including ones you may not want.
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), allSources);

        // Filter sources based on specific game objects or conditions.
        var filteredSources = allSources.Where(source => allowedColliders.Contains(source.component.gameObject)).ToList();

        _navMeshBuilderQueue.EnqueueNavMeshTask(MetaPort.Instance.CurrentWorldId, agent, filteredSources, bounds, onBakeFinish);
    }

    public override void OnApplicationQuit() {
        _navMeshBuilderQueue?.StopThread();
    }

    public override void OnUpdate() {
        if (!_navMeshBuilderQueue.BakeResults.TryDequeue(out var results)) return;

        // If we changed worlds, the bake is irrelevant...
        if (results.Item1 != MetaPort.Instance.CurrentWorldId) {
            CallResultsAction(results.Item4, results.Item2.AgentTypeID, false);
            return;
        }

        MelonLogger.Msg("Task done! Applying Nav Mesh data...");

        // Apply the bake results
        var navMeshDataInstance = NavMesh.AddNavMeshData(results.Item3);
        _currentWorldNavMeshDataInstances.Add(navMeshDataInstance);
        _currentWorldNavMeshAgentsBaked.Add(results.Item2);

        // Call the action of the original requester
        CallResultsAction(results.Item4, results.Item2.AgentTypeID, true);

        // Clear the currently baking flag
        _currentWorldNavMeshAgentsBaking.Remove(results.Item2);

        // Call other pending bakes for the current world and agent
        foreach (var queuedBake in _queuedBakesForCurrentWorld) {
            if (queuedBake.Item1 != results.Item2) continue;
            CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, true);
        }
        // Clear queue from those pending
        _queuedBakesForCurrentWorld.RemoveAll(qb => qb.Item1 != results.Item2);

        // // Auto-Generate NavMeshLinks
        // MelonLogger.Msg("Clearing previous NavMeshLinks...");
        // // Clear all instances of nav mesh links upon leaving the world if existent otherwise initialize the array
        // if (_currentWorldNavMeshLinkInstances.TryGetValue(results.Item2, out var instances)) {
        //     foreach (var instance in instances) {
        //         NavMesh.RemoveLink(instance);
        //     }
        //     instances.Clear();
        // }
        // else {
        //     _currentWorldNavMeshLinkInstances[results.Item2] = new HashSet<NavMeshLinkInstance>();
        // }
        // MelonLogger.Msg("Generating NavMeshLinks...");
        // _navMeshLinkAutoPlacer.Generate(results.Item2);
        // MelonLogger.Msg("\tFinished!");
    }

    internal static void CallResultsAction(Action<int, bool> onResults, int agentTypeID, bool result) {
        try {
            onResults?.Invoke(agentTypeID, result);
        }
        catch (Exception e) {
            MelonLogger.Error($"Error during the callback for finishing a bake... Check the StackTrace to see who's the culprit.");
            MelonLogger.Error(e);
        }
    }

    internal void AddNavMeshLink(API.Agent agent, NavMeshLinkData navMeshLinkData) {
        _currentWorldNavMeshLinkInstances[agent].Add(NavMesh.AddLink(navMeshLinkData));
    }

    private static HashSet<GameObject> FixAndGetColliders() {

        var colliders = new HashSet<GameObject>();

        var replacedColliderMeshes = new HashSet<string>();

        var runtimeSharedMeshes = new Dictionary<Mesh, Mesh>();

        foreach (var col in UnityEngine.Object.FindObjectsOfType<Collider>(true)) {

            // Ignore if the collider is in the DontDestroyOnLoad scene
            if (col.gameObject.scene.name is null or "DontDestroyOnLoad") continue;

            // Replace meshes without read/write
            if (col is MeshCollider meshCollider && meshCollider.sharedMesh != null && !meshCollider.sharedMesh.isReadable) {
                replacedColliderMeshes.Add(col.name);

                // Replace the non-readable with a readable one
                if (!runtimeSharedMeshes.TryGetValue(meshCollider.sharedMesh, out var readableMesh)) {
                    readableMesh = MakeReadableMeshCopy(meshCollider.sharedMesh);
                    runtimeSharedMeshes[meshCollider.sharedMesh] = readableMesh;
                }
                meshCollider.sharedMesh = readableMesh;
            }

            // Ignore bad colliders
            if (!IsGoodCollider(col)) continue;

            colliders.Add(col.gameObject);
        }

        MelonLogger.Msg($"Found {colliders.Count} good colliders to bake!");
        if (replacedColliderMeshes.Count > 0) {
            MelonLogger.Warning($"Replaced {replacedColliderMeshes.Count} mesh collider shared meshes that had their read/write disabled. " +
                                $"This might result in weird collision in certain worlds. " +
                                $"GameObject names: {string.Join(", ", replacedColliderMeshes)}");
        }
        return colliders;
    }

    private static readonly int UILayer = LayerMask.NameToLayer("UI");
    private static readonly int UIInternalLayer = LayerMask.NameToLayer("UI Internal");
    private static readonly int PlayerCloneLayer = LayerMask.NameToLayer("PlayerClone");
    private static readonly int PlayerLocalLayer = LayerMask.NameToLayer("PlayerLocal");
    private static readonly int PlayerNetworkLayer = LayerMask.NameToLayer("PlayerNetwork");
    private static readonly int IgnoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    private static readonly int MirrorReflectionLayer = LayerMask.NameToLayer("MirrorReflection");

    private static bool IsGoodCollider(Collider col) {
        var gameObject = col.gameObject;
        return
            // Ignore disabled
            col.enabled
            && gameObject.activeInHierarchy
            // Ignore colliders in pickup scripts
            && col.GetComponentInParent<CVRPickupObject>() == null
            // Ignore the some layers
            && gameObject.layer != PlayerCloneLayer
            && gameObject.layer != PlayerLocalLayer
            && gameObject.layer != PlayerNetworkLayer
            && gameObject.layer != IgnoreRaycastLayer
            && gameObject.layer != MirrorReflectionLayer
            && gameObject.layer != UILayer
            && gameObject.layer != UIInternalLayer
            // Ignore triggers
            && !col.isTrigger;
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
