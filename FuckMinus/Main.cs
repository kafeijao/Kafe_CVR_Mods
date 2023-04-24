using ABI_RC.Core.Networking;
using HarmonyLib;
using MelonLoader;

namespace Kafe.FuckMinus;

public class FuckMinus : MelonMod {

    [HarmonyPatch]
    internal class HarmonyPatches {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Update))]
        private static bool Before_NetworkManager_Update() {
            return false;
        }
    }
}
