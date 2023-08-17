using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;

namespace Kafe.NavMeshProp;

public static class ModConfig {

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static string GetPropImageUrl(string guid) {
        return $"https://files.abidata.io/user_content/spawnables/{guid}/{guid}.png";
    }

    internal static void UpdatePlayerPage() {
        if (CVRPlayerManager.Instance == null ||
            !CVRPlayerManager.Instance.NetworkPlayers.Exists(p => p.Uuid == BTKUILib.QuickMenuAPI.SelectedPlayerID))
            return;
        UpdatePlayerPage(BTKUILib.QuickMenuAPI.SelectedPlayerName, BTKUILib.QuickMenuAPI.SelectedPlayerID);
    }

    private static void UpdatePlayerPage(string playerName, string playerID) {
        if (_playersNavMeshCat == null) return;

        _playersNavMeshCat.ClearChildren();

        foreach (var petController in NavMeshProp.PetController.Controllers) {

            var isFollowingPlayer = petController.FollowingPlayer != null && petController.FollowingPlayer.Uuid == playerID;

            var button = _playersNavMeshCat.AddButton($"{(isFollowingPlayer ? "Following" : "Nop")}",
                GetPropImageUrl(petController.Spawnable.guid), $"Sets the pet to follow {playerName}");

            button.OnPress += () => {
                if (petController == null) {
                    button.Delete();
                    return;
                }

                var newTarget = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(e => e.Uuid == playerID);

                petController.FollowingPlayer = petController.FollowingPlayer == newTarget ? null : newTarget;

                isFollowingPlayer = petController.FollowingPlayer != null && petController.FollowingPlayer.Uuid == playerID;
                button.ButtonText = isFollowingPlayer ? "Following" : "Nop";
            };
        }
    }

    private static BTKUILib.UIObjects.Category _playersNavMeshCat;

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
        _playersNavMeshCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(NavMeshProp));
        BTKUILib.QuickMenuAPI.OnPlayerSelected += UpdatePlayerPage;
    }
}
