using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;

namespace Kafe.BetterCache;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<string> MeCacheDirectory;
    internal static MelonPreferences_Entry<int> MeMaxSizeGB;

    internal static readonly string DefaultDirectory = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Cache");

    internal static Action<long> TotalCacheUsedUpdated;
    internal static Action<bool> IsCleaningCache;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(BetterCache));

        MeCacheDirectory = _melonCategory.CreateEntry("CacheDirectory", DefaultDirectory,
            description: "Full path of the cache folder.");

        MeMaxSizeGB = _melonCategory.CreateEntry("MaxSizeGB", 20,
            description: "Maximum size in Gigabytes for the Cache Folder.");
    }


    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += LoadBTKUILib;
    }

    private static void LoadBTKUILib(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= LoadBTKUILib;

        var miscPage = BTKUILib.QuickMenuAPI.MiscTabPage;
        var miscCategory = miscPage.AddCategory(nameof(BetterCache));

        // Current cache info
        var currentCache = miscCategory.AddButton($"Currently Used {BetterCache.CacheSizeToReadable(CacheManager.LastCacheSize)}", "", "Displays the current used cache.");
        currentCache.Disabled = true;
        TotalCacheUsedUpdated += usedCache => currentCache.ButtonText = $"Currently Used {BetterCache.CacheSizeToReadable(usedCache)}";

        // Clear cache button
        var clearCache = miscCategory.AddButton("Clear Cache", "", "Clears Cache files until reaching 80% of the Max Cache.");
        clearCache.OnPress += CacheManager.StartManualCleaning;

        // Clear all cache button
        var clearAllCache = miscCategory.AddButton("Clear All Cache", "", "Clears ALL Cache files.");
        clearAllCache.OnPress += () => {
            CacheManager.CancelCacheCleaning();
            BetterCache.DeleteCacheFoldersContent(MeCacheDirectory.Value);
            CacheManager.LastCacheSize = 0;
        };

        // Handle cache cleaning updates
        IsCleaningCache += isCleaning => {
            clearCache.ButtonText = isCleaning ? "Busy Cleaning..." : "Clear Cache";
            clearCache.Disabled = isCleaning;
            clearAllCache.ButtonText = isCleaning ? "Busy Cleaning..." : "Clear All Cache";
            clearAllCache.Disabled = isCleaning;
        };

        // Max Cache button
        var maxCache = miscCategory.AddButton($"Max Cache Size ({MeMaxSizeGB.Value} GB)", "", "Change the max cache size (in GB).");
        maxCache.OnPress += () => {
            BTKUILib.QuickMenuAPI.OpenNumberInput("Max Cache Size (in GB)", MeMaxSizeGB.Value, newValue => {
                MeMaxSizeGB.Value = Math.Max((int) newValue, 1);
            });
        };
        MeMaxSizeGB.OnEntryValueChanged.Subscribe((_, newValue) => maxCache.ButtonText = $"Max Cache Size ({newValue} GB)");
    }

}