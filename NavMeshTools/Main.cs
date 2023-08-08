using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshTools : MelonMod {

    internal static NavMeshTools Instance;

    private NavMeshBuilderQueue _navMeshBuilderQueue;

    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaked = new();
    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaking = new();
    private readonly HashSet<NavMeshDataInstance> _currentWorldNavMeshDataInstances = new();

    private readonly List<Tuple<API.Agent, Action<int, bool>>> _queuedBakesForCurrentWorld = new();

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

        });

        CVRGameEventSystem.World.OnUnload.AddListener(_ => {
            // Clear all instances of nav mesh upon leaving the world
            foreach (var instance in _currentWorldNavMeshDataInstances) {
                NavMesh.RemoveNavMeshData(instance);
            }
            _currentWorldNavMeshDataInstances.Clear();
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
        var bounds = new Bounds(Vector3.zero, new Vector3(2000f, 2000f, 2000f));

        // This will collect all the sources in the bounds, including ones you may not want.
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), allSources);

        var allowedColliders = GetColliders();

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

        MelonLogger.Msg("Finished!");

        // Call the action of the original requester
        CallResultsAction(results.Item4, results.Item2.AgentTypeID, true);

        // Call other pending bakes for the current world
        foreach (var queuedBake in _queuedBakesForCurrentWorld) {
            CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, true);
        }
        _queuedBakesForCurrentWorld.Clear();
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

    private static HashSet<GameObject> GetColliders() {

        var colliders = new HashSet<GameObject>();

        var meshesNotReadable = new List<string>();

        foreach (var col in UnityEngine.Object.FindObjectsOfType<Collider>(true)) {

            // Ignore if the collider is in the DontDestroyOnLoad scene
            if (col.gameObject.scene.name is null or "DontDestroyOnLoad") continue;

            // Ignore meshes without read/write
            if (col is MeshCollider meshCollider && meshCollider.sharedMesh != null && !meshCollider.sharedMesh.isReadable) {
                meshesNotReadable.Add(col.name);
                continue;
            }

            // Ignore bad colliders
            if (!IsGoodCollider(col)) continue;

            colliders.Add(col.gameObject);
        }

        MelonLogger.Msg($"Found {colliders.Count} good colliders to bake!");
        if (meshesNotReadable.Count > 0) {
            MelonLogger.Warning($"Unfortunately ignored {meshesNotReadable.Count} mesh colliders that had their read/write disabled. GameObject names: {string.Join(", ", meshesNotReadable)}");
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
}
