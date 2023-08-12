# RequestLib

[![Download Latest RequestLib.dll](../.Resources/DownloadButtonEnabled.svg "Download Latest RequestLib.dll")](https://github.com/kafeijao/Kafe_CVR_Mods/releases/latest/download/RequestLib.dll)

The Request Lib is a Mod that allows users to receive requests from other players in the same instance via the Mod 
Network.

This will enable mods to ask permission for things to players without needing them to have the mod. For example if both 
players have the `RequestLib`, this will allow the player 1 with `TeleportRequest` to request to teleport to player 2 
even if the player 2 doesn't have the `TeleportRequest` mod.

## Features
- Receive requests from remote players via the `Mod Network`, similarly to invites.
- Fully customizable permissions that allow requests to be `asked`, `auto-accepted`, or `auto-declined`
- Several permissions granularity, they can be global, per player, and even per player + mod combo.

## Limitations
- Maximum request message size: `200` characters
- Requests will time out after 1 minute without an answer

## QuickStart for Mods

Here's a quickstart example for mods to use. You need to register your mod, this enables remote players to check if you
have the mod installed. After registering you can send requests, make sure you're in on an Online Instance, and that the
target player are in the instance.

```csharp
public override void OnInitializeMelon() {
    // Register your mod on the RequestLib
    RequestLib.API.RegisterMod();
}

public override void OnUpdate() {
    // When pressing F request the player targetPlayerGuid
    if (Input.GetKeyDown(KeyCode.F)) {
        var targetPlayerGuid = ""; // Get the target player ID from BTKUI menus for example
        var requestMessage = $"{MetaPort.Instance.username} is requesting you to ...";
        // Send the request and call out function OnResponse whenever we get the response
        RequestLib.API.SendRequest(new RequestLib.API.Request(targetPlayerGuid, requestMessage, OnResponse));
    }
}

private static void OnResponse(RequestLib.API.Request request, RequestLib.API.Response response) {
    // Here you can handle the response for the request :)
    var playerName = CVRPlayerManager.Instance.TryGetPlayerName(request.TargetPlayerGuid);
    switch (response.Result) {
        case RequestLib.API.RequestResult.Accepted:
            MelonLogger.Msg($"The player {playerName} has ACCEPTED our request!");
            break;
        case RequestLib.API.RequestResult.Declined:
            MelonLogger.Msg($"The player {playerName} has DECLINED the teleport request :(");
            break;
        case RequestLib.API.RequestResult.TimedOut:
            MelonLogger.Msg($"The request to the player {playerName} has TIMED OUT...");
            break; 
    }
}
```

## API for Mods

Here are the features you can expect from the API:
- Send a Request with a callback for the Response.
- Check whether a remote player has the `RequestLib` or your mod installed.
- Use `Interceptors` to prevent requests sent from your mod by a remote player from showing up to the user.
- Check pending sent and received request, and Cancel/Resolve them via the `API`.
- Subscribe to user's info updated, like whether they have the `RequestLib` or your `Mod` Installed.
- Send/Receive metadata as a string in the requests. Namely when using `Interceptors` or Resolving request via the `API`


All the interfaces to use this mod are located on [API.cs](./API.cs). You can also refer to [API.md](./API.md) as an API
reference.

Also I implemented a [TeleportRequest](../TeleportRequest) Mod that takes advantage of this library if you want an
implementation example.

## Disclosure

> ---
> ⚠️ **Notice!**
>
> This mod's developer(s) and the mod itself, along with the respective mod loaders, have no affiliation with ABI!
>
> ---
