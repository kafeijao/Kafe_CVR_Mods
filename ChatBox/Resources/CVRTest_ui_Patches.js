
// Shared
const ChatBoxIsKeyboardOpen = () => {
    return document.getElementById('keyboard').classList.contains('in');
}
const ChatBoxKeyboardInputElement = document.getElementById('keyoard-input');



// Listen to Auto presses to auto-complete usernames that are in the instance and Cancel Event presses
const ChatBoxOriginalSendFuncKey = sendFuncKey;
let ChatBoxPreviousAutoComplete = null;
let ChatBoxPreviousAutoCompleteIndex = 0;

// Remove references to the old event listener
for(let i=0; i < keyboardFuncKeys.length; i++){
    keyboardFuncKeys[i].removeEventListener('mousedown', sendFuncKey);
}
sendFuncKey = function (_e) {

    const input = document.getElementById('keyoard-input');
    const func = _e.target.getAttribute('data-key-func');

    // If the key is Back, call the original and trigger our event
    if (func === 'BACK') {
        ChatBoxOriginalSendFuncKey(_e);
        engine.trigger("ChatBoxClosedKeyboard");
        return;
    }

    if (ChatBoxPreviousAutoComplete == null) {
        ChatBoxPreviousAutoCompleteIndex = 0;
        ChatBoxPreviousAutoComplete = input.value;
    }
    else {
        ChatBoxPreviousAutoCompleteIndex++;
    }

    if (func === 'PREV') {
        engine.call("ChatBoxPrevious");
        return;
    }

    if (func === 'AUTO') {
        engine.call("ChatBoxAutoComplete", ChatBoxPreviousAutoComplete, ChatBoxPreviousAutoCompleteIndex);
    }
    else {
        ChatBoxPreviousAutoComplete = null;
        ChatBoxOriginalSendFuncKey(_e);
    }
};

// Add the updated event listener
for(let i=0; i < keyboardFuncKeys.length; i++){
    keyboardFuncKeys[i].addEventListener('mousedown', sendFuncKey);
}



// Add the auto button to the keyboard as a function key
const ChatBoxLastKeyboardRow = document.querySelector('.keyboard-row.r6')
const ChatBoxAutoCompleteKey = document.createElement('div');
ChatBoxAutoCompleteKey.classList.add('keyboard-func')
ChatBoxAutoCompleteKey.setAttribute('data-key-func', 'AUTO');
ChatBoxAutoCompleteKey.textContent = "Auto";
ChatBoxAutoCompleteKey.addEventListener('mousedown', sendFuncKey);
ChatBoxLastKeyboardRow.insertBefore(ChatBoxAutoCompleteKey, ChatBoxLastKeyboardRow.lastChild);

// Add the previous button to the keyboard as a function key
const ChatBoxPreviousKey = document.createElement('div');
ChatBoxPreviousKey.classList.add('keyboard-func')
ChatBoxPreviousKey.setAttribute('data-key-func', 'PREV');
ChatBoxPreviousKey.textContent = "Prev";
ChatBoxPreviousKey.addEventListener('mousedown', sendFuncKey);
ChatBoxLastKeyboardRow.insertBefore(ChatBoxPreviousKey, ChatBoxLastKeyboardRow.lastChild);



// Listen to Auto events from the mod
engine.on("ChatBoxAutoComplete", function() {
    if (ChatBoxIsKeyboardOpen()){
        ChatBoxAutoCompleteKey.dispatchEvent(new Event('mousedown'));
    }
});



// Listen to Key Presses to send the typing

// Detect when pressing the keys on the keyboard
const ChatBoxOriginalSendKey = sendKey;
for(var i=0; i < keyboardKeys.length; i++){
    keyboardKeys[i].removeEventListener('mousedown', sendKey);
}
sendKey = function (_e) {
    ChatBoxOriginalSendKey(_e);
    ChatBoxPreviousAutoComplete = null;
    engine.trigger("ChatBoxIsTyping");
};
for(var i=0; i < keyboardKeys.length; i++){
    keyboardKeys[i].addEventListener('mousedown', sendKey);
}

// Detect when text is added to the input
ChatBoxKeyboardInputElement.addEventListener("input", function() {
    ChatBoxPreviousAutoComplete = null;
    engine.trigger("ChatBoxIsTyping");
});



// Update the keyboard content
engine.on("ChatBoxUpdateContent", function(content){
    const input = document.getElementById('keyoard-input');
    if(keyboardMaxLength > 0 && content >= keyboardMaxLength) return;
    input.value = content;
    input.selectionStart = input.selectionEnd = input.value.length;
});



// Send the arrow up and arrow down events
ChatBoxKeyboardInputElement.addEventListener("keyup", function(e){
    if (e.keyCode === 38) {
        if (ChatBoxIsKeyboardOpen()) {
            engine.trigger("ChatBoxArrowUp");
        }
    }
    if (e.keyCode === 40) {
        if (ChatBoxIsKeyboardOpen()) {
            engine.trigger("ChatBoxArrowDown");
        }
    }
});



// Blur keyboard input
engine.on("ChatBoxBlurKeyboardInput", function(){
    document.getElementById('keyoard-input').blur();
});


console.log('ChatBox patches ran successfully!')
