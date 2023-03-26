using System.Security.Cryptography;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.Camera;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSuperMario64 : MelonMod {

    // Asset bundle
    private static Material _marioMaterialCached;
    private static Sprite _marioSpriteCached;
    private const string LibSM64AssetBundleName = "libsm64.assetbundle";
    private const string MarioMaterialAssetPath = "Assets/Content/Material/DefaultMario.mat";
    private const string MarioTextureAssetPath = "Assets/Content/Texture/Mario_Head_256.png";

    // Rom
    private const string SuperMario64UsZ64RomHashHex = "20b854b239203baf6c961b850a4a51a2";
    private const string SuperMario64UsZ64RomName = "baserom.us.z64";
    internal static byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded = false;

    public override void OnInitializeMelon() {

        Config.InitializeMelonPrefs();

        // Extract the native binary to the plugins folder
        const string dllName = "sm64.dll";
        var dstPath = Path.GetFullPath(Path.Combine("ChilloutVR_Data", "Plugins", "x86_64", dllName));

        try {
            MelonLogger.Msg($"Copying the sm64.dll to {dstPath}");
            using var resourceStream = MelonAssembly.Assembly.GetManifestResourceStream(dllName);
            using var fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
            resourceStream!.CopyTo(fileStream);
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
            // Load Material
            var mat = assetBundle.LoadAsset<Material>(MarioMaterialAssetPath);
            mat.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            _marioMaterialCached = mat;
            // Load Sprite
            var sprite = assetBundle.LoadAsset<Sprite>(MarioTextureAssetPath);
            sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            _marioSpriteCached = sprite;
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
            return;
        }

        // Load the ROM
        try {
            var smRomPath = Path.GetFullPath(Path.Combine("UserData", SuperMario64UsZ64RomName));
            MelonLogger.Msg($"Loading the Super Mario 64 [US] z64 ROM from {smRomPath}...");
            var smRomFileInfo = new FileInfo(smRomPath);
            if (!smRomFileInfo.Exists) {
                MelonLogger.Error($"You need to download the Super Mario 64 [US] z64 ROM " +
                                  $"(MD5 {SuperMario64UsZ64RomHashHex}), rename the file to {SuperMario64UsZ64RomName} " +
                                  $"and save it to the path: {smRomPath}");
                return;
            }
            using var md5 = MD5.Create();
            using var smRomFileSteam = File.OpenRead(smRomPath);
            var smRomFileMd5Hash = md5.ComputeHash(smRomFileSteam);
            var smRomFileMd5HashHex = BitConverter.ToString(smRomFileMd5Hash).Replace("-", "").ToLowerInvariant();

            if (smRomFileMd5HashHex != SuperMario64UsZ64RomHashHex) {
                MelonLogger.Error($"The file at {smRomPath} MD5 hash is {smRomFileMd5HashHex}. That file needs to be a copy of " +
                                  $"Super Mario 64 [US] z64 ROM, which has a MD5 Hash of {SuperMario64UsZ64RomHashHex}");
                return;
            }

            SuperMario64UsZ64RomBytes = File.ReadAllBytes(smRomPath);
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the Super Mario 64 [US] z64 ROM: " + ex.Message);
            return;
        }

        // Check for BTKUILib
        if (RegisteredMelons.Any(m => m.Info.Name == "BTKUILib")) {
            MelonLogger.Msg($"Detected BTKUILib mod, we're adding the integration!");
            Config.InitializeBTKUI();
        }

        // Add our CCK component to the prop whitelist
        var propWhitelist = SharedFilter._spawnableWhitelist;
        propWhitelist.Add(typeof(CVRSM64Mario));
        propWhitelist.Add(typeof(CVRSM64Interactable));
        propWhitelist.Add(typeof(CVRSM64LevelModifier));
        propWhitelist.Add(typeof(CVRSM64ColliderStatic));
        propWhitelist.Add(typeof(CVRSM64ColliderDynamic));
        propWhitelist.Add(typeof(CVRSM64InteractableParticles));
        propWhitelist.Add(typeof(CVRSM64Teleporter));

        // Add our CCK component to the avatar whitelist
        var avatarWhitelist = SharedFilter._avatarWhitelist;
        avatarWhitelist.Add(typeof(CVRSM64ColliderDynamic));

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif

        FilesLoaded = true;
    }

    public static Material GetMarioMaterial() {
        return _marioMaterialCached;
    }

    public static Sprite GetMarioSprite() {
        return _marioSpriteCached;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.Start))]
        public static void After_CVRInputManager_Start(CVRInputManager __instance) {
            __instance.gameObject.AddComponent<MarioInputModule>();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortableCamera), nameof(PortableCamera.Start))]
        public static void After_PortableCamera_Start(PortableCamera __instance) {
            var mod = new MarioCameraMod();
            __instance.RegisterMod(mod);
            __instance.RequireUpdate(mod);
            __instance.UpdateOptionsDisplay();
        }
    }
}
