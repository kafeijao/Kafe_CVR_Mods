using System.Collections;
using ABI_RC.Core.Base;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.ChatBox;

public class ChatBox : MelonMod {

    private const string KeyboardId = $"[MelonMod.kafe.{nameof(ChatBox)}Mod]";

    private const string AnimatorParameterTyping = "ChatBox/Typing";
    private static readonly int AnimatorParameterTypingLocal = Animator.StringToHash("#" + AnimatorParameterTyping);

    private static bool _isChatBoxKeyboardOpened;

    private static object _openKeyboardCoroutineToken;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);
    }

    internal static void OpenKeyboard(bool delayed, string initialMessage) {
        if (ViewManager.Instance == null) return;
        _isChatBoxKeyboardOpened = true;
        if (_openKeyboardCoroutineToken != null) {
            MelonCoroutines.Stop(_openKeyboardCoroutineToken);
        }
        if (delayed) {
            _openKeyboardCoroutineToken = MelonCoroutines.Start(OpenKeyboardWithDelay(initialMessage));
        }
        else {
            ActuallyOpenKeyboard(initialMessage);
        }
    }

    internal static void SetIsTyping(API.MessageSource msgSource, bool isTyping, bool notification) {
        if (PlayerSetup.Instance == null) return;
        ModNetwork.SendTyping(msgSource, isTyping, notification);
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter(AnimatorParameterTyping, isTyping ? 1f : 0f);
        var animator = PlayerSetup.Instance._animator;
        if (animator != null) {
            animator.SetBool(AnimatorParameterTypingLocal, isTyping);
        }
    }

    private static void ActuallyOpenKeyboard(string initialMessage) {
        ViewManager.Instance.openMenuKeyboard(KeyboardId + initialMessage);
        ModNetwork.SendTyping(API.MessageSource.Internal, true, true);
        _openKeyboardCoroutineToken = null;
    }

    public override void OnUpdate() {
        if (Input.GetKeyDown(KeyCode.Y) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) && !_isChatBoxKeyboardOpened) {
            OpenKeyboard(true, "");
        }
    }

    private static float _timer;

    private static IEnumerator OpenKeyboardWithDelay(string initialMsg) {
        _timer = 0f;
        while (_timer < 0.2f && Input.GetKey(KeyCode.Y)) {
            _timer += Time.deltaTime;
            yield return null;
        }
        ActuallyOpenKeyboard(initialMsg);
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static void DisableKeyboard() {
            _isChatBoxKeyboardOpened = false;
            SetIsTyping(API.MessageSource.Internal, false, true);
            if (_openKeyboardCoroutineToken != null) {
                MelonCoroutines.Stop(_openKeyboardCoroutineToken);
            }
        }

        private static IEnumerator DisableKeyboardWithDelay() {
            // This delay is here because the menu close menu event happens before the SendToWorldUi
            // Which would disable _openedKeyboard before we could send the message
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
                if (_isChatBoxKeyboardOpened) {
                    if (!previousValue.StartsWith(KeyboardId)) {
                        _isChatBoxKeyboardOpened = false;
                        SetIsTyping(API.MessageSource.Internal, false, true);
                    }
                    else {
                        // Remove the tag
                        previousValue = previousValue.Remove(0, KeyboardId.Length);
                    }
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_ViewManager_openMenuKeyboard)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputManager), nameof(InputManager.Update))]
        public static void After_InputManager_Update(InputManager __instance) {
            try {
                // If the keyboard is closed -> ignore
                if (!_isChatBoxKeyboardOpened) return;

                // Otherwise lets prevent the mic toggle/push to talk
                if (__instance.pushToTalk) {
                    Audio.SetMicrophoneActive(false);
                }
                else if (CVRInputManager.Instance.muteDown) {
                    // Toggle again, so we revert the toggling xD
                    Audio.ToggleMicrophone();
                }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_InputManager_Update)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.UiStateToggle), typeof(bool))]
        public static void After_ViewManager_UiStateToggle(ViewManager __instance, bool show) {
            try {
                if (!show && _isChatBoxKeyboardOpened) {
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
                if (_isChatBoxKeyboardOpened) {
                    ModNetwork.SendMessage(API.MessageSource.Internal, value, true, true);
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
