using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.UI.UIRework.Managers;
using HarmonyLib;
using MelonLoader;

namespace Kafe.MenuCSSLoader;

internal static class CohtmlPatches {

    private const string InitializedFunctionName = "MenuCSSLoaderInitialized";
    private const string LoadCSSFileFunctionName = "MenuCSSLoaderLoadCSSFile";
    private const string LoadCSSTextFunctionName = "MenuCSSLoaderLoadCSSText";

    public static Action MainMenuInitialized;
    public static Action QuickMenuInitialized;
    public static Action KeyboardViewInitialized;

    internal static void LoadCSSFileMM(string cssFilePath)
    {
        ViewManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSFileFunctionName, cssFilePath);
    }

    internal static void LoadCSSFileQM(string cssFilePath)
    {
        CVR_MenuManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSFileFunctionName, cssFilePath);
    }

    internal static void LoadCSSFileKeyboard(string cssFilePath)
    {
        KeyboardManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSFileFunctionName, cssFilePath);
    }

    internal static void LoadCSSTextMM(string cssText)
    {
        ViewManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSTextFunctionName, cssText);
    }

    internal static void LoadCSSTextQM(string cssText)
    {
        CVR_MenuManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSTextFunctionName, cssText);
    }

    internal static void LoadCSSTextKeyboard(string cssText)
    {
        KeyboardManager.Instance.cohtmlView.View.TriggerEvent(LoadCSSTextFunctionName, cssText);
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            // Load and inject our custom main menu behaviour
            try {

                // Load the bindings
                __instance.cohtmlView.Listener.ReadyForBindings += () =>
                {
                    __instance.cohtmlView.View.RegisterForEvent(InitializedFunctionName, MainMenuInitialized);
                };

                // Inject our Cohtml
                __instance.cohtmlView.Listener.FinishLoad += _ => {
                    __instance.cohtmlView.View._view.ExecuteScript(ModConfig.MenuJsPatchesContent);
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.Start))]
        public static void After_CVR_MenuManager_Start(CVR_MenuManager __instance) {
            // Load and inject our custom quick menu behaviour
            try {

                // Load the bindings
                __instance.cohtmlView.Listener.ReadyForBindings += () =>
                {
                    __instance.cohtmlView.View.RegisterForEvent(InitializedFunctionName, QuickMenuInitialized);
                };

                // Inject our Cohtml
                __instance.cohtmlView.Listener.FinishLoad += _ => {
                    __instance.cohtmlView.View._view.ExecuteScript(ModConfig.MenuJsPatchesContent);
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(KeyboardManager), nameof(KeyboardManager.Start))]
        public static void After_KeyboardManager_Start(KeyboardManager __instance) {
            // Load and inject our custom quick menu behaviour
            try {

                // Load the bindings
                __instance.cohtmlView.Listener.ReadyForBindings += () =>
                {
                    __instance.cohtmlView.View.RegisterForEvent(InitializedFunctionName, KeyboardViewInitialized);
                };

                // Inject our Cohtml
                __instance.cohtmlView.Listener.FinishLoad += _ => {
                    __instance.cohtmlView.View._view.ExecuteScript(ModConfig.MenuJsPatchesContent);
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_KeyboardManager_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
