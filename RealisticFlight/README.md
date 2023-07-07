# RealisticFlight

[![Download Latest RealisticFlight.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest RealisticFlight.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/RealisticFlight.dll)

Attempt of introducing a Flap/Glide realistic flight mode.
Small tweaks to the movement of the player when airborne (not in flight mode).

## Features
- Fixes falloff of velocity to not be instantly (for example when you get pushed by an explosion)
- `Flap` your arms to fling yourself up as if you had wings.
- `Spread` your arms when airborne in order to `Glide`. Going up reduces speed, going down gains speed.
- You can use the `bool` parameter `IsGliding` in your animator.
- You can use the `float` parameter `GlidingVelocity` in your animator.
- You can use the `bool` parameter `JustFlapped` in your animator. It will stay on for `0.2` seconds after flapping.
- You can use the `float` parameter `FlapVelocity` in your animator. It will keep the value for `0.2` seconds after
  flapping.

---

## Disclosure

> ---
> ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
