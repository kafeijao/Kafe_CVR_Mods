using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.UI.UILib;
using MelonLoader;

namespace Kafe.NavMeshFollower.Integrations;

public static class RequestLibIntegration {

    static RequestLibIntegration() {
        CVRGameEventSystem.Player.OnLeaveEntity.AddListener(entity => {
            if (AcceptedRequestPlayerIds.Remove(entity.PlayerDescriptor.ownerId)) {
                MelonLogger.Warning($"{entity.PlayerDescriptor.userName} has left, revoking {nameof(NavMeshFollower)} interaction...");
            }
        });
        RequestLib.API.RegisterMod();
    }

    private static readonly Dictionary<string, Action> PendingRequests = new();

    private static readonly HashSet<string> AcceptedRequestPlayerIds = new();

    public static bool HasRequestLib(string playerID) => RequestLib.API.HasRequestLib(playerID);

    public static bool HasPermission(string playerID) => AcceptedRequestPlayerIds.Contains(playerID);

    public static bool IsRequestPending(string playerID) => PendingRequests.ContainsKey(playerID);

    public static void RequestToInteract(string playerName, string playerID, Action onAccepted) {

        if (!PendingRequests.TryAdd(playerID, onAccepted)) {
            MelonLogger.Warning($"There is already a pending request for {playerName}");
            return;
        }

        MelonLogger.Msg($"Sending a {nameof(NavMeshFollower)} interact request to {playerName}...");
        RequestLib.API.SendRequest(new RequestLib.API.Request(playerID, $"{AuthManager.Username} is requesting allow their Prop Follower interact with you.", OnResponse));
        QuickMenuAPI.ShowAlertToast($"Asked {playerName} for permission, waiting for their reply...", 3);
    }

    private static void OnResponse(RequestLib.API.Request request, RequestLib.API.Response response) {

        // Remove from our cache preventing of sending duplicates
        PendingRequests.Remove(request.TargetPlayerGuid, out var onAccepted);

        var playerName = CVRPlayerManager.Instance.TryGetPlayerName(request.TargetPlayerGuid);
        switch (response.Result) {

            case RequestLib.API.RequestResult.Accepted:
                MelonLogger.Msg($"The player {playerName} has ACCEPTED the {nameof(NavMeshFollower)} interact request!");
                AcceptedRequestPlayerIds.Add(request.TargetPlayerGuid);
                QuickMenuAPI.ShowAlertToast($"{playerName} accepted, your followers can interact with them", 3);
                onAccepted?.Invoke();
                break;

            case RequestLib.API.RequestResult.Declined:
                MelonLogger.Msg($"The player {playerName} has DECLINED the {nameof(NavMeshFollower)} interact request!");
                QuickMenuAPI.ShowAlertToast($"{playerName} declined your request", 3);
                break;

            case RequestLib.API.RequestResult.TimedOut:
                MelonLogger.Msg($"The the {nameof(NavMeshFollower)} interact request to the player {playerName} has TIMED OUT...");
                QuickMenuAPI.ShowAlertToast($"{playerName} didn't reply to your request", 3);
                break;
        }
    }

}
