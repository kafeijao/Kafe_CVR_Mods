using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

internal class NavMeshBuilderQueue {

    private readonly Thread _workerThread;

    private readonly BlockingCollection<Tuple<string, Func<NavMeshData>, Action<bool>>> _queueToBake = new();
    internal readonly ConcurrentQueue<Tuple<string, NavMeshData, Action<bool>>> BakeResults = new();

    internal NavMeshBuilderQueue() {
        _workerThread = new Thread(Work) { IsBackground = true };
        _workerThread.Start();
    }

    public void EnqueueNavMeshTask(string worldGuid, NavMeshBuildSettings navMeshSettings, List<NavMeshBuildSource> filteredSources, Bounds bounds, Action<bool> onFinish) {
        NavMeshData BakeFunc() => NavMeshBuilder.BuildNavMeshData(navMeshSettings, filteredSources, bounds, Vector3.zero, Quaternion.identity);
        _queueToBake.Add(new Tuple<string, Func<NavMeshData>, Action<bool>>(worldGuid, BakeFunc, onFinish));
    }

    private void Work() {
        foreach (var toBake in _queueToBake.GetConsumingEnumerable()) {
            BakeResults.Enqueue(new Tuple<string, NavMeshData, Action<bool>>(toBake.Item1, toBake.Item2.Invoke(), toBake.Item3));
        }
    }

    public void StopThread() {
        _queueToBake.CompleteAdding();
        _workerThread.Join();
    }
}
