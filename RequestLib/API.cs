namespace Kafe.RequestLib;

public static class API {

    public enum RequestResult {
        Accepted,
        Declined,
        TimedOut,
    }

    internal static readonly List<string> RegisteredMods = new();
    internal static readonly Dictionary<string, string[]> RemotePlayerMods = new();

    public static Action<string, string[]> PlayerInfoUpdate;

    static API() {
        PlayerInfoUpdate += (playerGuid, registeredMods) => RemotePlayerMods[playerGuid] = registeredMods;
    }

    public static void RegisterMod() {
        var modName = RequestLib.GetModName();
        if (!RegisteredMods.Contains(modName)) {
            RegisteredMods.Add(modName);
        }
    }

    public static void UnRegisterMod() {
        var modName = RequestLib.GetModName();
        if (RegisteredMods.Contains(modName)) {
            RegisteredMods.Remove(modName);
        }
    }

    public static void SendRequest(string playerGuid, string message, Action<RequestResult> onResponse) {
        ModNetwork.SendRequest(RequestLib.GetModName(), playerGuid, message, onResponse);
    }

    public static bool HasRequestLib(string playerGuid) {
        return !ModNetwork.IsOfflineInstance() && RemotePlayerMods.ContainsKey(playerGuid);
    }

    public static bool HasMod(string playerGuid) {
        return HasRequestLib(playerGuid) && RemotePlayerMods[playerGuid].Contains(RequestLib.GetModName());
    }
}
