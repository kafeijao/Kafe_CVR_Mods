using System.Xml;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace LoginProfiles;

public class LoginProfiles : MelonMod {

    private static void PatchProfilePath(ref string path) {
        if (path == Application.dataPath + "/autologin.profile") {
            path = Application.dataPath + $"/autologin{GetProfile()}.profile";
        }
    }

    private static string GetProfile() {
        var profile = "";
        foreach (var commandLineArg in Environment.GetCommandLineArgs()) {
            if (commandLineArg.StartsWith("--profile=")) {
                profile = "-" + commandLineArg.Split(new[] { "=" }, StringSplitOptions.None)[1];
                break;
            }
        }
        return profile;
    }

    [HarmonyPatch]
    class HarmonyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(File), nameof(File.ReadAllText), typeof(string))]
        private static bool BeforeReadAllText(ref string path) {
            PatchProfilePath(ref path);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(File), nameof(File.Exists), typeof(string))]
        private static bool BeforeExists(ref string path) {
            PatchProfilePath(ref path);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(File), nameof(File.Delete), typeof(string))]
        private static bool BeforeDelete(ref string path) {
            PatchProfilePath(ref path);
            return true;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(XmlDocument), nameof(XmlDocument.Save), typeof(string))]
        private static bool BeforeSave(ref string filename) {
            PatchProfilePath(ref filename);
            return true;
        }
    }
}