using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using MelonLoader;

namespace Kafe.RCCOverrides;

public class RCCOverrides : MelonMod {

    public static readonly Dictionary<CVRSpawnable, RCC_CarControllerV3> CurrentControllers = new();

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();

        CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((string spawnerId, CVRSpawnable spawnable) => {
            if (!spawnable.TryGetComponent<RCC_CarControllerV3>(out var controller)) return;
            CurrentControllers[spawnable] = controller;
        });

        CVRGameEventSystem.Spawnable.OnDestroy.AddListener((string spawnerId, CVRSpawnable spawnable) => {
            CurrentControllers.Remove(spawnable);
        });
    }
}
