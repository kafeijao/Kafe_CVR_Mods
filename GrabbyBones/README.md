# GrabbyBones

[![Download Latest GrabbyBones.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest GrabbyBones.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/GrabbyBones.dll)

Mod that allows you to grab dynamic bones and magica cloth bones. There is an option to only calculate grabbing for 
friends, so it's easier on performance and you can prevent trolls from being annoying.

**Note**: Only people with the mod will see the bones being grabbed, and it is local so people might see different
things happening.

## Features
- [x] Grab `dynamic`/`magica cloth` bones
- [x] Use the bone radius for the grabbing area
- [ ] Pose bones (Waiting on the modding network)
- [ ] Synchronize grabbing and posing (Waiting for the modding network)

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
