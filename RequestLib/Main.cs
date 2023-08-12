using System.Diagnostics;
using Kafe.RequestLib.Properties;
using MelonLoader;

namespace Kafe.RequestLib;

internal class RequestLib : MelonMod {

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ConfigJson.LoadConfigJson();
        ModConfig.InitializeBTKUI();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        // Check for BTKSAImmersiveHud
        var possibleBtksaImmersiveHud = RegisteredMelons.FirstOrDefault(m => m.Info.Name == AssemblyInfoParams.BTKSAImmersiveHudName);
        if (possibleBtksaImmersiveHud != null) {
            MelonLogger.Msg($"Detected {AssemblyInfoParams.BTKSAImmersiveHudName} mod, we're adding the integration!");
            Integrations.BTKSAImmersiveHudIntegration.Initialize(possibleBtksaImmersiveHud);
        }

        ModNetwork.Initialize();
    }

    // public override void OnUpdate() {
    //     if (Input.GetKeyDown(KeyCode.Period)) {
    //         var playerGuid = CVRPlayerManager.Instance.NetworkPlayers.FirstOrDefault()?.Uuid;
    //         if (playerGuid == null) {
    //             playerGuid = Guid.NewGuid().ToString("D");
    //         }
    //         ModNetwork.OnRequest(playerGuid, Guid.NewGuid().ToString("D"), "RequestLib",
    //             "Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Aenean commodo ligula eget dolor." +
    //             " Aenean massa. Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus. Donec q?");
    //     }
    // }

    internal static string GetModName() {
        try {
            var callingFrame = new StackTrace().GetFrame(2);
            var callingAssembly = callingFrame.GetMethod().Module.Assembly;
            var callingMelonAttr = callingAssembly.CustomAttributes.FirstOrDefault(
                attr => attr.AttributeType == typeof(MelonInfoAttribute));
            return (string) callingMelonAttr!.ConstructorArguments[1].Value;
        }
        catch (Exception ex) {
            MelonLogger.Error("[GetModName] Attempted to get a mod's name...");
            MelonLogger.Error(ex);
        }
        return null;
    }
}
