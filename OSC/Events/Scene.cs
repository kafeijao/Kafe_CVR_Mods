namespace Kafe.OSC.Events;

public static class Scene
{
    internal static void ResetAll()
    {
        // Used when you want to get all the events when connecting after the game is already started.
        // This should clear all the caches and perform all the initializations

        // Clear devices connection status
        Tracking.Reset();

        // Re-initialize avatar
        Avatar.Reset();

        // Re-initialize spawnables
        Spawnable.Reset();
    }
}
