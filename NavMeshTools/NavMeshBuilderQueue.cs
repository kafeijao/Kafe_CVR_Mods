using System.Collections.Concurrent;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshBuilderQueue {

    private readonly Thread _navMeshWorkerThread;
    private readonly BlockingCollection<Tuple<string, API.Agent, Func<NavMeshData>, Action<int, bool>>> _queueToBake = new();
    internal readonly ConcurrentQueue<Tuple<string, API.Agent, NavMeshData, Action<int, bool>>> BakeResults = new();

    private readonly Thread _navMeshLinksWorkerThread;
    internal volatile API.Agent CurrentNavMeshGeneratingAgent;
    private readonly BlockingCollection<Tuple<string, API.Agent, (Vector3[] vertices, int[] triangles)>> _queueToGenerateLinks = new();
    internal readonly ConcurrentQueue<Tuple<string, API.Agent, HashSet<NavMeshLinkData>, HashSet<LinkVisualizer>>> GeneratedLinksResults = new();

    internal NavMeshBuilderQueue() {
        _navMeshWorkerThread = new Thread(NavMeshBuilderWork) { IsBackground = true };
        _navMeshWorkerThread.Start();
        _navMeshLinksWorkerThread = new Thread(NavMeshLinkBuilderWork) { IsBackground = true };
        _navMeshLinksWorkerThread.Start();
    }

    public void EnqueueNavMeshTask(string worldGuid, API.Agent agent, List<NavMeshBuildSource> filteredSources, Bounds bounds, Action<int, bool> onFinish) {
        NavMeshData BakeFunc() => NavMeshBuilder.BuildNavMeshData(agent.Settings, filteredSources, bounds, Vector3.zero, Quaternion.identity);
        _queueToBake.Add(new(worldGuid, agent, BakeFunc, onFinish));
    }

    private void NavMeshBuilderWork() {
        foreach (var toBake in _queueToBake.GetConsumingEnumerable()) {
            try {
                var bakeResults = toBake.Item3.Invoke();
                BakeResults.Enqueue(new(toBake.Item1, toBake.Item2, bakeResults, toBake.Item4));
            }
            catch (Exception e) {
                MelonLogger.Error("[NavMeshBuilderWork] Error on the thread :c");
                MelonLogger.Error(e);
            }
        }
    }

    public void EnqueueNavMeshLinkTask(string worldGuid, API.Agent agent, (Vector3[] vertices, int[] triangles) weldedNavMesh) {
        _queueToGenerateLinks.Add(new(worldGuid, agent, weldedNavMesh));
    }

    private void NavMeshLinkBuilderWork() {
        foreach (var (worldGuid, agent, weldedNavMesh) in _queueToGenerateLinks.GetConsumingEnumerable()) {
            try {
                CurrentNavMeshGeneratingAgent = agent;
                var (linkResults, linkVisualizers) = NavMeshTools.NavMeshLinkGenerator.Generate(agent, weldedNavMesh);
                GeneratedLinksResults.Enqueue(new(worldGuid, agent, linkResults, linkVisualizers));
                CurrentNavMeshGeneratingAgent = null;
            }
            catch (Exception e) {
                MelonLogger.Error("[NavMeshLinkBuilderWork] Error on the thread :c");
                MelonLogger.Error(e);
            }
        }
    }

    public void StopThread() {

        _queueToBake.CompleteAdding();
        _navMeshWorkerThread.Join();

        _queueToGenerateLinks.CompleteAdding();
        _navMeshLinksWorkerThread.Join();
    }
}
