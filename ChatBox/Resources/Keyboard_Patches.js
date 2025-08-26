const ChatBoxKeyboardInputElement = document.getElementById('keyboard-input');

// Detect when text is added to the input
ChatBoxKeyboardInputElement.addEventListener("input", function() {
    ChatBoxPreviousAutoComplete = null;
    engine.trigger("ChatBoxIsTyping");
});


console.log('ChatBox patches ran successfully!')
