# CCK.Debugger
This mod allows to debug content you upload to CVR, for now it allows debugging `Avatars` and `Props`. It does so by
creating a menu with a bunch information for the selected object. You can also use to debug your friend's content because
it allows to see the information about other people's avatars/props.

The menu will be attached to the left of your quick menu. There is a pin icon to fixate it in world space (this will also
prevent disappearing when you close the menu).

---
## Avatar Menu Features
- Attributes
  - Avatar User Name
  - Avatar Name (partial avatar id for now)
- Synced Parameters
- Local Parameters (parameters prefixed with a `#`)
- Default Parameters (parameters that cvr populates, eg: `GestureLeft`)

---
## Prop Menu Features
- Attributes
  - Prop Name (partial spawnable id for now)
  - User Name of the person that spawned it
  - Current person driving the sync (will be N/A when it's the local user)
  - Sync type (I attempted to make a half-ass mapping but requires more investigation)
- Synced Parameters
- Main Animator Parameters (all parameters (including the synced) from the animator on the root)
- Pickups (lists all pickups in the prop, and who's grabbing them)
- Attachments (lists all attachment scripts, and what they are attached to)

---
## Menu
![cck_debugger_menu.png](cck_debugger_menu.png)

- The `orange` marked buttons allows swapping between the `avatars` and `props` debug menu.
- The `green` marked button allows you to pin the menu in world space, and it will make it so it doesn't close with the menu.
- The `deep pink` buttons allow you to iterate through avatars/props.

---
## Todo
- [ ] Current playing animations by layer.
- [ ] `CVR Pointer`, `CVR Avanced Avatar Settings Trigger`, and `CVR Spawnable Trigger` debug.
- [ ] `Dynamic Bone` colliders and radius visualization.

---
## Disclosure

> ___
> ### ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
> ___