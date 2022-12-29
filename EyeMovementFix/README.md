# BetterEyeMovement

Mod to attempt to fix some of the current issues with the eye movement. This will affect the local and remote players
eye movement but only for the local client.

## Addressed Issues

- The Local Player look at target (local and in mirror) doesn't follow the viewpoint Up/Down when in FBT. (results in
  players looking higher/lower when looking at you).
- Some remote players don't have an instance of PuppetMaster attached to their CVRAvatar instance, which results in the
  viewpoint location to be completely wrong (results in crossed eyes a lot of times).
- The math for rotating the eyes has some issues. Sometimes one of my eyes would go up, and the other to the left. I
  redid that part with simpler code, it might not be the best, but the eyes won't move in opposite ways/axis anymore.

## Configuration

- Allow to enforce the camera to be the only look at target when held (and the camera setting to allow it be a look at
  target is enabled).
- Prevent the portable mirror mod's mirrors from creating look at targets of the players reflected on said mirrors

---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
