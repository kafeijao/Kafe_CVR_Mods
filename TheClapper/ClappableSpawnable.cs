using ABI_RC.Core.Util;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.TheClapper;

public class ClappableSpawnable : Clappable {

    private string _spawnableId;
    private CVRSpawnable _spawnable;

    protected override void OnClapped(Vector3 clappablePosition) {

        if (_spawnable == null) return;

        MelonLogger.Msg($"Clapped a prop with the id: {_spawnableId}!");

        // Delete prop globally if spawned by us, locally otherwise
        _spawnable.Delete();

        TheClapper.EmitParticles(clappablePosition, new Color(0f, 1f, 0f), 2f);
    }

    public static void Create(CVRSyncHelper.PropData propData) {

        var target = propData.Spawnable.gameObject;

        if (!target.gameObject.TryGetComponent(out ClappableSpawnable clappableSpawnable)) {
            clappableSpawnable = target.gameObject.AddComponent<ClappableSpawnable>();
        }

        clappableSpawnable._spawnableId = propData.InstanceId;
        clappableSpawnable._spawnable = propData.Spawnable;
    }
}
