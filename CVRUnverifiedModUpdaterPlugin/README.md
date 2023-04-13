# CVR Unverified Mod Updater Plugin

Latest
Release: [CVRUnverifiedModUpdaterPlugin.dll](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/CVRUnverifiedModUpdaterPlugin.dll)

**Note:**: This is a **__plugin__**, it goes in the `/Plugins` folder, not the `/Mods` one!

This **Plugin** allows to auto update mods directly from github repository releases.

---
⚠️ **Notice!** This is very dangerous as the Mods in git repository releases go through 0 verifications!!!

---

To setup the git repositories and files to update, you need to create a file in `ChilloutVR/UserData/` named
`CVRUnverifiedModUpdaterPluginConfig.json`

This file will be created the first time you launch the game, but it won't have any repository configured. You need to
follow the structure bellow (the Owner is the github username, and Repo is the name of the repository):

### Configuration

```json
{
  "RepoConfigs": [
    {
      "Owner": "kafeijao",
      "Repo": "Kafe_CVR_Mods",
      "Files": [
        {
          "Name": "CVRSuperMario64.dll"
        },
        {
          "Name": "TheClapper.dll"
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
