namespace CCK.Debugger.Events;

internal static class DebuggerMenu {

    public static event Action MainNextPage;
    public static void OnMainNextPage()=> MainNextPage?.Invoke();

    public static event Action MainPreviousPage;
    public static void OnMainPrevious() => MainPreviousPage?.Invoke();

    public static event Action ControlsNextPage;
    public static void OnControlsNext() => ControlsNextPage?.Invoke();

    public static event Action ControlsPreviousPage;
    public static void OnControlsPrevious() => ControlsPreviousPage?.Invoke();
}
