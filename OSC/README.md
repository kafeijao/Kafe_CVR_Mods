# OSC

[![Download Latest OSC.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest OSC.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/OSC.dll)

This mod enables interactions with ChilloutVR using OSC. It's very similar to other social VR games OSC Implementation,
so most external applications should work without many (if any) changes.

The avatar and input module were deprecated since they were implemented natively.

#### Main Features

- Spawn and delete props
- Interact with props (settings/reading their location and synced parameters)
- Retrieving the tracking data and battery info from `trackers`, `hmd`, `controllers`, `base stations`, and `play space`
- Resend all cached events (like all the current parameters) triggered via an endpoint

More features can be added, exploring CVR possibilities to the max. Feel free to submit Feature Requests in the github.

#### Official Python Library
There is also a [python library](https://github.com/kafeijao/cvr_osc_lib_py) that abstracts all the api provided by this
mod. This is a great starting point if you plan developing something in python.


## Table of Contents

- [OSC Props](#OSC-Props)
- [OSC Tracking](#OSC-Tracking)
- [Avatar Json Configurations](#Avatar-Json-Configurations)
- [OSC Config](#OSC-Config)
- [OSC ChatBox](#OSC-ChatBox)
- [Debugging](#Debugging)
- [Configuration](#General-Configuration)

For now there are 4 categories of endpoints you can use:

- [OSC Props](#OSC-Props) for interacting with props.
- [OSC Tracking](#OSC-Tracking) to fetch tracking information (headset, controllers, trackers, play space).
- [OSC Config](#OSC-Config) configuration/utilities via OSC.
- [OSC ChatBox](#OSC-ChatBox) to send ChatBox messages/isTyping via OSC.

## Intro

This is a long read ;_; Before you get discouraged if you only want to slap a mod that enables OSC just slap the mod in
and it should work. And if all you want is to have it react to OSC messages you can turn on the `Performance Mode` in
the configuration. This will make it so the mod won't send OSC messages out with the tracking data etc, and I'd say that
would be a decent basic setup.

If you are interested in listening to events from the mod then you're in for a great time! As this allows to do a lot of
fun things.

### Easy API

I have encapsulated this API into a [python library](https://github.com/kafeijao/cvr_osc_lib_py) with some examples, and
you won't need to worry about endpoints, parameters OSC server/client, as everything is done with dataclasses and
methods to send and receive info. I will be maintaining the library updated with the mod! The examples are especially
interesting.

---

## OSC Props

This mod module allows to interact with props. I've purposely added limitations to some interactions with props, you can
check these on bellow. These limitations exist to prevent both abuse and some weird behavior that might happen.  

All props require the Prop GUID in their address (*<prop_guid>*): `1aa10cac-36ba-4e01-b58d-a76dc35f61bb`
This value can be known beforehand, as it's the same guid that gets assigned when you upload something.

Some props require the Prop instance ID in their address (*<prop_instance_id>*): `8E143EA45EE8`
This value is a string with 12 characters corresponding to a Hexadecimal value. This value is created when you spawn a
prop in an instance, and the best way to obtain it is by listening to the `Create` and `Availability` addresses.

---

### OSC Props Create

You're able to spawn props by providing their GUID. Keep in mind that you can only spawn props you
have access to and there is a limit of 20 props spawned by yourself.

You can also listen for prop spawn events, which you can use to grab the instance IDs of said props.

#### Address

Here we have two addresses, when you want to create a prop you don't include an instance ID, but when you receive the
information a prop was created, it will come with the instance ID on the address

```/prop/create```

#### Arguments

The address is the same, but the parameters will be different depending whether you are listening for spawn events, or
you're sending messages to spawn props.

**Mod will send:**

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]
- `arg#3` - Count of sub-sync transforms [*int*], if count = 1 it means you can send location_sub to the index = 0

**Mod is expecting to receive:**

- `arg#1` - Prop GUID [*string*]
- `arg#2` - local position x [*Optional*] [*float*]
- `arg#3` - local position y [*Optional*] [*float*]
- `arg#4` - local position z [*Optional*] [*float*]

Those local position arguments are optional, they define where the prop should be spawned in relation to your play
space. If you want to provide a value, you need to provide all of them. If no values are provided they'll default to 0.

---

### OSC Props Delete

As the name suggests you can delete props that you've spawned. You can do so by providing the GUID of the prop as well
as their instance ID to uniquely identify them.

You can also listen for prop deletion events, which you can use to know that a certain prop instance stopped existing and
won't become available for interaction anymore.

#### Address

```/prop/delete```

#### Arguments

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]

---

### OSC Props Availability

This address will be called every time a prop has their availability changed. What I mean by availability is
where you are able to control or not this prop. The props become available when no remote player is
*grabbing*, *telegrabbing*, nor has it *attached* to themselves. Also, this **only** affects props spawned by yourself!

The only exception is it will say it is available, but you won't be able to set the location if you (the local player)
is *grabbing*, *telegrabbing*, or has it *attached*.

Obviously this is an address set by the game, so you can't send osc messages to try to change it.

#### Address

```/prop/available```

#### Arguments

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]
- `arg#3` - Whether the prop interactions are available `true`, or not `false` [*bool*]

---

### OSC Props Parameters

Here you will be able to listen and write to the prop's synced parameters. You will need to provide the GUID of the prop
and it's instance ID to do so.

#### Limitations

- You need to be the player that spawned the prop.
- The prop **not** being controlled by a remote player [*grabbed* | *telegrabbed* | *attached*]

#### Address

```/prop/parameter```

#### Arguments

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]
- `arg#3` - Sync parameter name (*read note bellow*) [*string*]
- `arg#4` - Value to be set on the sync parameter [*float*]

Note: the Sync parameter name **is** the name you defined in the CVR Spawnable Component, and **not**
the actual name of parameter inside of the animator. This name is **case sensitive**.

![image of spawnable component](Resources/spawnable_sync_name.png)

---

### OSC Props Location

You are also able to listen and set the location of a prop. This is very powerful as you can for example link the
tracking data you receive form the tracking module to a prop so it's controlled by the tracker.

**Note**: If you disable the Tracking Module, it will also disable the updates of the Props Location. You can still set
their positions without any issue tho.

#### Limitations

- You need to be the player that spawned the prop.
- The prop **not** being controlled by **any** player (both remote and local) [*grabbed* | *telegrabbed* | *attached*]

#### Address

```/prop/location```

#### Arguments

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]
- `arg#3` - position.x [*float*]
- `arg#4` - position.y [*float*]
- `arg#5` - position.z [*float*]
- `arg#6` - rotation.x [*float*]
- `arg#7` - rotation.y [*float*]
- `arg#8` - rotation.z [*float*]

---

### OSC Props Location Sub

You are also able to listen and set the location of a prop's sub-sync transforms. This is very powerful as you can for
example link the tracking data you receive form the tracking module to a sub-sync so it's controlled by the tracker.

**Note**: If you disable the Tracking Module, it will also disable the updates of the Location Sub. You can still set
their positions without any issue tho.

#### Limitations

- You need to be the player that spawned the prop.
- The prop **not** being controlled by **any** player (both remote and local) [*grabbed* | *telegrabbed* | *attached*]

#### Address

```/prop/location_sub```

#### Arguments

- `arg#1` - Prop GUID [*string*]
- `arg#2` - Instance ID of the prop spawned [*string*]
- `arg#3` - Index of the prop sub-sync transform [*int*], starts from 0 and increments following the order set in
the CVR Spawnable Component
- `arg#4` - position.x [*float*]
- `arg#5` - position.y [*float*]
- `arg#6` - position.z [*float*]
- `arg#7` - rotation.x [*float*]
- `arg#8` - rotation.y [*float*]
- `arg#9` - rotation.z [*float*]

---

## OSC Tracking

This mod module allows to read tracking data from the game namely from tracked devices, and the play space. You can
only listen to these, don't try messages to those (or else).

---

### OSC Tracking Play Space Data

The mod will keep sending the current play space position and rotation. This is especially useful if you want to create
avatar animations to drive the position of objects. Because the avatar origin is the play space origin. Meaning if you
have world space coordinates you want to make local to the avatar, you can do it by using the play space location data
to perform the calculations.

Both the position and rotation(euler angles) are in world space. And the address we're going to be sending is:

```/tracking/play_space```

The values are sent as `float` type arguments, and the values order is the following:

- `arg#1` - position.x [*float*]
- `arg#2` - position.y [*float*]
- `arg#3` - position.z [*float*]
- `arg#4` - rotation.x [*float*]
- `arg#5` - rotation.y [*float*]
- `arg#6` - rotation.z [*float*]

---

### OSC Tracking Device Status

You can listen here for steam vr device connected change status. Every device starts assuming it is disconnected so you
will always receive a connected = `True` as the first event from a device.

#### Address

```/tracking/device/status```

#### Arguments

- `arg#1` - Connected [*bool*], whether the device was connected `True` or disconnected `False`
- `arg#2` - Device type [*string*], Possible values: `hmd`, `base_station`, `left_controller`, `right_controller`,
 `tracker`, and
  `unknown`
- `arg#3` - Steam tracked index [*int*], given by SteamVR, it's unique for each device (*see note bellow*)
- `arg#4` - Device name [*string*], given by SteamVR, in some cases (like base stations) there is no name the string
will be empty.

---

### OSC Tracking Devices Data

The mod also exposes the tracking information for tracked devices, like base stations, controllers, and trackers.

Both the position and rotation are for world space, and the rotation is sent in euler angles.

#### Address

```/tracking/device```

#### Arguments

- `arg#01` - Device type [*string*], Possible values: `hmd`, `base_station`, `left_controller`, `right_controller`,
 `tracker`, and
`unknown`
- `arg#02` - Steam tracked index [*int*], given by SteamVR, it's unique for each device (*see note bellow*)
- `arg#03` - Device name [*string*], given by SteamVR, in some cases (like base stations) there is no name the string
 will be empty.
- `arg#04` - Position.x [*float*]
- `arg#05` - Position.y [*float*]
- `arg#06` - Position.z [*float*]
- `arg#07` - Rotation.x [*float*]
- `arg#08` - Rotation.y [*float*]
- `arg#09` - Rotation.z [*float*]
- `#arg10` - Battery percentage [*float*], from 0 to 1. If the devices is not reporting the battery info to SteamVR it
 will send 0 instead.

This address will work if the game has started in `VR` mode.

*Note*: Steam tracked index gives way to identify uniquely a connected device. This id is unique across all types
of devices, and is assigned by SteamVR and seems to be incrementing from 0, it will not change until SteamVR is restarted.

### Configuration

There are configurations to disable/enable the tracking module and also define which update rate it should send the
osc messages. The default value for the update rate is `0` which makes it sending at every frame of the game.
Consider lowering this value if you don't need such update rates as it lowers the overhead performance impact.
The value is
defined in seconds.  

*Note*: This will also affect the updates on the prop location updates.

---

## OSC Config

This mod module allows configure/interact with the mod via osc.

---

### OSC Config Reset

This endpoint will reset the caches for both avatar and props, and re-send the init events. It's useful if you
start your osc application after the game is running and require those initial events. Since this mod doesn't
keep spamming updates this is very useful sync the state with your app (if you need).

#### Address

```/config/reset```

#### Arguments

`N/A`


---

## OSC ChatBox

Here is where you can send Chat Box messages or is typing notifications, this requires the
mod [ChatBox](https://github.com/kafeijao/Kafe_CVR_Mods/tree/master/ChatBox) to work.

---

### ChatBox Message

You can use this endpoint to send text messages via the Chat Box.

#### Address

```/chatbox/input```

#### Arguments

- `arg#1` - Message [*string*], the message content you want to send. The mod allows a maximum of 2000 Characters.
- `arg#2` - Send Immediately [*bool*], whether the msg is directly sent, or opens the keyboard and pastes the msg.
- `arg#3` - Sound Notification [*Optional*] [*bool*], whether the message will do a sound notification or not. Defaults
  to `False` if not provided.
- `arg#4` - Display in ChatBox [*Optional*] [*bool*], whether the message will be displayed in the ChatBox or not.
  Defaults to `True` if not provided.
- `arg#5` - Display in History Window [*Optional*] [*bool*], whether the message will be displayed in the History Window
  or not. Defaults to `False` if not provided.

---

#### ChatBox IsTyping

You can set the typing state for the ChatBox. Whether it's on or off, and whether it should send a sound notification.

```/chatbox/typing```

#### Arguments

- `arg#2` - Is Typing [*bool*], whether Is Typing is active or not.
- `arg#3` - Sound Notification [*Optional*] [*bool*], whether the start typing will do a sound notification or not. 
Defaults to `False` if not provided.

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
  - `name` - Name of the parameter, needs to match 1:1 and it is case-sensitive
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
path uuids get prefixed with `usr_` for user ids, and `avtr_` for avatar ids (this is enabled by default).
This is to ensure compatibility with other existent applications.

---

## Debugging

Currently, there is no easy way to debug. I would recommend using my other mod [CCK.Debugger](../README.md), among other
things it allows you to see a menu with all your avatar parameters. Which will update realtime including the changes via
OSC.

---

## General Configuration

Most options to configure are on the Melon Loader configuration file. To access it install the mod, and run the
game at least one time so the configuration gets generated. After that you can visit (this might change if you
have the game installed somewhere else):

```console
C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\UserData\MelonPreferences.cfg
```

You can then edit and look for `[OSC]` line, bellow it there should be all configurations with a little description.
You **can** edit whether the game is running or not, they should take effect as soon as you save the file.
