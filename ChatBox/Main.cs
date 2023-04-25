using System.Collections;
using System.Reflection;
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

    public static Action<string, bool, bool> OnReceivedTyping;
    public static Action<string, string, bool> OnReceivedMessage;

    private static bool _isChatBoxKeyboardOpened;

    private static object _openKeyboardCoroutineToken;

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);
    }

    public override void OnLateInitializeMelon() {
        foreach (var modCommands in Commands.ModCommands) {
            MelonLogger.Msg($"[Commands] The mod {modCommands.Key} registered the commands: {string.Join(", ", modCommands.Value)}.");
        }
    }

    /// <summary>
    /// Sends a message through the ChatBox.
    /// </summary>
    /// <param name="message">The message to be sent through the ChatBox.</param>
    /// <param name="sendSoundNotification">Whether to send a sounds notification or not.</param>
    public static void SendMessage(string message, bool sendSoundNotification) {
        ModNetwork.SendMessage(message, Assembly.GetExecutingAssembly().GetName().Name == "Kafe.OSC.Integrations" ? ModNetwork.MessageSource.OSC : ModNetwork.MessageSource.Mod, sendSoundNotification);
    }

    /// <summary>
    /// Sets the typing status of the local player.
    /// </summary>
    /// <param name="isTyping">A boolean value indicating whether the local player is typing. Set to true if the player is typing, and false if the player is not typing.</param>
    /// <param name="sendSoundNotification">Whether to send a sounds notification or not.</param>
    public static void SetIsTyping(bool isTyping, bool sendSoundNotification) {
        SetIsTyping(isTyping, Assembly.GetExecutingAssembly().GetName().Name == "Kafe.OSC.Integrations" ? ModNetwork.MessageSource.OSC : ModNetwork.MessageSource.Mod, sendSoundNotification);
    }

    /// <summary>
    /// Opens the in-game keyboard, with an optional delay and an initial message.
    /// </summary>
    /// <param name="delayed">If set to true, the keyboard will be opened after a 0.15-second delay. If set to false, the keyboard will be opened immediately.</param>
    /// <param name="initialMessage">The initial message to be displayed on the keyboard when it is opened. This can be an empty string.</param>
    public static void OpenKeyboard(bool delayed, string initialMessage) {
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

    private static void SetIsTyping(bool isTyping, ModNetwork.MessageSource msgSource, bool notification) {
        ModNetwork.SendTyping(isTyping, msgSource, notification);
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter(AnimatorParameterTyping, isTyping ? 1f : 0f);
        var animator = PlayerSetup.Instance._animator;
        if (animator != null) {
            animator.SetBool(AnimatorParameterTypingLocal, isTyping);
        }
    }

    private static void ActuallyOpenKeyboard(string initialMessage) {
        ViewManager.Instance.openMenuKeyboard(KeyboardId + initialMessage);
        ModNetwork.SendTyping(true, ModNetwork.MessageSource.Internal, true);
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
            SetIsTyping(false, ModNetwork.MessageSource.Internal, true);
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
                        SetIsTyping(false, ModNetwork.MessageSource.Internal, true);
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
                    ModNetwork.SendMessage(value, ModNetwork.MessageSource.Internal, true);
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
