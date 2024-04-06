using System.Collections.Concurrent;
using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using SK.Libretro.Unity;
using UnityEngine;

namespace Kafe.RetroCVR.CCK;

[DisallowMultipleComponent]
public class RetroCVRCore : MonoBehaviour {

    [SerializeField] public string version;

    [SerializeField] public string coreName;

    [NonSerialized] private LibretroInstanceVariable _libretroInstanceVariable = RetroCVR.globalInstanceVariable;

    private void Start() {

        // Add and initialize the SK.Libretro components
        gameObject.AddComponent<AudioProcessor>();
        var libretroInstance = gameObject.AddComponent<LibretroInstance>();

        libretroInstance.Camera = PlayerSetup.Instance.GetActiveCamera().GetComponent<Camera>();
        libretroInstance.Renderer = GetComponent<MeshRenderer>();
        libretroInstance.Collider = GetComponent<Collider>();
        libretroInstance.Settings = new InstanceSettings();

        _libretroInstanceVariable.Current = libretroInstance;
        // libretroInstance.Camera = PlayerSetup.Instance.GetActiveCamera().GetComponent<Camera>();

        // Super Mario
        // coreName = "mupen64plus_next";
        // var game = "baserom.us";

        // Doom
        // coreName = "prboom";
        // var game = "Doom1";

        // Pokemon
        coreName = "mgba";
        // Note: Games should be without a extension...
        var game = "Pokemon - Red Version (USA, Europe) (SGB Enhanced)";

        _libretroInstanceVariable.Current.Initialize(coreName, ModConfig.GetRomFolder(coreName), game);
        MelonLogger.Msg($"Playing {string.Join(",", _libretroInstanceVariable.Current.GameNames)} on {_libretroInstanceVariable.Current.CoreName}. Games Directory: {_libretroInstanceVariable.Current.GamesDirectory}");
        _libretroInstanceVariable.StartContent();
    }

    private void OnDestroy() {
        // Traverse.Create(AccessTools.TypeByName("SK.Libretro.Unity.MainThreadDispatcher")).Property<ConcurrentQueue<Func<ValueTask>>>("_executionQueue").Value.Clear();
    }
}
