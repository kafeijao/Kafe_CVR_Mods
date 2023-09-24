using ABI_RC.Core.InteractionSystem;
using cohtml;
using HarmonyLib;
using MelonLoader;

namespace Kafe.ChatBox;

internal static class CohtmlPatches {

    private static bool _initialized;

    internal static Action KeyboardKeyPressed;
    internal static Action KeyboardCancelButtonPressed;

    internal static Action<string, int> AutoCompleteRequested;

    internal static Action KeyboardArrowUpPressed;
    internal static Action KeyboardArrowDownPressed;

    internal static Action KeyboardPreviousPressed;

    internal static void SetKeyboardContent(string content) {
        if (!_initialized) {
            MelonLogger.Warning($"[SetKeyboardContent] Attempted to set the keyboard content, but the view was not initialized.");
            return;
        }
        ViewManager.Instance.gameMenuView.View._view.TriggerEvent("ChatBoxUpdateContent", content);
    }

    internal static void SendAutoCompleteEvent() {
        if (!_initialized) {
            MelonLogger.Warning($"[SendAutoCompleteEvent] Attempted send the auto-complete event, but the view was not initialized.");
            return;
        }
        ViewManager.Instance.gameMenuView.View._view.TriggerEvent("ChatBoxAutoComplete");
    }

    internal static void SendKeyboardBlur() {
        if (!_initialized) {
            MelonLogger.Warning($"[SendKeyboardBlur] Attempted to send the blur keyboard event, but the view was not initialized.");
            return;
        }
        ViewManager.Instance.gameMenuView.View._view.TriggerEvent("ChatBoxBlurKeyboardInput");
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            try {

                // Load the bindings
                __instance.gameMenuView.Listener.ReadyForBindings += () => {
                    __instance.gameMenuView.View._view.RegisterForEvent("ChatBoxIsTyping", KeyboardKeyPressed);
                    __instance.gameMenuView.View._view.RegisterForEvent("ChatBoxClosedKeyboard", KeyboardCancelButtonPressed);

                    __instance.gameMenuView.View._view.RegisterForEvent("ChatBoxArrowUp", KeyboardArrowUpPressed);
                    __instance.gameMenuView.View._view.RegisterForEvent("ChatBoxArrowDown", KeyboardArrowDownPressed);

                    __instance.gameMenuView.View._view.BindCall("ChatBoxAutoComplete", AutoCompleteRequested);

                    __instance.gameMenuView.View._view.BindCall("ChatBoxPrevious", KeyboardPreviousPressed);
                };

                // Inject our Cohtml
                __instance.gameMenuView.Listener.FinishLoad += _ => {
                    __instance.gameMenuView.View._view.ExecuteScript(ModConfig.JavascriptPatchesContent);
                    _initialized = true;
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
