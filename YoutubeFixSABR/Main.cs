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

    public static readonly List<string> ArgsToRemove = new List<string>();

    public static readonly List<string> ArgsToAdd = new List<string>
    {
        $" --js-runtimes \"deno:{DenoExePath}\"",
    };

    public static string LastCvrVideoResolverHashUrl;
    public static string LastCvrVideoResolverExecutableUrl;

    public static string NightlyVideoResolverHashUrl;
    public static string NightlyVideoResolverExecutableUrl;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();
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

    public static void UpdateYoutubeDlLinks()
    {
        if (!ModConfig.Enabled)
        {
            MelonLogger.Msg($"Skipping Updating YoutubeDl Links since Mod is disabled on settings");
            return;
        }

        if (string.IsNullOrEmpty(NightlyVideoResolverHashUrl))
        {
            MelonLogger.Msg("Skipping UpdateYoutubeDlLinks, oo Nightly Video Resolver Hash URL provided yet. " +
                            "This should only happen if you're not logged in the game.");
            return;
        }

        if (ModConfig.UseNightly)
        {
            YoutubeDl.VideoResolverHashUrl = NightlyVideoResolverHashUrl;
            YoutubeDl.VideoResolverExecutableUrl = NightlyVideoResolverExecutableUrl;
            MelonLogger.Msg($"Nightly yt-dlp setting enabled, using the nightly yt-dlp: {NightlyVideoResolverExecutableUrl}");
        }
        else
        {
            YoutubeDl.VideoResolverHashUrl = LastCvrVideoResolverHashUrl;
            YoutubeDl.VideoResolverExecutableUrl = LastCvrVideoResolverExecutableUrl;
            MelonLogger.Msg($"Nightly setting disabled, using the default cvr yt-dlp link: {LastCvrVideoResolverExecutableUrl}");
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(YoutubeDl), nameof(YoutubeDl.UpdateVideoResolver))]
        private static void Before_YoutubeDl_UpdateVideoResolver()
        {
            try
            {
                // Cache results from last upload call (happens on login)
                LastCvrVideoResolverHashUrl = YoutubeDl.VideoResolverHashUrl;
                LastCvrVideoResolverExecutableUrl = YoutubeDl.VideoResolverExecutableUrl;

                // Cache the nightly versions of it
                NightlyVideoResolverHashUrl = YoutubeDl.VideoResolverHashUrl.Replace("yt-dlp/yt-dlp", "yt-dlp/yt-dlp-nightly-builds");
                NightlyVideoResolverExecutableUrl = YoutubeDl.VideoResolverExecutableUrl.Replace("yt-dlp/yt-dlp", "yt-dlp/yt-dlp-nightly-builds");

                UpdateYoutubeDlLinks();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(Before_YoutubeDl_UpdateVideoResolver)} Patch", e);
            }
        }
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
                string lower =  original.ToLower();
                if (!lower.Contains("youtube.com") && !lower.Contains("youtu.be"))
                {
                    MelonLogger.Msg($"Skipping since it's not a youtube video, using the original yt-dlp args: {original}");
                    return original;
                }

                if (!ModConfig.Enabled)
                {
                    MelonLogger.Msg($"Skipping since Mod is disabled on settings, using the original yt-dlp args: {original}");
                    return original;
                }

                MelonLogger.Msg($"About to call yt-dlp with the args: {original}");

                // Remove un-wanted args
                foreach (string argToRemove in ModConfig.UseCustomArgs ? ModConfig.CustomArgsToRemove : ArgsToRemove)
                    original = original.Replace(argToRemove, "");

                // Add our args
                foreach (string argToAdd in ModConfig.UseCustomArgs ? ModConfig.CustomArgsToAdd : ArgsToAdd)
                    original += argToAdd;

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
