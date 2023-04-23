using System.Collections;
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

    private static readonly Dictionary<string, ChatBoxBehavior> ChatBoxes;

    private PlayerNameplate _nameplate;
    private GameObject _typingGo;
    private readonly List<GameObject> _typingGoChildren = new();
    private GameObject _textBubbleGo;
    private TextMeshProUGUI _textBubbleOutputTMP;

    private int _lastTypingIndex;

    private string _playerGuid;
    private const float NameplateOffset = 0.35f;

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
    }

    private void Start() {

        _nameplate = transform.GetComponent<PlayerNameplate>();
        _playerGuid = _nameplate.player.ownerId;

        // Setup the game object
        var prefab = Instantiate(ModConfig.ChatBoxPrefab, transform);
        // prefab.layer = LayerMask.NameToLayer("UI Internal");
        prefab.name = $"[{nameof(ChatBox)} Mod]";
        prefab.transform.localPosition = new Vector3(0, NameplateOffset, 0);
        prefab.transform.rotation = _nameplate.transform.rotation;
        prefab.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        prefab.AddComponent<CameraFacingObject>();

        // Get the references for the Typing stuff and Text stuff
        var typingTransform = prefab.transform.Find(ChildTypingName);
        typingTransform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        _typingGo = typingTransform.gameObject;
        for (var i = 0; i < typingTransform.childCount; i++) {
            _typingGoChildren.Add(typingTransform.GetChild(i).gameObject);
        }
        _textBubbleGo = prefab.transform.Find(ChildTextBubbleName).gameObject;
        var tmpGo = _textBubbleGo.transform.Find(ChildTextBubbleOutputName);
        _textBubbleOutputTMP = tmpGo.GetComponent<TextMeshProUGUI>();

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
    }

    private IEnumerator ResetTextAfterDelay() {
        yield return new WaitForSeconds(10f);
        _textBubbleGo.SetActive(false);
    }
}
