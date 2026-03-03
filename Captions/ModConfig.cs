#nullable disable

using System.Reflection;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using ABI_RC.Systems.UI.UILib.UIObjects.Objects;
using MelonLoader;

namespace Kafe.Captions;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    private static MelonPreferences_Entry<bool> _skipExtractingNativeBinaries;

    private static MelonPreferences_Entry<bool> _mlPrefEnabled;
    private static ToggleButton _uiLibEnabled;

    private static MelonPreferences_Entry<string> _mlPrefSelectedModel;
    private static Button _uiLibSelectModelButton;


    private static MelonPreferences_Entry<int> _threadsCount;

    private static MelonPreferences_Entry<bool> _verbose;
    private static MelonPreferences_Entry<bool> _verboseWhisperNet;

    private static readonly string ModelsFolderPath = Path.Combine("UserData", nameof(Captions), "Models");

    private static readonly string NativeBinariesFolderPath = Path.Combine("UserData", nameof(Captions), "NativeBinaries");

    private static string[] GetModelNames() => Directory.GetFiles(ModelsFolderPath).Select(Path.GetFileName).ToArray();

    public static string SelectedModelFileName { get; private set; }

    public static string GetSelectedModelPath()
    {
        if (string.IsNullOrEmpty(SelectedModelFileName))
            throw new Exception($"{nameof(SelectedModelFileName)} is empty :(");
        return Path.GetFullPath(Path.Combine(ModelsFolderPath, SelectedModelFileName));
    }

    public static string NativeBinariesFolderFullPath => $"{Path.GetFullPath(NativeBinariesFolderPath)}{Path.DirectorySeparatorChar}";

    public static bool UseGpu => true;

    public static int ThreadsCount => _threadsCount.Value;

    public static bool Verbose { get; private set; }

    public static bool VerboseWhisperNet { get; private set; }

    public static bool Enabled { get; private set; }

    public static bool ProcessLocalPlayer { get; private set; } = true;
    public static bool ProcessFriends { get; private set; } = true;
    public static bool ProcessOthers { get; private set; } = true;

    public static float StartThreshold { get; private set; } = 0.05f;
    public static float ContinueThreshold { get; private set; } = 0.01f;
    public static float SilenceStopSeconds { get; private set; } = 1.5f;

    public static void LoadMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(Captions));

        _mlPrefEnabled = _melonCategory.CreateEntry("Enabled", false,
            description: "Whether this mod is enabled or not");
        _mlPrefEnabled.OnEntryValueChanged.Subscribe((_, newValue) => OnEnabledChange(newValue, true));
        Enabled = _mlPrefEnabled.Value;

        _mlPrefSelectedModel = _melonCategory.CreateEntry("Model", string.Empty,
            description: $"Name of the model to use. They need to exist in the folder: {ModelsFolderPath}");
        _mlPrefSelectedModel.OnEntryValueChanged.Subscribe((_, newValue) => OnModelChange(newValue));
        SelectedModelFileName = _mlPrefSelectedModel.Value;

        _skipExtractingNativeBinaries = _melonCategory.CreateEntry("SkipNativeBinaryExtraction", false,
            description: "Whether to skip extracting the native binaries or not");

        _threadsCount = _melonCategory.CreateEntry("CpuThreadCount", Math.Max(Environment.ProcessorCount / 2, 1),
            description: "Number of cpu threads whisper.cpp will be given");
        _threadsCount.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            if (newValue > Environment.ProcessorCount || newValue < 1)
            {
                MelonLogger.Error($"Threads count must be between 1 and {Environment.ProcessorCount} (your machine thread count)");
                _threadsCount.Value = oldValue;
                return;
            }
            MelonLogger.Msg($"Changed threads setting. Reloading with thread count: {_threadsCount.Value}");
            Captions.ReloadConfig();
        });

        _verbose = _melonCategory.CreateEntry("Verbose", false,
            description: "Whether to print the logs from the mod");
        _verbose.OnEntryValueChanged.Subscribe((_, newValue) => Verbose = newValue);

        _verboseWhisperNet = _melonCategory.CreateEntry("VerboseWhisperNet", false,
            description: "Whether to print the logs from whisper.net library");
        _verboseWhisperNet.OnEntryValueChanged.Subscribe((_, newValue) => VerboseWhisperNet = newValue);
    }

    public static void LoadUILib(Assembly assembly)
    {
        const string logoName = $"{nameof(Captions)}-Logo";
        QuickMenuAPI.PrepareIcon(nameof(Captions), logoName, assembly.GetManifestResourceStream("resources.logo.png"));

        const string selectName = $"{nameof(Captions)}-Select";
        QuickMenuAPI.PrepareIcon(nameof(Captions), selectName, assembly.GetManifestResourceStream("resources.select.png"));

        const string downloadName = $"{nameof(Captions)}-Download";
        QuickMenuAPI.PrepareIcon(nameof(Captions), downloadName, assembly.GetManifestResourceStream("resources.download.png"));

        var page = new Page(nameof(Captions), nameof(Captions), true, logoName)
        {
            MenuTitle = nameof(Captions),
            MenuSubtitle = "Captions Settings",
        };
        var cat = page.AddCategory("");

        _uiLibEnabled = cat.AddToggle("Enable", "Whether to enable or disable the mod (it will unload the model if disabled)", Enabled);
        _uiLibEnabled.OnValueUpdated += newEnabledValue =>
        {
            if (newEnabledValue == Enabled) return;
            if (newEnabledValue && string.IsNullOrEmpty(SelectedModelFileName))
            {
                QuickMenuAPI.ShowNotice("No model selected",
                    "To enable this mod you first need to select a model from within the Caption button in the Quick Menu",
                    okText: "Sounds good");
                _uiLibEnabled.ToggleValue = false;
                return;
            }
            OnEnabledChange(newEnabledValue, true);
        };

        cat.AddToggle("Process Self", "Process our own audio", ProcessLocalPlayer)
            .OnValueUpdated += newValue => ProcessLocalPlayer = newValue;

        cat.AddToggle("Process Friends", "Process our friends audio", ProcessFriends)
            .OnValueUpdated += newValue => ProcessFriends = newValue;

        cat.AddToggle("Process Others", "Process remote non-friends players audio", ProcessOthers)
            .OnValueUpdated += newValue => ProcessOthers = newValue;

        cat.AddSlider("Amplitude Start Threshold", "Amplitude for starting talking", StartThreshold, 0.01f, 0.5f, 2, StartThreshold, true)
            .OnValueUpdated += newValue => StartThreshold = newValue;

        cat.AddSlider("Amplitude Continue Threshold", "Amplitude for talking in progress", ContinueThreshold, 0.01f, 0.5f, 2, ContinueThreshold, true)
            .OnValueUpdated += newValue => ContinueThreshold = newValue;

        cat.AddSlider("Silent Seconds", "Time in seconds for lack of talking to stop sentence", SilenceStopSeconds, 1f, 5f, 1, SilenceStopSeconds, true)
            .OnValueUpdated += newValue => SilenceStopSeconds = newValue;

        _uiLibSelectModelButton = cat.AddButton($"Selected Model {SelectedModelFileName}", selectName, $"Pick the model to use. Current: {SelectedModelFileName}", ButtonStyle.TextWithIcon);
        _uiLibSelectModelButton.OnPress += () =>
        {
            var availableModels = GetModelNames();
            if (availableModels.Length == 0)
            {
                QuickMenuAPI.ShowNotice(
                    "No Models Available",
                    "There are no available models in the models folder. You can use the download model button to fetch a model",
                    okText: "Aye Aye");
                return;
            }
            var currentModelIndex = availableModels.IndexOf(SelectedModelFileName);
            MultiSelection selection = new MultiSelection("Whisper Model", availableModels, currentModelIndex);
            selection.OnOptionUpdated += selectedOption =>
            {
                if (selectedOption == -1)
                {
                    OnModelChange(string.Empty);
                    return;
                }
                string newModel = availableModels[selectedOption];
                OnModelChange(newModel);
            };
            QuickMenuAPI.OpenMultiSelect(selection);
        };

        cat.AddSpacer();
        cat.AddSpacer();

        var downloadModelButton = cat.AddButton("Download Models", downloadName, $"Chose a model to download from {WhisperModelDownloader.BaseUrl}");
        downloadModelButton.OnPress += () =>
        {
            MultiSelection selection = new MultiSelection("Whisper Model to Download", WhisperModelDownloader.ModelOptions, -1);
            selection.OnOptionUpdated += selectedOption =>
            {
                if (selectedOption == -1) return;
                string modelKey = WhisperModelDownloader.ModelOptions[selectedOption];
                var modelInfo = WhisperModelDownloader.GetModelInfo(modelKey);
                var downloadUrl = WhisperModelDownloader.GetModelDownloadUrl(modelInfo.FileName);
                // Add space in the url, so the url is more readable
                downloadUrl = downloadUrl.Replace(WhisperModelDownloader.BaseUrl, WhisperModelDownloader.BaseUrl + ' ');
                QuickMenuAPI.ShowConfirm(
                    "Download whisper model",
                    $"This will download the model {modelKey} onto your disk into the {ModelsFolderPath} folder. " +
                    $"Download Url: {downloadUrl}",
                    onYes: () => Task.Run(async () => await WhisperModelDownloader.DownloadModelAsync(modelKey, ModelsFolderPath)),
                    yesText: "Sure",
                    noText: "Nah man");
            };
            QuickMenuAPI.OpenMultiSelect(selection);
        };
    }

    public static void DisableMod()
    {
        OnEnabledChange(false, true);
    }

    private static void OnEnabledChange(bool newEnabled, bool reloadConfig)
    {
        if (Enabled == newEnabled) return;
        Enabled = newEnabled;

        // Update the values on the UI Buttons
        if (_mlPrefEnabled != null) _mlPrefEnabled.Value = newEnabled;
        if (_uiLibEnabled != null) _uiLibEnabled.ToggleValue = newEnabled;

        MelonLogger.Msg($"Changed the Enabled setting to Enabled: {Enabled}");

        if (reloadConfig)
            Captions.ReloadConfig();
    }

    public static void ResetModelSelection()
    {
        OnModelChange(string.Empty);
    }

    private static void OnModelChange(string newModelFileName)
    {
        if (SelectedModelFileName == newModelFileName) return;

        if (!string.IsNullOrEmpty(newModelFileName))
        {
            string newModelPath = Path.GetFullPath(Path.Combine(ModelsFolderPath, newModelFileName));
            if (!File.Exists(newModelPath))
            {
                MelonLogger.Warning($"Attempted to load a non-existing model at {newModelPath}, resetting to no model");
                newModelFileName = string.Empty;
            }
        }
        SelectedModelFileName = newModelFileName;

        // Update the values on the UI Buttons
        if (_mlPrefSelectedModel != null) _mlPrefSelectedModel.Value = newModelFileName;
        if (_uiLibSelectModelButton != null)
        {
            _uiLibSelectModelButton.ButtonText = $"Selected Model {newModelFileName}";
            _uiLibSelectModelButton.ButtonTooltip = $"Pick the model to use. Current: {newModelFileName}";
        }

        MelonLogger.Msg($"Changed the selected Model to: {SelectedModelFileName}");

        // Update the Enabled setting without reloading, since we're going to reload already
        if (string.IsNullOrEmpty(SelectedModelFileName))
            OnEnabledChange(false, false);

        Captions.ReloadConfig();
    }

    #region whiper.net

    #if WHISPER_NET

    private static readonly HashSet<string> NativeDllNames = new HashSet<string>
    {
        "ggml-base-whisper.dll",
        "ggml-cpu-whisper.dll",
        "ggml-vulkan-whisper.dll",
        "ggml-whisper.dll",
        "whisper.dll",
    };

    private static string GetNativeDllFullPath(string nativeDllName)
    {
        return Path.Combine(NativeBinariesFolderPath, "runtimes", "vulkan", "win-x64", nativeDllName);
    }

    public static void ExtractWhisperNetNativeBinaries(Assembly assembly)
    {
        // Extract native libs
        foreach (string nativeDllName in NativeDllNames)
        {
            string dstPath = GetNativeDllFullPath(nativeDllName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

                if (_skipExtractingNativeBinaries.Value)
                {
                    MelonLogger.Warning($"Skipped extracting native binary (melon prefs setting) to: {dstPath}");
                    continue;
                }

                MelonLogger.Msg($"Extracting the {nativeDllName} to {dstPath}");
                using Stream resourceStream = assembly.GetManifestResourceStream(nativeDllName);
                using FileStream fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
                resourceStream!.CopyTo(fileStream);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to extract {nativeDllName} native library into {dstPath}", ex);
                return;
            }
        }
    }

    #endif

    #endregion whiper.net

    #region whiper.unity

    #if WHISPER_UNITY

    private static readonly HashSet<string> NativeDllNames = new HashSet<string>
    {
        "ggml-base.dll",
        "ggml-cpu.dll",
        "ggml-vulkan.dll",
        "ggml.dll",
        "libwhisper.dll",
    };

    private static string GetNativeDllFullPath(string nativeDllName)
    {
        return Path.Combine(NativeBinariesFolderPath, nativeDllName);
    }

    public static void LoadWhisperUnityNativeBinaries(Assembly assembly)
    {
        // Extract native libs
        foreach (string nativeDllName in NativeDllNames)
        {
            string dstPath = GetNativeDllFullPath(nativeDllName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

                if (_skipExtractingNativeBinaries.Value)
                {
                    MelonLogger.Warning($"Skipped extracting native binary (melon prefs setting) to: {dstPath}");
                    continue;
                }

                MelonLogger.Msg($"Extracting the {nativeDllName} to {dstPath}");
                using Stream resourceStream = assembly.GetManifestResourceStream(nativeDllName);
                using FileStream fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
                resourceStream!.CopyTo(fileStream);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to extract {nativeDllName} native library into {dstPath}", ex);
                return;
            }
        }

        // Load native libs
        foreach (string nativeDllName in NativeDllNames)
        {
            string dstPath = GetNativeDllFullPath(nativeDllName);
            try
            {
                MelonLogger.Msg($"Loading the native library from {dstPath}");
                NativeLibrary.Load(dstPath);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load {dstPath} native library", ex);
                return;
            }
        }
    }

    #endif

    #endregion whiper.unity
}
