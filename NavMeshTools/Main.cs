using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
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
    private readonly Dictionary<API.Agent, HashSet<GameObject>> _currentWorldNavMeshLinkVisualizers = new();

    private readonly List<Tuple<API.Agent, Action<int, bool>>> _queuedBakesForCurrentWorld = new();

    internal static GameObject NavMeshToolsGo;
    internal static NavMeshLinksGenerator NavMeshLinkGenerator;

    public override void OnInitializeMelon() {

        // Load the asset bundle
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        _navMeshBuilderQueue = new NavMeshBuilderQueue();

        CVRGameEventSystem.World.OnLoad.AddListener(_ => {

            _currentWorldNavMeshAgentsBaked.Clear();
            _currentWorldNavMeshAgentsBaking.Clear();

            // Since we changed world, lets invalidate pending bakes
            foreach (var queuedBake in _queuedBakesForCurrentWorld) {
                CallResultsAction(queuedBake.Item2, queuedBake.Item1.AgentTypeID, false);
            }
            _queuedBakesForCurrentWorld.Clear();

            #if DEBUG
            // Clear all existing nav meshes (so we can see the new one easier)
            NavMesh.RemoveAllNavMeshData();
            MelonLogger.Warning("Removing all Nav Mesh Data that was bundled in the world. So our visualizers don't have overlaps.");
            #endif

        });

        CVRGameEventSystem.World.OnUnload.AddListener(_ => {

            // Clear all instances of nav mesh upon leaving the world
            foreach (var instance in _currentWorldNavMeshDataInstances) {
                NavMesh.RemoveNavMeshData(instance);
            }
            _currentWorldNavMeshDataInstances.Clear();

            // Clear NavMeshVisualizer (if present)
            if (NavMeshToolsGo.TryGetComponent<MeshFilter>(out var meshFilter)) {
                UnityEngine.Object.Destroy(meshFilter);
            }
            if (NavMeshToolsGo.TryGetComponent<MeshRenderer>(out var meshRenderer)) {
                UnityEngine.Object.Destroy(meshRenderer);
            }

            // Clear all instances of nav mesh links upon leaving the world
            foreach (var (_, instances) in _currentWorldNavMeshLinkInstances) {
                foreach (var instance in instances) {
                    NavMesh.RemoveLink(instance);
                }
            }
            _currentWorldNavMeshLinkInstances.Clear();

            #if DEBUG
            // Clear all instances of link visualizers upon leaving the world
            foreach (var (_, linkVisualizers) in _currentWorldNavMeshLinkVisualizers) {
                foreach (var linkVisualizer in linkVisualizers) {
                    UnityEngine.Object.Destroy(linkVisualizer);
                }
            }
            _currentWorldNavMeshLinkVisualizers.Clear();
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

        _navMeshBuilderQueue.EnqueueNavMeshTask(MetaPort.Instance.CurrentWorldId, agent, filteredSources, allowedColliders.bounds, onBakeFinish);
    }

    public override void OnApplicationQuit() {
        _navMeshBuilderQueue?.StopThread();
    }

    public override void OnUpdate() {

        // Handle Bake results if present
        if (_navMeshBuilderQueue.BakeResults.TryDequeue(out var navMeshResult)) {
            HandleNavMeshBake(navMeshResult);
        }

        // Handle Nav Mesh Link Generation results if present
        if (_navMeshBuilderQueue.GeneratedLinksResults.TryDequeue(out var linksResult)) {
            HandleNavMeshLinkGen(linksResult);
        }
    }

    private void HandleNavMeshBake(Tuple<string, API.Agent, NavMeshData, Action<int, bool>> results) {

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

        // Queue Nav Mesh Link Generation
        MelonLogger.Msg("Queuing NavMeshLinks Generation...");

        var triangulatedNavMesh = NavMesh.CalculateTriangulation();

        var currentNavMesh = new Mesh() {
            vertices = triangulatedNavMesh.vertices,
            triangles = triangulatedNavMesh.indices
        };

        // Create the nav mesh visualizer if in DEBUG mode
        #if DEBUG
        ShowNavMeshVisualizer(currentNavMesh);
        #endif

        // Queue the task to generate the nav mesh links (if requested to do so)
        if (results.Item2.GenerateNavMeshLinks) {
            _navMeshBuilderQueue.EnqueueNavMeshLinkTask(MetaPort.Instance.CurrentWorldId, results.Item2, currentNavMesh);
        }
    }

    public static void ShowNavMeshVisualizer(Mesh meshToVisualize = null) {

        // Get the current mesh if not provided
        if (meshToVisualize == null) {
            var triangulatedNavMesh = NavMesh.CalculateTriangulation();
            meshToVisualize = new Mesh() {
                vertices = triangulatedNavMesh.vertices,
                triangles = triangulatedNavMesh.indices
            };
        }

        // Create navmesh visualization
        if (!NavMeshToolsGo.TryGetComponent<MeshFilter>(out var meshFilter)) {
            meshFilter = NavMeshToolsGo.AddComponent<MeshFilter>();
        }

        if (!NavMeshToolsGo.TryGetComponent<MeshRenderer>(out var meshRenderer)) {
            meshRenderer = NavMeshToolsGo.AddComponent<MeshRenderer>();
        }

        meshFilter.mesh = meshToVisualize;

        // var unlitShader = Shader.Find("Unlit/Color");
        // var unlitMat = new Material(unlitShader) {
        //     color = new Color(0.2f, 0.5f, 1f)
        // };
        //
        // var neitriMat = new Material(ModConfig.NeitriDistanceFadeOutlineShader);
        // neitriMat.SetFloat(ModConfig.MatOutlineWidth, 0.8f);
        // neitriMat.SetFloat(ModConfig.MatOutlineSmoothness, 0.1f);
        // neitriMat.SetFloat(ModConfig.MatFadeInBehindObjectsDistance, 2f);
        // neitriMat.SetFloat(ModConfig.MatFadeOutBehindObjectsDistance, 10f);
        // neitriMat.SetFloat(ModConfig.MatFadeInCameraDistance, 10f);
        // neitriMat.SetFloat(ModConfig.MatFadeOutCameraDistance, 15f);
        // neitriMat.SetFloat(ModConfig.MatShowOutlineInFrontOfObjects, 0f);
        // neitriMat.SetColor(ModConfig.MatOutlineColor, ModConfig.ColorBlue);

        // var transparentMat = new Material(Shader.Find("Standard"));
        // transparentMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        // transparentMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // transparentMat.SetInt("_ZWrite", 0);
        // transparentMat.DisableKeyword("_ALPHATEST_ON");
        // transparentMat.DisableKeyword("_ALPHABLEND_ON");
        // transparentMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        // transparentMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // transparentMat.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        // meshRenderer.materials = new []{ unlitMat, neitriMat };


        var noachiWireFrameMat = new Material(ModConfig.NoachiWireframeShader);
        noachiWireFrameMat.SetColor(ModConfig.MatWireColor, ModConfig.ColorBlue);
        noachiWireFrameMat.SetColor(ModConfig.MatFaceColor, ModConfig.ColorPinkTransparent);
        noachiWireFrameMat.SetFloat(ModConfig.MatWireThickness, 0.03f);
        meshRenderer.materials = new []{ noachiWireFrameMat };
    }

    private void HandleNavMeshLinkGen(Tuple<string, API.Agent, HashSet<NavMeshLinkData>, HashSet<LinkVisualizer>> results) {

        var agent = results.Item2;

        // If we changed worlds, the bake is irrelevant... also check if there is another bake in progress (which also invalidates this)
        if (results.Item1 != MetaPort.Instance.CurrentWorldId
            || _queuedBakesForCurrentWorld.Exists(b => b.Item1 == agent)
            || _navMeshBuilderQueue.CurrentNavMeshGeneratingAgent == agent) {
            return;
        }

        // Auto-Generate NavMeshLinks
        MelonLogger.Msg("Clearing previous NavMeshLinks...");
        // Clear all previous instances of nav mesh links if existent otherwise initialize the list
        if (_currentWorldNavMeshLinkInstances.TryGetValue(agent, out var instances)) {
            foreach (var instance in instances) {
                NavMesh.RemoveLink(instance);
            }
            instances.Clear();
        }
        else {
            _currentWorldNavMeshLinkInstances[agent] = new HashSet<NavMeshLinkInstance>();
        }

        // Actually add the nav mesh link data to the nav mesh
        foreach (var navMeshLinkData in results.Item3) {
            _currentWorldNavMeshLinkInstances[agent].Add(NavMesh.AddLink(navMeshLinkData));
        }

        #if DEBUG
        // Clear odl and setup new line visualizers
        if (_currentWorldNavMeshLinkVisualizers.TryGetValue(agent, out var linkVisualizers)) {
            foreach (var linkVisualizer in linkVisualizers) {
                UnityEngine.Object.Destroy(linkVisualizer);
            }
            linkVisualizers.Clear();
        }
        else {
            _currentWorldNavMeshLinkVisualizers[agent] = new HashSet<GameObject>();
        }
        foreach (var linkVisualizer in results.Item4) {
            _currentWorldNavMeshLinkVisualizers[agent].Add(linkVisualizer.Instantiate());
        }
        #endif
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

    private static (HashSet<GameObject> gameObjectsToUse, Bounds bounds) FixAndGetColliders() {

        var colliders = new HashSet<GameObject>();
        var totalBounds = new Bounds();

        var replacedColliderMeshes = new HashSet<string>();
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
                    readableMesh = MakeReadableMeshCopy(meshCollider.sharedMesh);
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

        MelonLogger.Msg($"Found {colliders.Count} good colliders to bake!");
        if (replacedColliderMeshes.Count > 0) {
            MelonLogger.Warning($"Replaced {replacedColliderMeshes.Count} mesh collider shared meshes that had their read/write disabled. " +
                                $"This might result in weird collision in certain worlds. " +
                                $"GameObject names: {string.Join(", ", replacedColliderMeshes)}");
        }
        return (colliders, totalBounds);
    }

    internal static readonly int DefaultLayer = LayerMask.NameToLayer("Default");
    internal static readonly int UILayer = LayerMask.NameToLayer("UI");
    internal static readonly int UIInternalLayer = LayerMask.NameToLayer("UI Internal");
    internal static readonly int PlayerCloneLayer = LayerMask.NameToLayer("PlayerClone");
    internal static readonly int PlayerLocalLayer = LayerMask.NameToLayer("PlayerLocal");
    internal static readonly int PlayerNetworkLayer = LayerMask.NameToLayer("PlayerNetwork");
    internal static readonly int IgnoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
    internal static readonly int MirrorReflectionLayer = LayerMask.NameToLayer("MirrorReflection");

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

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start() {
            try {
                NavMeshToolsGo = new GameObject($"[{nameof(NavMeshTools)} Mod] NavMeshLinkAutoPlacer");
                UnityEngine.Object.DontDestroyOnLoad(NavMeshToolsGo);
                NavMeshLinkGenerator = NavMeshToolsGo.AddComponent<NavMeshLinksGenerator>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
