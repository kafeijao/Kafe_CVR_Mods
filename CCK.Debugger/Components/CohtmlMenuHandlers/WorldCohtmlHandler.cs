using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;

public class WorldCohtmlHandler : ICohtmlHandler {

    private static bool _isLoaded;

    static WorldCohtmlHandler() {
        CVRGameEventSystem.World.OnLoad.AddListener(_ => {
            _isLoaded = true;
            SetupWorld();
        });
        CVRGameEventSystem.World.OnUnload.AddListener(worldId => {
            if (string.IsNullOrEmpty(worldId)) return;
            _isLoaded = false;
            SetupWorld();
        });
        Events.World.WorldFinishedConfiguration += SetupWorld;
    }

    private static void SetupWorld() {

        var core = new Core("World");

        var world = CVRWorld.Instance;

        var attributesSection = core.AddSection("Attributes");
        attributesSection.AddSection("World Id").Value = _isLoaded ? world.AssetInfo.objectId : "N/A";
        attributesSection.AddSection("World Name").AddValueGetter(() => _isLoaded && Events.World.WorldNamesCache.TryGetValue(world.AssetInfo.objectId, out var worldName) ? worldName : "N/A");
        attributesSection.AddSection("Has Custom Matrix").Value = ToString(world.useCustomCollisionMatrix);

        var colMatrixSection = attributesSection.AddSection("Collision Matrix", "", true);

        for (var i = 0; i <= 31; ++i) {
            var layerSection = colMatrixSection.AddSection($"Layer {i} ({LayerMask.LayerToName(i)}) collides with", "", true);
            for(var j = 0; j <= 31; ++j) {
                if(i != j && !Physics.GetIgnoreLayerCollision(i, j)) {
                    layerSection.AddSection($"Layer {j} ({LayerMask.LayerToName(j)}) ");
                }
            }
        }

        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);
    }

    protected override void Load() {
        SetupWorld();
    }

    protected override void Unload() {
        SetupWorld();
    }

    protected override void Reset() {
        SetupWorld();
    }

    public override void Update() {

        // Update button's states
        Core.UpdateButtonsState();

        // Update section's getters
        Core.UpdateSectionsFromGetters();
    }

}
