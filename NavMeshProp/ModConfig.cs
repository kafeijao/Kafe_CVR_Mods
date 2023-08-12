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

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
        
        var playerCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(NavMeshProp));

        BTKUILib.QuickMenuAPI.OnPlayerSelected += (playerName, playerID) => {
            playerCat.ClearChildren();

            foreach (var petController in NavMeshProp.PetController.Controllers) {

                var isFollowingPlayer = petController.FollowingPlayer != null && petController.FollowingPlayer.Uuid == playerID;

                var button = playerCat.AddButton($"{(isFollowingPlayer ? "Following" : "Nop")}",
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
        };
    }

}
