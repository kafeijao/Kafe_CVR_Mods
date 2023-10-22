# CVRSuperMario64

[![Download Latest CVRSuperMario64.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest CVRSuperMario64.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/CVRSuperMario64.dll)

Mod to integrate the [libsm64](https://github.com/libsm64/libsm64) into CVR. It allows to spawn a Mario prop and control
it (both in Desktop and VR). Since we're running the actual reverse engineered SM64 engine it should behave exactly like
in the original game.

As a little extra it also supports multiplayer, mario pvp, mario-player interactions (punch people), and more.

This was suggested as a meme idea by [AstroDoge](https://github.com/AstroDogeDX) and then 
[NoachiCode](https://github.com/NoachiCode). Two people make a meme idea worthwhile I guess x)

---

## ROM Installation

1. Obtain a copy of `Super Mario 64 [US] z64 ROM`, with the MD5 hash: `20b854b239203baf6c961b850a4a51a2`. If you google
   the hash, you should easily find it.
2. Rename that file to `baserom.us.z64`
3. Copy that file to `UserData\` in the ChilloutVR folder. By
   default: `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\UserData\`

---

## CVR Super Mario 64 SDK

There should be public props in CVR that you can use right away. Or if you want to get adventurous you can create your
own. I also create a [CCK for this mod](https://github.com/kafeijao/Kafe_CVR_CCKs/tree/master/CVRSuperMario64). It
allows you to create entities used by the mod. Like your own Mario Prop (you can use custom material/shaders), make
levels with terrain types etc, create interactables to you can trigger the Mario Caps or spawn coins.

---

## Troubleshooting

1. If the Mario falls through the floor it is most likely caused because the world creator disabled the mesh read/write
   for the floor mesh. This reduces the memory usage, but nukes the vertex information from scripts. If you want it to
   work, ask the world creator to enable the mesh read/write for meshes that have essential colliders :)

---

## Features

- Play as Mario interacting with ChilloutVR Worlds with the Super Mario 64 engine running to control it.
- Synchronized Marios across people with the mod.
- Attack other Marios.
- Best effort attempt to integrate with CVR world colliders. Depending on the mesh settings/amount might not work/lag.
- Provide Dedicated components for more versatility to make content interact with Mario.
- Punch other players (with [Player Ragdoll Mod](https://github.com/SDraw/ml_mods_cvr/tree/master/ml_prm) support)
- Use the CVR Camera to control mario better. Also has a free camera mod.

---

## Public Props

I've published some props to get people started, you can make your own props by using
the [CCK for this mod](https://github.com/kafeijao/Kafe_CVR_CCKs/tree/master/CVRSuperMario64). You can search for them
in-game in the props tab.

- **SM64 Mario** - Spawns a Mario that you can control.
- **SM64 Interactable Caps** - Spawns a platform with the 3 Caps for mario, Wing, Metal, and Vanish Cap. Stand on top of
the respective cubes to trigger them.
- **SM64 Colliders Dynamic** - Static Colliders that can interact with Mario. Some have special attributes.
- **SM64 Colliders Dynamic** - Movable Colliders that can interact with Mario. Some have special attributes.
- **SM64 Level Modifier - Water/Gas** - Sets the height level for water/gas. This is global for everyone.
- **SM64 Interactable Particles - Coins** - Plane that spawns coins that Mario can pickup, coins heal Mario.
- **SM64 Teleporters** - A one and two ways teleporters.

---

## Todos

- [ ] Offload the heavy work to a thread.
- [ ] Add support for custom damage interactables.

---

## Credits

- [libSM64](https://github.com/libsm64/libsm64) For the amazing library
- [libSM64 Unity plugin](https://github.com/libsm64/libsm64-unity-dev) For an example on how to integrate with unity.
  This was my starting point!

---

## Building Instructions for sm64.dll

If you want to build the `libsm64/Plugins/sm64.dll` follow the instructions at: 
[libsm64](https://github.com/libsm64/libsm64).

The `libsm64/Plugins/sm64.dll.info` file should have information of which commit I used to build the current supported
version of the dll.
