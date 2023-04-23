using System.Collections;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util.Object_Behaviour;
using TMPro;
using UnityEngine;

#if DEBUG
using MelonLoader;
#endif

namespace Kafe.ChatBox;

public class ChatBoxBehavior : MonoBehaviour {

    private const string ChildTypingName = "Typing";
    private const string ChildTextBubbleName = "Text Bubble";
    private const string ChildTextBubbleOutputName = "Output";

    private static readonly Vector3 TypingDefaultLocalScale = new Vector3(0.5f, 0.5f, 0.5f) * 0.8f;
    private static readonly Vector3 ChatBoxDefaultLocalScale = new(0.002f, 0.002f, 0.002f);

    private static readonly Dictionary<string, ChatBoxBehavior> ChatBoxes;

    private PlayerNameplate _nameplate;

    private GameObject _root;

    private GameObject _typingGo;
    private readonly List<GameObject> _typingGoChildren = new();
    private AudioSource _typingAudioSource;

    private GameObject _textBubbleGo;
    private TextMeshProUGUI _textBubbleOutputTMP;
    private AudioSource _textBubbleAudioSource;

    private CanvasGroup _canvasGroup;

    private int _lastTypingIndex;

    private string _playerGuid;
    private const float NameplateOffset = 0.1f;
    private const float NameplateDistanceMultiplier = 0.25f;

    // Config updates
    private static float _volume;
    private static float _chatBoxSize;
    private static float _chatBoxOpacity;
    private static float _notificationSoundMaxDistance;

    static ChatBoxBehavior() {

        ChatBoxes = new Dictionary<string, ChatBoxBehavior>();

        ChatBox.OnReceivedTyping += (guid, isTyping) => {
            #if DEBUG
            MelonLogger.Msg($"Received a Typing message from: {guid} -> {isTyping}");
            #endif
            if (ChatBoxes.TryGetValue(guid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnTyping(isTyping);
            }
        };

        ChatBox.OnReceivedMessage += (guid, msg) => {
            #if DEBUG
            MelonLogger.Msg($"Received a Message message from: {guid} -> {msg}");
            #endif
            if (ChatBoxes.TryGetValue(guid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnMessage(msg);
            }
        };

        // Config Listeners
        _volume = ModConfig.MeSoundsVolume.Value;
        ModConfig.MeSoundsVolume.OnEntryValueChanged.Subscribe((_, newValue) => {
            _volume = newValue;
            UpdateChatBoxes();
        });
        _chatBoxSize = ModConfig.MeChatBoxSize.Value;
        ModConfig.MeChatBoxSize.OnEntryValueChanged.Subscribe((_, newValue) => {
            _chatBoxSize = newValue;
            UpdateChatBoxes();
        });
        _chatBoxOpacity = ModConfig.MeChatBoxOpacity.Value;
        ModConfig.MeChatBoxOpacity.OnEntryValueChanged.Subscribe((_, newValue) => {
            _chatBoxOpacity = newValue;
            UpdateChatBoxes();
        });
        _notificationSoundMaxDistance = ModConfig.MeNotificationSoundMaxDistance.Value;
        ModConfig.MeNotificationSoundMaxDistance.OnEntryValueChanged.Subscribe((_, newValue) => {
            _notificationSoundMaxDistance = newValue;
            UpdateChatBoxes();
        });
    }

    private static void UpdateChatBoxes() {
        foreach (var chatBox in ChatBoxes.Select(chatBoxKeyValue => chatBoxKeyValue.Value).Where(chatBox => chatBox != null)) {
            chatBox.UpdateChatBox();
        }
    }
    private void UpdateChatBox() {
        _canvasGroup.alpha = _chatBoxOpacity;
        _textBubbleAudioSource.volume = _volume;
        _typingAudioSource.volume = _volume;
        _root.transform.localPosition = new Vector3(0, NameplateOffset + NameplateDistanceMultiplier * _chatBoxSize, 0);
        _root.transform.localScale = ChatBoxDefaultLocalScale * _chatBoxSize;
        _typingAudioSource.maxDistance = _notificationSoundMaxDistance;
        _textBubbleAudioSource.maxDistance = _notificationSoundMaxDistance;
    }

    private void Start() {

        _nameplate = transform.GetComponent<PlayerNameplate>();
        _playerGuid = _nameplate.player.ownerId;

        // Setup the game object
        _root = Instantiate(ModConfig.ChatBoxPrefab, transform);
        // prefab.layer = LayerMask.NameToLayer("UI Internal");
        _root.name = $"[{nameof(ChatBox)} Mod]";
        _root.transform.rotation = _nameplate.transform.rotation;

        // Handle the chat box postion and scale
        _root.AddComponent<CameraFacingObject>();

        // Add Canvas Group
        _canvasGroup = _root.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.ignoreParentGroups = false;
        _canvasGroup.interactable = false;

        // Get the references for the Typing stuff and Text stuff
        var typingTransform = _root.transform.Find(ChildTypingName);

        // Handle the typing scale
        typingTransform.localScale = TypingDefaultLocalScale * _chatBoxSize;

        _typingGo = typingTransform.gameObject;
        for (var i = 0; i < typingTransform.childCount; i++) {
            _typingGoChildren.Add(typingTransform.GetChild(i).gameObject);
        }
        _textBubbleGo = _root.transform.Find(ChildTextBubbleName).gameObject;
        var tmpGo = _textBubbleGo.transform.Find(ChildTextBubbleOutputName);
        _textBubbleOutputTMP = tmpGo.GetComponent<TextMeshProUGUI>();

        // Add Typing Audio Source
        _typingAudioSource = _typingGo.AddComponent<AudioSource>();
        _typingAudioSource.spatialBlend = 1f;
        _typingAudioSource.minDistance = 0.5f;
        _typingAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _typingAudioSource.clip = ModConfig.AudioClips[ModConfig.Sound.Typing];
        _typingAudioSource.loop = false;
        _typingAudioSource.playOnAwake = false;

        // Add Message Audio Source
        _textBubbleAudioSource = _textBubbleGo.AddComponent<AudioSource>();
        _textBubbleAudioSource.spatialBlend = 1f;
        _textBubbleAudioSource.minDistance = 0.5f;
        _textBubbleAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _textBubbleAudioSource.clip = ModConfig.AudioClips[ModConfig.Sound.Message];
        _textBubbleAudioSource.loop = false;
        _textBubbleAudioSource.playOnAwake = false;

        UpdateChatBox();

        // Add to the cache
        ChatBoxes[_playerGuid] = this;
    }

    private void OnDestroy() {
        if (ChatBoxes.ContainsKey(_playerGuid)) ChatBoxes.Remove(_playerGuid);
    }

    private void StopTyping() {
        if (_typingGo.activeSelf) {
            StopCoroutine(nameof(ResetIsTypingAfterDelay));
        }
        _typingGo.SetActive(false);
        _lastTypingIndex = 0;
    }

    private void OnTyping(bool isTyping) {

        if (!isTyping) {
            StopTyping();
            return;
        }

        // Ignore typing if we got a message staying
        if (_textBubbleGo.activeSelf) return;

        if (_typingGo.activeSelf) {
            StopCoroutine(nameof(ResetIsTypingAfterDelay));
        }

        StartCoroutine(nameof(ResetIsTypingAfterDelay));
        _typingGo.SetActive(true);
        if (ModConfig.MeSoundOnStartedTyping.Value) _typingAudioSource.Play();
    }

    private IEnumerator ResetIsTypingAfterDelay() {

        // Timeout after 60 seconds...
        for (var i = 0; i < 120; i++) {
            _typingGoChildren[_lastTypingIndex].SetActive(false);
            _lastTypingIndex = (_lastTypingIndex + 1) % _typingGoChildren.Count;
            _typingGoChildren[_lastTypingIndex].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }

        StopTyping();
    }

    private void OnMessage(string msg) {
        StopTyping();

        // Update the text
        if (_textBubbleOutputTMP.text != msg) {
            _textBubbleOutputTMP.text = msg;
        }

        if (_textBubbleGo.activeSelf) {
            StopCoroutine(nameof(ResetTextAfterDelay));
        }
        StartCoroutine(nameof(ResetTextAfterDelay));
        _textBubbleGo.SetActive(true);
        if (ModConfig.MeSoundOnMessage.Value) _textBubbleAudioSource.Play();
    }

    private IEnumerator ResetTextAfterDelay() {
        yield return new WaitForSeconds(ModConfig.MeMessageTimeoutSeconds.Value);
        _textBubbleGo.SetActive(false);
    }
}
