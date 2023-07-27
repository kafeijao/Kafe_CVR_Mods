using ABI_RC.Core.Networking;
using MelonLoader;
using UnityEngine;

namespace Kafe.LoginProfiles;

public class LoginProfiles : MelonMod {

    private const string AutoLoginFilePath = "/autologin.profile";

    public override void OnEarlyInitializeMelon() {
        AuthManager.filePath = Application.dataPath + GetProfilePath();
    }

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
}
