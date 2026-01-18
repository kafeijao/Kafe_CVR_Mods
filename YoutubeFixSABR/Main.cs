using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.VideoPlayer;
using HarmonyLib;
using MelonLoader;

namespace Kafe.YoutubeFixSABR;

public class YoutubeFixSABR : MelonMod
{
    public static readonly string UserDataFolderPath = Path.GetFullPath(Path.Combine("UserData", nameof(YoutubeFixSABR)));
    public static readonly string DenoExePath = Path.Combine(UserDataFolderPath, "deno.exe");

    public override void OnInitializeMelon()
    {
        MelonCoroutines.Start(EnsureDeno());
        RunPatches();
    }

    private static IEnumerator EnsureDeno()
    {
        var denoTask = Task.Run(DenoDownloader.EnsureDenoAsync);
        while (!denoTask.IsCompleted) yield return null;
    }

    private void RunPatches()
    {
        var targetMethod = typeof(YoutubeDl).GetMethod(nameof(YoutubeDl.GetVideoMetaDataAsync), BindingFlags.Static | BindingFlags.NonPublic);
        var stateMachineAttr = targetMethod!.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>();
        var moveNextMethod = stateMachineAttr.StateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

        HarmonyInstance.Patch(
            moveNextMethod,
            transpiler: new HarmonyMethod(typeof(YoutubeDlPatch), nameof(YoutubeDlPatch.Transpiler))
        );

        MelonLogger.Msg("Patches ran successfully!");
    }

    private static class YoutubeDlPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            var modifyMethod = AccessTools.Method(typeof(YoutubeDlPatch), nameof(ModifyArguments));

            bool done = false;

            for (int i = 0; i < code.Count; i++)
            {
                yield return code[i];

                // stloc.2 == string str
                if (!done && code[i].opcode == OpCodes.Stloc_2)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, modifyMethod);
                    yield return new CodeInstruction(OpCodes.Stloc_2);
                    done = true;
                }
            }
        }

        private static string ModifyArguments(string original)
        {
            try
            {
                MelonLogger.Msg($"About to call yt-dlp with the args: {original}");

                // Remove un-wanted args
                original = original.Replace(" --impersonate=Safari-15.3", "");
                original = original.Replace(" --extractor-arg \"youtube:player_client=web\"", "");

                // Add our args
                original += $" --js-runtimes \"deno:{DenoExePath}\"";
                original += " --extractor-args \"youtube:player-client=default,-web_safari\"";

                MelonLogger.Msg($"Replaced with our args: {original}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning(e);
            }

            return original;
        }
    }
}
