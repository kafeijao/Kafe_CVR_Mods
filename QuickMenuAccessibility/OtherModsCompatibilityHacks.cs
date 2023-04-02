using MelonLoader;
using UnityEngine;

namespace Kafe.QuickMenuAccessibility;

public static class OtherModsCompatibilityHacks {

    public static Action<Transform> AnchorChanged;

    public static void Initialize(IEnumerable<MelonMod> registeredMelons) {

        // Check for Menu Scale Patch
        if (registeredMelons.FirstOrDefault(m => m.Info.Name == "MenuScalePatch") is NAK.Melons.MenuScalePatch.MenuScalePatch menuScalePatch) {
            var isCompatibleVersion = menuScalePatch.Info.Version == "4.2.6";
            if (isCompatibleVersion) {
                MelonLogger.Msg($"[Compatibility] Detected MenuScalePatch mod version {menuScalePatch.Info.Version}. Integrating...");
            }
            else {
                MelonLogger.Warning($"[Compatibility] Detected MenuScalePatch mod version {menuScalePatch.Info.Version}." +
                                    $"This version integration is untested, you might run into strange behavior with the quick menu.");
            }
            AnchorChanged += anchorTransform => {
                NAK.Melons.MenuScalePatch.Helpers.QuickMenuHelper.Instance.handAnchor = anchorTransform;
            };
        }
    }

}
