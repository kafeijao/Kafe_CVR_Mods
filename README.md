# Kafe's Melon Loader Mods

Welcome to my little collection of mods, feel free to leave bug reports or feature requests!

---

## Mods:

- [BetterAFK](BetterAFK)
- [BetterLipsync](BetterLipsync)
- [CCK.Debugger](CCK.Debugger)
- [CVRSuperMario64](CVRSuperMario64)
- [ChatBox](ChatBox)
- [EyeMovementFix](EyeMovementFix)
- [FirstPersonHider](FirstPersonHider)
- [FreedomFingers](FreedomFingers)
- [F*ckMinus](FuckMinus)
- [F*ckRTSP](FuckRTSP)
- [Instances](Instances)
- [LoginProfiles](LoginProfiles)
- [MinimizeWindows](MinimizeWindows)
- [NoHardwareAcceleration](NoHardwareAcceleration)
- [OSC](OSC)
- [PickupOverrides](PickupOverrides)
- [PostProcessingOverrides](PostProcessingOverrides)
- [ProfilesExtended](ProfilesExtended)
- [QuickMenuAccessibility](QuickMenuAccessibility)
- [RealisticFlight](RealisticFlight)
- [TheClapper](TheClapper)

---

## Plugins:
- [CVRUnverifiedModUpdaterPlugin](CVRUnverifiedModUpdaterPlugin)

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
