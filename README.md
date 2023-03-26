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
- [ProfilesExtended](ProfilesExtended) *in-depth url*
- ~~[BetterLipsync](BetterLipsync) *in-depth url*~~ *discontinued*
- [EyeMovementFix](EyeMovementFix) *in-depth url*
- [CVRSuperMario64](CVRSuperMario64) *in-depth url*
- [Instances](Instances) *in-depth url*

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

### Profiles Extended

Mod to extend the functionality of the avatar parameter profile system. It allows you to exclude parameters from being
changed via the profiles, and remember the current parameters across avatar changes and game restarts.

Check [Profiles Extended In-Dept](ProfilesExtended) for more info.

---

### Better Lipsync

> ⚠️ **Discontinued**
>
> This mod's functionality was implemented to the base game.

Mod to replace the viseme controller with the
[oculus lipsync](https://developer.oculus.com/documentation/unity/audio-ovrlipsync-unity/).
Might be a bit heavy on the CPU performance, so beware.

Check [Better Lipsync In-Dept](BetterLipsync) for more info.

---

### Eye Movement Fix

Mod to attempt to fix some of the current issues with the eye movement. This will affect the local and remote players
eye movement but only for the local client.

Check [Eye Movement Fix In-Dept](EyeMovementFix) for more info.

---

### CVR Super Mario 64

Mod to integrate the [libsm64](https://github.com/libsm64/libsm64) into CVR. It allows to spawn a Mario prop and control
it (both in Desktop and VR). Since we're running the actual reverse engineered SM64 engine it should behave exactly like
in the original game.

As a little extra it also supports multiplayer, mario pvp, mario-player interactions (punch people), and others.

There should be public props in CVR that you can use right away. Or if you want to get adventurous you can create your
own. I also create a [CCK for this mod](https://github.com/kafeijao/Kafe_CVR_CCKs/tree/master/CVRSuperMario64). It
allows you to create entities used by the mod. Like your own Mario Prop (you can use custom material/shaders), make
levels with terrain types etc, create interactables to you can trigger the Mario Caps or spawn coins.

Check [CVR Super Mario 64 In-Dept](CVRSuperMario64) for more info.

---

### Instances

Instances is a mod for ChilloutVR that enhances the management of world instances. With this mod, you can quickly rejoin
the last instance you were in before logging out (as long as it still exists), and easily revisit the last 12 instances
you've visited with just a single click. Note that to use the history feature, you need to have the BTKUILib mod
installed.

Check [Instances](Instances) for more info.

---

## Building

In order to build this project follow the instructions (thanks [@Daky](https://github.com/dakyneko)):

- (1) Install `NStrip.exe` from https://github.com/BepInEx/NStrip into this directory (or into your PATH). This tools
  converts all assembly symbols to public ones! If you don't strip the dlls, you won't be able to compile some mods.
- (2) If your ChilloutVR folder is `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR` you can ignore this step.
  Otherwise follow the instructions bellow
  to [Set CVR Folder Environment Variable](#set-cvr-folder-environment-variable)
- (3) Run `copy_and_nstrip_dll.ps1` on the Power Shell. This will copy the required CVR, MelonLoader, and Mod DLLs into
  this project's `/ManagedLibs`. Note if some of the required mods are not found, it will display the url from the CVR
  Modding Group API so you can download.

### Set CVR Folder Environment Variable

To build the project you need `CVRPATH` to be set to your ChilloutVR Folder, so we get the path to grab the libraries 
we need to compile. By running the `copy_and_nstrip_dll.ps1` script that env variable is set automatically, but only
works if the ChilloutVR folder is on the default location `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR`.

Otherwise you need to set the `CVRPATH` env variable yourself, you can do that by either updating the default path in
the `copy_and_nstrip_dll.ps1` and then run it, or manually set it via the windows menus.


#### Setup via editing copy_and_nstrip_dll.ps1

Edit `copy_and_nstrip_dll.ps1` and look the line bellow, and then replace the Path with your actual path.
```$cvrDefaultPath = "C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR"```

Now you're all set and you can go to the step (2) of the [Building](#building) instructions!


#### Setup via Windows menus

In Windows Start Menu, search for `Edit environment variables for your account`, and click `New` on the top panel.
Now you input `CVRPATH` for the **Variable name**, and the location of your ChilloutVR folder as the **Variable value**

By default this value would be `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR`, but you wouldn't need to do
this if that was the case! Make sure it points to the folder where your `ChilloutVR.exe` is located.

Now you're all set and you can go to the step (2) of the [Building](#building) instructions! If you already had a power
shell window opened, you need to close and open again, so it refreshes the Environment Variables.

---

# Disclosure  

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
