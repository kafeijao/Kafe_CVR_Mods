using ABI_RC.Core.UI.UIRework.Managers;
using HarmonyLib;
using MelonLoader;

namespace Kafe.ChatBox;

internal static class CohtmlPatches {

    internal static Action KeyboardKeyPressed;

    // Todo: Waiting for bono add custom keys
    // internal static Action<string, int> AutoCompleteRequested;

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(KeyboardManager), nameof(KeyboardManager.Start))]
        public static void After_KeyboardManager_Start(KeyboardManager __instance) {
            try {

                // Load the bindings
                __instance.cohtmlView.Listener.ReadyForBindings += () => {
                    __instance.cohtmlView.View._view.RegisterForEvent("ChatBoxIsTyping", KeyboardKeyPressed);

                    // Todo: Reimplement when bono add custom keys
                    // __instance.cohtmlView.View._view.BindCall("ChatBoxAutoComplete", AutoCompleteRequested);
                };

                // Inject our Cohtml
                __instance.cohtmlView.Listener.FinishLoad += _ => {
                    __instance.cohtmlView.View._view.ExecuteScript(ModConfig.JavascriptPatchesContent);
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_KeyboardManager_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
