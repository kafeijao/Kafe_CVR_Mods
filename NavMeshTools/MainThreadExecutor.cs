using System.Collections;
using System.Collections.Concurrent;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshTools;

public class MainThreadExecutor : MonoBehaviour {

    // Big timeout because some stuff we let it run over time in a coroutine
    private const int WaitForMainTimeout = 5*60*1000;

    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly BlockingCollection<Workload> _workloads = new();

    private Thread _workerThread;
    private CancellationTokenSource _cts;

    private void Start() {
        _cts = new CancellationTokenSource();
        _workerThread = new Thread(WorkerThreadLoop);
        _workerThread.Start();
    }

    private void WorkerThreadLoop() {

        try {
            foreach (var workload in _workloads.GetConsumingEnumerable(_cts.Token)) {
                try {
                    // Execute the tasks of a workload
                    while (workload.HasTasks) {
                        if (workload.IsNextMainThread()) {
                            // Wait until the main thread has processed the task.
                            var resetEvent = new ManualResetEvent(false);
                            _mainThreadActions.Enqueue(() => StartCoroutine(ExecuteCoroutineTask(workload, resetEvent)));
                            if (!resetEvent.WaitOne(WaitForMainTimeout)) {
                                MelonLogger.Warning($"[Thread {Thread.CurrentThread.ManagedThreadId}] Waiting for the Main Thread Coroutine to process a task Timed Out... [{WaitForMainTimeout} ms]");
                            }
                        }
                        else {
                            workload.ExecuteNext();
                        }
                    }
                }
                catch (Exception e) {
                    MelonLogger.Error($"[Thread {Thread.CurrentThread.ManagedThreadId}] There was an error during a workload process. It won't be completed...");
                    MelonLogger.Error(e);

                    // Queue the call for On Finish when Errored
                    var resetEventOnFailed = new ManualResetEvent(false);
                    _mainThreadActions.Enqueue(() => { ExecuteOnFinish(workload, resetEventOnFailed, false); });
                    if (!resetEventOnFailed.WaitOne(WaitForMainTimeout)) {
                        MelonLogger.Warning($"[Thread {Thread.CurrentThread.ManagedThreadId}] Waiting for the Main Thread Coroutine for OnFinish(error), Timed Out... [{WaitForMainTimeout} ms]");
                    }
                    continue;
                }

                // Queue the call for On Finish when successfully ran all tasks
                var resetEventOnSuccess = new ManualResetEvent(false);
                _mainThreadActions.Enqueue(() => { ExecuteOnFinish(workload, resetEventOnSuccess, true); });
                if (!resetEventOnSuccess.WaitOne(WaitForMainTimeout)) {
                    MelonLogger.Warning($"[Thread {Thread.CurrentThread.ManagedThreadId}] Waiting for the Main Thread Coroutine to OnFinish(success), Timed Out... [{WaitForMainTimeout} ms]");
                }
            }
        }
        catch (OperationCanceledException) {
            MelonLogger.Warning($"[Thread {Thread.CurrentThread.ManagedThreadId}] Cancellation request received, going to terminate the Thread!");
        }
    }

    private IEnumerator ExecuteCoroutineTask(Workload workload, ManualResetEvent resetEvent) {
        yield return workload.ExecuteNextCoroutine();
        resetEvent.Set();
    }

    private static void ExecuteOnFinish(Workload workload, ManualResetEvent resetEvent, bool success) {
        try {
            workload.OnFinish.Invoke(workload.Payload, success);
        }
        catch (Exception e) {
            MelonLogger.Error(e);
        }
        resetEvent.Set();
    }

    private void Update() {
        if (_mainThreadActions.TryDequeue(out var action)) {
            action.Invoke();
        }
    }

    public void AddWorkload(Workload workload) {
        _workloads.Add(workload);
    }

    /// <summary>
    /// Cancel current workload and clear the Queue. This should be called from the Main Thread.
    /// </summary>
    public void CancelAndRestartThread() {

        _cts.Cancel();

        // Clear the workload queue.
        while (_workloads.TryTake(out _)) { }

        // Clear the main thread actions queue.
        while (_mainThreadActions.TryDequeue(out _)) { }

        // Reset the CancellationTokenSource
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        // Create a new thread to start with a clean state
        _workerThread = new Thread(WorkerThreadLoop);
        _workerThread.Start();
    }

    private void OnDestroy() {
        _workloads.CompleteAdding();
        _cts?.Cancel();
        _workerThread.Join();
    }

    private void OnApplicationQuit() => OnDestroy();

    public class Workload {

        // Delegates
        public delegate void TaskDelegate(NavMeshBakePipeline.BakerPayload payload);
        public delegate IEnumerator CoroutineTaskDelegate(NavMeshBakePipeline.BakerPayload payload);

        internal readonly NavMeshBakePipeline.BakerPayload Payload;
        private readonly Queue<(Delegate taskSequence, bool isMainThreadSequence)> _tasks;
        internal readonly Action<NavMeshBakePipeline.BakerPayload, bool> OnFinish;

        public Workload(IEnumerable<(Delegate taskSequence, bool isMainThreadSequence)> tasks, NavMeshBakePipeline.BakerPayload payload, Action<NavMeshBakePipeline.BakerPayload, bool> onFinish) {
            Payload = payload;
            OnFinish = onFinish;
            _tasks = new Queue<(Delegate taskSequence, bool isMainThreadSequence)>(tasks);
        }

        public bool HasTasks => _tasks.Count > 0;

        public bool IsNextMainThread() => HasTasks && _tasks.Peek().isMainThreadSequence;

        public void ExecuteNext() {
            if (HasTasks && _tasks.Peek().taskSequence is TaskDelegate) {
                (_tasks.Dequeue().taskSequence as TaskDelegate)?.Invoke(Payload);
            }
        }

        public IEnumerator ExecuteNextCoroutine() {
            if (HasTasks && _tasks.Peek().taskSequence is CoroutineTaskDelegate) {
                yield return (_tasks.Dequeue().taskSequence as CoroutineTaskDelegate)?.Invoke(Payload);
            }
        }
    }
}
