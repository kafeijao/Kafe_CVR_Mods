# CVRSuperMario64

Mod to integrate the [libsm64](https://github.com/libsm64/libsm64) into CVR. It allows to spawn a Mario prop and control
it (both in Desktop and VR). Since we're running the actual reverse engineered SM64 engine it should behave exactly like
in the original game.

This was suggested as a meme idea by [AstroDoge](https://github.com/AstroDogeDX) and then Noachi. Two people make a meme
idea juicy I guess x)

---

## CVR Super Mario 64 SDK

There should be public props in CVR that you can use right away. Or if you want to get adventurous you can create your
own. I also create a [CCK for this mod](https://github.com/kafeijao/Kafe_CVR_CCKs/tree/master/CVRSuperMario64). It
allows you to create entities used by the mod. Like your own Mario Prop (you can use custom material/shaders), make
levels with terrain types etc, create interactables to you can trigger the Mario Caps or spawn coins.

---

## ROM Installation

1. Obtain a copy of `Super Mario 64 [US] z64 ROM`, with the MD5 hash: `20b854b239203baf6c961b850a4a51a2`. If you google
   the hash, you should easily find it.
2. Rename that file to `baserom.us.z64`
3. Copy that file to `UserData\` in the ChilloutVR folder. By
   default: `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\UserData\`

---

## Troubleshooting

1. If the Mario falls through the floor it is most likely caused because the world creator disabled the mesh read/write
   for the floor mesh. This reduces the memory usage, but nukes the vertex information from scripts. If you want it to
   work, ask the world creator to enable the mesh read/write for meshes that have essential colliders :)

---

## Todos

- [x] Fix the sync between players.
- [x] Fix frame rate.
- [ ] Offload the heavy work to a thread.
- [x] Add support to configure terrain types.
- [x] Add support for moving colliders.
- [x] Add support for coins?
- [ ] Add support for enemies?
- [x] Add Mario pvp?

---

## Credits

- [libSM64](https://github.com/libsm64/libsm64) For the amazing library
- [libSM64 Unity plugin](https://github.com/libsm64/libsm64-unity-dev) For an example on how to integrate with unity.
  This was my starting point!

---

## Building Instructions for sm64.dll

If you want to build the `libsm64/Plugins/sm64.dll` follow the instructions
at: [libsm64](https://github.com/libsm64/libsm64).

The `libsm64/Plugins/sm64.dll.info` file should have information of which commit I used to build the current supported
version of the dll.

---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
