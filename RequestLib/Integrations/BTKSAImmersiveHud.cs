using MelonLoader;

namespace Kafe.RequestLib.Integrations;

public static class BTKSAImmersiveHudIntegration {
    internal static void Initialize(MelonMod possibleBTKSAImmersiveHud) {
        var immersiveHud = (BTKSAImmersiveHud.BTKSAImmersiveHud) possibleBTKSAImmersiveHud;
        CohtmlPatches.HasNotifications += () => immersiveHud.HudUpdatedNotifier(true);
    }
}
