using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.UI;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.TeleportRequest;

public class TeleportRequest : MelonMod {

    private static readonly HashSet<string> PendingRequests = new();

    private static string _currentWorld;
    private static readonly Queue<Tuple<Vector3, Vector3>> PreviousTeleportLocations = new();

    internal static Action PreviousTeleportLocationsChanged;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        // Check for ChatBox
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == Properties.AssemblyInfoParams.ChatBoxName) != null) {
            MelonLogger.Msg("Detected ChatBox mod! Adding the integration...");
            Integrations.ChatBoxIntegration.InitializeChatBox();
        }

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
                MelonLogger.Msg($"The player {playerName} has ACCEPTED the teleport request!");
                if (ModConfig.MeShowHudMessages.Value) {
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>The player {playerName} has <span style=\"color:green; display:inline\">Accepted</span> the teleport request!</span>");
                }
                var target = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(np => np.Uuid == request.TargetPlayerGuid);
                if (target == null) {
                    MelonLogger.Warning($"The player {playerName} is not in the Instance anymore...");
                    return;
                }
                if (!MovementSystem.Instance.canFly) {
                    MelonLogger.Warning($"This world doesn't allow flight, so we won't be able to teleport.");
                    return;
                }

                // Save position and rotation before teleporting
                PreviousTeleportLocations.Enqueue(new Tuple<Vector3, Vector3>(
                    MovementSystem.Instance.transform.position with { y = 0 },
                    MovementSystem.Instance.rotationPivot.eulerAngles with { x = 0, z = 0 })
                );
                PreviousTeleportLocationsChanged?.Invoke();

                MovementSystem.Instance.TeleportTo(target.PlayerObject.transform.position, target.PlayerObject.transform.eulerAngles);
                break;

            case RequestLib.API.RequestResult.Declined:
                MelonLogger.Msg($"The player {playerName} has DECLINED the teleport request!");
                if (ModConfig.MeShowHudMessages.Value) {
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>The player {playerName} has <span style=\"color:red; display:inline\">Declined</span> the teleport request!</span>");
                }
                break;

            case RequestLib.API.RequestResult.TimedOut:
                MelonLogger.Msg($"The teleport request to the player {playerName} has TIMED OUT...");
                if (ModConfig.MeShowHudMessages.Value) {
                    CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>The teleport request to the player {playerName} has <span style=\"color:yellow; display:inline\">Timed Out</span>...</span>");
                }
                break;
        }
    }

    public static void RequestToTeleport(string playerName, string playerID) {

        // Check for the Request Lib
        if (!RequestLib.API.HasRequestLib(playerID)) {
            MelonLogger.Warning($"Attempted to send a teleport request to {playerName}, but the player doesn't have the {nameof(RequestLib)} mod :(");
            if (ModConfig.MeShowHudMessages.Value) {
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>Attempted to send a teleport request to <span style=\"color:green\">{playerName}</span>, but the player doesn't have the <span style=\"color:green\">{nameof(RequestLib)}</span> mod :(</span>");
            }
            return;
        }

        if (PendingRequests.Contains(playerID)) {
            MelonLogger.Warning($"There is already a pending request for {playerName}");
            if (ModConfig.MeShowHudMessages.Value) {
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>There is already a pending request to <span style=\"color:green\">{playerName}</span>...</span>");
            }
            return;
        }

        PendingRequests.Add(playerID);

        MelonLogger.Msg($"Sending teleport request to {playerName}...");
        if (ModConfig.MeShowHudMessages.Value) {
            CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>Sending <span style=\"color:green; display:inline\">{playerName}</span> a teleport request...</span>");
        }
        RequestLib.API.SendRequest(new RequestLib.API.Request(playerID, $"{AuthManager.username} is requesting to teleport to you.", OnResponse));
    }

    internal static void TeleportBack() {
        if (PreviousTeleportLocations.Count != 0) {
            var lastLocation = PreviousTeleportLocations.Dequeue();
            MelonLogger.Msg("Teleporting back to the location before teleporting...");
            MovementSystem.Instance.TeleportToPosRot(lastLocation.Item1, Quaternion.Euler(lastLocation.Item2));
        }
        else {
            MelonLogger.Msg("There are no previous destinations to teleport to...");
            if (ModConfig.MeShowHudMessages.Value) {
                CohtmlHud.Instance.ViewDropText(nameof(TeleportRequest), $"<span>There are no previous destinations to teleport to...</span>");
            }
        }
        PreviousTeleportLocationsChanged?.Invoke();
    }

    internal static int GoBackCount() => PreviousTeleportLocations.Count;

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRWorld), nameof(CVRWorld.OnEnable))]
        private static void After_CVRWorld_OnEnable(CVRWorld __instance) {
            // Clear previous teleport locations upon joining different worlds
            try {
                var newWorldGuid = __instance.GetComponent<CVRAssetInfo>().objectId;
                if (_currentWorld != newWorldGuid) {
                    PreviousTeleportLocations.Clear();
                    PreviousTeleportLocationsChanged?.Invoke();
                }
                _currentWorld = newWorldGuid;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRWorld_OnEnable)}");
                MelonLogger.Error(e);
            }
        }
    }

}
