using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshTools;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<int> MeMaxBakeBounds;
    internal static MelonPreferences_Entry<int> MeSamplesToProcessPerFrame;

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(NavMeshTools));

        MeMaxBakeBounds = _melonCategory.CreateEntry("MaxBakeBounds", 300,
            description: "The size of the bounds to calculate the nav mesh to, the smaller the fastest it is. Defaults to 300");

        MeSamplesToProcessPerFrame = _melonCategory.CreateEntry("SamplesToProcessPerFrame", 10,
            description: "How many nav mesh links should be processed per frame (reduce if you're noticing while calculating mesh links). Defaults to 10");
    }


    // Asset Resources
    public static Shader NoachiWireframeShader;
    private const string AssetBundleName = "navmeshtools.assetbundle";
    private const string ShaderAssetPath = "Assets/NavMeshTools/WireFrame.shader";

    // Material Properties - Noachi Wireframe
    public static readonly int MatFaceColor = Shader.PropertyToID("_Color");
    public static readonly int MatWireColor = Shader.PropertyToID("_ColorWireFrame");
    public static readonly int MatWireThickness = Shader.PropertyToID("_Thickness");
    public static readonly Color ColorPinkTransparent = new Color(1f, 0f, 1f, 0.05f);
    public static readonly Color ColorBlue = new Color(0f, .69f*1.2f, 1f*1.2f);

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
            NoachiWireframeShader = assetBundle.LoadAsset<Shader>(ShaderAssetPath);
            NoachiWireframeShader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
        catch (Exception ex) {
            MelonLogger.Error("Failed to Load the asset bundle: " + ex.Message);
        }
    }
}
