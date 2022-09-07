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
}
