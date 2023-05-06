using ABI_RC.Core.Player;
using MelonLoader;
using UnityEngine;

namespace Kafe.RequestLib;

internal class RequestLib : MelonMod {

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        // Check for ChatBox
        var possibleBtksaImmersiveHud = RegisteredMelons.FirstOrDefault(m => m.Info.Name == "BTKSAImmersiveHud");
        if (possibleBtksaImmersiveHud != null) {
            MelonLogger.Msg($"Detected BTKSAImmersiveHud mod, we're adding the integration!");
            Integrations.BTKSAImmersiveHudIntegration.Initialize(possibleBtksaImmersiveHud);
        }

    }

    public override void OnUpdate() {
        if (Input.GetKeyDown(KeyCode.Period)) {
            var playerGuid = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault()?.Uuid;
            if (playerGuid == null) {
                playerGuid = Guid.NewGuid().ToString("D");
            }
            ModNetwork.OnRequest(playerGuid, Guid.NewGuid().ToString("D"), "RequestLib",
                "Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Aenean commodo ligula eget dolor." +
                " Aenean massa. Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Donec q?");
        }
    }
}
