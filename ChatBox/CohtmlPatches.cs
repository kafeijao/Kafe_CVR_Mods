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
        ViewManager.Instance.gameMenuView.View.TriggerEvent("ChatBoxUpdateContent", content);
    }

    internal static void SendAutoCompleteEvent() {
        if (!_initialized) {
            MelonLogger.Warning($"[SetKeyboardContent] Attempted to set the keyboard content, but the view was not initialized.");
            return;
        }
        ViewManager.Instance.gameMenuView.View.TriggerEvent("ChatBoxAutoComplete");
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            try {

                // Load the bindings
                __instance.gameMenuView.Listener.ReadyForBindings += () => {
                    __instance.gameMenuView.View.RegisterForEvent("ChatBoxIsTyping", KeyboardKeyPressed);
                    __instance.gameMenuView.View.RegisterForEvent("ChatBoxClosedKeyboard", KeyboardCancelButtonPressed);

                    __instance.gameMenuView.View.RegisterForEvent("ChatBoxArrowUp", KeyboardArrowUpPressed);
                    __instance.gameMenuView.View.RegisterForEvent("ChatBoxArrowDown", KeyboardArrowDownPressed);

                    __instance.gameMenuView.View.BindCall("ChatBoxAutoComplete", AutoCompleteRequested);

                    __instance.gameMenuView.View.BindCall("ChatBoxPrevious", KeyboardPreviousPressed);
                };

                // Inject our Cohtml
                __instance.gameMenuView.Listener.FinishLoad += _ => {
                    __instance.gameMenuView.View.ExecuteScript(ModConfig.javascriptPatchesContent);
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
