# FirstPersonHider
This mod allows to hide game objects locally, you will still see stuff in the mirror/camera. This uses the same 
mechanism your head does to shrink, so you don't view your own head.

This could be useful if you have "fake heads" setup on your avatars, since cvr will only make the rig head disappear.

Basically if you add the `[FPH]` tag to a game object name (eg: `HairRoot[FPH]69`), this object and all their children 
objects will be hidden.

There is also the counterpart `[FPR]`, but this one is implemented in the base game, and works without the mod. But you 
need to put on all game objects you want to prevent getting hidden.

## Configuration
You are able to change the the tag `[FPH]` or add multiple other ones since it is a list. Although you will need to 
edit the `MelonPreferences.cfg`, because it is a list of strings and seems `UI Expansion Kit` doesn't support this yet.

---
## Disclosure

> ___
> ### ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
> ___
