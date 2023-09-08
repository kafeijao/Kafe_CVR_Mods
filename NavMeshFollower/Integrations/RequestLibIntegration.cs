using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Systems.GameEventSystem;
using MelonLoader;

namespace Kafe.NavMeshFollower.Integrations;

public static class RequestLibIntegration {

    static RequestLibIntegration() {
        CVRGameEventSystem.Player.OnLeave.AddListener(descriptor => {
            if (AcceptedRequestPlayerIds.Remove(descriptor.ownerId)) {
                MelonLogger.Warning($"{descriptor.userName} has left, revoking {nameof(NavMeshFollower)} interaction...");
            }
        });
        RequestLib.API.RegisterMod();
    }

    private static readonly HashSet<string> PendingRequests = new();

    private static readonly HashSet<string> AcceptedRequestPlayerIds = new();

    public static bool HasRequestLib(string playerID) => RequestLib.API.HasRequestLib(playerID);

    public static bool HasPermission(string playerID) => AcceptedRequestPlayerIds.Contains(playerID);

    public static bool IsRequestPending(string playerID) => PendingRequests.Contains(playerID);

    public static void RequestToInteract(string playerName, string playerID) {

        if (PendingRequests.Contains(playerID)) {
            MelonLogger.Warning($"There is already a pending request for {playerName}");
            return;
        }

        PendingRequests.Add(playerID);
        MelonLogger.Msg($"Sending a {nameof(NavMeshFollower)} interact request to {playerName}...");
        RequestLib.API.SendRequest(new RequestLib.API.Request(playerID, $"{AuthManager.username} is requesting allow their Prop Follower interact with you.", OnResponse));
    }

    private static void OnResponse(RequestLib.API.Request request, RequestLib.API.Response response) {

        // Remove from our cache preventing of sending duplicates
        if (PendingRequests.Contains(request.TargetPlayerGuid)) PendingRequests.Remove(request.TargetPlayerGuid);

        var playerName = CVRPlayerManager.Instance.TryGetPlayerName(request.TargetPlayerGuid);
        switch (response.Result) {

            case RequestLib.API.RequestResult.Accepted:
                MelonLogger.Msg($"The player {playerName} has ACCEPTED the {nameof(NavMeshFollower)} interact request!");
                AcceptedRequestPlayerIds.Add(request.TargetPlayerGuid);
                break;

            case RequestLib.API.RequestResult.Declined:
                MelonLogger.Msg($"The player {playerName} has DECLINED the {nameof(NavMeshFollower)} interact request!");
                break;

            case RequestLib.API.RequestResult.TimedOut:
                MelonLogger.Msg($"The the {nameof(NavMeshFollower)} interact request to the player {playerName} has TIMED OUT...");
                break;
        }

        // Update the button info
        ModConfig.UpdatePlayerPage();
    }

}
