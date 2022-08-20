namespace CCK.Debugger.Events;

internal static class QuickMenu {

    public static bool IsQuickMenuOpened;
    
    public static event Action<bool> QuickMenuIsShownChanged;
    public static void OnQuickMenuIsShownChanged(bool open) {
        IsQuickMenuOpened = open;
        QuickMenuIsShownChanged?.Invoke(open);
    }
}
