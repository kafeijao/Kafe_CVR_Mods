
// Shared
const RequestLibMod = {}

// Create filter button
RequestLibMod.messagesFilter = document.createElement('div');
RequestLibMod.messagesFilter.classList.add('filter-option');
RequestLibMod.messagesFilter.classList.add('button');
RequestLibMod.messagesFilter.classList.add('message-mod-requests');
RequestLibMod.messagesFilter.addEventListener('click', () => switchMessageCategorie('message-mod-requests', RequestLibMod.messagesFilter));
RequestLibMod.messagesFilter.innerHTML = 'Mod Requests <div class="filter-number">0</div>';
document.querySelector('#messages .filter-content').appendChild(RequestLibMod.messagesFilter);

// Create messages container
RequestLibMod.messagesContent = document.createElement('div');
RequestLibMod.messagesContent.id = 'message-mod-requests';
RequestLibMod.messagesContent.classList.add('message-categorie');
document.querySelector('#messages .list-content').appendChild(RequestLibMod.messagesContent);

// Create messages wrapper
RequestLibMod.messagesContentContainer = document.createElement('div');
RequestLibMod.messagesContentContainer.classList.add('message-list');
RequestLibMod.messagesContent.appendChild(RequestLibMod.messagesContentContainer);

RequestLibMod.SendResponse = (requestId, response) => engine.call('RequestLibModResponse', requestId, response);

// Function to generate a request display
RequestLibMod.GetRequestDisplay = (req) => {

    const msgContent = document.createElement('div');
    msgContent.classList.add('message-content');

    const msgTextWrapper = document.createElement('div');
    msgTextWrapper.classList.add('message-text-wrapper');
    msgContent.appendChild(msgTextWrapper);

    const msgName = document.createElement('div');
    msgName.classList.add('message-name');
    msgName.textContent = `${req.Name.makeSafe()} [${req.SenderName.makeSafe()}]`;
    msgTextWrapper.appendChild(msgName);

    const msgText = document.createElement('div');
    msgText.classList.add('message-text');
    msgText.textContent = req.Text.makeSafe();
    msgTextWrapper.appendChild(msgText);

    for (const option of req.Options) {
        const button = document.createElement('div');
        button.classList.add('message-btn');
        button.classList.add('button');
        button.innerHTML = `<img src="gfx/${option.Image}.svg"> ${option.Name}`;
        button.addEventListener('click', () => RequestLibMod.SendResponse(req.ID, option.Name))
        msgContent.appendChild(button);
    }

    return msgContent;
}

// Create the update requests function
RequestLibMod.UpdateRequests = (requestLibModRequests) => {

    document.querySelector('.message-mod-requests > .filter-number').textContent = requestLibModRequests.length;

    // Set the empty message
    if (requestLibModRequests.length === 0) {
        RequestLibMod.messagesContentContainer.innerHTML = `
            <div class="noMessagesWrapper">
                <div class="noMessagesInfo">
                    <img src="gfx\\attention.svg">
                    <div>No mod requests found.</div>
                </div>
            </div>`;
    }

    // Otherwise iterate through the requests and replace them
    else {
        RequestLibMod.messagesContentContainer.innerHTML = '';
        for (const request of requestLibModRequests) {
            RequestLibMod.messagesContentContainer.appendChild(RequestLibMod.GetRequestDisplay(request));
        }
    }
}

// Listen to Request Events from the mod
engine.on("RequestLibModRequestsUpdate", (requestLibModRequests) => RequestLibMod.UpdateRequests(requestLibModRequests));

console.log('RequestLib patches ran successfully!')
