# Kafe's Melon Loader Mods

Welcome to my little collection of mods, feel free to leave bug reports or feature requests!

---
## In-Depth Mods info Links:
- [OSC](OSC)
- [CCK.Debugger](CCK.Debugger)
- [LoginProfiles](LoginProfiles)
- [PickupOverrides](PickupOverrides)

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

More features can be added, exploring CVR possibilities to the max. Feel free to submit Feature Requests in the github.
There is a in-depth **Readme** in the github page.


Check [OSC In-Depth](OSC) for for info.


---
### CCK.Debugger
The Content Creation Debugger allows you to debug `avatars` and `props`.

Adds a menu that displays useful information, like `synced`/`local` parameter values, prop pickups/attachments, 
who's grabbing, which bone is attached to, who's controlling the sync, etc...

You can click on [CCK.Debugger In-Dept](CCK.Debugger) for a more detailed readme (with pictures).

### LoginProfiles
Allows to start CVR with different credentials profiles via args. The argument is:
```
--profile=profile_id
```

Check [LoginProfiles In-Dept](LoginProfiles) for more info.

---
### PickupOverrides
Allows to override the Auto-Hold setting on all pickups.
Currently you need to change the Auto-Hold settings to enforce on pickups on the Melon Loader config file:
```
<game_folder>\UserData\MelonPreferences.cfg
```
You can change the config while the game is running and it will update as soon as you save. 
The default setting for Auto-Hold is `false`.

Check [PickupOverrides In-Dept](PickupOverrides) for more info.

---
# Disclosure

> ___
> ### ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
> ___