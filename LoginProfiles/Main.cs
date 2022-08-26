using System.Reflection.Emit;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using HarmonyLib;
using MelonLoader;

namespace LoginProfiles;

public class LoginProfiles : MelonMod {

    private const string AutoLoginFilePath = "/autologin.profile";
    private static readonly string ProfilePath = GetProfilePath();

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

    private static IEnumerable<CodeInstruction> ReplaceAutoLoginTranspiler(IEnumerable<CodeInstruction> instructions) {
        foreach (var instruction in instructions) {
            if (instruction.Is(OpCodes.Ldstr, AutoLoginFilePath)) {
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
        [HarmonyPatch(typeof(AuthUIManager), "Start")]
        private static IEnumerable<CodeInstruction> Transpiler_AuthUIManager_Start(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(AuthUIManager), nameof(AuthUIManager.Authenticate), typeof(AuthUIManager.AuthType), typeof(string), typeof(string))]
        private static IEnumerable<CodeInstruction> Transpiler_AuthUIManager_Authenticate(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MetaPort), "Awake")]
        private static IEnumerable<CodeInstruction> Transpiler_MetaPort_Awake(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MetaPort), nameof(MetaPort.Logout))]
        private static IEnumerable<CodeInstruction> Transpiler_MetaPort_Logout(IEnumerable<CodeInstruction> instructions) => ReplaceAutoLoginTranspiler(instructions);
    }
}
