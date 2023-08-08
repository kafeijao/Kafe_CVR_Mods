using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Systems.MovementSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshProp;

public static class ModConfig {

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
        
        var playerCat = BTKUILib.QuickMenuAPI.PlayerSelectPage.AddCategory(nameof(NavMeshProp));

        BTKUILib.QuickMenuAPI.OnPlayerSelected += (playerName, playerID) => {
            playerCat.ClearChildren();
            playerCat.AddButton("Agent Follow", "", $"Sets the agent to follow {playerName}")
                .OnPress += () => {
                var newTarget = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault(e => e.Uuid == playerID);
                NavMeshProp.FollowingPlayer = NavMeshProp.FollowingPlayer == newTarget ? null : newTarget;
            };
        };
    }

}
