using System.Security.Cryptography;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class CVRSuperMario64 : MelonMod {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeDisableAudio;
    internal static MelonPreferences_Entry<float> MeAudioPitch;
    internal static MelonPreferences_Entry<float> MeAudioVolume;
    internal static MelonPreferences_Entry<int> MeGameTickMs;
    internal static MelonPreferences_Entry<int> MeIgnoreCollidersHigherThanPolygons;
    internal static MelonPreferences_Entry<bool> MePlayRandomMusicOnMarioJoin;
    internal static MelonPreferences_Entry<float> MeSkipFarMarioDistance;
    internal static MelonPreferences_Entry<int> MeMaxMariosAnimatedPerPerson;

    // Asset bundle
    private static Material _marioMaterialCached;
    private const string LibSM64AssetBundleName = "libsm64.assetbundle";
    private const string MarioMaterialAssetPath = "Assets/Content/Material/DefaultMario.mat";

    // Rom
    private const string SuperMario64UsZ64RomHashHex = "20b854b239203baf6c961b850a4a51a2";
    private const string SuperMario64UsZ64RomName = "baserom.us.z64";
    internal static byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded = false;

    public override void OnInitializeMelon() {


        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(CVRSuperMario64));

        MeDisableAudio = _melonCategory.CreateEntry("DisableAudio", false,
            description: "Whether to disable the game audio or not.");

        MeAudioVolume = _melonCategory.CreateEntry("AudioVolume", 0.1f,
            description: "The audio volume.");

        MeAudioPitch = _melonCategory.CreateEntry("AudioPitch", 0.74f,
            description: "The audio pitch of the game sounds.");

        MeGameTickMs = _melonCategory.CreateEntry("GameTickMs", 25,
            description: "The game ticks frequency in Milliseconds.");

        MeIgnoreCollidersHigherThanPolygons = _melonCategory.CreateEntry("IgnoreCollidersHigherThanPolygons", 10000,
            description: "Ignore colliders with a poly count higher than.");

        MePlayRandomMusicOnMarioJoin = _melonCategory.CreateEntry("PlayRandomMusicOnMarioJoin", true,
            description: "Whether to play a random music when a mario joins or not.");
        MePlayRandomMusicOnMarioJoin.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (!newValue) Interop.StopMusic();
        });

        MeMaxMariosAnimatedPerPerson = _melonCategory.CreateEntry("MaxMariosAnimatedPerPerson", 3,
            description: "The max number of marios other people can control at the same time.");

        MeSkipFarMarioDistance = _melonCategory.CreateEntry("SkipFarMarioDistance", 15f,
            description: "The max distance that we're going to calculate the mario animations for other people.");

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
            var mat = assetBundle.LoadAsset<Material>(MarioMaterialAssetPath);
            mat.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            _marioMaterialCached = mat;
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

        // Add our CCK component to the whitelist
        var propWhitelist = Traverse.Create(typeof(SharedFilter)).Field<HashSet<Type>>("_spawnableWhitelist").Value;
        propWhitelist.Add(typeof(CVRSM64Mario));
        propWhitelist.Add(typeof(CVRSM64Interactable));
        propWhitelist.Add(typeof(CVRSM64LevelModifier));
        propWhitelist.Add(typeof(CVRSM64ColliderStatic));
        propWhitelist.Add(typeof(CVRSM64ColliderDynamic));
        propWhitelist.Add(typeof(CVRSM64InteractableParticles));
        var avatarWhitelist = Traverse.Create(typeof(SharedFilter)).Field<HashSet<Type>>("_avatarWhitelist").Value;
        avatarWhitelist.Add(typeof(CVRSM64ColliderDynamic));

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
