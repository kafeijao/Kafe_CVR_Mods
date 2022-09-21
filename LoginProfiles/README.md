# LoginProfiles

This mod allows to use an argument when starting ChilloutVR in order to select a login profile. This enables
swapping accounts without requiring to enter the credentials (if logged in on that profile previously).
This is useful for me to deploy two clients on two different
accounts to test avatars and worlds synchronization stuff.

The syntax for the argument is `--profile=profile_id`. Where `profile_id` is whatever name you want the profile to be called.
It doesn't need to be the same as the username.

By default the game saves the credentials in the file `\ChilloutVR\ChilloutVR_Data\autologin.profile`. This mod modifies
the path to `\ChilloutVR\ChilloutVR_Data\autologin-profile_id.profile`.

**Note**: You will need to enter your credentials when using a profile for the first time, and from there on when using
that profile, it will re-use the profile credentials. To clear a profile you can either delete
the corresponding `autologin-profile_id.profile` file,
or start the game with the profile you want to clear, and then use the in-game menu to logout (this also deletes the file).

---

## Example using via steam:

![login_profile_example.png](login_profile_example.png)

---

## Example using a bat file:

Create a `random_name_123.bat` file next to where `ChilloutVR.exe` file is, with the following contents:

```bat
ChilloutVR.exe --profile=kafeijao
```

Then all you need is double click that file and should open ChilloutVR with the respective profile.

---

## Disclosure

> ---
> ⚠️ **Notice!**  
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
