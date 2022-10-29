# ProfilesExtended

This mod adds more functionality to the avatar profiles system.

### Features:
1. Blacklist parameters from changing that include `*` in their name (not the parameter name in the animator, but the
   one in the AAS)
2. Bypass the said blacklist by having `*` in the profile name
3. Remember current parameters across avatar changes or game restarts

![image of where to set the parameter name](AAS_parameter_name.png)

### Configuration:
You can **define your own tags**, for both parameters and profiles. To configure you can either use `UIExpansionKit` or
manually edit the configuration file. To configure manually you need to install the mod, and run the
game at least one time so the configuration gets generated. After that you can go to (this might change if you
have the game installed somewhere else):

```console
C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\UserData\MelonPreferences.cfg
```

You can then edit and look for `[ProfilesExtended]` line, bellow it there should be all configurations with a little 
description. You **can** edit whether the game is running or not, they should take effect as soon as you save the file.


---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
