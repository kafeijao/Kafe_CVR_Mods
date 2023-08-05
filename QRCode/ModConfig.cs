using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Kafe.QRCode;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<int> MeQRScanIntervalSeconds;

    // Asset Bundle
    public static GameObject QRCodePrefab;
    private const string QRCodeAssetBundleName = "qrcode.assetbundle";
    private const string QRCodePrefabAssetPath = "Assets/QRCode/QRCodeRoot.prefab";
    private const string QRCodeImagesFolderAssetPath = "Assets/QRCode/Icons/";

    internal static readonly Dictionary<ImageType, Sprite> ImageSprites = new();

    internal enum ImageType {
        User,
        QRCode,
        Instance,
        Avatar,
        World,
        Prop,
        Url,
        Clipboard,
    }

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(QRCode));

        MeQRScanIntervalSeconds = _melonCategory.CreateEntry("QRScanIntervalSeconds", 1,
            description: "Number of seconds between scans for QR Codes. Min, Max Values: [1, 240]");
    }

    public static void LoadAssemblyResources(Assembly assembly) {

        try {
            using var resourceStream = assembly.GetManifestResourceStream(QRCodeAssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {QRCodeAssetBundleName}!");
                return;
            }

            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load Prefab
            QRCodePrefab = assetBundle.LoadAsset<GameObject>(QRCodePrefabAssetPath);
            QRCodePrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            
            // Load Sounds
            foreach (ImageType imgType in Enum.GetValues(typeof(ImageType))) {
                var imgSprite = assetBundle.LoadAsset<Sprite>($"{QRCodeImagesFolderAssetPath}{imgType}.png");
                imgSprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                ImageSprites[imgType] = imgSprite;
            }
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }
    }
}
