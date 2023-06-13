# GrabbyBones

[![Download Latest GrabbyBones.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest GrabbyBones.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/GrabbyBones.dll)

Mod that allows you to grab dynamic bones and magica cloth bones. There is an option to only calculate grabbing for
friends, so it's easier on performance and you can prevent trolls from being annoying.

**Note**: Only people with the mod will see the bones being grabbed, and it is local so people might see different
things happening.

## Features

- [x] Grab `dynamic`/`magica cloth` bones
- [x] Use the bone radius for the grabbing area
- [x] Use `[NGB]` tag somewhere on the GameObject name that contains the dynamic/magica component to prevent being
  grabbed.
- [x] Use `[NGBB]` tag on a bones belonging to the chain to prevent a single specific bone from being grabbed.
- [x] Animator parameters for detecting when a bone is grabbed, or the grabbed bone.
- [x] Use `Rotation Limit Angle`, `Rotation Limit Hinge`, `Rotation Limit Polygon`, and `Rotation Limit Spline` from
  FinalIK to limit how grabbed bones can move. You can also use
  the [Final-IK-Stub](https://github.com/VRLabs/Final-IK-Stub/tree/main) instead of the full `FinalIK` package (but you
  won't have visualizers in unity)
- [ ] Pose bones (Waiting on the modding network)
- [ ] Synchronize grabbing and posing (Waiting for the modding network)

## Parameters

The parameters name will use the **game object name** where the dynamic/magica script is attached as a prefix. This
means that if you have multiple scripts on the same game object or with the same name they will conflict with each
other.

### Suffixes:

- `_IsGrabbed` - [Bool] Is any bone in the current script being grabbed.
- `_Angle` - [Float] Range of 0.0-1.0. Normalized 180 angle made between the end bone's is from its original rest
  position . In other words, if you twist a bone completely opposite of its start direction, this param will have a
  value of `1.0`

So if your game object is called `HairRoot`, the respective parameters would be called:

- `HairRoot_IsGrabbed` [Bool]
- `HairRoot_Angle` [Float]

### Local vs Synced

The previous parameters will be only set by the owner of the avatar, and then they will be synced over the network. So
if a remote grabs your bone, it will take a while between they grabbing and they seeing the effect of their game,
because cvr has to sync the parameters. Since the avatar owner is driving it, this only works if the owner has the mod
installed, and the bone is being interacted on their game.

But, you can also prefix those parameters with `#` making them local parameters. These will be updated locally for
everyone, so it will seem instant for remote users. Using the previous case it would be something like:

- `#HairRoot_IsGrabbed` [Bool]
- `#HairRoot_Angle` [Float]

## How it works

Basically it works by going through the dynamic bones/magica cloth scrips and adding FABRIK scripts to the calculated
roots. Then when someone grabs a point of the chain that is setup to be grabbed, it will setup the IK chain from the
root up to the grabbed bone.

It is janky, and will probably break some bone setups since it bypasses the limits imposed by dynamic bones/magica
cloth. It shouldn't cause too much performance drops since the FABRIK script is only active when a bone is being
grabbed.

## Disclosure

> ---
> ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
