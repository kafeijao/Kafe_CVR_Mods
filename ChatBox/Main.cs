using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.UI.UIRework.Managers;
using HarmonyLib;
using Kafe.ChatBox.Properties;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace Kafe.ChatBox;

public class ChatBox : MelonMod
{
    private const string AnimatorParameterTyping = "ChatBox/Typing";
    private static readonly int AnimatorParameterTypingLocal = Animator.StringToHash("#" + AnimatorParameterTyping);

    private static readonly Action<string> OnKeyboardSubmitChatboxMessageDelegate = OnKeyboardSubmitChatboxMessage;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();
        ConfigJson.LoadConfigJson();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        // Check for VRBinding
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.VRBindingName) != null)
        {
            MelonLogger.Msg(
                $"Detected {AssemblyInfoParams.VRBindingName} mod, we're adding the integration! You can now bind the Action to open the ChatBox keyboard in SteamVR.");
            Integrations.VRBindingsIntegration.Initialize();
        }
        else
        {
            MelonLogger.Msg(
                $"You can optionally install {AssemblyInfoParams.VRBindingName} mod to be able to bind a SteamVR controller button to opening the ChatBox keyboard.");
        }

        // Disable warnings for broken Characters, as it may cause lag.
        TMP_Settings.instance.m_warningsDisabled = true;

        API.OnMessageSent += chatBoxMessage =>
        {
            // Send TTS with chatbox option
            if (ModConfig.MeAlsoSendMsgsToTTS.Value) ViewManager.Instance.SendTTSMessage(chatBoxMessage.Message);
        };

        // Send IsTyping to the network
        CohtmlPatches.KeyboardKeyPressed += () =>
        {
            if (!IsChatBoxKeyboardOpened()) return;
            SetIsTyping(API.MessageSource.Internal, true, true);
        };

        // CohtmlPatches.AutoCompleteRequested += (currentInput, index) => {
        //     if (CVRPlayerManager.Instance == null) return;
        //     var usernames = new List<string> { AuthManager.Username };
        //     usernames.AddRange(CVRPlayerManager.Instance.NetworkPlayers.Select(u => u.Username));
        //
        //     var isEmptyStart = string.IsNullOrEmpty(currentInput) || currentInput.EndsWith(" ") || currentInput.EndsWith("@");
        //
        //     // Filter the list with usernames that start with the last word
        //     if (!isEmptyStart) {
        //         var lastSpaceIndex = Math.Max(currentInput.LastIndexOf(' '), currentInput.LastIndexOf('@'));
        //         var lastWord = currentInput;
        //         if (lastSpaceIndex != -1 && lastSpaceIndex < currentInput.Length - 1) {
        //             lastWord = currentInput.Substring(lastSpaceIndex + 1);
        //         }
        //         usernames = usernames.FindAll(username => lastWord != "" && username.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase));
        //         currentInput = currentInput.Substring(0, currentInput.Length - lastWord.Length);
        //     }
        //     if (usernames.Count == 0) return;
        //
        //     // Send the initial message + the username
        //     CohtmlPatches.SetKeyboardContent(currentInput + usernames[index % usernames.Count]);
        // };
    }

    /// <summary>
    /// Send the chatbox message to the network
    /// </summary>
    private static void OnKeyboardSubmitChatboxMessage(string message)
    {
        ModNetwork.SendMessage(API.MessageSource.Internal, "", message, true, true, true);
        SetIsTyping(API.MessageSource.Internal, false, false);
        SetIsTypingAnimatorParameter(false);
    }

    /// <summary>
    /// Whether the chatbox keyboard is opened or not
    /// </summary>
    private static bool IsChatBoxKeyboardOpened()
    {
        return KeyboardManager.Instance != null
               && KeyboardManager.Instance.IsViewShown
               && KeyboardManager.Instance._keyboardCallback == OnKeyboardSubmitChatboxMessageDelegate;
    }

    internal static void OpenKeyboard(string initialMessage)
    {
        // Keyboard is not ready
        if (KeyboardManager.Instance == null || !KeyboardManager.Instance.IsReady) return;
        SetIsTypingAnimatorParameter(true);
        string title = $"Send a Chatbox {(ModConfig.MeAlsoSendMsgsToTTS.Value ? "& TTS " : "")}Message";
        KeyboardManager.Instance.ShowKeyboard(initialMessage, OnKeyboardSubmitChatboxMessageDelegate, "Chatbox Message",
            maxCharacterCount: 1000, multiLine: true, title: title);
    }

    internal static void SetIsTyping(API.MessageSource msgSource, bool isTyping, bool notification)
    {
        if (PlayerSetup.Instance == null) return;
        ModNetwork.SendTyping(msgSource, isTyping, notification);
    }

    private static void SetIsTypingAnimatorParameter(bool isTyping)
    {
        PlayerSetup.Instance.AnimatorManager.SetParameter(AnimatorParameterTyping, isTyping);
        var animator = PlayerSetup.Instance.Animator;
        if (animator != null)
        {
            animator.SetBool(AnimatorParameterTypingLocal, isTyping);
        }
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Y) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)
            && KeyboardManager.Instance != null && !KeyboardManager.Instance.IsViewShown)
        {
            OpenKeyboard("");
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerNameplate), nameof(PlayerNameplate.Start))]
        public static void After_PlayerNameplate_Start(PlayerNameplate __instance)
        {
            // Initialize the ChatBox component for the player
            try
            {
                #if DEBUG
                MelonLogger.Msg($"Attaching the ChatBoxBehavior to {__instance.player.ownerId}");
                #endif
                __instance.gameObject.AddComponent<ChatBoxBehavior>();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerNameplate_Start)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(KeyboardManager), nameof(KeyboardManager.OnKeyboardClose))]
        public static void After_KeyboardManager_OnKeyboardClose(KeyboardManager __instance)
        {
            // Initialize the ChatBox component for the player
            try
            {
                #if DEBUG
                MelonLogger.Msg($"Detected a keyboard close event");
                #endif

                // Set typing to false if the keyboard is closed
                if (__instance._keyboardCallback == OnKeyboardSubmitChatboxMessageDelegate)
                {
                    SetIsTypingAnimatorParameter(false);
                    SetIsTyping(API.MessageSource.Internal, false, false);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_KeyboardManager_OnKeyboardClose)}");
                MelonLogger.Error(e);
            }
        }
    }
}
