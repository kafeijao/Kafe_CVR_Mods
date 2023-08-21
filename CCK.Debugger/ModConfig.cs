using System.Reflection;
using Kafe.CCK.Debugger.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger;

public static class ModConfig {

    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeIsHidden;
    internal static MelonPreferences_Entry<bool> MeOverwriteUIResources;

    public static void InitializeMelonPrefs() {

        _melonCategory = MelonPreferences.CreateCategory(nameof(CCKDebugger));

        MeOverwriteUIResources = _melonCategory.CreateEntry("OverwriteUIResources", true,
            description: "Whether the mod should overwrite all Cohtml UI resources when loading or not.");

        MeIsHidden = _melonCategory.CreateEntry("Hidden", false,
            description: "Whether to hide completely the CCK Debugger menu or not.");

        MeIsHidden.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            // Update whether the menu is visible or not
            CohtmlMenuController.Instance.UpdateMenuState();
            Core.PinToQuickMenu();
        });
    }

    public enum ShaderType {
        NeitriDistanceFadeOutline,
    }

    private const string CCKDebuggerAssetBundleName = "cckdebugger.assetbundle";

    private const string AssetsRoot = "Assets/CCK.Debugger/";

    // internal static GameObject UnityUIMenuPrefab;
    // private const string DebuggerMenuAssetPath = $"{AssetsRoot}Prefabs/CCKDebuggerMenu.prefab";
    //
    // internal static GameObject UnityUIMenuPinPrefab;
    // private const string DebuggerMenuPinAssetPath = $"{AssetsRoot}Prefabs/CCKDebuggerMenu_Pin.prefab";

    internal static GameObject BoneVisualizerPrefab;
    private const string DebuggerMenuBoneVisualizerPath = $"{AssetsRoot}Prefabs/CCKDebuggerVisualizer_Bone.prefab";

    internal static GameObject TrackerVisualizerPrefab;
    private const string DebuggerMenuTrackerVisualizerPath = $"{AssetsRoot}Prefabs/CCKDebuggerVisualizer_ViveTracker3.prefab";

    internal static GameObject LabelVisualizerPrefab;
    private const string DebuggerMenuLabelVisualizerPath = $"{AssetsRoot}Prefabs/CCKDebuggerVisualizer_Label.prefab";

    internal static readonly Dictionary<ShaderType, Shader> ShaderCache = new();
    private const string ShaderAssetPath = $"{AssetsRoot}Shaders/Distance Fade Outline Texture.shader";

    private const string CouiPath = @"ChilloutVR_Data\StreamingAssets\Cohtml\UIResources\CCKDebugger";
    private const string CouiManifestResourcePrefix = @"CCK.Debugger.Resources.UIResources.";

    public static void LoadAssemblyResources(Assembly assembly) {

        try {

            using var resourceStream = assembly.GetManifestResourceStream(CCKDebuggerAssetBundleName);
            using var memoryStream = new MemoryStream();
            if (resourceStream == null) {
                MelonLogger.Error($"Failed to load {CCKDebuggerAssetBundleName}!");
                return;
            }
            resourceStream.CopyTo(memoryStream);
            var assetBundle = AssetBundle.LoadFromMemory(memoryStream.ToArray());

            // Load Prefabs and Shader
            // UnityUIMenuPrefab = assetBundle.LoadAsset<GameObject>(DebuggerMenuAssetPath);
            // UnityUIMenuPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            //
            // UnityUIMenuPinPrefab = assetBundle.LoadAsset<GameObject>(DebuggerMenuPinAssetPath);
            // UnityUIMenuPinPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            BoneVisualizerPrefab = assetBundle.LoadAsset<GameObject>(DebuggerMenuBoneVisualizerPath);
            BoneVisualizerPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            TrackerVisualizerPrefab = assetBundle.LoadAsset<GameObject>(DebuggerMenuTrackerVisualizerPath);
            TrackerVisualizerPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            LabelVisualizerPrefab = assetBundle.LoadAsset<GameObject>(DebuggerMenuLabelVisualizerPath);
            LabelVisualizerPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;

            var shader = assetBundle.LoadAsset<Shader>(ShaderAssetPath);
            shader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            ShaderCache[ShaderType.NeitriDistanceFadeOutline] = shader;

            // Load and save the cohtml ui elements
            if (MeOverwriteUIResources.Value) {
                // Copy the UI Resources from the assembly to the CVR Cohtml UI folder
                // Warning: The file and folder names inside of UIResources cannot contain dot characters "." nor "-"
                // except on the extensions that must include a dot ".", and always require an extension
                // Example: "index.js"
                var fileNames = new List<string>();
                foreach (var manifestResourceName in assembly.GetManifestResourceNames()) {
                    if (!manifestResourceName.StartsWith(CouiManifestResourcePrefix)) continue;

                    // Convert assembly resource namespace into a path
                    var resourceName = manifestResourceName.Remove(0, CouiManifestResourcePrefix.Length);
                    var resourceExtension = Path.GetExtension(manifestResourceName);
                    var resourcePath = Path.GetFileNameWithoutExtension(resourceName).Replace('.', Path.DirectorySeparatorChar) + resourceExtension;
                    fileNames.Add(resourcePath);
                    var resourceFullPath = Path.Combine(CouiPath, resourcePath);

                    // Create folder if doesn't exist and save into a file
                    var directoryPath = Path.GetDirectoryName(resourceFullPath);
                    Directory.CreateDirectory(directoryPath!);
                    var cohtmlFileResourceStream = assembly.GetManifestResourceStream(manifestResourceName);
                    if (cohtmlFileResourceStream == null) {
                        var ex = $"Failed to find the Resource {manifestResourceName} in the Assembly.";
                        MelonLogger.Error($"Failed to find the Resource {manifestResourceName} in the Assembly.");
                        throw new Exception(ex);
                    }
                    using var resourceOutputFile = new FileStream(resourceFullPath, FileMode.Create);
                    cohtmlFileResourceStream.CopyTo(resourceOutputFile);
                }
                MelonLogger.Msg($"Loaded and saved all UI Resource files: {string.Join(", ", fileNames.ToArray())}");
            }
            else {
                MelonLogger.Msg("Skipping copying the Cohtml resources as define in the configuration... You should " +
                                "only see this message if you are manually editing the Cohtml UI Resources!");
            }
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load resources from the asset bundle");
            MelonLogger.Error(ex);
        }
    }
}
