using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Kafe.ChatBox;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<int> MeMessageTimeoutSeconds;

    // Asset Bundle
    public static GameObject ChatBoxPrefab;
    private const string ChatBoxAssetBundleName = "chatbox.assetbundle";
    private const string ChatBoxPrefabAssetPath = "Assets/Chatbox/ChatBox.prefab";

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(ChatBox));

        MeMessageTimeoutSeconds = _melonCategory.CreateEntry("MessageTimeoutSeconds", 20,
            description: "How long should a message stay on top of a player's head after written.");

    }

    public static void LoadAssetBundles(Assembly assembly) {

        try {

            MelonLogger.Msg($"Loading the asset bundle...");
            using var resourceStream = assembly.GetManifestResourceStream(ChatBoxAssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {ChatBoxAssetBundleName}!");
                return;
            }
            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load Chatbox Prefab
            ChatBoxPrefab = assetBundle.LoadAsset<GameObject>(ChatBoxPrefabAssetPath);
            ChatBoxPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;

        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }
    }

}
