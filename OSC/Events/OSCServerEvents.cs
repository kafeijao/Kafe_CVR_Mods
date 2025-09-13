namespace Kafe.OSC.Events;

public static class OSCServerEvents
{
    public static event Action<bool> OSCServerStateUpdated;

    internal static void OnOSCServerStateUpdate(bool isRunning)
    {
        OSCServerStateUpdated?.Invoke(isRunning);
    }
}
