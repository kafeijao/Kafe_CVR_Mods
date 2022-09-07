using ABI_RC.Core.Util;
using ABI.CCK.Components;
using Assets.ABI_RC.Systems.Safety.AdvancedSafety;
using UnityEngine;

namespace OSC.Events;

public static class Spawnable {

    // Caches for spawnable output (because some parameters might get spammed like hell)
    private static readonly Dictionary<string, CVRSyncHelper.PropData> PropCache = new();
    private static readonly Dictionary<string, Dictionary<string, float>> PropParametersCacheOutFloat = new();

    public static event Action<CVRSyncHelper.PropData> SpawnableCreated;
    public static event Action<CVRSpawnable, CVRSpawnableValue> SpawnableParameterChanged;


    internal static void OnSpawnableCreated(CVRSyncHelper.PropData propData) {

        // Add prop to caches
        if (!PropCache.ContainsKey(propData.InstanceId)) {
            PropCache.Add(propData.InstanceId, propData);
        }
        if (!PropParametersCacheOutFloat.ContainsKey(propData.InstanceId)) {
            PropParametersCacheOutFloat.Add(propData.InstanceId, new Dictionary<string, float>());
        }

        //MelonLogger.Msg($"[Spawnable] Spawnable {propData.Spawnable.instanceId} was created!");

        SpawnableCreated?.Invoke(propData);
    }

    internal static void OnSpawnableDeleted(CVRSyncHelper.PropData propData) {

        // Remove prop from caches
        if (PropCache.ContainsKey(propData.InstanceId)) {
            PropCache.Remove(propData.InstanceId);
        }
        if (PropParametersCacheOutFloat.ContainsKey(propData.InstanceId)) {
            PropParametersCacheOutFloat.Remove(propData.InstanceId);
        }

        //MelonLogger.Msg($"[Spawnable] Spawnable {propData.Spawnable.instanceId} was deleted!");

        //SpawnableDeleted?.Invoke(propData);
    }

    internal static void OnSpawnableParameterChanged(CVRSpawnable spawnable, CVRSpawnableValue spawnableValue) {
        if (spawnable == null || spawnableValue == null || !PropParametersCacheOutFloat.ContainsKey(spawnable.instanceId)) return;

        var cache = PropParametersCacheOutFloat[spawnable.instanceId];

        // Value already exists and it's updated
        if (cache.ContainsKey(spawnableValue.name) && Mathf.Approximately(cache[spawnableValue.name], spawnableValue.currentValue)) return;

        // Otherwise update the cache value
        cache[spawnableValue.name] = spawnableValue.currentValue;

        SpawnableParameterChanged?.Invoke(spawnable, spawnableValue);
    }
    internal static void OnSpawnableParameterSet(string spawnableInstanceId, string spawnableParamName, float spawnableParamValue) {
        if (!PropCache.ContainsKey(spawnableInstanceId) || spawnableParamName == "" || spawnableParamValue.IsAbsurd()) return;
        var spawnable = PropCache[spawnableInstanceId].Spawnable;
        var spawnableValueIndex = spawnable.syncValues.FindIndex( match => match.name == spawnableParamName);
        if (spawnableValueIndex == -1) return;

        //MelonLogger.Msg($"[Spawnable] Setting spawnable prop {spawnableInstanceId} {spawnableParamName} parameter to {spawnableParamValue}!");

        if (!ShouldControl(spawnable)) return;

        spawnable.SetValue(spawnableValueIndex, spawnableParamValue);

        //SpawnableParameterSet?.Invoke(spawnable, spawnable.syncValues[spawnableValueIndex]);
    }

    internal static void OnSpawnableLocationSet(string spawnableInstanceId, Vector3 pos, Vector3 rot) {
        if (!PropCache.ContainsKey(spawnableInstanceId) || pos.IsAbsurd() || pos.IsBad() || rot.IsAbsurd() || rot.IsBad()) return;
        var spawnable = PropCache[spawnableInstanceId].Spawnable;
        var spawnableTransform = spawnable.transform;

        //MelonLogger.Msg($"[Spawnable] Setting spawnable prop {spawnableInstanceId} {spawnableParamName} parameter to {spawnableParamValue}!");

        if (!ShouldControl(spawnable)) return;

        // Update location
        spawnableTransform.position = pos;
        spawnableTransform.eulerAngles = rot;
        spawnable.ForceUpdate();

        //SpawnableLocationSet?.Invoke();
    }

    private static bool ShouldControl(CVRSpawnable spawnable) {

        // Spawned by other people -> Ignore
        if (!spawnable.IsMine()) return false;

        // var pickup = Traverse.Create(spawnable).Field<CVRPickupObject>("pickup").Value;
        // var attachments = Traverse.Create(spawnable).Field<List<CVRAttachment>>("_attachments").Value;

        // Ignore prop if we're not grabbing it nor it is attached to us
        //if ((pickup == null || pickup.grabbedBy != MetaPort.Instance.ownerId) &&
        //    (attachments.Count <= 0 || !attachments.Any(a => a.IsAttached()))) return;

        // Other people are syncing it (grabbing/telegrabbing/attatched) -> Ignore
        if (spawnable.SyncType != 0) return false;

        // This will be true when being synced by us (grabbing/telegrabbing/attatched) or physics -> Ignore
        if (spawnable.isPhysicsSynced) return false;

        return true;
    }
}
