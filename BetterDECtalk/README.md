# BetterDECtalk

[![Download Latest BetterDECtalk.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest BetterDECtalk.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/BetterDECtalk.dll)

Allows to use the DECtalk as a ChilloutVR TTS module. Down the hood it's using the resources
from [dectalk repo](<https://github.com/dectalk/dectalk>)

Features:
- Adds the DECtalk and their voices to ChilloutVR
- Allows to configure the Speaking Rate in Melon Prefs (defaults to 200 words per minute)

## Fetch DECTalk resources

Go to <https://github.com/dectalk/dectalk/actions/workflows/build.yml>, pick the latest, and download the `vs2022`
artifact. You will then find the `DECtalk.dll`, `dtalk_us.dll` and `dtalk_us.dic` inside of `AMD64` folder in the zip
file.

## Credits

- [dectalk](<https://github.com/dectalk/dectalk>) repo, which provided the DECtalk code
- `HerpDerpinstine` for the `CVR_DECtalk` mod, the mod has now been outdated and the repo deleted. Since the mod was
  liked by so many people, I decided to re-create it from scratch
