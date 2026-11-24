using ABI_RC.Systems.SyncedObjects;
using ABI.CCK.Components;
using GameCommon.Models.Sync;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower.InteractableWrappers;

public static class Pickups
{
    public static readonly HashSet<PickupWrapper> AvailablePickups = new();
    public static readonly HashSet<SpawnablePickupWrapper> AvailableSpawnablePickups = new();
    public static readonly HashSet<ObjectSyncPickupWrapper> AvailableObjectSyncPickups = new();

    public abstract class PickupWrapper : MonoBehaviour
    {
        public CVRPickupObject pickupObject;

        public bool hasInteractable;
        public CVRInteractable interactable;

        /// <summary>
        /// Whether nobody is holding, attaching or sitting on this pickup
        /// </summary>
        public bool IsGrabbable() => SyncedObjectManager.GetSyncType(transform) < SyncType.TeleGrab
                                     && !FollowerController.CarriedTransforms.Contains(transform);

        protected abstract void StartCall();
        protected abstract void DestroyCall();

        private void Start()
        {
            AvailablePickups.Add(this);
            pickupObject = GetComponent<CVRPickupObject>();
            if (TryGetComponent(out CVRInteractable cvrInteractable))
            {
                interactable = cvrInteractable;
                hasInteractable = true;
            }

            StartCall();
            ModConfig.RefreshPickupPage();
        }

        private void OnDestroy()
        {
            AvailablePickups.Remove(this);
            DestroyCall();
            ModConfig.RefreshPickupPage();
        }
    }

    public class SpawnablePickupWrapper : PickupWrapper
    {
        internal CVRSpawnable Spawnable;

        internal bool HasUpdatedByOwnerSyncValues;

        internal static readonly HashSet<CVRSpawnableValue.UpdatedBy> OwnerUpdatedBy = new()
        {
            CVRSpawnableValue.UpdatedBy.OwnerCurrentGrip,
            CVRSpawnableValue.UpdatedBy.OwnerCurrentTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerLeftGrip,
            CVRSpawnableValue.UpdatedBy.OwnerLeftTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerOppositeGrip,
            CVRSpawnableValue.UpdatedBy.OwnerOppositeTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerRightGrip,
            CVRSpawnableValue.UpdatedBy.OwnerRightTrigger,
        };

        public bool GrabbedByFollower(out FollowerController controller)
        {
            foreach (var followerController in FollowerController.FollowerControllers)
            {
                if (!followerController.IsGrabbedByMyLeftHand(this) && !followerController.IsGrabbedByMyRightHand(this)) continue;
                controller = followerController;
                return true;
            }

            controller = null;
            return false;
        }

        protected override void StartCall()
        {
            Spawnable = GetComponent<CVRSpawnable>();
            HasUpdatedByOwnerSyncValues = Spawnable.syncValues.Exists(s => OwnerUpdatedBy.Contains(s.updatedBy));
            AvailableSpawnablePickups.Add(this);
        }

        protected override void DestroyCall()
        {
            AvailableSpawnablePickups.Remove(this);
        }
    }

    public class ObjectSyncPickupWrapper : PickupWrapper
    {
        public CVRObjectSync objectSync;

        protected override void StartCall()
        {
            objectSync = GetComponent<CVRObjectSync>();
            AvailableObjectSyncPickups.Add(this);
        }

        protected override void DestroyCall()
        {
            AvailableObjectSyncPickups.Remove(this);
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPickupObject), nameof(CVRPickupObject.Start))]
        public static void After_CVRPickupObject_Start(CVRPickupObject __instance)
        {
            // Add our wrapper (with the correct type) to all pickups
            try
            {
                if (__instance.TryGetComponent<CVRObjectSync>(out _))
                {
                    __instance.gameObject.AddComponent<ObjectSyncPickupWrapper>();
                }
                else if (__instance.TryGetComponent<CVRSpawnable>(out _))
                {
                    __instance.gameObject.AddComponent<SpawnablePickupWrapper>();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error(e);
            }
        }
    }
}
