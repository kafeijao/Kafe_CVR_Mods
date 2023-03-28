using ABI_RC.VideoPlayer.Scripts;
using HarmonyLib;
using MelonLoader;

namespace Kafe.FuckRTSP;

public class FuckRTSP : MelonMod {

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UrlWhitelist), nameof(UrlWhitelist.UrlAllowed))]
        public static bool Before_UrlWhitelist_UrlAllowed(ref UrlWhitelist.UrlStatus __result, string url) {
            if (url.StartsWith("rtsp")) {
                MelonLogger.Msg($"We attempted to load an evil RTSP Video... FUCK RTSP! Url: {url}");
                __result = UrlWhitelist.UrlStatus.Denied;
                return false;
            }
            return true;
        }
    }
}
