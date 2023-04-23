using System.Collections;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.ChatBox;

public class ChatBox : MelonMod {

    private const string KeyboardId = $"[MelonMod.kafe.{nameof(ChatBox)}Mod]";

    public static Action<string, bool> OnReceivedTyping;
    public static Action<string, string> OnReceivedMessage;

    private static bool _openedKeyboard;

    private static object _openKeyboardCoroutineToken;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);
    }

    public static void OpenKeyboard() {
        if (ViewManager.Instance == null) return;
        if (!_openedKeyboard) {
            _openedKeyboard = true;
            if (_openKeyboardCoroutineToken != null) {
                MelonCoroutines.Stop(_openKeyboardCoroutineToken);
            }
            _openKeyboardCoroutineToken = MelonCoroutines.Start(OpenKeyboardWithDelay());
        }
    }

    public override void OnUpdate() {
        if (Input.GetKeyDown(KeyCode.Y) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) {
            OpenKeyboard();
        }
    }

    private static IEnumerator OpenKeyboardWithDelay() {
        yield return new WaitForSeconds(0.2f);
        ViewManager.Instance.openMenuKeyboard(KeyboardId);
        _openKeyboardCoroutineToken = null;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static void DisableKeyboard() {
            _openedKeyboard = false;
            if (_openKeyboardCoroutineToken != null) {
                MelonCoroutines.Stop(_openKeyboardCoroutineToken);
            }
        }

        private static IEnumerator DisableKeyboardWithDelay() {
            yield return new WaitForSeconds(0.1f);
            DisableKeyboard();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerNameplate), nameof(PlayerNameplate.Start))]
        public static void After_PlayerNameplate_Start(PlayerNameplate __instance) {
            try {
                #if DEBUG
                MelonLogger.Msg($"Attaching the ChatBoxBehavior to {__instance.player.ownerId}");
                #endif
                __instance.gameObject.AddComponent<ChatBoxBehavior>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerNameplate_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.openMenuKeyboard), typeof(string))]
        public static void After_ViewManager_openMenuKeyboard(ViewManager __instance, ref string previousValue) {
            try {
                if (_openedKeyboard) {
                    if (previousValue != KeyboardId) {
                        _openedKeyboard = false;
                        ModNetwork.SendTyping(false);
                    }
                    else {
                        previousValue = "";
                        ModNetwork.SendTyping(true);
                    }
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_openMenuKeyboard)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.UiStateToggle), typeof(bool))]
        public static void After_ViewManager_UiStateToggle(ViewManager __instance, bool show) {
            try {
                if (!show && _openedKeyboard) {
                    ModNetwork.SendTyping(false);
                    MelonCoroutines.Start(DisableKeyboardWithDelay());
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_UiStateToggle)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.SendToWorldUi))]
        public static void After_ViewManager_SendToWorldUi(ViewManager __instance, string value) {
            try {
                if (_openedKeyboard) {
                    ModNetwork.SendMessage(value);
                    DisableKeyboard();
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_SendToWorldUi)}");
                MelonLogger.Error(e);
            }
        }
    }
}
