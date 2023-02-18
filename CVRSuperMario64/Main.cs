using System.Security.Cryptography;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using HarmonyLib;
using LibSM64;
using MelonLoader;
using UnityEngine;

namespace CVRSuperMario64;

public class CVRSuperMario64 : MelonMod {

    // Asset bundle
    private static Material _marioMaterialCached;
    private const string LibSM64AssetBundleName = "libsm64.assetbundle";
    private const string MarioMaterialAssetPath = "Assets/Content/Material/DefaultMario.mat";

    // Rom
    private const string SuperMario64UsZ64RomHashHex = "20b854b239203baf6c961b850a4a51a2";
    private const string SuperMario64UsZ64RomName = "baserom.us.z64";
    internal static Byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded = false;

    public override void OnInitializeMelon() {

        // Limit max polygons on the meshes
        // Configure framework

        // Add our CCK component to the whitelist
        var whitelist = Traverse.Create(typeof(SharedFilter)).Field<HashSet<Type>>("_spawnableWhitelist").Value;
        whitelist.Add(typeof(CVRSM64InputSpawnable));
        whitelist.Add(typeof(CVRSM64DynamicCollider));
        whitelist.Add(typeof(CVRSM64StaticCollider));
        whitelist.Add(typeof(CVRSM64CMario));

        // Extract the native binary to the plugins folder
        const string dllName = "sm64.dll";
        const string dstPath = "ChilloutVR_Data/Plugins/x86_64/" + dllName;

        try {
            var sm64LibFileInfo = new FileInfo(dstPath);
            if (!sm64LibFileInfo.Exists) {
                MelonLogger.Msg($"Copying the sm64.dll to {dstPath}");
                using var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(dllName);
                using var fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
                resourceStream!.CopyTo(fileStream);
            }
            else {
                MelonLogger.Msg($"The lib sm64.dll already exists at {dstPath}, we won't overwrite...");
            }
        }
        catch (IOException ex) {
            MelonLogger.Error("Failed to copy native library: " + ex.Message);
            return;
        }

        // Import asset bundle
        try {
            MelonLogger.Msg($"Loading the asset bundle...");
            using var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(LibSM64AssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {LibSM64AssetBundleName}!");
                return;
            }
            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());
            var mat = assetBundle.LoadAsset<Material>(MarioMaterialAssetPath);
            mat.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            _marioMaterialCached = mat;
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }

        // Load the ROM
        try {
            var abiAppDataPath = Application.persistentDataPath;
            var smRomPath = Path.Combine(abiAppDataPath, nameof(LibSM64), SuperMario64UsZ64RomName);
            MelonLogger.Msg($"Loading the Super Mario 64 [US] z64 ROM from ${smRomPath}...");
            var smRomFileInfo = new FileInfo(smRomPath);
            if (!smRomFileInfo.Exists) {
                // Create directory if doesn't exist
                smRomFileInfo.Directory?.Create();
                MelonLogger.Error($"You need to download the Super Mario 64 [US] z64 ROM " +
                                  $"(MD5 20b854b239203baf6c961b850a4a51a2) rename the file to {SuperMario64UsZ64RomName} and" +
                                  $"save it on {smRomPath}");
            }
            using var md5 = MD5.Create();
            using var smRomFileSteam = File.OpenRead(smRomPath);
            var smRomFileMd5Hash = md5.ComputeHash(smRomFileSteam);
            var smRomFileMd5HashHex = BitConverter.ToString(smRomFileMd5Hash).Replace("-", "").ToLowerInvariant();

            if (smRomFileMd5HashHex != SuperMario64UsZ64RomHashHex) {
                MelonLogger.Error($"The file at {smRomPath} MD5 hash is {smRomFileMd5HashHex}. That file needs to be a copy of " +
                                  $"Super Mario 64 [US] z64 ROM, which has a MD5 Hash of 20b854b239203baf6c961b850a4a51a2");
                return;
            }

            SuperMario64UsZ64RomBytes = File.ReadAllBytes(smRomPath);
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif

        FilesLoaded = true;
    }

    public static Material GetMarioMaterial() {
        return _marioMaterialCached;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRInputManager), "Start")]
        public static void After_CVRInputManager_Start(CVRInputManager __instance) {
            __instance.gameObject.AddComponent<MarioInputModule>();
        }
    }
}
