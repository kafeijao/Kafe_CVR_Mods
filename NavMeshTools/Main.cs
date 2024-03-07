using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshTools : MelonMod {

    internal static NavMeshTools Instance;

    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaked = new();
    private readonly HashSet<API.Agent> _currentWorldNavMeshAgentsBaking = new();

    private readonly List<Tuple<API.Agent, Action<int, bool>>> _queuedBakesForCurrentWorld = new();

    public override void OnInitializeMelon() {

        // Load melon prefs
        ModConfig.InitializeMelonPrefs();

        // Load the asset bundle
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);


        CVRGameEventSystem.World.OnLoad.AddListener(_ => {

            _currentWorldNavMeshAgentsBaked.Clear();
            _currentWorldNavMeshAgentsBaking.Clear();

            // Since we changed world, lets invalidate pending bakes
            foreach (var queuedBake in _queuedBakesForCurrentWorld) {
                Utils.CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, false);
            }
            _queuedBakesForCurrentWorld.Clear();

            #if DEBUG
            // Clear all existing nav meshes (so we can see the new one easier)
            NavMesh.RemoveAllNavMeshData();
            MelonLogger.Warning("Removing all Nav Mesh Data that was bundled in the world. So our visualizers don't have overlaps.");
            #endif

        });

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging, performance overhead, or weird visualizers...");
        #endif

        Instance = this;
    }

    internal void RequestWorldBake(API.Agent agent, Action<int, bool> onBakeFinish, bool force) {

        // If this world was already baked and we're not forcing, tell the bake is done
        if (_currentWorldNavMeshAgentsBaked.Contains(agent) && !force) {
            Utils.CallResultsAction(onBakeFinish, agent.AgentTypeID, true);
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

        // Get the colliders we want to bake
        var allowedColliders = FixAndGetColliders();

        // Check for violations for this specific agent for this bake
        var agentSettings = agent.Settings;
        var violations = agentSettings.ValidationReport(allowedColliders.bounds);
        if (violations.Length > 0) {
            MelonLogger.Warning($"Navmesh settings violations:\n\t{string.Join("\n\t", violations)}");
        }

        // This will collect all the sources in the bounds, including ones you may not want.
        NavMeshBuilder.CollectSources(allowedColliders.bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), allSources);

        // Filter sources based on specific game objects or conditions.
        var filteredSources = allSources.Where(source => allowedColliders.gameObjectsToUse.Contains(source.component.gameObject)).ToList();

        var previousBoundsSize = allowedColliders.bounds.size;
        if (Utils.ClampBounds(ref allowedColliders.bounds)) {
            MelonLogger.Warning($"The bounds for this world are too big clamping the nav mesh size... " +
                                $"Size: {previousBoundsSize.ToString("F2")} -> {allowedColliders.bounds.size.ToString("F2")}");
        }

        void OnFinishBake(NavMeshBakePipeline.BakerPayload payload, bool succeeded) {

            // Handle the baked and baking collections
            if (succeeded) {
                _currentWorldNavMeshAgentsBaked.Add(payload.Agent);
            }
            _currentWorldNavMeshAgentsBaking.Remove(payload.Agent);

            // Call the handler for finishing the bake
            onBakeFinish?.Invoke(payload.Agent.AgentTypeID, succeeded);

            // Call the queued handlers for finishing the bake
            var agentQueuedBakes = _queuedBakesForCurrentWorld.Where(q => q.Item1 == payload.Agent).ToArray();
            foreach (var queuedBake in agentQueuedBakes) {
                _queuedBakesForCurrentWorld.Remove(queuedBake);
                Utils.CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, succeeded);
            }
        }

        MelonLogger.Msg($"Queuing Nav Mesh Bake for Agent id {agent.AgentTypeID}...");
        NavMeshBakePipeline.QueueBakeWorkload(agent, OnFinishBake, MetaPort.Instance.CurrentWorldId, filteredSources, allowedColliders.bounds);
    }

    private static (HashSet<GameObject> gameObjectsToUse, Bounds bounds) FixAndGetColliders() {

        var colliders = new HashSet<GameObject>();
        var totalBounds = new Bounds();

        var replacedColliderMeshes = new HashSet<string>();
        var replacedColliderMeshesFailedNoBuffer = new HashSet<string>();
        var runtimeSharedMeshes = new Dictionary<Mesh, Mesh>();

        var boundsInitialized = false;

        foreach (var col in UnityEngine.Object.FindObjectsOfType<Collider>(true)) {

            // Ignore if the collider is in the DontDestroyOnLoad scene
            if (col.gameObject.scene.name is null or "DontDestroyOnLoad") continue;

            // Replace meshes without read/write
            if (col is MeshCollider meshCollider && meshCollider.sharedMesh != null && !meshCollider.sharedMesh.isReadable) {
                replacedColliderMeshes.Add(col.name);

                // Replace the non-readable with a readable one
                if (!runtimeSharedMeshes.TryGetValue(meshCollider.sharedMesh, out var readableMesh)) {

                    // Prevent shared meshes without vertex buffers
                    if (meshCollider.sharedMesh.vertexBufferCount <= 0) {
                        replacedColliderMeshesFailedNoBuffer.Add(meshCollider.sharedMesh.name);
                        continue;
                    }

                    readableMesh = Utils.MakeReadableMeshCopy(meshCollider.sharedMesh);
                    runtimeSharedMeshes[meshCollider.sharedMesh] = readableMesh;
                }

                meshCollider.sharedMesh = readableMesh;
            }

            // Ignore bad colliders
            if (!IsGoodCollider(col)) continue;

            colliders.Add(col.gameObject);

            // Calculate total bounds
            if (!boundsInitialized) {
                totalBounds = col.bounds;
                boundsInitialized = true;
            }
            else {
                totalBounds.Encapsulate(col.bounds);
            }
        }

        if (replacedColliderMeshes.Count > 0) {
            MelonLogger.Warning($"Replaced {replacedColliderMeshes.Count} mesh collider shared meshes that had their read/write disabled. \n" +
                                $"Ignored {replacedColliderMeshesFailedNoBuffer.Count} mesh colliders because they had no vertex buffer #0. \n"+
                                $"This might result in weird collision in certain worlds. " +
                                $"GameObject names: {string.Join(", ", replacedColliderMeshes)}");
        }
        return (colliders, totalBounds);
    }

    private static bool IsGoodCollider(Collider col) {
        var gameObject = col.gameObject;
        var rb = col.attachedRigidbody;
        return
            // Ignore disabled
            col.enabled
            && gameObject.activeInHierarchy
            // Ignore colliders in pickup scripts
            && col.GetComponentInParent<CVRPickupObject>() == null
            // Ignore the some layers
            && gameObject.layer != CVRLayers.PlayerClone
            && gameObject.layer != CVRLayers.PlayerLocal
            && gameObject.layer != CVRLayers.PlayerNetwork
            && gameObject.layer != CVRLayers.IgnoreRaycast
            && gameObject.layer != CVRLayers.CVRReserved3
            && gameObject.layer != CVRLayers.UI
            && gameObject.layer != CVRLayers.UIInternal
            // Ignore triggers
            && !col.isTrigger
            // Ignore movable colliders
            && rb == null;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start() {
            try {
                NavMeshBakePipeline.Initialize();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
