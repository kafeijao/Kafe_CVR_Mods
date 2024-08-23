using ABI_RC.Core.Util;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.TheClapper;

public class ClappableSpawnable : Clappable {

    private string _spawnableId;
    private CVRSpawnable _spawnable;

    protected override bool IsClappable()
    {
        if (TheClapper.DisableClappingProps.Value) return false;
        return true;
    }

    protected override void OnClapped(Vector3 clappablePosition) {

        if (_spawnable == null) return;

        MelonLogger.Msg($"Clapped a prop with the id: {_spawnableId}!");

        // Delete prop globally if spawned by us, locally otherwise
        _spawnable.Delete();

        TheClapper.EmitParticles(clappablePosition, new Color(0f, 1f, 0f), 2f);
    }

    public static void Create(CVRSyncHelper.PropData propData) {

        var rootTarget = propData.Spawnable.gameObject;

        var targets = new HashSet<GameObject> { rootTarget };

        if (TheClapper.ClappablePropPickups.Value)
        {
            // Add all pickup targets
            CVRPickupObject[] pickupGameObjects = rootTarget.GetComponentsInChildren<CVRPickupObject>();
            targets.UnionWith(pickupGameObjects.Select(p => p.gameObject));
        }

        if (TheClapper.ClappablePropSubSyncs.Value)
        {
            // Add all prop sun-sync transforms
            foreach (var subSync in propData.Spawnable.subSyncs)
            {
                if (subSync == null || subSync.transform == null) continue;
                targets.Add(subSync.transform.gameObject);
            }
        }

        // Add the clappable component to all targets
        foreach (GameObject target in targets)
        {
            if (!target.TryGetComponent(out ClappableSpawnable clappableSpawnable)) {
                clappableSpawnable = target.AddComponent<ClappableSpawnable>();
            }

            clappableSpawnable._spawnableId = propData.InstanceId;
            clappableSpawnable._spawnable = propData.Spawnable;
        }

    }
}
