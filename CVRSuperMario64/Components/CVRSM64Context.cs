using LibSM64;
using UnityEngine;

namespace CVRSuperMario64;

public class CVRSM64CContext : MonoBehaviour {
    
    static CVRSM64CContext s_instance = null;

    List<CVRSM64CMario> _marios = new List<CVRSM64CMario>();
    readonly List<CVRSM64DynamicCollider> _surfaceObjects = new List<CVRSM64DynamicCollider>();

    private void Awake() {
        
        //Interop.GlobalInit( File.ReadAllBytes( Application.dataPath + "/../baserom.us.z64" ));
        Interop.GlobalInit(CVRSuperMario64.SuperMario64UsZ64RomBytes);
        //RefreshStaticTerrain();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Misc.GetAllStaticSurfaces());
    }

    // private void Start() {
    //     // Update the ticks at 30 times a second
    //     InvokeRepeating(nameof(FunctionToCall), 0, 1f / 30f);
    // }
    //
    // private void FunctionToCall() {
    //     FakeFixedUpdate();
    //     FakeUpdate();
    // }

    private void Update() {

        foreach (var o in _surfaceObjects) {
            o.contextUpdate();
        }

        foreach (var o in _marios) {
            o.contextUpdate();
        }
    }

    private void FixedUpdate() {

        foreach (var o in _surfaceObjects) {
            o.contextFixedUpdate();
        }

        foreach (var o in _marios) {
            o.contextFixedUpdate();
        }
    }

    private void OnApplicationQuit() {
        Interop.GlobalTerminate();
        s_instance = null;
    }

    private static void EnsureInstanceExists() {
        if (s_instance == null) {
            var contextGo = new GameObject("SM64_CONTEXT");
            contextGo.hideFlags |= HideFlags.HideInHierarchy;
            s_instance = contextGo.AddComponent<CVRSM64CContext>();
        }
    }

    public static void RefreshStaticTerrain() {
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
    }

    public static void RegisterMario(CVRSM64CMario mario) {
        EnsureInstanceExists();

        if (!s_instance._marios.Contains(mario)) {
            s_instance._marios.Add(mario);
        }
    }

    public static void UnregisterMario(CVRSM64CMario mario) {
        if (s_instance != null && s_instance._marios.Contains(mario)) {
            s_instance._marios.Remove(mario);
        }
    }

    public static void RegisterSurfaceObject(CVRSM64DynamicCollider surfaceObject) {
        EnsureInstanceExists();

        if (!s_instance._surfaceObjects.Contains(surfaceObject)) {
            s_instance._surfaceObjects.Add(surfaceObject);
        }
    }

    public static void UnregisterSurfaceObject(CVRSM64DynamicCollider surfaceObject) {
        if (s_instance != null && s_instance._surfaceObjects.Contains(surfaceObject)) {
            s_instance._surfaceObjects.Remove(surfaceObject);
        }
    }
}
