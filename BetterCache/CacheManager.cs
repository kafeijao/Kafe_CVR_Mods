using MelonLoader;

namespace Kafe.BetterCache;

public static class CacheManager {

    private const float CleaningThreshold = 0.8f;

    private static CancellationTokenSource _cleaningTaskCancellationToken;

    private static readonly object CacheSizeLock = new();
    private static readonly object CleaningProgressLock = new();

    private static long _lastCacheSize = -1;
    private static bool _isCleaningInProgress = false;

    public static long LastCacheSize {
        get {
            lock (CacheSizeLock) {
                return _lastCacheSize;
            }
        }
        set {
            lock (CacheSizeLock) {
                _lastCacheSize = value;
            }
            ModConfig.TotalCacheUsedUpdated?.Invoke(value);
        }
    }

    internal static void Initialize() {

        BetterCache.OnDownloadsFinish += ScheduleCacheCleaning;
        BetterCache.OnDownloadsStart += CancelCacheCleaning;

        // Cleanup after a Max Size Change
        ModConfig.MeMaxSizeGB.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            StartManualCleaning();
        });

        // Do a startup cleaning to grab the current Cache Size
        StartManualCleaning();
    }

    public static void StartManualCleaning() {
        if (IsCleaningAllowed()) {
            Task.Run(CleanCache);
        }
        else {
            MelonLogger.Msg("Cache cleaning is already in progress...");
        }
    }

    private static bool IsCleaningAllowed() {
        lock (CleaningProgressLock) {
            if (_isCleaningInProgress) {
                return false;
            }

            _isCleaningInProgress = true;
            ModConfig.IsCleaningCache?.Invoke(true);
            return true;
        }
    }

    private static void ScheduleCacheCleaning() {

        _cleaningTaskCancellationToken?.Cancel();
        _cleaningTaskCancellationToken = new CancellationTokenSource();

        Task.Delay(TimeSpan.FromSeconds(30), _cleaningTaskCancellationToken.Token).ContinueWith(t => {

            if (t.IsCanceled) return;

            if (!IsCleaningAllowed()) {
                MelonLogger.Msg("Cache cleaning is already in progress, skipping scheduling....");
                return;
            }

            Task.Run(CleanCache);

        }, _cleaningTaskCancellationToken.Token);
    }

    internal static void CancelCacheCleaning() {
        _cleaningTaskCancellationToken?.Cancel();
    }

    private static void CleanCache() {

        CancelCacheCleaning();

        var directories = new[] {
            Path.Combine(ModConfig.MeCacheDirectory.Value, "Avatars"),
            Path.Combine(ModConfig.MeCacheDirectory.Value, "Worlds"),
            Path.Combine(ModConfig.MeCacheDirectory.Value, "Spawnables")
        }.Where(Directory.Exists).ToArray();

        var maxSizeBytes = ModConfig.MeMaxSizeGB.Value * 1024L * 1024L * 1024L;
        var targetSizeBytes = (long)(CleaningThreshold * maxSizeBytes);

        var currentSize = CalculateTotalSize(directories);

        #if DEBUG
        MelonLogger.Msg($"Starting cache cleaning check. Current cache size: {BetterCache.CacheSizeToReadable(currentSize)}");
        #endif

        if (currentSize <= maxSizeBytes) {
            #if DEBUG
            MelonLogger.Msg($"Cache size is below the Max Limit of {ModConfig.MeMaxSizeGB.Value} GB, skipping it...");
            #endif
            ResetCleaningStatus(currentSize);
            return;
        }

        var allFiles = directories.SelectMany(Directory.GetFiles)
            .Where(filePath => BetterCache.FileExtensions.Contains(Path.GetExtension(filePath)))
            .Select(filePath => new FileInfo(filePath))
            .ToList();

        var initialFileCount = allFiles.Count;
        long deletedBytes = 0;
        var deletedFilesCount = 0;

        while (currentSize > targetSizeBytes && allFiles.Count > 0) {
            var fileToDelete = allFiles.OrderBy(file => file.LastAccessTimeUtc).FirstOrDefault();

            if (fileToDelete == null) continue;

            currentSize -= fileToDelete.Length;
            deletedBytes += fileToDelete.Length;
            deletedFilesCount++;

            try {
                fileToDelete.Delete();
                allFiles.Remove(fileToDelete);
            }
            catch (Exception ex) {
                MelonLogger.Warning($"Failed to delete file: {fileToDelete.FullName} due to {ex.Message}. Skipping current cleanup...");
                break;
            }
        }

        MelonLogger.Msg($"Cache cleaning complete. Deleted {deletedFilesCount} out of {initialFileCount} files, freeing up {BetterCache.CacheSizeToReadable(deletedBytes)}");

        ResetCleaningStatus(currentSize);
    }

    private static void ResetCleaningStatus(long newSize) {

        LastCacheSize = newSize;

        lock (CleaningProgressLock) {
            _isCleaningInProgress = false;
            ModConfig.IsCleaningCache?.Invoke(false);
        }
    }

    private static long CalculateTotalSize(string[] directories) {
        return directories.Sum(dir => Directory.GetFiles(dir)
            .Where(filePath => BetterCache.FileExtensions.Contains(Path.GetExtension(filePath)))
            .Select(filePath => new FileInfo(filePath))
            .Sum(fileInfo => fileInfo.Length));
    }
}