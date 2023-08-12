# CVRUnverifiedModUpdaterPlugin

[![Download Latest CVRUnverifiedModUpdaterPlugin.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest CVRUnverifiedModUpdaterPlugin.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/CVRUnverifiedModUpdaterPlugin.dll)

**Note:** This is a **__plugin__**, it goes in the `/Plugins` folder, not the `/Mods` one!

This **Plugin** allows to auto update mods directly from github repository releases.

---
⚠️ **Notice!** This is very dangerous as the Mods in git repository releases go through **0** verifications!!!

---

To setup the git repositories and files to update, you need to create a file in `ChilloutVR/UserData/` named
`CVRUnverifiedModUpdaterPluginConfig.json`

This file will be created the first time you launch the game, but it won't have any repository configured. You need to
follow the structure bellow (the Owner is the github username, and Repo is the name of the repository):

**Note:** You can also provide a `Type`, if not type is define it will default to `Mod` which then downloads it into 
the `/Mods` folder.

### Types
- `Mod` Folder: `/Mods`
- `Plugin` Folder: `/Plugins`
- `ModVR` Folder: `/Mods/VR`
- `ModDesktop` Folder: `/Mods/Desktop`

### Configuration

```json
{
  "RepoConfigs": [
    {
      "Owner": "kafeijao",
      "Repo": "Kafe_CVR_Mods",
      "Files": [
        {
          "Name": "CVRSuperMario64.dll",
          "Type": "Mod"
        },
        {
          "Name": "Logger++.dll",
          "Type": "Plugin"
        },
        {
          "Name": "FreedomFingers.dll",
          "Type": "ModVR"
        }
      ]
    },
    {
      "Owner": "SDraw",
      "Repo": "ml_mods_cvr",
      "Files": [
        {
          "Name": "ml_prm.dll"
        }
      ]
    },
    {
      "Owner": "NotAKidOnSteam",
      "Repo": "NAK_CVR_Mods",
      "Files": [
        {
          "Name": "DesktopVRIK.dll",
          "Type": "ModDesktop"
        }
      ]
    }
  ]
}
```

---

## Disclosure

> ---
> ⚠️ **Notice!**
>
> This plugin's developer(s) and the plugin itself, along with the respective plugin loaders, have no affiliation with
> ABI!
>
> ---
