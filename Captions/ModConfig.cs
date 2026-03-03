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

    private static MelonPreferences_Entry<string> _modelToUse;
    // private static MelonPreferences_Entry<bool> _useGpu;
    private static MelonPreferences_Entry<int> _threadsCount;

    private static MelonPreferences_Entry<bool> _verbose;
    private static MelonPreferences_Entry<bool> _verboseWhisperNet;

    private const string EmbedModel = "ggml-tiny.bin";

    private static readonly string ModelsFolderPath = Path.Combine("UserData", nameof(Captions), "Models");

    public static readonly string NativeBinariesFolderPath = Path.Combine("UserData", nameof(Captions), "NativeBinaries");

    public static string[] GetModelNames() => Directory.GetFiles(ModelsFolderPath).Select(Path.GetFileName).ToArray();

    public static string SelectedModelPath => Path.GetFullPath(Path.Combine(ModelsFolderPath, _modelToUse.Value));

    public static string NativeBinariesFolderFullPath => $"{Path.GetFullPath(NativeBinariesFolderPath)}{Path.DirectorySeparatorChar}";

    public static string ModelFileName
    {
        get => _modelToUse.Value;
        set => _modelToUse.Value = value;
    }

    public static bool UseGpu => true;
    public static int ThreadsCount => _threadsCount.Value;

    public static bool Verbose { get; private set; }

    public static bool VerboseWhisperNet { get; private set; }

    public static bool Enabled { get; private set; } = true;

    public static bool ProcessLocalPlayer { get; private set; } = true;
    public static bool ProcessFriends { get; private set; } = true;
    public static bool ProcessOthers { get; private set; } = true;

    public static float StartThreshold { get; private set; } = 0.05f;
    public static float ContinueThreshold { get; private set; } = 0.01f;
    public static float SilenceStopSeconds { get; private set; } = 1.5f;

    public static void LoadMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(nameof(Captions));

        _modelToUse = _melonCategory.CreateEntry("Model", EmbedModel,
            description: $"Name of the model to use. They need to exist in the folder: {Path.GetFullPath(ModelsFolderPath)}");
        _modelToUse.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            string newModelPath = Path.GetFullPath(Path.Combine(ModelsFolderPath, newValue));
            if (!File.Exists(newModelPath))
            {
                MelonLogger.Warning($"Attempted to load a non-existing model at {newModelPath}, resetting to the default: {EmbedModel}");
                _modelToUse.Value = EmbedModel;
                return;
            }
            MelonLogger.Msg($"Changed mode. Reloading with the model: {_modelToUse.Value}");
            Captions.ReloadModel?.Invoke();
        });
        MelonLogger.Msg($"Using the model {_modelToUse.Value}");

        // _useGpu = _melonCategory.CreateEntry("UseGpu", true,
        //     description: "Whether to use GPU or not. This will use Vulkan");
        // _useGpu.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        // {
        //     if (oldValue == newValue) return;
        //     MelonLogger.Msg($"Changed useGpu setting. Reloading with useGpu: {_useGpu.Value}");
        //     Captions.ReloadModel?.Invoke();
        // });

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
            MelonLogger.Msg($"Changed threads setting. Reloading with threads: {_threadsCount.Value}");
            Captions.ReloadModel?.Invoke();
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
        var page = new Page(nameof(Captions), nameof(Captions), true, logoName)
        {
            MenuTitle = nameof(Captions),
            MenuSubtitle = "Captions Settings",
        };
        var cat = page.AddCategory("");

        cat.AddToggle("Enable", "Whether to enable or disable the mod (it will unload the model if disabled)", Enabled)
            .OnValueUpdated += newValue =>
        {
            Enabled = newValue;
            if (newValue) Captions.InitWhisper();
            else Captions.DeInitWhisper();
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

        var modelButton = cat.AddButton($"Selected Model {ModelFileName}", "", "Pick the model to use", ButtonStyle.TextOnly);
        modelButton.OnPress += () =>
        {
            var availableModels = GetModelNames();
            if (availableModels.Length == 0)
            {
                QuickMenuAPI.ShowNotice(
                    "No Models Available",
                    "There are no available models in the models folder\nCheck the mod's readme for instructions",
                    okText: "Ok, I will");
                return;
            }
            var currentModelIndex = availableModels.IndexOf(ModelFileName);
            MultiSelection selection = new MultiSelection("Whisper Model", availableModels, currentModelIndex);
            selection.OnOptionUpdated += selectedOption =>
            {
                if (selectedOption == -1) return;
                string newModel = availableModels[selectedOption];
                ModelFileName = newModel;
                modelButton.ButtonText = $"Selected Model {newModel}";
            };
            QuickMenuAPI.OpenMultiSelect(selection);
        };

        var downloadModelButton = cat.AddButton("Download Models", "", $"Chose a model to download from {WhisperModelDownloader.BaseUrl}", ButtonStyle.TextOnly);
        downloadModelButton.OnPress += () =>
        {
            MultiSelection selection = new MultiSelection("Whisper Model to Download", WhisperModelDownloader.ModelOptions, -1);
            selection.OnOptionUpdated += selectedOption =>
            {
                if (selectedOption == -1) return;
                string selectedFileName = WhisperModelDownloader.ModelOptions[selectedOption];
                Task.Run(async () => await WhisperModelDownloader.DownloadModelAsync(selectedFileName, ModelsFolderPath));
            };
            QuickMenuAPI.OpenMultiSelect(selection);
        };
    }

    public static void LoadEmbedResources(Assembly assembly)
    {
        // Copy the embedded model
        string dstEmbedModelPath = Path.GetFullPath(Path.Combine(ModelsFolderPath, EmbedModel));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dstEmbedModelPath)!);

            if (!File.Exists(dstEmbedModelPath))
            {
                if (assembly.GetManifestResourceNames().Contains(EmbedModel))
                {
                    MelonLogger.Msg($"Extracting the {EmbedModel} model to {dstEmbedModelPath}");
                    using Stream resourceStream = assembly.GetManifestResourceStream(EmbedModel);
                    using FileStream fileStream = File.Open(dstEmbedModelPath, FileMode.Create, FileAccess.Write);
                    resourceStream!.CopyTo(fileStream);
                }
                else
                {
                    MelonLogger.Msg($"This mod doesn't include an embedded {EmbedModel} model, you need to grab your own...");
                }
            }
            else
            {
                MelonLogger.Msg($"{EmbedModel} already exists in {dstEmbedModelPath}, skip extracting it...");
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Failed to copy {EmbedModel} native library into {dstEmbedModelPath}", ex);
        }
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
