using UnityEngine;

namespace CCK.Debugger.Utils;

public static class Misc {

    // Colors
    public static readonly Color ColorWhite = new Color(1f, 1f, 1f);
    public static readonly Color ColorWhiteFade = new Color(1f, 1f, 1f, 0.3f);
    public static readonly Color ColorBlue = new Color(0f, .69f, 1f);
    public static readonly Color ColorBlueFade = new Color(0f, .69f, 1f, 0.35f);
    public static readonly Color ColorYellow = new Color(1f, .95f, 0f);
    public static readonly Color ColorYellowFade = new Color(1f, .95f, 0f, 0.35f);
    public static readonly Color ColorOrange = new Color(.9882f, .4157f, .0118f);

    // Material Properties - Standard
    public static readonly Shader ShaderStandard = Shader.Find("Standard");
    public static readonly int MatMainColor = Shader.PropertyToID("_Color");
    public static readonly int MatSrcBlend = Shader.PropertyToID("_SrcBlend");
    public static readonly int MatDstBlend = Shader.PropertyToID("_DstBlend");
    public static readonly int MatZWrite = Shader.PropertyToID("_ZWrite");

    // Material Properties - Nietri
    public static readonly int MatOutlineColor = Shader.PropertyToID("_OutlineColor");
    public static readonly int MatOutlineWidth = Shader.PropertyToID("_OutlineWidth");
    public static readonly int MatOutlineSmoothness = Shader.PropertyToID("_OutlineSmoothness");
    public static readonly int MatFadeInBehindObjectsDistance = Shader.PropertyToID("_FadeInBehindObjectsDistance");
    public static readonly int MatFadeOutBehindObjectsDistance = Shader.PropertyToID("_FadeOutBehindObjectsDistance");
    public static readonly int MatFadeInCameraDistance = Shader.PropertyToID("_FadeInCameraDistance");
    public static readonly int MatFadeOutCameraDistance = Shader.PropertyToID("_FadeOutCameraDistance");
    public static readonly int MatShowOutlineInFrontOfObjects = Shader.PropertyToID("_ShowOutlineInFrontOfObjects");

    // Shader keywords
    public const string ShaderAlphaTest = "_ALPHATEST_ON";
    public const string ShaderAlphaBlend = "_ALPHABLEND_ON";
    public const string ShaderAlphaPreMultiply = "_ALPHAPREMULTIPLY_ON";

    private static readonly Dictionary<PrimitiveType, Mesh> MeshesCache = new();

    // Get mesh primitives (cached)
    public static Mesh GetPrimitiveMesh(PrimitiveType type) {
        if (MeshesCache.ContainsKey(type)) return MeshesCache[type];

        var go = GameObject.CreatePrimitive(type);
        var mesh = go.GetComponent<MeshFilter>().mesh;
        MeshesCache.Add(type, mesh);
        UnityEngine.Object.Destroy(go);

        return mesh;
    }

}
