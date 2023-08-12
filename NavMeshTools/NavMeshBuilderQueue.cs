using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshBuilderQueue {

    private readonly Thread _navMeshWorkerThread;
    private readonly BlockingCollection<Tuple<string, API.Agent, Func<NavMeshData>, Action<int, bool>>> _queueToBake = new();
    internal readonly ConcurrentQueue<Tuple<string, API.Agent, NavMeshData, Action<int, bool>>> BakeResults = new();

    private readonly Thread _navMeshLinksWorkerThread;
    internal volatile API.Agent CurrentNavMeshGeneratingAgent;
    private readonly BlockingCollection<Tuple<string, API.Agent, Mesh>> _queueToGenerateLinks = new();
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
            var bakeResults = toBake.Item3.Invoke();
            BakeResults.Enqueue(new(toBake.Item1, toBake.Item2, bakeResults, toBake.Item4));
        }
    }

    public void EnqueueNavMeshLinkTask(string worldGuid, API.Agent agent, Mesh triangulatedNavMesh) {
        _queueToGenerateLinks.Add(new(worldGuid, agent, triangulatedNavMesh));
    }

    private void NavMeshLinkBuilderWork() {
        foreach (var (worldGuid, agent, mesh) in _queueToGenerateLinks.GetConsumingEnumerable()) {
            CurrentNavMeshGeneratingAgent = agent;
            var (linkResults, linkVisualizers) = NavMeshTools.NavMeshLinkGenerator.Generate(agent, mesh);
            GeneratedLinksResults.Enqueue(new(worldGuid, agent, linkResults, linkVisualizers));
            CurrentNavMeshGeneratingAgent = null;
        }
    }

    public void StopThread() {

        _queueToBake.CompleteAdding();
        _navMeshWorkerThread.Join();

        _queueToGenerateLinks.CompleteAdding();
        _navMeshLinksWorkerThread.Join();
    }
}
