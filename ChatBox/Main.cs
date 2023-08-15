using System.Collections;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.InputManagement;
using HarmonyLib;
using Kafe.ChatBox.Properties;
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

        // Check for VRBinding
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.VRBindingName) != null) {
            MelonLogger.Msg($"Detected {AssemblyInfoParams.VRBindingName} mod, we're adding the integration! You can now bind the Action to open the ChatBox keyboard in SteamVR.");
            Integrations.VRBindingsIntegration.Initialize();
        }
        else {
            MelonLogger.Msg($"You can optionally install {AssemblyInfoParams.VRBindingName} mod to be able to bind a SteamVR controller button to opening the ChatBox keyboard.");
        }

        // Disable warnings for broken Characters, as it may cause lag.
        TMP_Settings.instance.m_warningsDisabled = true;

        // Setup Sent History
        API.OnMessageSent += chatBoxMessage => {
            if (chatBoxMessage.Source != API.MessageSource.Internal) return;
            _currentHistoryIndex = -1;

            // Ignore if the message is the same
            if (SentHistory.Count > 0 && SentHistory.Last() == chatBoxMessage.Message) return;

            SentHistory.Add(chatBoxMessage.Message);
        };

        // Setup the Cohtml Events
        CohtmlPatches.KeyboardCancelButtonPressed += DisableKeyboard;
        CohtmlPatches.KeyboardKeyPressed += () => {
            if (!_isChatBoxKeyboardOpened) return;
            SetIsTyping(API.MessageSource.Internal, true, true);
        };
        CohtmlPatches.AutoCompleteRequested += (currentInput, index) => {
            if (CVRPlayerManager.Instance == null) return;
            var usernames = new List<string> { AuthManager.username };
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

        CVRGameEventSystem.MainMenu.OnClose.AddListener(() => {
            // When closing the Game Menu (also closes the keyboard) mark keyboard as disabled
            if ( _isChatBoxKeyboardOpened) {
                MelonCoroutines.Start(DisableKeyboardWithDelay());
            }
        });
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
        if (!MetaPort.Instance.isUsingVr) {
            CVRInputManager.Instance.inputEnabled = false;
            CVRInputManager.Instance.textInputFocused = true;
        }
        else {
            ViewManager.Instance._inputField = null;
            ViewManager.Instance._tmp_inputField = null;
        }
        ViewManager.Instance.openMenuKeyboard(KeyboardId + initialMessage);
        ModNetwork.SendTyping(API.MessageSource.Internal, true, true);
        _openKeyboardCoroutineToken = null;
    }

    public override void OnUpdate() {

        if (Input.GetKeyDown(KeyCode.Y) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)
            && !_isChatBoxKeyboardOpened
            && CVRInputManager.Instance != null && !CVRInputManager.Instance.textInputFocused
            && CVR_MenuManager.Instance != null && !CVR_MenuManager.Instance.textInputFocused
            && ViewManager.Instance != null && !ViewManager.Instance.textInputFocused) {
            OpenKeyboard(true, "");
        }

        if (Input.GetKeyDown(KeyCode.Tab) && _isChatBoxKeyboardOpened) {
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

        // Clear stuff (seems seems that is needed when clicking the Enter on the virtual keyboard?
        if (!MetaPort.Instance.isUsingVr) {
            CVRInputManager.Instance.inputEnabled = true;
            CVRInputManager.Instance.textInputFocused = false;
        }

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

    [HarmonyPatch]
    internal class HarmonyPatches {

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
        public static void Before_ViewManager_openMenuKeyboard(ViewManager __instance, ref string previousValue) {
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
                MelonLogger.Error($"Error during the patched function {nameof(Before_ViewManager_openMenuKeyboard)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.SendToWorldUi))]
        public static void After_ViewManager_SendToWorldUi(ViewManager __instance, string value) {
            // Capture the keyboard input, and close it after sending
            try {
                if (_isChatBoxKeyboardOpened) {
                    ModNetwork.SendMessage(API.MessageSource.Internal, "", value, true, true, true);
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
