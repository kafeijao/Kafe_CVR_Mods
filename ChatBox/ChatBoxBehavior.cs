using System.Collections;
using System.Text.RegularExpressions;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.Object_Behaviour;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    internal static readonly Color TealTransparency = new Color(0.2494215f, 0.8962264f, 0.8223274f, 0.75f);
    // Mod
    internal static readonly Color PinkTransparency = new Color(1f, 0.4009434f, 0.9096327f, 0.75f);
    // Astro - Official InuCast Salmon Colour^tm - Shiba Inu Shaped Bubble
    private readonly Color AstroTransparency = new Color(1f, 0.4980392f, 0.4980392f, 0.75f);

    private static readonly Vector3 ChatBoxDefaultLocalScale = new(0.002f, 0.002f, 0.002f);
    private static readonly Vector3 TypingScaleMultiplier = new(0.2f, 0.2f, 0.2f);

    private static readonly Dictionary<string, ChatBoxBehavior> ChatBoxes;

    private PlayerNameplate _nameplate;

    private GameObject _root;

    private Transform _typingTransform;
    private GameObject _typingGo;
    private Image _typingBackground;
    private readonly List<GameObject> _typingGoChildren = new();
    private AudioSource _typingAudioSource;

    private Transform _textBubbleTransform;
    private GameObject _textBubbleGo;
    private Image _textBubbleHexagonImg;
    private Image _textBubbleRoundImg;
    private TextMeshProUGUI _textBubbleOutputTMP;
    private AudioSource _textBubbleAudioSource;
    private AudioSource _textBubbleMentionAudioSource;

    private CanvasGroup _canvasGroup;

    private int _lastTypingIndex;

    private string _playerGuid;
    private const float NameplateOffsetBubble = 160f;
    private const float NameplateOffsetBubbleMultiplier = 110f;

    private const float NameplateOffsetXTyping = 150f;
    private const float NameplateOffsetXTypingMultiplier = 45f;
    private const float NameplateOffsetYTyping = -65f;
    private const float NameplateOffsetYTypingMultiplier = 25f;

    private static Coroutine _resetTextAfterDelayCoroutine;
    private static Coroutine _resetTypingAfterDelayCoroutine;

    // Config updates
    private static float _volume;
    private static float _chatBoxSize;
    private static float _chatBoxOpacity;
    private static float _notificationSoundMaxDistance;

    static ChatBoxBehavior() {

        ChatBoxes = new Dictionary<string, ChatBoxBehavior>();

        API.OnIsTypingReceived += chatBoxIsTyping => {

            // Ignore our own messages
            if (chatBoxIsTyping.SenderGuid == MetaPort.Instance.ownerId) return;

            // If visibility options say we shouldn't display, don't :)
            if (!ConfigJson.ShouldShowMessage(chatBoxIsTyping.SenderGuid)) return;

            // Handle typing source ignores
            if (ModConfig.MeIgnoreOscMessages.Value && chatBoxIsTyping.Source == API.MessageSource.OSC) return;
            if (ModConfig.MeIgnoreModMessages.Value && chatBoxIsTyping.Source == API.MessageSource.Mod) return;

            #if DEBUG
            MelonLogger.Msg($"Received a Typing message from: {chatBoxIsTyping.SenderGuid} -> {chatBoxIsTyping.IsTyping}");
            #endif
            if (ChatBoxes.TryGetValue(chatBoxIsTyping.SenderGuid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnTyping(chatBoxIsTyping.IsTyping, chatBoxIsTyping.TriggerNotification);
            }
        };

        API.OnMessageReceived += chatBoxMessage => {

            // Ignore messages that are not supposed to be displayed
            if (!chatBoxMessage.DisplayOnChatBox) return;

            // Ignore our own messages
            if (chatBoxMessage.SenderGuid == MetaPort.Instance.ownerId) return;

            // If visibility options say we shouldn't display, don't :)
            if (!ConfigJson.ShouldShowMessage(chatBoxMessage.SenderGuid)) return;

            // Handle typing source ignores
            if (ModConfig.MeIgnoreOscMessages.Value && chatBoxMessage.Source == API.MessageSource.OSC) return;
            if (ModConfig.MeIgnoreModMessages.Value && chatBoxMessage.Source == API.MessageSource.Mod) return;

            var msg = chatBoxMessage.Message;

            // Check for profanity and replace if needed
            if (ModConfig.MeProfanityFilter.Value) {
                msg = Regex.Replace(msg, ConfigJson.GetProfanityPattern(), m => new string('*', m.Length), RegexOptions.IgnoreCase);
            }

            #if DEBUG
            MelonLogger.Msg($"Received a Message message from: {chatBoxMessage.SenderGuid} -> {msg}");
            #endif
            if (ChatBoxes.TryGetValue(chatBoxMessage.SenderGuid, out var chatBoxBehavior)) {
                chatBoxBehavior.OnMessage(chatBoxMessage.Source, msg, chatBoxMessage.TriggerNotification);
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
        _textBubbleMentionAudioSource.volume = _volume;

        _textBubbleTransform.localPosition = new Vector3(0, NameplateOffsetBubble + NameplateOffsetBubbleMultiplier * (_chatBoxSize - 1), 0);
        _textBubbleTransform.localScale = Vector3.one * _chatBoxSize;

        // Resizing the typing thing is stupid ?
        // _typingTransform.locsalPosition = new Vector3(NameplateOffsetXTyping + NameplateOffsetXTypingMultiplier * _chatBoxSize, NameplateOffsetYTyping + NameplateOffsetYTypingMultiplier * _chatBoxSize, 0);
        // _typingTransform.localScale = TypingScaleMultiplier * (_chatBoxSize + 1);

        _typingAudioSource.maxDistance = _notificationSoundMaxDistance;
        _textBubbleAudioSource.maxDistance = _notificationSoundMaxDistance;

        void SetCustomRolloff(bool isGlobal) {
            var customRolloff = new AnimationCurve();
            customRolloff.AddKey(0.0f, 1.0f);
            customRolloff.AddKey(_notificationSoundMaxDistance, isGlobal ? 1.0f : 0f);
            _textBubbleMentionAudioSource.maxDistance = _notificationSoundMaxDistance;
            _textBubbleMentionAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloff);
        }
        SetCustomRolloff(ModConfig.MeMessageMentionGlobalAudio.Value);
        ModConfig.MeMessageMentionGlobalAudio.OnEntryValueChanged.Subscribe((_, newValue) => {
            SetCustomRolloff(newValue);
        });
    }

    private void Start() {

        _nameplate = transform.GetComponent<PlayerNameplate>();
        _playerGuid = _nameplate.player.ownerId;

        // Setup the game object
        _root = Instantiate(ModConfig.ChatBoxPrefab, transform);
        // prefab.layer = LayerMask.NameToLayer("UI Internal");
        _root.name = $"[{nameof(ChatBox)} Mod]";
        _root.transform.rotation = _nameplate.transform.rotation;
        _root.transform.localPosition = Vector3.zero;
        _root.transform.localScale = ChatBoxDefaultLocalScale;

        // Handle the chat box position and scale
        _root.AddComponent<CameraFacingObject>();

        // Add Canvas Group
        _canvasGroup = _root.GetComponent<CanvasGroup>();

        // Get the references for the Typing stuff and Text stuff
        _typingTransform = _root.transform.Find(ChildTypingName);

        // Typing
        _typingTransform.localPosition = new Vector3(NameplateOffsetXTyping, NameplateOffsetYTyping, 0);
        _typingTransform.localScale = TypingScaleMultiplier;
        _typingGo = _typingTransform.gameObject;
        _typingBackground = _typingGo.transform.GetChild(0).GetComponent<Image>();
        _typingBackground.color = Green;
        for (var i = 0; i < _typingBackground.transform.childCount; i++) {
            _typingGoChildren.Add(_typingBackground.transform.GetChild(i).gameObject);
        }

        // Text Bubble
        _textBubbleGo = _root.transform.Find(ChildTextBubbleName).gameObject;
        _textBubbleTransform = _textBubbleGo.transform;
        var tmpGo = _textBubbleTransform.Find(ChildTextBubbleOutputName);
        _textBubbleHexagonImg = _textBubbleTransform.Find(ChildTextBubbleHexagonName).GetComponent<Image>();
        _textBubbleRoundImg = _textBubbleTransform.Find(ChildTextBubbleRoundName).GetComponent<Image>();
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

        // Add Message Mention Audio Source
        _textBubbleMentionAudioSource = _textBubbleGo.AddComponent<AudioSource>();
        _textBubbleMentionAudioSource.spatialBlend = 1f;
        _textBubbleMentionAudioSource.minDistance = 0.5f;
        _textBubbleMentionAudioSource.rolloffMode = AudioRolloffMode.Custom;
        _textBubbleMentionAudioSource.clip = ModConfig.AudioClips[ModConfig.Sound.MessageMention];
        _textBubbleMentionAudioSource.loop = false;
        _textBubbleMentionAudioSource.playOnAwake = false;

        UpdateChatBox();

        // Add to the cache
        ChatBoxes[_playerGuid] = this;
    }

    private void OnDestroy() {
        if (ChatBoxes.ContainsKey(_playerGuid)) ChatBoxes.Remove(_playerGuid);
    }

    private void StopTyping() {
        if (_resetTypingAfterDelayCoroutine != null) StopCoroutine(_resetTypingAfterDelayCoroutine);
        _typingGo.SetActive(false);
        _lastTypingIndex = 0;
    }

    private void OnTyping(bool isTyping, bool notify) {

        if (!isTyping) {
            StopTyping();
            return;
        }

        // Ignore typing if we got a message staying
        // if (_textBubbleGo.activeSelf) return;

        var wasOn = _typingGo.activeSelf;

        if (_resetTypingAfterDelayCoroutine != null) StopCoroutine(_resetTypingAfterDelayCoroutine);
        _resetTypingAfterDelayCoroutine = StartCoroutine(ResetIsTypingAfterDelay());
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

        _resetTypingAfterDelayCoroutine = null;
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

    private API.MessageSource _previousSource = API.MessageSource.Internal;

    private void OnMessage(API.MessageSource source, string msg, bool notify) {
        StopTyping();

        // Ignore non-internal msg if currently displaying an internal one, and Cancel the bubble reset
        if (_textBubbleGo.activeSelf) {
            if (_previousSource == API.MessageSource.Internal && source != API.MessageSource.Internal) return;
            if (_resetTextAfterDelayCoroutine != null) StopCoroutine(_resetTextAfterDelayCoroutine);
        }
        _previousSource = source;

        // Update the text
        if (_textBubbleOutputTMP.text != msg) {
            _textBubbleOutputTMP.text = msg;
        }

        _resetTextAfterDelayCoroutine = StartCoroutine(ResetTextAfterDelay(msg.Length));
        _textBubbleGo.SetActive(true);
        _textBubbleHexagonImg.gameObject.SetActive(notify);
        _textBubbleRoundImg.gameObject.SetActive(!notify);
        SetColor(source);
        if (notify && ModConfig.MeSoundOnMessage.Value) {
            _textBubbleAudioSource.Play();
            if (msg.IndexOf($"@{AuthManager.Username}", StringComparison.OrdinalIgnoreCase) >= 0) {
                _textBubbleMentionAudioSource.PlayDelayed(0.5f);
            }
        }
    }

    private IEnumerator ResetTextAfterDelay(int msgLength) {
        var timeout = ModConfig.MeMessageTimeoutDependsLength.Value
            ? Mathf.Clamp(msgLength / 10f, ModConfig.MessageTimeoutMin, ModConfig.MeMessageTimeoutSeconds.Value)
            : ModConfig.MeMessageTimeoutSeconds.Value;
        yield return new WaitForSeconds(timeout);
        _textBubbleGo.SetActive(false);
        _resetTextAfterDelayCoroutine = null;
    }
}
