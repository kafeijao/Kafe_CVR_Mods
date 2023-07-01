using System.Collections;
using ABI_RC.Core.Base;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace Kafe.ChatBox;

public class ChatBox : MelonMod {

    private const string KeyboardId = $"[MelonMod.kafe.{nameof(ChatBox)}Mod]";

    private const string AnimatorParameterTyping = "ChatBox/Typing";
    private static readonly int AnimatorParameterTypingLocal = Animator.StringToHash("#" + AnimatorParameterTyping);

    private static bool _isChatBoxKeyboardOpened;

    private static object _openKeyboardCoroutineToken;

    private static readonly List<string> SentHistory = new();
    private static int _currentHistoryIndex = -1;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ConfigJson.LoadConfigJson();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        // Disable warnings for broken Characters, as it may cause lag.
        TMP_Settings.instance.m_warningsDisabled = true;

        // Setup Sent History
        API.OnMessageSent += (source, msg, notify, show) => {
            if (source != API.MessageSource.Internal || !show) return;
            _currentHistoryIndex = -1;

            // Ignore if the message is the same
            if (SentHistory.Count > 0 && SentHistory.Last() == msg) return;

            SentHistory.Add(msg);
        };

        // Setup the Cohtml Events
        CohtmlPatches.KeyboardCancelButtonPressed += DisableKeyboard;
        CohtmlPatches.KeyboardKeyPressed += () => {
            if (!_isChatBoxKeyboardOpened) return;
            SetIsTyping(API.MessageSource.Internal, true, true);
        };
        CohtmlPatches.AutoCompleteRequested += (currentInput, index) => {
            if (CVRPlayerManager.Instance == null) return;
            var usernames = new List<string> { MetaPort.Instance.username };
            usernames.AddRange(CVRPlayerManager.Instance.NetworkPlayers.Select(u => u.Username));

            var isEmptyStart = string.IsNullOrEmpty(currentInput) || currentInput.EndsWith(" ") || currentInput.EndsWith("@");

            // Filter the list with usernames that start with the last word
            if (!isEmptyStart) {
                var lastSpaceIndex = Math.Max(currentInput.LastIndexOf(' '), currentInput.LastIndexOf('@'));
                var lastWord = currentInput;
                if (lastSpaceIndex != -1 && lastSpaceIndex < currentInput.Length - 1) {
                    lastWord = currentInput.Substring(lastSpaceIndex + 1);
                }
                usernames = usernames.FindAll(username => lastWord != "" && username.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase));
                currentInput = currentInput.Substring(0, currentInput.Length - lastWord.Length);
            }
            if (usernames.Count == 0) return;

            // Send the initial message + the username
            CohtmlPatches.SetKeyboardContent(currentInput + usernames[index % usernames.Count]);
        };

        void HandleOnPrevious() {
            if (SentHistory.Count == 0 || _currentHistoryIndex == 0) return;
            if (_currentHistoryIndex == -1) _currentHistoryIndex = SentHistory.Count - 1;
            else if (_currentHistoryIndex > 0) _currentHistoryIndex -= 1;
            else return;
            CohtmlPatches.SetKeyboardContent(SentHistory[_currentHistoryIndex]);
        }
        CohtmlPatches.KeyboardPreviousPressed += HandleOnPrevious;
        CohtmlPatches.KeyboardArrowUpPressed += HandleOnPrevious;
        CohtmlPatches.KeyboardArrowDownPressed += () => {
            if (SentHistory.Count == 0 || _currentHistoryIndex < 0 || _currentHistoryIndex >= SentHistory.Count) return;
            if (_currentHistoryIndex < SentHistory.Count - 1) {
                _currentHistoryIndex += 1;
                CohtmlPatches.SetKeyboardContent(SentHistory[_currentHistoryIndex]);
            }
            else {
                // If we arrow down on the last history string, reset and set the keyboard input to empty.
                _currentHistoryIndex = -1;
                CohtmlPatches.SetKeyboardContent("");
            }
        };
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

        if (Input.GetKeyDown(KeyCode.Y) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)
            && !_isChatBoxKeyboardOpened && !ViewManager.Instance._gameMenuOpen) {
            OpenKeyboard(true, "");
        }

        if (Input.GetKeyDown(KeyCode.Insert) && _isChatBoxKeyboardOpened) {
            CohtmlPatches.SendAutoCompleteEvent();
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

    private static void DisableKeyboard() {
        _isChatBoxKeyboardOpened = false;
        SetIsTyping(API.MessageSource.Internal, false, true);
        if (_openKeyboardCoroutineToken != null) {
            MelonCoroutines.Stop(_openKeyboardCoroutineToken);
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static IEnumerator DisableKeyboardWithDelay() {
            // This delay is here because the menu close menu event happens before the SendToWorldUi
            // Which would disable _openedKeyboard before we could send the message
            yield return new WaitForSeconds(0.1f);
            DisableKeyboard();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerNameplate), nameof(PlayerNameplate.Start))]
        public static void After_PlayerNameplate_Start(PlayerNameplate __instance) {
            // Initialize the ChatBox component for the player
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
            // When opening the ChatBox keyboard, send the typing event
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
            // If typing on the keyboard Revert the mute/unmute events
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
            // When closing the Game Menu (also closes the keyboard) mark keyboard as disabled
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
            // Capture the keyboard input, and close it after sending
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
