# OSC

This **Melon Loader** mod allows you to use OSC to interact with **ChilloutVR**. It tries to be compatible with other social VR 
games that also use OSC. This way allows the usage of tools made for other games with relative ease.

- [OSC Avatar Changes](#OSC-Avatar-Changes)
- [OSC Avatar Parameters](#OSC-Avatar-Parameters)
- [OSC Inputs](#OSC-Inputs)
- [Avatar Json Configurations](#Avatar-Json-Configurations)
- [Debugging](#Debugging)
- [Configuration](#Configuration)


For now there are 3 categories of endpoints you can use:
- [OSC Avatar Changes](#OSC-Avatar-Changes) for listening/triggering avatar changes.
- [OSC Avatar Parameters](#OSC-Avatar-Parameters) for listening/triggering avatar parameter changes.
- [OSC Inputs](#OSC-Inputs) for triggering inputs/actions.

---
## OSC Avatar Changes
Whenever you load into an avatar the mod will send a message to `/avatar/change` containing as the first and only
argument a string representing the avatar UUID.

```console
/avatar/change
```

**New:** The mod will also listen to the address `/avatar/change`, so if you send a UUID of an avatar (that you have 
permission to use), it will change your avatar in-game. This is `enabled` by default, but you can go to the 
configurations and disable it.


---
## OSC Avatar Parameters
You can listen and trigger parameter changes on your avatar via OSC messages, by default the endpoint to change and 
listen to parameter changes is:
```console
/avatar/parameters/<parameter_name>
```
Where `<parameter_name>` would be the name of your parameter. *The parameter name is case sensitive!*

And then the value is sent as the first argument, this argument should be sent as the same type as the parameter is
defined in the animator. But you can also send as a `string` or some other type that has a conversion implied. **Note:**
Sending the correct type will require less code to run, making it more performant.

We support all animator parameter types, `Float`, `Int`, `Bool`, and `Trigger`([*](#Triggers))

You can listen for **All** the parameters changes present in the animator!

As for sending parameter you can send parameter changes for all present in the animator **Except** for the **core
parameters**. Those are the default parameters CVR modifies for you, Like `GestureRight`, `MovementX`, `Emote`, etc), 
and since they are set every frame we can't change them in this endpoint.

If you wish to change those, check the [OSC Inputs](#OSC-Inputs) section, as
it allows you to control the input that drives those parameters, for example setting the Input `GestureRight`
(using [OSC Inputs](#OSC-Inputs)) to Open Hand will make the game then change the parameter `Gesture Right` to `-1`.

### Triggers
We support the parameter type `Trigger`, but it needs to be enabled in the configuration as it may break some existing
apps. It uses the same parameter change address, but it sends just the address without any value.

And when listening
the same thing, you will receive an OSC message to the parameter address, but there won't be a value.

---
## OSC Inputs
Here is where you can interact with the game in a more generic ways, allow you to send controller inputs or triggering
actions in the game.

The endpoint for the inputs is:
```console
/input/<input_name>
```
Where the `<input_name>` is the actual name of the input you're trying to change, and then it takes as the first 
argument the value.

There are some inputs that are not present that exist in other VR Social platforms, this is due CVR not having those
features implemented yet. Like rotating the object you're holding with keyboard inputs. And some others that are new,
and I'll be making them as [*new*] while listing them.

**Note:** The inputs will stick with the latest value you send, so lets say if you send `/input/Jump` with the value `1`
will act the same as you holding down the key to Jump, and it will only be released when you send `/input/Jump` with the
value `0`. So don't forget to reset them in your apps, otherwise you might end up jumping forever.

There are 3 types of Inputs:
- [Axes](#Axes)
- [Buttons](#Buttons)
- [Values](#Values)

### Axes
Axes expecting a `float` value that ranges between `-1`/`0` and `1`. They are namely used for things that require a 
range of values instead of a on/off, for example the Movement, where `Horizontal` can be set to `-0.5` which would be
the same as having the thumbstick on your controller to the left (because it's a negative value) but only halfway 
(because it's -0.5, -1 would be all the way to the left).

- `/intput/Horizontal` - Move your avatar right `1` or left `-1`
- `/intput/Vertical` - Move your avatar forward `1` or backwards `-1`
- `/intput/LookHorizontal` - Look right `1` or left `-1`
- `/intput/LookVertical` - [*new*] Look up `1` or down `-1`
- `/intput/MouseScrollWheel` - [*new*] Move a held object forwards `1` and backwards `-1`
- `/intput/GripLeftValue` - [*new*] Left hand trigger grip released `0` or pulled to max `1`
- `/intput/GripRightValue` - [*new*] Right hand trigger grip released `0` or pulled to max `1`

### Buttons
Buttons are expecting `boolean` values, which can be represented by the boolean types `true` for button
pressed and `false` for released. You can also send `integers` with the values `1` for pressed and `0` for released.

**Note1:** Don't forget to release the buttons, as it will prevent you from sending the press event again.

**Note2:** Some inputs will keep triggering the value to the input (when it says `while true/1`) and others will trigger
just once (when it says `when true/1`). This is referring to the little description on each input on the next section.

#### Movement and look:
- `/intput/MoveForward` - Move forward **while** `true`/`1`
- `/intput/MoveBackward` - Move backwards **while** `true`/`1`
- `/intput/MoveLeft` - Move left **while** `true`/`1`
- `/intput/MoveRight` - Move right **while** `true`/`1`
- `/intput/LookLeft` - Look left **while** `true`/`1`
- `/intput/LookRight` - Look right **while** `true`/`1`
- `/intput/ComfortLeft` - Look left **while** `true`/`1`
- `/intput/ComfortRight` - Look right **while** `true`/`1`

#### Held Objects Interactions:
- `/intput/DropRight` - **Drops** currently held object on the right hand **when** `true`/`1`
- `/intput/UseRight` - **Uses** currently held object on the right hand **when** `true`/`1`
- `/intput/GrabRight` - **Grabs** currently object targeted by right hand (or crosshair in desktop) **when** `true`/`1`
- `/intput/DropLeft` - **Drops** currently held object on the left hand **when** `true`/`1`
- `/intput/UseLeft` - **Uses** currently held object on the left hand **when** `true`/`1`
- `/intput/GrabLeft` - **Grabs** currently object targeted by left hand (doesn't work in desktop) **when** `true`/`1`

#### Actions:
- `/intput/Jump` - Jump **while** `true`/`1`
- `/intput/Run` - Run **while** `true`/`1`
- `/intput/PanicButton` - Disables all avatars and props **when** `true`/`1` (might require to reloading the instance to revert)
- `/intput/QuickMenuToggleLeft` - Toggles the quick menu **when** `true`/`1`
- `/intput/QuickMenuToggleRight` - Toggles the big menu **when** `true`/`1`
- `/intput/Voice` - Toggles the local mute setting **when** `true`/`1`

#### Special Actions [*new*]:
- `/intput/Crouch` - Toggles crouch **when** `true`/`1`
- `/intput/Prone` - Toggles prone **when** `true`/`1`
- `/intput/IndependentHeadTurn` - Enables being able to look around without moving the body **while** `true`/`1`
- `/intput/Zoom` - Enables zoom **while** `true`/`1`
- `/intput/Reload` - Reloads the UI **when** `true`/`1` [*Blacklisted by default*]
- `/intput/ToggleNameplates` - Toggles nameplates **when** `true`/`1`
- `/intput/ToggleHUD` - Toggles the HUD **when** `true`/`1`
- `/intput/ToggleFlightMode` - Toggles flight mode **when** `true`/`1`
- `/intput/Respawn` - Respawns **when** `true`/`1` [*Blacklisted by default*]
- `/intput/ToggleCamera` - Toggles camera **when** `true`/`1`
- `/intput/ToggleSeated` - Toggles seated mode **when** `true`/`1`
- `/intput/QuitGame` - Closes the game **when** `true`/`1` [*Blacklisted by default*]

#### Configuration
You can have certain inputs blacklisted, as some can be very annoying. By default the `Reload`, `Respawn`, and `QuitGame`
are on the blacklist (`Reload` at the time of writing was bugged and would crash your game (same as spamming `F5`)).
You can also disable the whole input module on the configuration as well.


### Values
Values are similar to `Axes` but removes the restriction of being between `-1` and `1`, they are used to send values to
certain properties of the game. The values are of the type `float` or `int` and their range is dependent on each entry.

**Note:** Most of these default to the value 0, but there are exceptions. As the other inputs you need to reset to the
default value otherwise they will remain the last value you sent.

- `/intput/Emote` - Sets which emote to play when settings the value. Default: `-1`
- `/intput/GestureLeft` - Sets which gesture to perform on the left hand. Default: `0`
- `/intput/GestureRight` - Sets which gesture to perform on the right hand. Default: `0`
- `/intput/Toggle` - Sets which toggle is active. Default: `0`

---
## Avatar Json Configurations
When you load into an avatar you will trigger the generation of a json configuration file, this file will be located at:
```console
"C:\Users\kafeijao\AppData\LocalLow\Alpha Blend Interactive\ChilloutVR\OSC\usr_4a0661f1-4eeb-426e-52ec-1b2f48e609b3\Avatars\avtr_5bf29e54-b3dd-4c15-ba72-c4dc6e410efb.json"
```

### Example json config
```json
{
  "id": "5bf29e54-b3dd-4c15-ba72-c4dc6e410efb",
  "name": "Farday",
  "parameters": [
    {
      "name": "GestureLeft",
      "output": {
        "address": "/avatar/parameters/GestureLeft",
        "type": "Float"
      }
    },
    {
      "name": "OutfitBoots",
      "input": {
        "address": "/avatar/parameters/OutfitBoots",
        "type": "Bool"
      },
      "output": {
        "address": "/avatar/parameters/OutfitBoots",
        "type": "Bool"
      }
    }
  ]
}
```
You can use these files to let your external programs know which parameters are available to listen/change and their types.
Having an input is optional, as there are some Parameters that you are not allowed to change (via the change parameter
address), like the `GestureLeft` in our example (if you want to change those check [OSC Inputs](#OSC-Inputs)).

There are 4 types, `Float`, `Int`, `Bool`, and `Trigger`. The `Trigger` type will appear here but won't work by default,
you need to enable Triggers parameter type on the mod configuration.

- `id` - Avatar unique id
- `name` - Name of the avatar
- `parameters`
  - `name` - Name of the parameter, needs to match 1:1 and it is case sensitive
  - `input` [*Optional*]
    - `address` - Address [*string*] the mod will **listen** for incoming OSC messages [**ignored**]
    - `type` - Expected type of the incoming data: `Int`, `Bool`, `Float`, `Trigger` [**ignored**]
  - `output`
    - `address` - Address [*string*] where the mod will **send** OSC messages [**working**]
    - `type` - Expected type of the outgoing data: `Int`, `Bool`, `Float`, `Trigger` [**working**] This will actively 
convert the parameter to the type you specify. Even if it differs from the parameter type.

**Todo**:
- [ ] Handle the input type and address.

**⚠️Note:** You **cannot** edit the config files unless you change the setting to prevent them being overwritten. If
you want to customize those check the next section!

### Customize Addresses and Type conversion
The mod will always use the json configurations `output` to decide where to send the parameter changes. Also will 
respect the type you chose on the output by converting before sending. **But** beware, if you want to use this feature
you need to disable the `replaceConfigIfExists` configuration, otherwise the game will overwrite your json config files
when loading an avatar.

For now the `input` address/type of the json config is not used. Maybe will be implemented in a near future.

### Compatibility Configuration
You are able to overwrite the location of the mentioned json configs gets stored in the configurations. Also the file 
path uuids get prefixed with `usr_` for user ids, and `avtr_` for avatar ids (this is enabled by default). This is to 
ensure compatibility with other existent applications.


---
## Debugging
Currently there is no easy way to debug. I would recommend using my other mod [CCK.Debugger](../README.md), among other
things it allows you to see a menu with all your avatar parameters. Which will update realtime including the changes via
OSC.


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

#### Launch Option
```commandline
--osc=inPort:senderIP:outPort
```
*Note:* If you want to replicate the default settings you would use: `--osc=9000:127.0.0.1:9001`


---
## Disclosure
> ___
> ### ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
> ___