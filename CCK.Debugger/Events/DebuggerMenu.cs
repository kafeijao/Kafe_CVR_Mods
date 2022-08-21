using CCK.Debugger.Components;

namespace CCK.Debugger.Events; 

internal static class DebuggerMenu {
    
    public static bool IsPinned;
    
    public static event Action<bool> Pinned;
    
    public static event Action MainNextPage;
    public static event Action MainPreviousPage;
    
    public static event Action ControlsNextPage;
    public static event Action ControlsPreviousPage;
    
    public static event Action<Menu> MenuInit;
    public static event Action<Menu> MenuUpdate;
    
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
    
    public static void OnMenuInit(Menu menu) {
        MenuInit?.Invoke(menu);
    }
    public static void OnMenuUpdate(Menu menu) {
        MenuUpdate?.Invoke(menu);
    }
}