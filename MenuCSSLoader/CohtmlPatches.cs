using ABI_RC.Core.InteractionSystem;
using HarmonyLib;
using MelonLoader;

namespace MenuCSSLoader;

internal static class CohtmlPatches {

    private const string InitializedFunctionName = "MenuCSSLoaderInitialized";
    private const string LoadCSSFileFunctionName = "MenuCSSLoaderLoadCSSFile";
    private const string LoadCSSTextFunctionName = "MenuCSSLoaderLoadCSSText";

    public static Action MainMenuInitialized;
    public static Action QuickMenuInitialized;

    internal static void LoadCSSFileMM(string cssFilePath)
    {
        ViewManager.Instance.gameMenuView.View.TriggerEvent(LoadCSSFileFunctionName, cssFilePath);
    }

    internal static void LoadCSSFileQM(string cssFilePath)
    {
        CVR_MenuManager.Instance.quickMenu.View.TriggerEvent(LoadCSSFileFunctionName, cssFilePath);
    }

    internal static void LoadCSSTextMM(string cssText)
    {
        ViewManager.Instance.gameMenuView.View.TriggerEvent(LoadCSSTextFunctionName, cssText);
    }

    internal static void LoadCSSTextQM(string cssText)
    {
        CVR_MenuManager.Instance.quickMenu.View.TriggerEvent(LoadCSSTextFunctionName, cssText);
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.Start))]
        public static void After_ViewManager_Start(ViewManager __instance) {
            // Load and inject our custom main menu behaviour
            try {

                // Load the bindings
                __instance.gameMenuView.Listener.ReadyForBindings += () =>
                {
                    __instance.gameMenuView.View.RegisterForEvent(InitializedFunctionName, MainMenuInitialized);
                };

                // Inject our Cohtml
                __instance.gameMenuView.Listener.FinishLoad += _ => {
                    __instance.gameMenuView.View._view.ExecuteScript(ModConfig.MenuJsPatchesContent);
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
                __instance.quickMenu.Listener.ReadyForBindings += () =>
                {
                    __instance.quickMenu.View.RegisterForEvent(InitializedFunctionName, QuickMenuInitialized);
                };

                // Inject our Cohtml
                __instance.quickMenu.Listener.FinishLoad += _ => {
                    __instance.quickMenu.View._view.ExecuteScript(ModConfig.MenuJsPatchesContent);
                };

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_MenuManager_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
