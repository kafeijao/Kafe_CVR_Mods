using System.Collections;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

public static class NavMeshBakePipeline {

    private static GameObject _navMeshToolsGo;
    private static MainThreadExecutor _bakerExecutor;

    private static readonly HashSet<NavMeshDataInstance> CurrentWorldNavMeshDataInstances = new();
    private static readonly Dictionary<API.Agent, HashSet<NavMeshLinkInstance>> CurrentWorldNavMeshLinkInstances = new();
    private static readonly Dictionary<API.Agent, HashSet<GameObject>> CurrentWorldNavMeshLinkVisualizers = new();

    public static void Initialize() {

        // Initialize our component for the mod management
        _navMeshToolsGo = new GameObject($"[{nameof(NavMeshTools)} Mod]");
        UnityEngine.Object.DontDestroyOnLoad(_navMeshToolsGo);
        _bakerExecutor = _navMeshToolsGo.AddComponent<MainThreadExecutor>();

        CVRGameEventSystem.World.OnUnload.AddListener(_ => {

            // Clear all instances of nav mesh upon leaving the world
            foreach (var instance in CurrentWorldNavMeshDataInstances) {
                NavMesh.RemoveNavMeshData(instance);
            }
            CurrentWorldNavMeshDataInstances.Clear();

            // Clear NavMeshVisualizer (if present)
            if (_navMeshToolsGo.TryGetComponent<MeshFilter>(out var meshFilter)) {
                UnityEngine.Object.Destroy(meshFilter);
            }
            if (_navMeshToolsGo.TryGetComponent<MeshRenderer>(out var meshRenderer)) {
                UnityEngine.Object.Destroy(meshRenderer);
            }

            // Clear all instances of nav mesh links upon leaving the world
            foreach (var (_, instances) in CurrentWorldNavMeshLinkInstances) {
                foreach (var instance in instances) {
                    NavMesh.RemoveLink(instance);
                }
            }
            CurrentWorldNavMeshLinkInstances.Clear();

            #if DEBUG
            // Clear all instances of link visualizers upon leaving the world
            foreach (var (_, linkVisualizers) in CurrentWorldNavMeshLinkVisualizers) {
                foreach (var linkVisualizer in linkVisualizers) {
                    UnityEngine.Object.Destroy(linkVisualizer);
                }
            }
            CurrentWorldNavMeshLinkVisualizers.Clear();
            #endif
        });
    }

    public static void QueueBakeWorkload(API.Agent agent, Action<BakerPayload, bool> onBakeFinishCallback, string worldGuid, List<NavMeshBuildSource> sources, Bounds bounds) {

        var payload = new BakerPayload(agent, onBakeFinishCallback, worldGuid, sources, bounds);
        var tasks = new List<(Delegate, bool)> {
            ((MainThreadExecutor.Workload.TaskDelegate) BakeNavMesh, false),
            ((MainThreadExecutor.Workload.CoroutineTaskDelegate) ApplyNavMesh, true),
        };

        if (agent.GenerateNavMeshLinks) {
            tasks.Add(((MainThreadExecutor.Workload.TaskDelegate) CalculateBoundaryEdges, false));
            tasks.Add(((MainThreadExecutor.Workload.CoroutineTaskDelegate) GenerateAndPlaceMeshLinks, true));
        }

        _bakerExecutor.AddWorkload(new MainThreadExecutor.Workload(tasks, payload, OnFinish));
    }

    private static void BakeNavMesh(BakerPayload payload) {
        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(BakeNavMesh)}] [{payload.Agent.AgentTypeID}] Starting...");
        #endif

        var result = NavMeshBuilder.BuildNavMeshData(payload.Agent.Settings, payload.BakeSources, payload.BakeBounds, Vector3.zero, Quaternion.identity);
        payload.NavMeshData = result;

        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(BakeNavMesh)}] [{payload.Agent.AgentTypeID}] Done!");
        #endif
    }

    private static IEnumerator ApplyNavMesh(BakerPayload payload) {
        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(ApplyNavMesh)}] [{payload.Agent.AgentTypeID}] Starting...");
        #endif

        // If we changed worlds, the bake is irrelevant...
        if (payload.WorldGuid != MetaPort.Instance.CurrentWorldId || payload.NavMeshData == null) {
            MelonLogger.Warning($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(ApplyNavMesh)}] [{payload.Agent.AgentTypeID}] Done! (Changed world)");
            throw new Exception("Changed worlds while baking the Nav Mesh or payload.NavMeshData == null, interrupting and discarding current task!");
        }

        // Apply the bake results
        var navMeshDataInstance = NavMesh.AddNavMeshData(payload.NavMeshData);
        CurrentWorldNavMeshDataInstances.Add(navMeshDataInstance);

        // Create a mesh of the current nav mesh (either for debugging or mesh link generation
        payload.Triangulation = NavMesh.CalculateTriangulation();


        // Create the nav mesh visualizer if in DEBUG mode
        #if DEBUG
        Utils.ShowNavMeshVisualizer(_navMeshToolsGo, payload.Triangulation);
        #endif

        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(ApplyNavMesh)}] [{payload.Agent.AgentTypeID}] Done!");
        #endif
        yield break;
    }

    private static void CalculateBoundaryEdges(BakerPayload payload) {
        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(CalculateBoundaryEdges)}] [{payload.Agent.AgentTypeID}] Starting...");
        #endif

        // Weld the triangulation vertices to have a cleaner boundary edge detection
        var weldedMesh = MeshBoundaryFinder.WeldVertices(payload.Triangulation.vertices, payload.Triangulation.indices);
        // Calculate the possible edges
        var edges = MeshBoundaryFinder.FindBoundaryEdges(weldedMesh);
        // Calculate the possible sample points
        payload.SamplePoints = NavMeshLinksGenerator.GenerateSamplePoints(payload.Agent, edges, in payload.LinkVisualizers);

        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(CalculateBoundaryEdges)}] [{payload.Agent.AgentTypeID}] Done!");
        #endif
    }

    private static IEnumerator GenerateAndPlaceMeshLinks(BakerPayload payload) {
        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(GenerateAndPlaceMeshLinks)}] [{payload.Agent.AgentTypeID}] Starting...");
        #endif

        // If we changed worlds, the nav mesh links are irrelevant...
        if (payload.WorldGuid != MetaPort.Instance.CurrentWorldId) {
            MelonLogger.Warning($"Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(GenerateAndPlaceMeshLinks)}] [{payload.Agent.AgentTypeID}] Done! (changed world)");
            throw new Exception("Changed worlds while generating the nav mesh links, interrupting and discarding current task!");
        }

        // Place the links on the NavMesh (pretty heavy, so it's process in batches across frames)
        yield return NavMeshLinksGenerator.PlaceLinks(payload.Agent, payload.SamplePoints, payload.NavMeshLinkResults, payload.LinkVisualizers);

        // Auto-Generate NavMeshLinks

        // Clear all previous instances of nav mesh links if existent otherwise initialize the list
        if (CurrentWorldNavMeshLinkInstances.TryGetValue(payload.Agent, out var instances)) {
            foreach (var instance in instances) {
                NavMesh.RemoveLink(instance);
            }
            instances.Clear();
        }
        else {
            CurrentWorldNavMeshLinkInstances[payload.Agent] = new HashSet<NavMeshLinkInstance>();
        }

        // Actually add the nav mesh link data to the nav mesh
        foreach (var navMeshLinkData in payload.NavMeshLinkResults) {
            CurrentWorldNavMeshLinkInstances[payload.Agent].Add(NavMesh.AddLink(navMeshLinkData));
        }

        #if DEBUG
        // Clear old and setup new line visualizers
        if (CurrentWorldNavMeshLinkVisualizers.TryGetValue(payload.Agent, out var linkVisualizers)) {
            foreach (var linkVisualizer in linkVisualizers) {
                UnityEngine.Object.Destroy(linkVisualizer);
            }
            linkVisualizers.Clear();
        }
        else {
            CurrentWorldNavMeshLinkVisualizers[payload.Agent] = new HashSet<GameObject>();
        }
        foreach (var linkVisualizer in payload.LinkVisualizers) {
            CurrentWorldNavMeshLinkVisualizers[payload.Agent].Add(linkVisualizer.Instantiate(_navMeshToolsGo));
        }
        #endif

        #if DEBUG
        MelonLogger.Msg($"Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(GenerateAndPlaceMeshLinks)}] [{payload.Agent.AgentTypeID}] Done!");
        #endif
    }

    private static void OnFinish(BakerPayload payload, bool finishedWithSuccess) {
        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(OnFinish)}] [{payload.Agent.AgentTypeID}] Starting...");
        #endif

        payload.OnFinishCallback.Invoke(payload, finishedWithSuccess);

        #if DEBUG
        MelonLogger.Msg($"[Thread {Thread.CurrentThread.ManagedThreadId}] [{nameof(OnFinish)}] [{payload.Agent.AgentTypeID}] Done!");
        #endif
    }

    public class BakerPayload {

        public readonly string WorldGuid;
        public readonly API.Agent Agent;
        public readonly Action<BakerPayload, bool> OnFinishCallback;
        public readonly List<NavMeshBuildSource> BakeSources;
        public readonly Bounds BakeBounds;

        public NavMeshData NavMeshData;
        public NavMeshTriangulation Triangulation;

        public List<(Vector3 placePos, Vector3 edgeNormal)> SamplePoints = new();

        public readonly List<NavMeshLinkData> NavMeshLinkResults = new();

        public readonly List<LinkVisualizer> LinkVisualizers = new();

        public BakerPayload(API.Agent agent, Action<BakerPayload, bool> onFinishCallback, string worldGuid, List<NavMeshBuildSource> bakeSources, Bounds bakeBounds) {
            Agent = agent;
            OnFinishCallback = onFinishCallback;
            WorldGuid = worldGuid;
            BakeSources = bakeSources;
            BakeBounds = bakeBounds;
        }
    }
}
