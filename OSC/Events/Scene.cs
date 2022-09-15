namespace OSC.Events;

public static class Scene {

    public static event Action InputManagerCreated;

    public static event Action PlayerSetup;

    internal static void OnInputManagerCreated() {
        InputManagerCreated?.Invoke();
    }

    internal static void OnPlayerSetup() {
        PlayerSetup?.Invoke();
    }

    internal static void ResetAll() {
        // Used when you want to get all the events when connecting after the game is already started.
        // This should clear all the caches and perform all the initializations

        // Re-initialize avatar
        Avatar.Reset();

        // Re-initialize spawnables
        Spawnable.Reset();

        // Clear devices connection status
        Tracking.Reset();
    }
}
