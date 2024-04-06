using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using MelonLoader.ICSharpCode.SharpZipLib.Zip;
using MelonLoader.Utils;
using SK.Libretro.Unity;
using UnityEngine;

namespace Kafe.RetroCVR;

public static class ModConfig {

    // Melon Prefs
    // private static MelonPreferences_Category _melonCategory;
    // internal static MelonPreferences_Entry<bool> MeRejoinLastInstanceOnGameRestart;

    private const string AssetBundleName = "retrocvr.assetbundle";
    internal static GameObject LibretroUserInputPrefab;
    private const string LibretroUserInputPrefabPath = "Assets/RetroCVR/pfLibretroUserInput.prefab";

    public static readonly string MainFolderPath = Path.Combine(MelonEnvironment.UserDataDirectory, nameof(RetroCVR));
    public static readonly string LibRetroFolderPath = Path.Combine(MainFolderPath, "libretro");

    public static readonly string NativeLibsPath = Path.Combine(MainFolderPath, "NativeLibs");
    public const string NativeLibsHashFile = "NativeLibsHash";

    public static void InitializeMelonPrefs() {
        //
        // // Melon Config
        // _melonCategory = MelonPreferences.CreateCategory(nameof(Instances));
        //
        // // Use a diff file path so we can have per-user melon prefs. Requires to be ran after MetaPort.Instance.ownerId was set
        // _melonCategory.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, $"MelonPreferences-{MetaPort.Instance.ownerId}.cfg"));
        //
        // MeRejoinLastInstanceOnGameRestart = _melonCategory.CreateEntry("RejoinLastInstanceOnRestart", true,
        //     description: "Whether to join the last instance (if still available) when restarting the game or not.");
    }

    internal static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
    }

    internal static void InitializeFolders() {
        var libRetroFolder = new DirectoryInfo(LibRetroFolderPath);
        libRetroFolder.Create();
        LibretroInstance.SetMainDirectory(LibRetroFolderPath);
    }

    public static string GetRomFolder(string coreName) {
        var coreRomFolderPath = Path.Combine(MainFolderPath, "Roms", coreName);
        var coreRomFolder = new DirectoryInfo(coreRomFolderPath);
        coreRomFolder.Create();
        return coreRomFolderPath;
    }

    public static void LoadAssemblyResources(Assembly assembly) {

        try {
            using var assetBundleResourceStream = assembly.GetManifestResourceStream(AssetBundleName);
            using var memoryStream = new MemoryStream();
            if (assetBundleResourceStream == null) {
                MelonLogger.Error($"Failed to load {AssetBundleName}!");
                return;
            }
            assetBundleResourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load pfLibretroUserInput.prefab
            LibretroUserInputPrefab = assetBundle.LoadAsset<GameObject>(LibretroUserInputPrefabPath);
            LibretroUserInputPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        catch (Exception ex) {
            MelonLogger.Error($"Failed to Load resources from the {AssetBundleName} asset bundle");
            MelonLogger.Error(ex);
        }

        // Load Native Libraries
        var updateNeeded = true;
        using var resourceStream = assembly.GetManifestResourceStream("NativeLibraries.zip");
        if (resourceStream == null) {
            MelonLogger.Error("Unable to load the Native Library Resources!");
            return;
        }

        using var tempStream = new MemoryStream((int)resourceStream.Length);
        resourceStream.CopyTo(tempStream);
        var resourceHash = Utils.CreateMD5(tempStream.ToArray());

        var nativeLibsFolder = new DirectoryInfo(NativeLibsPath);
        if (nativeLibsFolder.Exists) {
            var file = nativeLibsFolder.GetFiles().FirstOrDefault(x => x.Name.Equals(NativeLibsHashFile));
            if (file != null) {
                var fileHash = File.ReadAllText(file.FullName);
                updateNeeded = !resourceHash.Equals(fileHash, StringComparison.InvariantCultureIgnoreCase);
            }
        }
        else {
            nativeLibsFolder.Create();
        }

        if (updateNeeded && resourceHash != null) {
            MelonLogger.Msg("Native Libraries need to be updated, extracting...");
            var fastZip = new FastZip();
            fastZip.ExtractZip(resourceStream, nativeLibsFolder.FullName, FastZip.Overwrite.Always, null, "", "", true, true);
            File.WriteAllText(Path.Combine(nativeLibsFolder.FullName, NativeLibsHashFile), resourceHash);
        }
        else {
            MelonLogger.Msg("Native Libraries are up to date!");
        }

        // Load every .dll file in the NativeLibsFolder
        foreach (var file in nativeLibsFolder.GetFiles("*.dll")) {
            try {
                NativeLibrary.Load(file.FullName);
            }
            catch (Exception ex) {
                MelonLogger.Error($"Failed to load {file.Name}");
                MelonLogger.Error(ex);
            }
        }
    }

}
