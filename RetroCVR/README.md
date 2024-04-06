# RetroCVR

[![Download Latest RetroCVR.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest RetroCVR.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/RetroCVR.dll)

**Note:** This is still a hardcoded buggy mess. Still requires a ton of work. Leaving here for storage purposes.

### Installation Instructions
1. Start the game once with the mod so it generates the folders (and then close)
2. Go to `ChilloutVR\UserData\RetroCVR\` folder
    - `\libretro\` - Similar to retro arch folder, look for the manual installation of cores for specific instructions
    - `\Roms\` - Create a folder per core, and then place the roms inside of the specific core folder's
    - `\NativeLibs\` - Don't touch this, the mod should populate this folder with native `.dll`s
3. Spawn a prop that has the required components
   from [RetroCVR CCK](https://github.com/kafeijao/Kafe_CVR_CCKs/tree/master/RetroCVR)

### Build Special Instructions

Build SK.Libretro dlls using
my [SK.Libretro Fork on cvr-compatibility branch](https://github.com/kafeijao/SK.Libretro/tree/cvr-compatibility#retrocvr-building-specifics) (
has instructions on the README.me)

---

## Credits

- Using [Skurdt/SK.Libretro](https://github.com/Skurdt/SK.Libretro)
- Inspired myself from [Skurdt/LibretroUnityFE example](https://github.com/Skurdt/LibretroUnityFE)
- Grabbed some code from [XenuIsWatching/SK.Libretro fork](https://github.com/XenuIsWatching/SK.Libretro)
