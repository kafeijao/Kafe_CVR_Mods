# YoutubeFixSABR

[![Download Latest YoutubeFixSABR.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest YoutubeFixSABR.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/YoutubeFixSABR.dll)

Fixes the SABR issue that causes youtube not loading in video players. This works for me without using cookies.

## How it works

1. Downloads the latest deno.exe onto the `ChilloutVR\UserData\YoutubeFixSABR\deno.exe` folder. This follows the same
   download logic as the official deno installer https://github.com/denoland/deno_install/blob/master/install.ps1 but we
   __exclude__ writing the Path env vars
2. Changes CVR's yt-dlp arguments:
    - Adds `--js-runtimes "deno:<ChilloutVR\UserData\YoutubeFixSABR\deno.exe>"`
3. Changes the game to download the nightly version of yt-dlp instead of stable

## Customize Arguments

I've added a way to customize the yt-dlp parameters easily, you can manually edit the melon prefs file
`MelonPreferences.cfg` and make a custom list of arguments to be removed, and arguments to be added.

- **Note1:** These arguments are only used if the option `Use Custom Args` is enabled
- **Note2:** There are examples of how it should be configured, but you probably need to flip one of the configs in-game
  in order to save the default config to the file
- **Note3:** You can change the lists of add/remove in runtime without restarting the game (at least it works for me).
  The melon console will mention if the lists were reloaded from file (if this doesn't happen you might need to restart)
