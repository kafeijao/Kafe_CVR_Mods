using HarmonyLib;
using MelonLoader;
using RenderHeads.Media.AVProVideo;

namespace Kafe.NoHardwareAcceleration;

public class NoHardwareAcceleration : MelonMod {

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MediaPlayer), nameof(MediaPlayer.Awake))]
        public static void After_MediaPlayer_Awake(MediaPlayer __instance) {
            var useHardwareAcceleration = !ModConfig.MeDisableHardwareAcceleration.Value;
            __instance._optionsWindows.useHardwareDecoding = useHardwareAcceleration;
            __instance._optionsWindowsUWP.useHardwareDecoding = useHardwareAcceleration;
        }
    }
}
