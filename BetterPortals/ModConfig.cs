using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Kafe.BetterPortals;

public static class ModConfig {

    // Melon Prefs
    internal static MelonPreferences_Category MelonCategory;
    internal static MelonPreferences_Entry<bool> MePlacePortalsMidAir;
    internal static MelonPreferences_Entry<bool> MeJoinPortalWhenClose;
    internal static MelonPreferences_Entry<float> MeEnterPortalDistance;
    internal static MelonPreferences_Entry<bool> MeNeedInputToTriggerJoining;
    internal static MelonPreferences_Entry<bool> MeNotifyOnInvisiblePortalDrop;


    // Asset Resources
    public static GameObject TextPrefab;
    private const string AssetBundleName = "betterportals.assetbundle";
    private const string PrefabAssetPath = "Assets/BetterPortals/BetterPortals_Text.prefab";

    internal static string JavascriptPatchesContent;
    private const string JsPatches = "betterportals.cohtml.cvrtest.ui.patches.js";

    public static void InitializeMelonPrefs() {

        // Melon Config
        MelonCategory = MelonPreferences.CreateCategory(nameof(BetterPortals));

        MePlacePortalsMidAir = MelonCategory.CreateEntry("PlacePortalsMidAir", true,
            description: "Whether to be able to place portals mid air or not.");

        MeJoinPortalWhenClose = MelonCategory.CreateEntry("JoinPortalWhenClose", true,
            description: "Whether to join portals when getting close to them or not.");

        MeEnterPortalDistance = MelonCategory.CreateEntry("EnterPortalDistance", 1f,
            description: "Minimum distance between the player and the portal to trigger portal joining.");

        MeNeedInputToTriggerJoining = MelonCategory.CreateEntry("NeedInputToTriggerJoining", true,
            description: "Whether it requires special input to join the portal or not.");

        MeNotifyOnInvisiblePortalDrop = MelonCategory.CreateEntry("NotifyOnInvisiblePortalDrop", true,
            description: "Whether it notifies when a portal was dropped on top if you (making it invisible) or not.");
    }

    public static void LoadAssemblyResources(Assembly assembly) {

        try {
            using var resourceStream = assembly.GetManifestResourceStream(AssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {AssetBundleName}!");
                return;
            }
            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load Prefab
            TextPrefab = assetBundle.LoadAsset<GameObject>(PrefabAssetPath);
            TextPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load resources from the asset bundle");
            MelonLogger.Error(ex);
        }

        try {
            using var resourceStream = assembly.GetManifestResourceStream(JsPatches);
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {JsPatches}!");
                return;
            }

            using var streamReader = new StreamReader(resourceStream);
            JavascriptPatchesContent = streamReader.ReadToEnd();
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to load the assembly resource");
            MelonLogger.Error(ex);
        }
    }
}
