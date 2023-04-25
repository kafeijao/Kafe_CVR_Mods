using ABI_RC.Core.Player;
using ABI_RC.Core.Util;
using ABI.CCK.Components;
using Assets.ABI_RC.Systems.Safety.AdvancedSafety;
using MelonLoader;
using UnityEngine;

namespace Kafe.OSC.Events;

public static class Spawnable {

    private static bool _debugMode;

    // Caches for spawnable output (because some parameters might get spammed like hell)
    private static readonly Dictionary<string, CVRSyncHelper.PropData> PropCache;
    private static readonly Dictionary<string, Dictionary<string, float>> SpawnableParametersCacheOutFloat;
    private static readonly Dictionary<CVRSyncHelper.PropData, bool> PropAvailabilityCache;

    public static event Action<CVRSyncHelper.PropData> SpawnableCreated;
    public static event Action<CVRSpawnable> SpawnableDeleted;
    public static event Action<CVRSpawnable, CVRSpawnableValue> SpawnableParameterChanged;
    public static event Action<CVRSpawnable, bool> SpawnableAvailable;
    public static event Action<CVRSpawnable> SpawnableLocationTrackingTicked;

    static Spawnable() {

        // Handle config debug value and changes
        _debugMode = OSC.Instance.meOSCDebug.Value;
        OSC.Instance.meOSCDebug.OnValueChanged += (_, newValue) => _debugMode = newValue;

        // Instantiate caches
        PropCache = new Dictionary<string, CVRSyncHelper.PropData>();
        SpawnableParametersCacheOutFloat = new Dictionary<string, Dictionary<string, float>>();
        PropAvailabilityCache = new Dictionary<CVRSyncHelper.PropData, bool>();
    }

    // Events from the game

    internal static void Reset() {
        PropAvailabilityCache.Clear();
        SpawnableParametersCacheOutFloat.Clear();
        foreach (var prop in PropCache.Values) {
            if (prop.Spawnable == null) return;
            OnSpawnableCreated(prop);
            OnSpawnableUpdateFromNetwork(prop);
            foreach (var syncValue in prop.Spawnable.syncValues) {
                OnSpawnableParameterChanged(prop.Spawnable, syncValue);
            }
        }
    }

    internal static void OnSpawnableCreated(CVRSyncHelper.PropData propData) {
        if (propData?.Spawnable == null || !propData.Spawnable.IsMine()) return;

        // Add prop data to caches
        if (!PropCache.ContainsKey(propData.InstanceId)) {
            PropCache.Add(propData.InstanceId, propData);
        }
        if (!SpawnableParametersCacheOutFloat.ContainsKey(propData.InstanceId)) {
            SpawnableParametersCacheOutFloat.Add(propData.InstanceId, new Dictionary<string, float>());
        }

        SpawnableCreated?.Invoke(propData);

        // Update availability because spawning doesn't trigger UpdateFromNetwork
        OnSpawnableUpdateFromNetwork(propData);
    }

    internal static void OnSpawnableDestroyed(CVRSpawnable spawnable) {
        if (spawnable == null || !PropCache.ContainsKey(spawnable.instanceId)) return;

        // Remove spawnable from caches
        if (PropCache.ContainsKey(spawnable.instanceId)) {
            PropCache.Remove(spawnable.instanceId);
        }
        if (SpawnableParametersCacheOutFloat.ContainsKey(spawnable.instanceId)) {
            SpawnableParametersCacheOutFloat.Remove(spawnable.instanceId);
        }

        SpawnableDeleted?.Invoke(spawnable);
    }

    internal static void OnSpawnableParameterChanged(CVRSpawnable spawnable, CVRSpawnableValue spawnableValue) {
        if (spawnable == null || spawnableValue == null || !spawnable.IsMine() || !SpawnableParametersCacheOutFloat.ContainsKey(spawnable.instanceId)) return;

        var cache = SpawnableParametersCacheOutFloat[spawnable.instanceId];

        // Value already exists and it's updated
        if (cache.ContainsKey(spawnableValue.name) && Mathf.Approximately(cache[spawnableValue.name], spawnableValue.currentValue)) return;

        // Otherwise update the cache value
        cache[spawnableValue.name] = spawnableValue.currentValue;

        SpawnableParameterChanged?.Invoke(spawnable, spawnableValue);
    }

    internal static void OnSpawnableUpdateFromNetwork(CVRSyncHelper.PropData propData) {
        var spawnable = propData.Spawnable;
        if (spawnable == null || !spawnable.IsMine()) return;

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
        var spawnable = PropCache[spawnableInstanceId]?.Spawnable;

        // Prevent NullReferenceException when we're setting the location of a prop that was just deleted
        if (spawnable == null) return;


        var spawnableValueIndex = spawnable.syncValues.FindIndex( match => match.name == spawnableParamName);
        if (spawnableValueIndex == -1) return;

        // Value is already up to date -> Ignore
        if (Mathf.Approximately(spawnable.syncValues[spawnableValueIndex].currentValue, spawnableParamValue)) return;

        if (!ShouldControl(spawnable, true)) return;

        if (_debugMode) {
            MelonLogger.Msg($"[Debug] Set p+{spawnable.guid}~{spawnableInstanceId} {spawnableParamName} parameter" +
                            $" to {spawnableParamValue}");
        }

        spawnable.SetValue(spawnableValueIndex, spawnableParamValue);
    }


    internal static void OnSpawnableLocationSet(string spawnableInstanceId, Vector3 pos, Vector3 rot, int? subIndex = null) {

        if (_debugMode) MelonLogger.Msg($"[Debug] Attempting to set {spawnableInstanceId} [{(subIndex.HasValue ? subIndex.Value : "Main")}] location...");

        if (!PropCache.ContainsKey(spawnableInstanceId) || pos.IsAbsurd() || pos.IsBad() || rot.IsAbsurd() || rot.IsBad()) {
            if (_debugMode) {
                MelonLogger.Msg($"[Debug] Attempted to fetch {spawnableInstanceId} from cache. But it was missing or" +
                                $"the location was borked! InCache: {PropCache.ContainsKey(spawnableInstanceId)}" +
                                $"\n\t\t\tpos: {pos.ToString()}, rot: {rot.ToString()}");
            }
            return;
        }
        var spawnable = PropCache[spawnableInstanceId]?.Spawnable;

        // Prevent NullReferenceException when we're setting the location of a prop that was just deleted
        if (spawnable == null) {
            if (_debugMode) {
                MelonLogger.Msg($"[Debug] Attempted to fetch {spawnableInstanceId} from cache. But the associated " +
                                $"spawnable was null...");
            }
            return;
        }

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

        if (!ShouldControl(spawnable)) {
            if (_debugMode) {
                MelonLogger.Msg($"[Debug] Attempted to control {spawnableInstanceId} but got refused! " +
                                $"Sync: {spawnable.SyncType} IsPhysics: {spawnable.isPhysicsSynced}");
            }
            return;
        }

        // Update location
        transformToSet.position = pos;
        transformToSet.eulerAngles = rot;
        spawnable.ForceUpdate();

        if (_debugMode) MelonLogger.Msg($"[Debug] \t{spawnableInstanceId} [{(subIndex.HasValue ? subIndex.Value : "Main")}] location set!");
    }


    internal static void OnSpawnableCreate(string propGuid, float? posX = null, float? posY = null, float? posZ = null) {
        if (Guid.TryParse(propGuid, out _)) {
            if (posX.HasValue && posX.Value.IsAbsurd() && posY.HasValue && posY.Value.IsAbsurd() && posZ.HasValue && posZ.Value.IsAbsurd()) {
                // Spawn prop with the local coordinates provided
                CVRSyncHelper.SpawnProp(propGuid, posX.Value, posY.Value, posZ.Value);
            }
            else {
                // Spawn prop without coordinates -> spawns in front of the player
                PlayerSetup.Instance.DropProp(propGuid);
            }
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

        // Other people are syncing it (grabbing/telegrabbing/attatched) -> Ignore
        if (spawnable.SyncType != 0) return false;

        // This will be true when being synced by us (grabbing/telegrabbing/attatched) or physics -> Ignore
        if (!allowWhenLocalPlayerIsInteracting && spawnable.isPhysicsSynced) return false;

        return true;
    }
}
