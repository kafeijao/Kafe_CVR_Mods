# BetterAFK

[![Download Latest BetterAFK.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest BetterAFK.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/BetterAFK.dll)

Enhance your AFK experience in CVR with Better AFK, a mod that improves the AFK detection and provides a more accurate
and customizable AFK state.

**Note:** Parameter Stream will take priority if you make them override the `AFK` or `AFKTimer` parameters.

### Features

- Fixes the Parameter Stream `Headset On Head` and `Time Since Headset Removed` to work correctly.
- Sets the `AFK` **bool** parameter, and the `AFKTimer` **float** or **int** parameter (**float** will have decimal 
  cases) representing the time in seconds since you've been AFK.
- Allows manual triggering of the AFK state by pressing the `End` key on your keyboard.
- Configure whether to enter AFK state when Opening the Steam Overlay (enabled by default).

### Configuration

The mod comes with a set of Melon Preferences that can be customized to your liking.

#### Available options:

- **AfkWhileSteamOverlay:** Whether to mark as AFK while the Steam Overlay is opened or not. (default: `true`)
- **UseEndKeyToToggleAFK:** Whether to allow pressing the END key to override the AFK State or not. (default: `true`)
- **SetAnimatorParameterAFK:** Whether to attempt to set the bool parameter AFK when AFK or not. (default: `true`)
- **SetAnimatorParameterAFKTimer:** Whether to attempt to set the int or float parameter `AFKTimer` with the time you've
  been AFK for in **seconds** or not. (default: `true`)
