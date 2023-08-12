using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshTools;

public static class ModConfig {

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
