using MelonLoader;

namespace Kafe.RequestLib.Integrations;

internal static class BTKSAImmersiveHudIntegration {

    private static void SendNotification(MelonMod possibleBTKSAImmersiveHud) {
        var immersiveHud = (BTKSAImmersiveHud.BTKSAImmersiveHud) possibleBTKSAImmersiveHud;
        immersiveHud.HudUpdatedNotifier(true);
    }

    internal static void Initialize(MelonMod possibleBTKSAImmersiveHud) {
        try {
            CohtmlPatches.HasNotifications += () => SendNotification(possibleBTKSAImmersiveHud);
        }
        catch (Exception e) {
            MelonLogger.Error($"Error during the BTKSAImmersiveHudIntegration.Initialize");
            MelonLogger.Error(e);
        }
    }
}
