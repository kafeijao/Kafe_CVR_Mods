using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshBuilderQueue {

    private readonly Thread _workerThread;

    private readonly BlockingCollection<Tuple<string, API.Agent, Func<NavMeshData>, Action<int, bool>>> _queueToBake = new();
    internal readonly ConcurrentQueue<Tuple<string, API.Agent, NavMeshData, Action<int, bool>>> BakeResults = new();

    internal NavMeshBuilderQueue() {
        _workerThread = new Thread(Work) { IsBackground = true };
        _workerThread.Start();
    }

    public void EnqueueNavMeshTask(string worldGuid, API.Agent agent, List<NavMeshBuildSource> filteredSources, Bounds bounds, Action<int, bool> onFinish) {
        NavMeshData BakeFunc() => NavMeshBuilder.BuildNavMeshData(agent.Settings, filteredSources, bounds, Vector3.zero, Quaternion.identity);
        _queueToBake.Add(new Tuple<string, API.Agent, Func<NavMeshData>, Action<int, bool>>(worldGuid, agent, BakeFunc, onFinish));
    }

    private void Work() {
        foreach (var toBake in _queueToBake.GetConsumingEnumerable()) {
            BakeResults.Enqueue(new Tuple<string, API.Agent, NavMeshData, Action<int, bool>>(toBake.Item1, toBake.Item2, toBake.Item3.Invoke(), toBake.Item4));
        }
    }

    public void StopThread() {
        _queueToBake.CompleteAdding();
        _workerThread.Join();
    }
}
