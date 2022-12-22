using ABI_RC.Core.InteractionSystem;

namespace CCK.Debugger.Events;

internal static class QuickMenu {

    public static bool IsQuickMenuOpened;
    
    public static event Action<bool> QuickMenuIsShownChanged;
    public static event Action<CVR_MenuManager> QuickMenuInitialized;


    public static void OnQuickMenuIsShownChanged(bool open) {
        IsQuickMenuOpened = open;
        QuickMenuIsShownChanged?.Invoke(open);
    }

    public static void OnQuickMenuInitialized(CVR_MenuManager menuManager) {
        QuickMenuInitialized?.Invoke(menuManager);
    }
}
