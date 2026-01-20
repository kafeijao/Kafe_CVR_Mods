# YoutubeFixSABR

[![Download Latest YoutubeFixSABR.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest YoutubeFixSABR.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/YoutubeFixSABR.dll)

Fixes the SABR issue that causes youtube not loading in video players. This works for me without using cookies.

## How it works

1. Downloads the latest deno.exe onto the `ChilloutVR\UserData\YoutubeFixSABR\deno.exe` folder. This follows the same
   download logic as the official deno installer https://github.com/denoland/deno_install/blob/master/install.ps1 but we
   __exclude__ writing the Path env vars
2. Changes CVR's yt-dlp arguments:
    - Removes `--impersonate=Safari-15.3`
    - Removes `--extractor-arg "youtube:player_client=web"`
    - Adds `--js-runtimes "deno:<ChilloutVR\UserData\YoutubeFixSABR\deno.exe>"`
    - Adds `--extractor-args "youtube:player-client=default,-web_safari"`

I yoinked the fix from: <https://github.com/yt-dlp/yt-dlp/issues/15569#issuecomment-3756488415>

## Garbo quality

Currently, CVR filters out the good quality options by iterating over the available formats and ignoring any that
doesn't have both audio and video (the way it's currently setup it doesn't allow separated video and audio formats)

You can customize the arguments being used (look at the next topic), if you do find arguments that make the quality not
trash, please do share them on the CVRMG discord

## Customize Arguments

I've added a way to customize the yt-dlp parameters easily, you can manually edit the melon prefs file
`MelonPreferences.cfg` and make a custom list of arguments to be removed, and arguments to be added.

- **Note1:** These arguments are only used if the option `Use Custom Args` is enabled
- **Note2:** There are examples of how it should be configured, but you probably need to flip one of the configs in-game
  in order to save the default config to the file
- **Note3:** You can change the lists of add/remove in runtime without restarting the game (at least it works for me).
  The melon console will mention if the lists were reloaded from file (if this doesn't happen you might need to restart)
