# BetterLipsync

Refactor the viseme controller use the 
[oculus lipsync](https://developer.oculus.com/documentation/unity/audio-ovrlipsync-unity/).

This might be a bit heavy on the CPU, I've added some settings to experiment. It needs heavy performance improvements (
I think) to make it reliable. Feel free to contribute if you have ideas/implementations!

This mod will only work locally, it calculates the visemes for your own avatar, and everyone else's.

## Configuration

You have a few options to mess with:

```python
# How smooth should the viseme transitions be [0, 100] where 100 is maximum smoothing. Requires EnhancedMode activated
to work.
VisemeSmoothing = 70
# How many frames to skip between viseme checks [1,25], skipping more = more performance.
CalculateVisemesEveryXFrame = 1
# Where to use enhanced mode or original, original doesn't have smoothing but is more performant.
EnhancedMode = false
# Whether this mod will be changing the visemes or not.
Enabled = true
```

You can use the `UI Expansion Kit` (very recommended), or by editing the the config config file located at
`<game_folder>\UserData\MelonPreferences.cfg`. You can change the configs while the game and it will take effect as soon
as you save the config file.

---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
