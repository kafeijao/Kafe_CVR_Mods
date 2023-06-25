using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.MovementSystem;
using MelonLoader;

namespace Kafe.TeleportRequest;

public class TeleportRequest : MelonMod {

    private static readonly HashSet<string> PendingRequests = new();

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();

        // Register this mod on the request lib
        RequestLib.API.RegisterMod();
    }

    private static void OnResponse(RequestLib.API.Request request, RequestLib.API.Response response) {

        // Remove from our cache preventing of sending duplicates
        if (PendingRequests.Contains(request.TargetPlayerGuid)) PendingRequests.Remove(request.TargetPlayerGuid);

        var playerName = CVRPlayerManager.Instance.TryGetPlayerName(request.TargetPlayerGuid);
        switch (response.Result) {

            case RequestLib.API.RequestResult.Accepted:
                var msgAccepted = $"The player {playerName} has <color=green>Accepted</color> the teleport request!";
                MelonLogger.Msg(msgAccepted);
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgAccepted);
                var target = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(np => np.Uuid == request.TargetPlayerGuid);
                if (target == null) {
                    MelonLogger.Warning($"The player {playerName} is not in the Instance anymore...");
                    return;
                }
                if (!MovementSystem.Instance.canFly) {
                    MelonLogger.Warning($"This world doesn't allow flight, so we won't be able to teleport.");
                    return;
                }
                MovementSystem.Instance.TeleportTo(target.PlayerObject.transform.position, target.PlayerObject.transform.eulerAngles);
                break;

            case RequestLib.API.RequestResult.Declined:
                var msgDeclined = $"The player {playerName} has <color=red>Declined</color> the teleport request!";
                MelonLogger.Msg(msgDeclined);
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgDeclined);
                break;

            case RequestLib.API.RequestResult.TimedOut:
                var msgTimedOut = $"The teleport request to the player {playerName} has <color=yellow>timed out</color>...";
                MelonLogger.Msg(msgTimedOut);
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgTimedOut);
                break;
        }
    }

    public static void RequestToTeleport(string playerName, string playerID) {

        if (PendingRequests.Contains(playerID)) {
            MelonLogger.Warning($"There is already a pending request for {playerName}");
            return;
        }
        PendingRequests.Add(playerID);

        MelonLogger.Msg($"Sending teleport request to {playerName}...");
        RequestLib.API.SendRequest(new RequestLib.API.Request(playerID, $"{MetaPort.Instance.username} is requesting to teleport to you.", OnResponse));
    }

}
