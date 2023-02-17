# CVRSuperMario64

Attempt of [LibSM64](https://github.com/libsm64/libsm64) integration with CVR. I
used [this repo](https://github.com/libsm64/libsm64-unity-dev) as a starting point.

Suggested as a meme idea by [AstroDoge](https://github.com/AstroDogeDX) and then Noachi. Two people make a meme idea
juicy I guess x)


---

## Troubleshooting

1. If the Mario falls through the floor it is most likely caused because the world creator disabled the mesh read/write
   for the floor mesh. This reduces the memory usage, but nukes the vertex information from scripts. If you want it to
   work, ask the world creator to enable the mesh read/write for meshes that have essential colliders :)

---

## Todos

- [ ] Fix the sync between players.
- [ ] Fix frame rate.
- [ ] Offload the heavy work to a thread.
- [ ] Add support to configure terrain types.
- [ ] Add support for moving colliders.
- [ ] Add marios collision.
- [ ] Add support for coins?
- [ ] Add support for enemies?

---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
