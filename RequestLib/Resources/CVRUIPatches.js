
// Shared
const RequestLibMod = {}

// Load our css
RequestLibMod.style = document.createElement('style');
RequestLibMod.style.innerHTML = `
    .request-lib-mod-root {
    }
    .request-lib-mod-header {
      height: 42px;
      position: absolute;
      top: 0;
      left: 0px;
      font-size: var(--font-size-big);
    }
    .request-lib-mod-content {
      position: absolute;
      top: 50px;
      left: 0px;
      font-weight: normal;
    }
  `;
document.head.appendChild(RequestLibMod.style);

RequestLibMod.SendResponse = (requestId, response) => engine.call('RequestLibModResponse', requestId, response);

// Function to generate a request display
RequestLibMod.GetRequestDisplay = (req) => {

    const msgContent = document.createElement('div');
    msgContent.classList.add('notification');
    msgContent.classList.add('request-lib-mod-root');
    // msgContent.classList.add('invite');

    const msgName = document.createElement('div');
    // msgName.classList.add('header');
    msgName.classList.add('request-lib-mod-header');
    msgName.textContent = req.Name;
    msgContent.appendChild(msgName);


    const msgText = document.createElement('div');
    // msgText.classList.add('content');
    msgText.classList.add('request-lib-mod-content');

    const senderName = document.createElement('p');
    senderName.textContent = req.SenderName;
    msgText.appendChild(senderName);

    const msgTextContent = document.createElement('p');
    msgTextContent.textContent = req.TextQM;
    msgText.appendChild(msgTextContent);

    msgContent.appendChild(msgText);

    for (const option of req.Options) {
        // Only allow Accept and Decline, since this notification doesn't support other stuff
        if (option.Name !== 'Accept' && option.Name !== 'Decline') continue;

        const button = document.createElement('div');
        button.classList.add(option.Image);
        button.classList.add('button');
        button.innerHTML = `<div class="icon"></div>`;
        button.addEventListener('click', () => RequestLibMod.SendResponse(req.ID, option.Name))
        msgContent.appendChild(button);
    }

    return msgContent;
}

// Create the update requests function
RequestLibMod.UpdateRequests = (requestLibModRequests) => {
    const notificationWrapper = document.querySelector('.notification-container .notifications-wrapper');
    for (const request of requestLibModRequests) {
        notificationWrapper.appendChild(RequestLibMod.GetRequestDisplay(request));
    }
}

// Listen to Request Events from the mod
engine.on("RequestLibModRequestsUpdate", (requestLibModRequests) => RequestLibMod.UpdateRequests(requestLibModRequests));

console.log('RequestLib patches ran successfully!')
