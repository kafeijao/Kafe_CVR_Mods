
const BetterPortalsMod = {};

// Add drop portal button to friends
BetterPortalsMod.userToolbarNode = document.querySelector('.user-toolbar')
BetterPortalsMod.userToolbarJoinButtonNode = document.querySelector('#user-detail .join-btn')

BetterPortalsMod.dropPortalButton = document.createElement('div');
BetterPortalsMod.dropPortalButton.classList.add('toolbar-btn');
BetterPortalsMod.dropPortalButton.classList.add('button');
BetterPortalsMod.dropPortalButton.classList.add('better-portals-drop-btn');
BetterPortalsMod.dropPortalButton.innerHTML = `<img src="gfx/portal.svg">Drop Portal`;

BetterPortalsMod.userToolbarNode.insertBefore(BetterPortalsMod.dropPortalButton, BetterPortalsMod.userToolbarJoinButtonNode.nextSibling);

// Updates the on-click for the drop portal
engine.on("LoadUserDetails", function(_data, _profile){
    const _activity = _data.Instance;
    if (PlayerData.OnlineState && _activity.IsInJoinableInstance && _activity.InstanceId !== null) {
        BetterPortalsMod.dropPortalButton.setAttribute('onclick', 'dropInstancePortal(\''+_activity.InstanceId+'\');');
        BetterPortalsMod.dropPortalButton.classList.remove('disabled');
    }
    else {
        BetterPortalsMod.dropPortalButton.setAttribute('onclick', '');
        BetterPortalsMod.dropPortalButton.classList.add('disabled');
    }
});


// Override the width and font size for the buttons
BetterPortalsMod.buttons = document.querySelectorAll('.user-toolbar .toolbar-btn');
for (let i= 0; BetterPortalsMod.buttons[i]; i++){
    BetterPortalsMod.buttons[i].style.width = `${100 / BetterPortalsMod.buttons.length}%`;
    BetterPortalsMod.buttons[i].style.fontSize = '0.9em';
}
