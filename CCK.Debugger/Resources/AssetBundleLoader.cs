using MelonLoader;
using UnityEngine;

namespace CCK.Debugger.Resources;

public enum ShaderType {
    NeitriDistanceFadeOutline,
}

public static class AssetBundleLoader {

    private static GameObject _menuCache;
    private static readonly Dictionary<ShaderType, Shader> _shaderCache = new();

    private const string _assetPath = "Assets/Prefabs/CCKDebuggerMenu.prefab";

    public static GameObject GetMenuGameObject() {
        if (_menuCache != null) return _menuCache;

        AssetBundle assetBundle = AssetBundle.LoadFromMemory(Resources.cckdebugger);
        assetBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        var prefab = assetBundle.LoadAsset<GameObject>(_assetPath);
        _menuCache = prefab;
        return prefab;
    }

    public static Shader GetShader(ShaderType type) {
        if (_shaderCache.ContainsKey(type)) return _shaderCache[type];

        AssetBundle assetBundle = AssetBundle.LoadFromMemory(Resources.cckdebugger);
        assetBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        var shader = assetBundle.LoadAsset<Shader>(
            "Assets/Neitri-Unity-Shaders-master/Distance Fade Outline.shader");
        _shaderCache[type] = shader;
        return shader;
    }
}
