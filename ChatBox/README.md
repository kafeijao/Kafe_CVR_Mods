# ChatBox

[![Download Latest ChatBox.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest ChatBox.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/ChatBox.dll)

The mod adds a text ChatBox over the player's head, allowing them to send messages to each other via keyboard input.
ChatBoxes of other players can be configured to display only from your friends (off by default), note that everyone will
be able to see your ChatBoxes.

## Features
- Press `Y` or click the BTKUI button in the Misc section to bring the keyboard to send messages.
- Heavily customizable via BKTUI settings menu in the Misc section.
- Modify the notification sounds by replacing the `.wav` sound files at `/UserData/Chatbox/` folder (next to the `/Mods`
  folder). Make sure the naming is the same.
- Add a boolean to your animator of your avatar named `ChatBox/Typing` or `#ChatBox/Typing` (if you want it local). It
  will be set to `true` when you're writing a message (ChatBox keyboard opened), and `false` otherwise.
- View the ChatBox History window via the BTKUI button in the Misc section.
- Keep the ChatBox History window on the center of the QuickMenu or place it on the right.
- Press `Arrow up`/`Arrow down` (keyboard) to iterate through the sent messages. You can also click `Prev` if you're in
  VR.
- Press `TAB` (keyboard) or `Auto` in VR to attempt to auto-complete usernames. 
- Use `@username` to mention people in messages.
- Bind a SteamVR controller button to opening the ChatBox keyboard.
  Requires [VRBinding Mod](https://github.com/dakyneko/DakyModsCVR/tree/master/VRBinding)

---

## External Access

There's an API for mods to use [API Class](https://github.com/kafeijao/Kafe_CVR_Mods/blob/master/ChatBox/API.cs) if you
want to integrate.

You can also check [OSC Mod](https://github.com/kafeijao/Kafe_CVR_Mods/tree/master/OSC) for the ChatBox integration
endpoints.

## Credits

Thanks [AstroDogeDX](https://github.com/AstroDogeDX) for all the UI!

## Disclosure

> ---
> ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
