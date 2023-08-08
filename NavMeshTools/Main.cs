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

    private bool _isCurrentWorldNavMeshBaked;
    private bool _isCurrentWorldNavMeshBaking;

    private readonly List<Action<bool>> _queuedBakesForCurrentWorld = new();

    public override void OnInitializeMelon() {

        _navMeshBuilderQueue = new NavMeshBuilderQueue();

        _navMeshSettings = new NavMeshBuildSettings {
            agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID,
            agentRadius = 0.5f,
            agentHeight = 2.0f,
            agentSlope = 45.0f,
            agentClimb = 0.4f,
            tileSize = 512,
            minRegionArea = 2f,
        };

        CVRGameEventSystem.World.OnLoad.AddListener(worldGuid => {

            _isCurrentWorldNavMeshBaked = false;
            _isCurrentWorldNavMeshBaking = false;

            // Since we changed world, lets invalidate pending bakes
            foreach (var queuedBake in _queuedBakesForCurrentWorld) {
                CallResultsAction(queuedBake, false);
            }
            _queuedBakesForCurrentWorld.Clear();

        });

        Instance = this;
    }

    internal void RequestWorldBake(Action<bool> onBakeFinish, bool force) {

        // If this world was already baked and we're not forcing, tell the bake is done
        if (_isCurrentWorldNavMeshBaked && !force) {
            CallResultsAction(onBakeFinish, true);
            return;
        }

        // If is currently baking, make it wait for the current bake
        if (_isCurrentWorldNavMeshBaking) {
            _queuedBakesForCurrentWorld.Add(onBakeFinish);
            return;
        }

        // Otherwise just bake it!
        _isCurrentWorldNavMeshBaking = true;

        var allSources = new List<NavMeshBuildSource>();
        var bounds = new Bounds(Vector3.zero, new Vector3(2000f, 2000f, 2000f));

        // MelonLogger.Msg("Collecting Sources...");

        // This will collect all the sources in the bounds, including ones you may not want.
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), allSources);

        // MelonLogger.Msg("Getting all good colliders...");

        var allowedColliders = GetColliders();

        // MelonLogger.Msg("Filtering good colliders...");

        // Filter sources based on specific game objects or conditions.
        var filteredSources = allSources.Where(source => allowedColliders.Contains(source.component.gameObject)).ToList();

        // MelonLogger.Msg("Queuing Task...");

        _navMeshBuilderQueue.EnqueueNavMeshTask(MetaPort.Instance.CurrentWorldId, _navMeshSettings, filteredSources, bounds, onBakeFinish);
    }

    public override void OnApplicationQuit() {
        _navMeshBuilderQueue.StopThread();
    }

    public override void OnUpdate() {
        if (!_navMeshBuilderQueue.BakeResults.TryDequeue(out var results)) return;

        // If we changed worlds, the bake is irrelevant...
        if (results.Item1 != MetaPort.Instance.CurrentWorldId) {
            CallResultsAction(results.Item3, false);
            return;
        }

        MelonLogger.Msg("Task done! Applying Nav Mesh data...");

        // Apply the bake results
        NavMesh.AddNavMeshData(results.Item2);
        _isCurrentWorldNavMeshBaked = true;

        MelonLogger.Msg("Finished!");

        // Call the action of the original requester
        CallResultsAction(results.Item3, true);

        // Call other pending bakes for the current world
        foreach (var queuedBake in _queuedBakesForCurrentWorld) {
            CallResultsAction(queuedBake, true);
        }
        _queuedBakesForCurrentWorld.Clear();
    }

    internal static void CallResultsAction(Action<bool> onResults, bool result) {
        try {
            onResults?.Invoke(result);
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
    private NavMeshBuildSettings _navMeshSettings;

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
