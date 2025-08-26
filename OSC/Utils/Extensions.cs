using ABI_RC.Systems.OSC.Jobs;

namespace Kafe.OSC.Utils;

public static class OSCJobSystemExtensions
{
    public static OSCJobQueue<T> RegisterQueue<T>(int capacity, Action<T> handler) where T : unmanaged
    {
        lock (OSCJobSystem.Lock)
        {
            OSCJobQueue<T> queue = new OSCJobQueue<T>(handler, capacity);
            OSCJobSystem.Queues.Add(queue);
            return queue;
        }
    }
}
