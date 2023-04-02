using MelonLoader;

namespace Kafe.QuickMenuAccessibility;

public static class CompatibilityHacksMenuScalePatch {

    public static void Initialize(MelonMod possibleMenuScalePatch) {

        var menuScalePatch = (NAK.Melons.MenuScalePatch.MenuScalePatch) possibleMenuScalePatch;
        var isCompatibleVersion = menuScalePatch.Info.Version == "4.2.6";
        if (isCompatibleVersion) {
            MelonLogger.Msg($"[Compatibility] Detected MenuScalePatch mod version {menuScalePatch.Info.Version}. Integrating...");
        }
        else {
            MelonLogger.Warning($"[Compatibility] Detected MenuScalePatch mod version {menuScalePatch.Info.Version}." +
                                $"This version integration is untested, you might run into errors/strange behavior with the quick menu.");
        }
        QuickMenuAccessibility.AnchorChanged += anchorTransform => {
            NAK.Melons.MenuScalePatch.Helpers.QuickMenuHelper.Instance.handAnchor = anchorTransform;
        };

    }
}
