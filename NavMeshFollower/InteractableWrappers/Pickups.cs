using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower.InteractableWrappers;

public static class Pickups {

    public static readonly List<PickupWrapper> AvailablePickups = new();
    public static readonly List<SpawnablePickupWrapper> AvailableSpawnablePickups = new();
    public static readonly List<ObjectSyncPickupWrapper> AvailableObjectSyncPickups = new();

    public abstract class PickupWrapper : MonoBehaviour {

        public CVRPickupObject pickupObject;

        public bool hasInteractable;
        public CVRInteractable interactable;

        public abstract bool IsGrabbable();

        protected abstract void StartCall();
        protected abstract void DestroyCall();

        private void Start() {
            AvailablePickups.Add(this);
            pickupObject = GetComponent<CVRPickupObject>();
            if (TryGetComponent(out CVRInteractable cvrInteractable)) {
                interactable = cvrInteractable;
                hasInteractable = true;
            }
            StartCall();
            ModConfig.UpdatePickupList();
        }

        private void OnDestroy() {
            AvailablePickups.Remove(this);
            DestroyCall();
            ModConfig.UpdatePickupList();
        }
    }

    public class SpawnablePickupWrapper : PickupWrapper {

        internal CVRSpawnable Spawnable;

        internal bool HasUpdatedByOwnerSyncValues;

        internal static readonly HashSet<CVRSpawnableValue.UpdatedBy> OwnerUpdatedBy = new() {
            CVRSpawnableValue.UpdatedBy.OwnerCurrentGrip,
            CVRSpawnableValue.UpdatedBy.OwnerCurrentTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerLeftGrip,
            CVRSpawnableValue.UpdatedBy.OwnerLeftTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerOppositeGrip,
            CVRSpawnableValue.UpdatedBy.OwnerOppositeTrigger,
            CVRSpawnableValue.UpdatedBy.OwnerRightGrip,
            CVRSpawnableValue.UpdatedBy.OwnerRightTrigger,
        };

        public override bool IsGrabbable() => Spawnable.SyncType == 0
                                              // For some reason the SyncType doesn't go back to 0 when we drop it... So let's consider this grabbable!
                                              || Spawnable.SyncType == 1 && Spawnable.IsMine() && Spawnable.pickup != null && Spawnable.pickup.GrabbedBy == "";

        public bool GrabbedByFollower(out FollowerController controller) {
            // When our followers are grabbing it's as if we were grabbing it. So ignore all others.
            if (pickupObject == null || !pickupObject.IsGrabbedByMe|| pickupObject._controllerRay == null) {
                controller = null;
                return false;
            }
            var controllerRay = pickupObject._controllerRay;
            // Look for a follower matching the controller ray
            foreach (var followerController in FollowerController.FollowerControllers) {
                if (followerController.RootControllerRay != controllerRay
                    && followerController.HeadControllerRay != controllerRay
                    && followerController.LeftArmControllerRay != controllerRay
                    && followerController.RightArmControllerRay != controllerRay) continue;
                controller = followerController;
                return true;
            }
            controller = null;
            return false;
        }

        protected override void StartCall() {
            Spawnable = GetComponent<CVRSpawnable>();
            HasUpdatedByOwnerSyncValues = Spawnable.syncValues.Exists(s => OwnerUpdatedBy.Contains(s.updatedBy));
            AvailableSpawnablePickups.Add(this);
        }
        protected override void DestroyCall() {
            AvailableSpawnablePickups.Remove(this);
        }
    }

    public class ObjectSyncPickupWrapper : PickupWrapper {

        public CVRObjectSync objectSync;

        public override bool IsGrabbable() => objectSync.SyncType == 0;
        protected override void StartCall() {
            objectSync = GetComponent<CVRObjectSync>();
            AvailableObjectSyncPickups.Add(this);
        }
        protected override void DestroyCall() {
            AvailableObjectSyncPickups.Remove(this);
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPickupObject), nameof(CVRPickupObject.Start))]
        public static void After_CVRPickupObject_Start(CVRPickupObject __instance) {
            // Add our wrapper (with the correct type) to all pickups
            try {
                if (__instance.TryGetComponent<CVRObjectSync>(out _)) {
                    __instance.gameObject.AddComponent<ObjectSyncPickupWrapper>();
                }
                else if (__instance.TryGetComponent<CVRSpawnable>(out _)) {
                    __instance.gameObject.AddComponent<SpawnablePickupWrapper>();
                }
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }
    }
}
