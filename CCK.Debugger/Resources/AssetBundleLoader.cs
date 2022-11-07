using MelonLoader;
using UnityEngine;

namespace CCK.Debugger.Resources;

public enum ShaderType {
    NeitriDistanceFadeOutline,
}

public static class AssetBundleLoader {

    private static AssetBundle _assetBundleCache;

    private static GameObject _menuCache;
    private const string DebuggerMenuAssetPath = "Assets/Prefabs/CCKDebuggerMenu.prefab";

    private static GameObject _menuPinCache;
    private const string DebuggerMenuPinAssetPath = "Assets/Prefabs/CCKDebuggerMenu_Pin.prefab";

    private static GameObject _boneVisualizerCache;
    private const string DebuggerMenuBoneVisualizerPath = "Assets/Prefabs/CCKDebuggerVisualizer_Bone.prefab";

    private static GameObject _trackerVisualizerCache;
    private const string DebuggerMenuTrackerVisualizerPath = "Assets/Prefabs/CCKDebuggerVisualizer_ViveTracker3.prefab";

    private static readonly Dictionary<ShaderType, Shader> _shaderCache = new();
    private const string ShaderAssetPath = "Assets/Neitri-Unity-Shaders-master/Distance Fade Outline Texture.shader";

    private static AssetBundle GetCckDebuggerAssetBundle() {
        if (_assetBundleCache != null) return _assetBundleCache;

        var assetBundle = AssetBundle.LoadFromMemory(Resources.cckdebugger);
        assetBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        _assetBundleCache = assetBundle;
        return assetBundle;
    }

    public static GameObject GetMenuGameObject() {
        if (_menuCache != null) return _menuCache;

        var prefab = GetCckDebuggerAssetBundle().LoadAsset<GameObject>(DebuggerMenuAssetPath);
        _menuCache = prefab;
        return prefab;
    }

    public static GameObject GetMenuPinGameObject() {
        if (_menuPinCache != null) return _menuPinCache;

        var prefab = GetCckDebuggerAssetBundle().LoadAsset<GameObject>(DebuggerMenuPinAssetPath);
        _menuPinCache = prefab;
        return prefab;
    }

    public static GameObject GetBoneVisualizerObject() {
        if (_boneVisualizerCache != null) return _boneVisualizerCache;

        var prefab = GetCckDebuggerAssetBundle().LoadAsset<GameObject>(DebuggerMenuBoneVisualizerPath);
        _boneVisualizerCache = prefab;
        return prefab;
    }

    public static GameObject GetTrackerVisualizerObject() {
        if (_trackerVisualizerCache != null) return _trackerVisualizerCache;

        var prefab = GetCckDebuggerAssetBundle().LoadAsset<GameObject>(DebuggerMenuTrackerVisualizerPath);
        _trackerVisualizerCache = prefab;
        return prefab;
    }

    public static Shader GetShader(ShaderType type) {
        if (_shaderCache.ContainsKey(type)) return _shaderCache[type];

        var shader = GetCckDebuggerAssetBundle().LoadAsset<Shader>(ShaderAssetPath);
        _shaderCache[type] = shader;
        return shader;
    }
}
