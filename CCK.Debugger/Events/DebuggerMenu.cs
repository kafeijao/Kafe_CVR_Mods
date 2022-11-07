using TMPro;

namespace CCK.Debugger.Events;

internal static class DebuggerMenu {

    public static event Action<bool> Pinned;
    public static event Action<bool> HudToggled;
    public static event Action<bool> GrabToggled;
    public static event Action<bool> PointerToggled;
    public static event Action<bool> TriggerToggled;
    public static event Action<bool> BoneToggled;
    public static event Action<bool> TrackerToggled;
    public static event Action<bool> ResetToggled;

    public static event Action MainNextPage;
    public static event Action MainPreviousPage;

    public static event Action ControlsNextPage;
    public static event Action ControlsPreviousPage;

    public static event Action<bool> SwitchedInspectedEntity;

    public static event Action<TextMeshProUGUI> TextMeshProUGUIDestroyed;

    public static void OnPinned(bool pinned) {
        Pinned?.Invoke(pinned);
    }
    public static void OnHudToggled(bool hudToggled) {
        HudToggled?.Invoke(hudToggled);
    }
    public static void OnGrabToggle(bool grabToggled) {
        GrabToggled?.Invoke(grabToggled);
    }
    public static void OnPointerToggle(bool pointerToggled) {
        PointerToggled?.Invoke(pointerToggled);
    }
    public static void OnTriggerToggle(bool triggerToggled) {
        TriggerToggled?.Invoke(triggerToggled);
    }
    public static void OnBoneToggle(bool boneToggled) {
        BoneToggled?.Invoke(boneToggled);
    }
    public static void OnTrackerToggle(bool trackerToggled) {
        TrackerToggled?.Invoke(trackerToggled);
    }
    public static void OnResetToggle(bool resetToggled) {
        ResetToggled?.Invoke(resetToggled);
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

    public static void OnSwitchInspectedEntity(bool finishedInitializing) {
        SwitchedInspectedEntity?.Invoke(finishedInitializing);
    }

    public static void OnTextMeshProUGUIDestroyed(TextMeshProUGUI tmpText) {
        TextMeshProUGUIDestroyed?.Invoke(tmpText);
    }
}
