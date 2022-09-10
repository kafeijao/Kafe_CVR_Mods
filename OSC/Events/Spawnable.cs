using ABI_RC.Core.Util;
using ABI.CCK.Components;
using Assets.ABI_RC.Systems.Safety.AdvancedSafety;
using UnityEngine;

namespace OSC.Events;

public static class Spawnable {

    // Caches for spawnable output (because some parameters might get spammed like hell)
    private static readonly Dictionary<string, CVRSyncHelper.PropData> PropCache = new();
    private static readonly Dictionary<string, Dictionary<string, float>> PropParametersCacheOutFloat = new();
    private static readonly Dictionary<CVRSyncHelper.PropData, bool> PropAvailabilityCache = new();

    public static event Action<CVRSyncHelper.PropData> SpawnableCreated;
    public static event Action<CVRSyncHelper.PropData> SpawnableDeleted;
    public static event Action<CVRSpawnable, CVRSpawnableValue> SpawnableParameterChanged;
    public static event Action<CVRSpawnable, bool> SpawnableAvailable;
    public static event Action<CVRSpawnable> SpawnableLocationTrackingTicked;

    // Events from the game

    internal static void Reset() {
        foreach (var prop in PropCache) {
            OnSpawnableCreated(prop.Value);
        }
    }

    internal static void OnSpawnableCreated(CVRSyncHelper.PropData propData) {
        if (propData?.Spawnable == null || !propData.Spawnable.IsMine()) return;

        // Add prop to caches
        if (!PropCache.ContainsKey(propData.InstanceId)) {
            PropCache.Add(propData.InstanceId, propData);
        }
        if (!PropParametersCacheOutFloat.ContainsKey(propData.InstanceId)) {
            PropParametersCacheOutFloat.Add(propData.InstanceId, new Dictionary<string, float>());
        }

        //MelonLogger.Msg($"[Spawnable] Spawnable {propData.Spawnable.instanceId} was created!");

        SpawnableCreated?.Invoke(propData);

        // Update availability because spawning doesn't trigger UpdateFromNetwork
        OnSpawnableUpdateFromNetwork(propData, propData.Spawnable);
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

        SpawnableDeleted?.Invoke(propData);
    }

    internal static void OnSpawnableParameterChanged(CVRSpawnable spawnable, CVRSpawnableValue spawnableValue) {
        if (spawnable == null || spawnableValue == null || !spawnable.IsMine() || !PropParametersCacheOutFloat.ContainsKey(spawnable.instanceId)) return;

        var cache = PropParametersCacheOutFloat[spawnable.instanceId];

        // Value already exists and it's updated
        if (cache.ContainsKey(spawnableValue.name) && Mathf.Approximately(cache[spawnableValue.name], spawnableValue.currentValue)) return;

        // Otherwise update the cache value
        cache[spawnableValue.name] = spawnableValue.currentValue;

        SpawnableParameterChanged?.Invoke(spawnable, spawnableValue);
    }

    internal static void OnSpawnableUpdateFromNetwork(CVRSyncHelper.PropData propData, CVRSpawnable spawnable) {
        if (!spawnable.IsMine()) return;

        var shouldControl = ShouldControl(spawnable);

        // Check if the state changed and ignore if it didn't
        if (PropAvailabilityCache.ContainsKey(propData)) {
            if (PropAvailabilityCache[propData] == shouldControl) return;
        }

        PropAvailabilityCache[propData] = shouldControl;

        SpawnableAvailable?.Invoke(spawnable, shouldControl);
    }

    internal static void OnTrackingTick() {
        foreach (var prop in PropCache) {
            var spawnable = prop.Value?.Spawnable;
            if (spawnable == null) continue;
            SpawnableLocationTrackingTicked?.Invoke(spawnable);
        }
    }

    // Events to the game

    internal static void OnSpawnableParameterSet(string spawnableInstanceId, string spawnableParamName, float spawnableParamValue) {
        if (!PropCache.ContainsKey(spawnableInstanceId) || spawnableParamName == "" || spawnableParamValue.IsAbsurd()) return;
        var spawnable = PropCache[spawnableInstanceId].Spawnable;

        // Prevent NullReferenceException when we're setting the location of a prop that was just deleted
        if (spawnable == null) return;

        var spawnableValueIndex = spawnable.syncValues.FindIndex( match => match.name == spawnableParamName);
        if (spawnableValueIndex == -1) return;

        //MelonLogger.Msg($"[Spawnable] Setting spawnable prop {spawnableInstanceId} {spawnableParamName} parameter to {spawnableParamValue}!");

        // Value is already up to date -> Ignore
        if (Mathf.Approximately(spawnable.syncValues[spawnableValueIndex].currentValue, spawnableParamValue)) return;

        if (!ShouldControl(spawnable, true)) return;

        spawnable.SetValue(spawnableValueIndex, spawnableParamValue);

        //SpawnableParameterSet?.Invoke(spawnable, spawnable.syncValues[spawnableValueIndex]);
    }


    internal static void OnSpawnableLocationSet(string spawnableInstanceId, Vector3 pos, Vector3 rot, int? subIndex = null) {
        if (!PropCache.ContainsKey(spawnableInstanceId) || pos.IsAbsurd() || pos.IsBad() || rot.IsAbsurd() || rot.IsBad()) return;
        var spawnable = PropCache[spawnableInstanceId].Spawnable;

        // Prevent NullReferenceException when we're setting the location of a prop that was just deleted
        if (spawnable == null) return;

        Transform transformToSet;
        // The transform is a subSync of the spawnable
        if (subIndex.HasValue) {
            var index = subIndex.Value;
            if (index < 0 || index >= spawnable.subSyncs.Count) return;
            transformToSet = spawnable.subSyncs[index].transform;
        }
        // Use the spawnable transform
        else {
            transformToSet = spawnable.transform;
        }

        //MelonLogger.Msg($"[Spawnable] Setting spawnable prop {spawnableInstanceId} {spawnableParamName} parameter to {spawnableParamValue}!");

        if (!ShouldControl(spawnable)) return;

        // Update location
        transformToSet.position = pos;
        transformToSet.eulerAngles = rot;
        spawnable.ForceUpdate();

        //SpawnableLocationSet?.Invoke();
    }


    internal static void OnSpawnableCreate(string propGuid, float posX = 0f, float posY = 0f, float posZ = 0f) {
        if (Guid.TryParse(propGuid, out _) && !posX.IsAbsurd() && !posY.IsAbsurd() && !posZ.IsAbsurd()) {
            CVRSyncHelper.SpawnProp(propGuid, posX, posY, posZ);
        }
    }

    internal static void OnSpawnableDelete(string spawnableInstanceId) {
        var propData = CVRSyncHelper.Props.Find( match => match.InstanceId == spawnableInstanceId);
        if (propData != null && propData.Spawnable != null) propData.Spawnable.Delete();
    }

    // Utils

    private static bool ShouldControl(CVRSpawnable spawnable, bool allowWhenLocalPlayerIsInteracting = false) {

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
        if (!allowWhenLocalPlayerIsInteracting && spawnable.isPhysicsSynced) return false;

        return true;
    }
}
