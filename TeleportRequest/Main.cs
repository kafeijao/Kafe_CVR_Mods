using ABI_RC.Core.Player;
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

    public static void RequestToTeleport(string playerName, string playerID) {

        if (PendingRequests.Contains(playerID)) {
            MelonLogger.Warning($"There is already a pending request for {playerName}");
            return;
        }

        PendingRequests.Add(playerID);

        MelonLogger.Msg($"Sending teleport request to {playerName}...");

        RequestLib.API.SendRequest(playerID, $"{playerName} is requesting to teleport to you.", requestResult => {
            if (PendingRequests.Contains(playerID)) PendingRequests.Remove(playerID);
            switch (requestResult) {
                case RequestLib.API.RequestResult.Accepted:
                    var msgAccepted = $"The player {playerName} has accepted the teleport request!";
                    MelonLogger.Msg(msgAccepted);
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgAccepted);
                    var target = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(np => np.Uuid == playerID);
                    if (target == null) {
                        MelonLogger.Error($"The player {playerName} couldn't be found...");
                        return;
                    }
                    MovementSystem.Instance.TeleportTo(target.PlayerObject.transform.position, target.PlayerObject.transform.eulerAngles);
                    break;
                case RequestLib.API.RequestResult.Declined:
                    var msgDeclined = $"The player {playerName} has declined the teleport request!";
                    MelonLogger.Msg(msgDeclined);
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgDeclined);
                    break;
                case RequestLib.API.RequestResult.TimedOut:
                    var msgTimedOut = $"The teleport request to the player {playerName} has timed out...";
                    MelonLogger.Msg(msgTimedOut);
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), msgTimedOut);
                    break;
            }
        });
    }

}
