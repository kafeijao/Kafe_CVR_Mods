namespace CCK.Debugger.Events; 

internal static class Player {
    
    public static readonly Dictionary<string, string> PlayersUsernamesCache = new();
    
    public static void OnPlayerLoaded(string guid, string username) {
        PlayersUsernamesCache[guid] = username;
    }
}
