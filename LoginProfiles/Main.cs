using ABI_RC.Core.Networking;
using ABI_RC.Core.UI;
using HarmonyLib;
using MelonLoader;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace LoginProfiles;

public class LoginProfiles : MelonMod {

    private const string AutoLoginFilePath = "/autologin.profile";
    private static readonly string ProfilePath = GetProfilePath();

    private static int _patchedCount = 0;

    private static string GetProfilePath() {
        var profilePath = AutoLoginFilePath;
        foreach (var commandLineArg in Environment.GetCommandLineArgs()) {
            if (!commandLineArg.StartsWith("--profile=")) continue;

            var profile = "-" + commandLineArg.Split(new[] { "=" }, StringSplitOptions.None)[1];
            profilePath = $"/autologin{profile}.profile";
            break;
        }
        return profilePath;
    }

    #if DEBUG
    public override void OnLateInitializeMelon() {
        MelonLogger.Msg($"Patched {_patchedCount} File Paths!");
    }
    #endif

    private static IEnumerable<CodeInstruction> ReplaceAutoLoginTranspiler(IEnumerable<CodeInstruction> instructions) {
        foreach (var instruction in instructions) {
            if (instruction.Is(OpCodes.Ldstr, AutoLoginFilePath)) {
                _patchedCount++;
                yield return new CodeInstruction(OpCodes.Ldstr, ProfilePath);
            }
            else {
                yield return instruction;
            }
        }
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(AuthUIManager), nameof(AuthUIManager.Start))]
        private static IEnumerable<CodeInstruction> Transpiler_AuthUIManager_Start(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(AuthManager), nameof(AuthManager.Authenticate), MethodType.Enumerator)]
        [HarmonyPatch( new[] {typeof(AuthUIManager), typeof(AuthUIManager.AuthType), typeof(string), typeof(string)})]
        private static IEnumerable<CodeInstruction> Transpiler_AuthManager_Authenticate(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(AuthManager), nameof(AuthManager.LoadAutoLogin))]
        private static IEnumerable<CodeInstruction> Transpiler_AuthManager_LoadAutoLogin(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(AuthManager), nameof(AuthManager.Logout))]
        private static IEnumerable<CodeInstruction> Transpiler_AuthManager_Logout(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);
    }
}
