using ABI.CCK.Components;

namespace CCK.Debugger.Events;

internal static class DebuggerMenu {

    public static event Action MainNextPage;
    public static void OnMainNextPage()=> MainNextPage?.Invoke();

    public static event Action MainPreviousPage;
    public static void OnMainPrevious() => MainPreviousPage?.Invoke();

    public static event Action ControlsNextPage;
    public static void OnControlsNext() => ControlsNextPage?.Invoke();

    public static event Action ControlsPreviousPage;
    public static void OnControlsPrevious() => ControlsPreviousPage?.Invoke();

    public static event Action EntityChanged;

    public static void OnEntityChange() => EntityChanged?.Invoke();

    private static readonly HashSet<CVRAvatar> AvatarLoadedCache = new();
    public static bool IsAvatarLoaded(CVRAvatar avatar) => AvatarLoadedCache.Contains(avatar);
    public static event Action<CVRAvatar, bool> AvatarLoaded;
    public static void OnAvatarLoad(CVRAvatar avatar, bool isLoaded) {

        // Update cache for the loaded avatars
        if (isLoaded) AvatarLoadedCache.Add(avatar);
        else if (AvatarLoadedCache.Contains(avatar)) AvatarLoadedCache.Remove(avatar);

        AvatarLoaded?.Invoke(avatar, isLoaded);
    }


    private static readonly HashSet<CVRSpawnable> SpawnableLoadedCache = new();
    public static bool IsSpawnableLoaded(CVRSpawnable spawnable) => SpawnableLoadedCache.Contains(spawnable);
    public static event Action<CVRSpawnable, bool> SpawnableLoaded;
    public static void OnSpawnableLoad(CVRSpawnable spawnable, bool isLoaded) {

        // Update cache for the loaded spawnables
        if (isLoaded) SpawnableLoadedCache.Add(spawnable);
        else if (SpawnableLoadedCache.Contains(spawnable)) SpawnableLoadedCache.Remove(spawnable);

        SpawnableLoaded?.Invoke(spawnable, isLoaded);
    }
}
