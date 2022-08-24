# OSC

This **Melon Loader** mod allows you to use OSC to interact with **ChilloutVR**. It tries to be compatible with other social VR 
games that also use OSC. This way allows to use tools made for other stuff with relative ease.

- [Configuration](#Configuration)
- [OSC Avatar Changes](#OSC-Avatar-Changes)
- [OSC Avatar Parameters](#OSC-Avatar-Parameters)
- [OSC Inputs](#OSC-Inputs)
- [Avatar Json Configurations](#Avatar-Json-Configurations)
- [Debugging](#Debugging)

---
## Configuration

Most options to configure are on the Melon Loader configuration file. To access it install the mod, and run the
game at least one time so the configuration gets generated. After that you can visit (this might change if you
have the game installed somewhere else):
```console
C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\UserData\MelonPreferences.cfg
```
You can then edit and look for `[OSC]` line, bellow it there should be all configurations with a little description.
You **can** edit whether the game is running or not, they should take effect as soon as you save the file.

### Ports
You can configure the ports the mod uses to receive and send the messages. By default it receives on port `9000` and 
sends on port `9001`. So your external program should send to `9000`, and in case you want to listen for messages from
the mod, your program should listen to `9001`.

You can change these values on steam `Launch Options` (right click cvr and then properties), or on the Melon Loader
configuration. Note that the `Launch Options` will override your Melon Loader configuration.

#### Command argument
```commandline
--osc=inPort:senderIP:outPort
```
*Note:* If you want to replicate the default settings you would use: `--osc=9000:127.0.0.1:9001`

### Triggers
You can optionally enable listening and sending `trigger` type parameters, but since this might break existing applications
it is disabled by default. It uses the same parameter change address, but it sends just the address without any value.

---
## OSC Avatar Changes
Whenever you load into an avatar the mod will send a message to `/avatar/change` containing as argument a string with
the avatar UUID.

**New:** The mod will also listen to the address `/avatar/change`, so if you send a UUID of an avatar (that you have 
permission to use), it will change your avatar in-game. This is `enabled` by default, but you can go to the configurations
and disable it.

---
## OSC Avatar Parameters
- [ ] Todo: Add avatar parameters info

---
## OSC Inputs
- [ ] Todo: Add Inputs info

### Configuration
You can have certain inputs blacklisted, as some can be very annoying. By default the `Reload`, `Respawn`, and `QuitGame`
are on the blacklist (`Reload` at the time of writing was bugged and would crash your game (same as spamming `F5`)).
You can also disable the whole input module on the configuration as well.

---
## Avatar Json Configurations
- [ ] Todo: Add Avatar Json Configurations info

### Customize addresses and Type conversion
The mod will always use the json configurations `output` to decide where to send the parameter changes. Also will 
respect the type you chose on the output by converting before sending. **But** beware, if you want to use this feature
you need to disable the `replaceConfigIfExists` configuration, otherwise the game will overwrite your json config files.

For now the `input` address/type of the json config are not used. Maybe will be implemented in a near future.

### Configuration
You are able to overwrite the location of the mentioned json configs gets stored in the configurations. Also the file 
path uuids get prefixed with `usr_` for user ids, and `avtr_` for avatar ids (this is enabled by default). This is to 
ensure compatibility with other existent applications.

---
## Debugging
Currently there is no easy way to debug. I would recommend using my other mod [CCK.Debugger](../README.md), among other
things it allows you to see a menu with all your avatar parameters. Which will update realtime with OSC changes.

---
## Disclosure
> ___
> ### ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
> ___