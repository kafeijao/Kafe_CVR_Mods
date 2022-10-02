using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using HarmonyLib;
using Valve.VR;

namespace FreedomFingers;

internal static class Api {

    internal static Traverse AreGesturesActiveField;

    internal static Action<bool> GestureToggledByGame;

    private static void SendNotification(bool isActive) {
        if (FreedomFingers.melonEntryEnableNotification.Value) {
            CohtmlHud.Instance.ViewDropTextImmediate("", "", $"Gestures {(isActive ? "Enabled" : "Disabled")}");
        }
    }

    internal static void OnGestureToggleByGame(bool isActive) {
        SendNotification(isActive);
        GestureToggledByGame?.Invoke(isActive);
    }

    private static void InitializeField() {
        if (AreGesturesActiveField != null) return;
        var modules = Traverse.Create(CVRInputManager.Instance).Field<List<CVRInputModule>>("_inputModules").Value;
        var module = modules?.FirstOrDefault(m => m is InputModuleSteamVR);
        if (module == null || !module.enabled) return;
        var steamVrModule = (InputModuleSteamVR) module;
        AreGesturesActiveField = Traverse.Create(steamVrModule).Field("_steamVrIndexGestureToggleValue");
    }

    public static bool AreGesturesActive() {
        InitializeField();
        return AreGesturesActiveField.GetValue<bool>();
    }

    public static void ToggleGestures() {
        InitializeField();
        var newAreGesturesActiveValue = !AreGesturesActive();
        AreGesturesActiveField.SetValue(newAreGesturesActiveValue);
        SendNotification(newAreGesturesActiveValue);
    }
}
