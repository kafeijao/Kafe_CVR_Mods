namespace CCK.Debugger.Events;

internal static class DebuggerMenu {

    public static bool IsPinned;

    public static event Action<bool> Pinned;

    public static event Action MainNextPage;
    public static event Action MainPreviousPage;

    public static event Action ControlsNextPage;
    public static event Action ControlsPreviousPage;

    public static void OnPinned(bool pinned) {
        IsPinned = pinned;
        Pinned?.Invoke(pinned);
    }

    public static void OnMainNextPage() {
        MainNextPage?.Invoke();
    }
    public static void OnMainPrevious() {
        MainPreviousPage?.Invoke();
    }

    public static void OnControlsNextPage() {
        ControlsNextPage?.Invoke();
    }
    public static void OnControlsPrevious() {
        ControlsPreviousPage?.Invoke();
    }
}
