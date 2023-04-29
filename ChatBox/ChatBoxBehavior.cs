using System.Collections;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.Object_Behaviour;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if DEBUG
using MelonLoader;
#endif

namespace Kafe.ChatBox;

public class ChatBoxBehavior : MonoBehaviour {

    private const string ChildTypingName = "Typing";
    private const string ChildTextBubbleName = "Text Bubble";
    private const string ChildTextBubbleOutputName = "Output";
    private const string ChildTextBubbleHexagonName = "Bubble Hexagon";
    private const string ChildTextBubbleRoundName = "Bubble Round";

    // Normal
    private readonly Color Green = new Color(0.2235294f, 0.7490196f, 0f);
    private readonly Color GreenTransparency = new Color(0.2235294f, 0.7490196f, 0f, 0.75f);
    // Whisper
    private readonly Color BlueTransparency = new Color(0f, 0.6122726f, 0.7490196f, 0.75f);
    // OSC
    private readonly Color TealTransparency = new Color(0.2494215f, 0.8962264f, 0.8223274f, 0.75f);
    // Mod
    private readonly Color PinkTransparency = new Color(1f, 0.4009434f, 0.9096327f, 0.75f);
    // Astro - Official InuCast Salmon Colour^tm - Shiba Inu Shaped Bubble
    private readonly Color AstroTransparency = new Color(1f, 0.4980392f, 0.4980392f, 0.75f);

    private static readonly Vector3 TypingDefaultLocalScale = new Vector3(0.5f, 0.5f, 0.5f) * 0.8f;
    private static readonly Vector3 ChatBoxDefaultLocalScale = new(0.002f, 0.002f, 0.002f);

    private static readonly Dictionary<string, ChatBoxBehavior> ChatBoxes;

    private PlayerNameplate _nameplate;

    private GameObject _root;

    private GameObject _typingGo;
    private Image _typingBackground;
    private readonly List<GameObject> _typingGoChildren = new();
    private AudioSource _typingAudioSource;

    private GameObject _textBubbleGo;
    private Image _textBubbleHexagonImg;
    private Image _textBubbleRoundImg;
    private TextMeshProUGUI _textBubbleOutputTMP;
    private AudioSource _textBubbleAudioSource;

    private CanvasGroup _canvasGroup;

    private int _lastTypingIndex;

    private string _playerGuid;
    private const float NameplateOffset = 0.1f;
    private const float NameplateDistanceMultiplier = 0.25f;

    private static Coroutine _resetTextAfterDelayCoroutine;

    // Config updates
    private static float _volume;
    private static float _chatBoxSize;
    private static float _chatBoxOpacity;
    private static float _notificationSoundMaxDistance;

    static ChatBoxBehavior() {

        ChatBoxes = new Dictionary<string, ChatBoxBehavior>();

        API.OnIsTypingReceived += (source, senderGuid, isTyping, notify) => {

            // Ignore our own messages
            if (senderGuid == MetaPort.Instance.ownerId) return;

            // Handle typing source ignores
            if (ModConfig.MeIgnoreOscMessages.Value && source == API.MessageSource.OSC) return;
            if (ModConfig.MeIgnoreModMessages.Value && source == API.MessageSource.Mod) return;

            #if DEBUG
            MelonLogger.Msg($"Received a Typing message from: {senderGuid} -> {isTyping}");
            #endif
            if (ChatBoxes.TryGetValue(senderGuid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnTyping(isTyping, notify);
            }
        };

        API.OnMessageReceived += (source, senderGuid, msg, notify, displayMessage) => {

            // Ignore messages that are not supposed to be displayed
            if (!displayMessage) return;

            // Ignore our own messages
            if (senderGuid == MetaPort.Instance.ownerId) return;

            // Handle typing source ignores
            if (ModConfig.MeIgnoreOscMessages.Value && source == API.MessageSource.OSC) return;
            if (ModConfig.MeIgnoreModMessages.Value && source == API.MessageSource.Mod) return;

            #if DEBUG
            MelonLogger.Msg($"Received a Message message from: {senderGuid} -> {msg}");
            #endif
            if (ChatBoxes.TryGetValue(senderGuid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnMessage(source, msg, notify);
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

        // Handle the chat box position and scale
        _root.AddComponent<CameraFacingObject>();

        // Add Canvas Group
        _canvasGroup = _root.GetComponent<CanvasGroup>();

        // Get the references for the Typing stuff and Text stuff
        var typingTransform = _root.transform.Find(ChildTypingName);

        // Handle the typing scale
        typingTransform.localScale = TypingDefaultLocalScale * _chatBoxSize;

        // Typing
        _typingGo = typingTransform.gameObject;
        _typingBackground = _typingGo.transform.GetChild(0).GetComponent<Image>();
        _typingBackground.color = Green;
        for (var i = 0; i < _typingBackground.transform.childCount; i++) {
            _typingGoChildren.Add(_typingBackground.transform.GetChild(i).gameObject);
        }

        // Text Bubble
        _textBubbleGo = _root.transform.Find(ChildTextBubbleName).gameObject;
        var tmpGo = _textBubbleGo.transform.Find(ChildTextBubbleOutputName);
        _textBubbleHexagonImg = _textBubbleGo.transform.Find(ChildTextBubbleHexagonName).GetComponent<Image>();
        _textBubbleRoundImg = _textBubbleGo.transform.Find(ChildTextBubbleRoundName).GetComponent<Image>();
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

    private void OnTyping(bool isTyping, bool notify) {

        if (!isTyping) {
            StopTyping();
            return;
        }

        // Ignore typing if we got a message staying
        if (_textBubbleGo.activeSelf) return;

        var wasOn = false;

        if (_typingGo.activeSelf) {
            StopCoroutine(nameof(ResetIsTypingAfterDelay));
            wasOn = true;
        }

        StartCoroutine(nameof(ResetIsTypingAfterDelay));
        _typingGo.SetActive(true);
        if (!wasOn && notify && ModConfig.MeSoundOnStartedTyping.Value) _typingAudioSource.Play();
    }

    private IEnumerator ResetIsTypingAfterDelay() {

        // Timeout after 5 seconds without writing...
        for (var i = 0; i < 10; i++) {
            _typingGoChildren[_lastTypingIndex].SetActive(false);
            _lastTypingIndex = (_lastTypingIndex + 1) % _typingGoChildren.Count;
            _typingGoChildren[_lastTypingIndex].SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }

        StopTyping();
    }

    private void SetColor(API.MessageSource source) {
        var color = Green;
        switch (source) {
            case API.MessageSource.Internal:
                color = GreenTransparency;
                break;
            case API.MessageSource.OSC:
                color = TealTransparency;
                break;
            case API.MessageSource.Mod:
                color = BlueTransparency;
                break;
        }
        _textBubbleHexagonImg.color = color;
        _textBubbleRoundImg.color = color;
    }

    private void OnMessage(API.MessageSource source, string msg, bool notify) {
        StopTyping();

        // Update the text
        if (_textBubbleOutputTMP.text != msg) {
            _textBubbleOutputTMP.text = msg;
        }

        if (_textBubbleGo.activeSelf) {
            StopCoroutine(_resetTextAfterDelayCoroutine);
        }
        _resetTextAfterDelayCoroutine = StartCoroutine(ResetTextAfterDelay(msg.Length));
        _textBubbleGo.SetActive(true);
        _textBubbleHexagonImg.gameObject.SetActive(notify);
        _textBubbleRoundImg.gameObject.SetActive(!notify);
        SetColor(source);
        if (notify && ModConfig.MeSoundOnMessage.Value) _textBubbleAudioSource.Play();
    }

    private IEnumerator ResetTextAfterDelay(int msgLength) {
        var timeout = ModConfig.MeMessageTimeoutDependsLength.Value
            ? Mathf.Clamp(msgLength / 10f, ModConfig.MessageTimeoutMin, ModConfig.MeMessageTimeoutSeconds.Value)
            : ModConfig.MeMessageTimeoutSeconds.Value;
        yield return new WaitForSeconds(timeout);
        _textBubbleGo.SetActive(false);
    }
}
