using System.Diagnostics;
using MelonLoader;

namespace Kafe.RequestLib;

public static class API {

    public enum RequestResult {
        Accepted,
        Declined,
        TimedOut,
    }

    internal static readonly List<string> RegisteredMods = new();

    public static void RegisterMod() {
        var modName = GetModName();
        if (!RegisteredMods.Contains(modName)) {
            RegisteredMods.Add(modName);
        }
    }

    public static void UnRegisterMod() {
        var modName = GetModName();
        if (RegisteredMods.Contains(modName)) {
            RegisteredMods.Remove(modName);
        }
    }

    public static void SendRequestAll(string message, Action<RequestResult> onResponse) {
        ModNetwork.SendRequest(GetModName(), message, onResponse);
    }

    private static string GetModName() {
        try {
            var callingFrame = new StackTrace().GetFrame(2);
            var callingAssembly = callingFrame.GetMethod().Module.Assembly;
            var callingMelonAttr = callingAssembly.CustomAttributes.FirstOrDefault(
                    attr => attr.AttributeType == typeof(MelonInfoAttribute));
            return (string) callingMelonAttr!.ConstructorArguments[1].Value;
        }
        catch (Exception ex) {
            MelonLogger.Error("[GetModName] Attempted to get a mod's name...");
            MelonLogger.Error(ex);
        }
        return null;
    }
}
