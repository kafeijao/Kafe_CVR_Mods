# EyeMovementFix

![Download Latest EyeMovementFix.dll](../.Resources/DownloadButtonDisabled.svg "Download Latest EyeMovementFix.dll")

## Retired ⚠️

#### This mod has been retired because it was implemented into the game as a native feature.


Mod to attempt to fix some of the current issues with the eye movement. This will affect the local and remote players
eye movement but only for the local client. Over time I also implemented some new features.

## Added Features
- Reworked the eye targeting system, now finding a target will require line of sight and gives priority to:
  - Distance of the target and how close to the viewpoint center it is.
  - How loud the target is talking (if the target is a player).
  - Prefers the real players over their reflections on the mirror.
  - Is the local player :)
- Allow to set the avatar eye rotation limits by:
  - Using a Unity [Custom CCK Component](https://github.com/kafeijao/Kafe_CVR_CCKs/blob/master/EyeMovementFix)
  - Using the Unity Muscle rig limits for the eyes

## Addressed Issues

- The Local Player look at target (local and in mirror) doesn't follow the viewpoint Up/Down when in FBT. (results in
  players looking higher/lower when looking at you). This seems to be because of some janky coded that during the 
  Update Loop makes the viewpoint default to the avatar height position. In FixedUpdate and LateUpdate it is on the
  correct place.
- Some remote players don't have an instance of PuppetMaster attached to their CVRAvatar instance, which results in the
  viewpoint location to be completely wrong (results in crossed eyes a lot of times).
- The math for rotating the eyes has some issues. Sometimes one of my eyes would go up, and the other to the left. I
  redid that part with simpler code, it might not be the best, but the eyes won't move in opposite ways/axis anymore.
- The parameter stream fetches the Y eye angle from the X axis.

## Configuration

- Allow to enforce the camera to be the only look at target when held (and the camera setting to allow it be a look at
  target is enabled).
- Prevent the portable mirror mod's mirrors from creating look at targets of the players reflected on said mirrors
