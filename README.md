# Kafe's Melon Loader Mods

Welcome to my little collection of mods, feel free to leave bug reports or feature requests!

---

## In-Depth Mods info Links:

- [OSC](OSC) *in-depth url*
- [CCK.Debugger](CCK.Debugger) *in-depth url*
- [LoginProfiles](LoginProfiles) *in-depth url*
- [PickupOverrides](PickupOverrides) *in-depth url*
- [FirstPersonHider](FirstPersonHider) *in-depth url*
- [FreedomFingers](FreedomFingers) *in-depth url*

---

## Small Descriptions:  

### OSC

This mod enables interactions with ChilloutVR using OSC. It's very similar to other social VR games OSC Implementation,
so most external applications should work without many (if any) changes.

#### Main Features

- Change avatar parameters
- Control the game inputs (like Gestures, movement, etc)
- Trigger special game features (like flight, mute, etc)
- Configurable endpoints (parameters address & type conversion)
- Change avatar by providing avatar id
- Spawn and delete props
- Interact with props (settings/reading their location and synced parameters)
- Retrieving the tracking data and battery info from `trackers`, `hmd`, `controllers`, `base stations`, and `play space`
- Resend all cached events (like all the current parameters) triggered via an endpoint

More features can be added, exploring CVR possibilities to the max. Feel free to submit Feature Requests in the github.

Check [OSC In-Depth](OSC) for in-depth info.

There is also a [python library](https://github.com/kafeijao/cvr_osc_lib_py) that abstracts all the api provided by this
mod.

---

### CCK.Debugger

The Content Creation Debugger allows you to debug `avatars` and `props`.

Adds a menu that displays useful information, like `synced`/`local` parameter values, prop pickups/attachments,  
who's grabbing, which bone is attached to, who's controlling the sync, etc...

You can click on [CCK.Debugger In-Dept](CCK.Debugger) for a more detailed readme (with pictures).

### LoginProfiles

Allows to start CVR with different credentials profiles via args. The argument is:

```cfg
--profile=profile_id
```

Check [LoginProfiles In-Dept](LoginProfiles) for more info.

---

### PickupOverrides

Allows to override the Auto-Hold setting on all pickups.
Currently you need to change the Auto-Hold settings to enforce on pickups on the Melon Loader config file:

```cfg
<game_folder>\UserData\MelonPreferences.cfg
```

You can change the config while the game is running and it will update as soon as you save.  
The default setting for Auto-Hold is `false`.

Check [PickupOverrides In-Dept](PickupOverrides) for more info.

---

### First Person Hider

This mod enables hiding avatar game objects in the first person view. You will need to add the tag `[FPH]` to a game  
object name (Eg: `HairRoot[FPH]69`), it will **also** hide its children game objects.

Is uses the same mechanism as the head shrinking, so hidden game objects will still be visible for mirrors and cameras.

You can customize the tags to hide in the config. Has to be via `MelonPreferences.cfg` since it's a list of strings.

You can exclude single game objects from being hidden with the tag `[FPR]`, this is implemented in the base game and  
doesn't need the mod to work (you can use this to reveal your hair in first person that's hidden by the head shrink for  
example).

Check [First Person Hider In-Dept](FirstPersonHider) for more info.

---

### Freedom Fingers

This removes the annoying index controller toggle to switch between finger tracking and animation. And replaces it with
a gesture toggle. Where your fingers are tracked all the time but you can pick whether you want gestures or not.

Check [Freedom Fingers In-Dept](FreedomFingers) for more info.

---

## Building

In order to build this project follow the instructions (thanks [@Daky](https://github.com/dakyneko)):

- (1) Install NStrip.exe from https://github.com/BepInEx/NStrip into this directory (or into your PATH). This tools
  converts all assembly symbols to public ones. Make life easy!
- (2) Create a new Windows environment variable `%CVRPATH%` which should point to your game path (folder
  where `ChilloutVR.exe` resides). In Windows, look for Settings > Advanced system settings > Advanced > Environment
  Variables, add a new one there, it should point to something
  like `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR` or similar.
- (3) Run `copy_and_nstrip_dll.bat` this will copy the game + MelonLoader .dll into this project and run NStrip.exe to
  make them public (easier developers).

---

# Disclosure  

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
