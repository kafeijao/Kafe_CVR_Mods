using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Kafe.NavMeshFollower.Behaviors;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower.CCK;

[Serializable]
public class FollowerBehaviorControl : FollowerStateMachine {

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (!IsInitialized) {
            Destroy(this);
            return;
        }
        if (!onEnter) return;
        MelonLogger.Msg($"[FollowerBehaviorControl.OnStateEnter] Executing...");
        if (!Controller.Initialized) {
            MelonLogger.Warning($"[Attempted to use run a ${nameof(FollowerBehaviorControl)}] before the Follower is fully initialized (you can use the Parameter {SyncedParamNames.IsInitialized}).");
            return;
        }
        enterTask.Execute(Controller);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (!IsInitialized) {
            Destroy(this);
            return;
        }
        if (!onExit) return;
        MelonLogger.Msg($"[FollowerBehaviorControl.OnStateExit] Executing...");
        if (!Controller.Initialized) {
            MelonLogger.Warning($"[Attempted to use run a ${nameof(FollowerBehaviorControl)}] before the Follower is fully initialized (you can use the Parameter {SyncedParamNames.IsInitialized}).");
            return;
        }
        exitTask.Execute(Controller);
    }

    [SerializeField] public FollowerBehaviorControlTask enterTask = new();
    [SerializeField] public FollowerBehaviorControlTask exitTask = new();

    [SerializeField] public bool onEnter = false;
    [SerializeField] public bool onExit = false;

    [Serializable]
    public class FollowerBehaviorControlTask
    {

        public void Execute(FollowerController controller) {

            string err;
            Behavior targetBehavior;

            MelonLogger.Msg($"[FollowerBehaviorControlTask] Picked behavior: {behavior.ToString()}, isEnabled: {isEnabled}, targetPlayer: {playerTarget.ToString()}, targetProp: {propTarget.ToString()}");

            switch (behavior) {
                case BehaviorType.PlayFetch:
                    var playFetchBehavior = controller.GetBehavior<PlayFetch>();
                    targetBehavior = playFetchBehavior;

                    if (isEnabled) {
                        Pickups.PickupWrapper fetchTarget;
                        switch (propTarget) {
                            case TargetPropType.ClosestProp:
                                if (!GetClosestProp(controller.Agent.transform.position, out fetchTarget)) return;
                                break;
                            case TargetPropType.HeldProp:
                                if (!GetHeldProp(out fetchTarget)) return;
                                break;
                            default:
                                err = $"Attempted to Execute a Behavior with a prop target that doesn't exist! {propTarget.ToString()}. This should never happen.";
                                MelonLogger.Error(err);
                                throw new ArgumentOutOfRangeException(err);
                        }
                        playFetchBehavior.StartPlayingFetch(fetchTarget);
                    }

                    break;
                case BehaviorType.FetchPickup:
                    var fetchPickupBehavior = controller.GetBehavior<FetchPickup>();
                    targetBehavior = fetchPickupBehavior;

                    if (isEnabled) {

                        Pickups.PickupWrapper fetchTarget;
                        switch (propTarget) {
                            case TargetPropType.ClosestProp:
                                if (!GetClosestProp(controller.Agent.transform.position, out fetchTarget)) return;
                                break;
                            case TargetPropType.HeldProp:
                                if (!GetHeldProp(out fetchTarget)) return;
                                break;
                            default:
                                err = $"Attempted to Execute a Behavior with a prop target that doesn't exist! {propTarget.ToString()}. This should never happen.";
                                MelonLogger.Error(err);
                                throw new ArgumentOutOfRangeException(err);
                        }

                        string followTarget;
                        switch (playerTarget) {
                            case TargetPlayerType.Spawner:
                                followTarget = MetaPort.Instance.ownerId;
                                break;
                            case TargetPlayerType.ClosestPlayer:
                                if (!GetClosestPlayer(controller.Agent.transform.position, out followTarget)) return;
                                break;
                            default:
                                err = $"Attempted to Execute a Behavior with a player target that doesn't exist! {playerTarget.ToString()}. This should never happen.";
                                MelonLogger.Error(err);
                                throw new ArgumentOutOfRangeException(err);
                        }

                        fetchPickupBehavior.FetchPickupTo(fetchTarget, followTarget);
                    }

                    break;
                case BehaviorType.FollowPlayer:
                    var followPlayerBehavior = controller.GetBehavior<FollowPlayer>();
                    targetBehavior = followPlayerBehavior;

                    if (isEnabled) {

                        string followTarget;
                        switch (playerTarget) {
                            case TargetPlayerType.Spawner:
                                followTarget = MetaPort.Instance.ownerId;
                                break;
                            case TargetPlayerType.ClosestPlayer:
                                if (!GetClosestPlayer(controller.Agent.transform.position, out followTarget)) return;
                                break;
                            default:
                                err = $"Attempted to Execute a Behavior with a player target that doesn't exist! {playerTarget.ToString()}. This should never happen.";
                                MelonLogger.Error(err);
                                throw new ArgumentOutOfRangeException(err);
                        }
                        followPlayerBehavior.SetTarget(followTarget);
                    }

                    break;
                default:
                    err = $"Attempted to Execute a Behavior that doesn't exist! {behavior.ToString()}. This should never happen.";
                    MelonLogger.Error(err);
                    throw new ArgumentOutOfRangeException(err);
            }

            // If we're turning it off, disable it
            if (!isEnabled) {
                targetBehavior.Disable();
            }

            // Disable all other behaviors
            targetBehavior.DisableAllBehaviorsExcept(targetBehavior);

            // Update the Menus
            ModConfig.UpdateFollowerControllerPage();
        }

        private bool GetClosestProp(Vector3 sourcePos, out Pickups.PickupWrapper pickupWrapper) {
            pickupWrapper = Pickups.AvailablePickups.OrderBy(p => Vector3.Distance(p.transform.position, sourcePos)).FirstOrDefault();
            return pickupWrapper != null;
        }

        private bool GetHeldProp(out Pickups.PickupWrapper pickupWrapper) {
            if (MetaPort.Instance.isUsingVr) {
                pickupWrapper = Pickups.AvailablePickups.FirstOrDefault(p =>
                    p.pickupObject == PlayerSetup.Instance.vrRayRight.grabbedObject ||
                    p.pickupObject == PlayerSetup.Instance.vrRayLeft.grabbedObject);
                return pickupWrapper != null;
            }
            else {
                pickupWrapper = Pickups.AvailablePickups.FirstOrDefault(p => p.pickupObject == PlayerSetup.Instance.desktopRay.grabbedObject);
                return pickupWrapper != null;
            }
        }

        private bool GetClosestPlayer(Vector3 sourcePos, out string playerGuid) {

            var closestRemotePlayer = CVRPlayerManager.Instance.NetworkPlayers
                .Where(np => np.PuppetMaster != null)
                .OrderBy(p => Vector3.Distance(p.PuppetMaster.netIkController.avatarHeadPosition, sourcePos))
                .FirstOrDefault();

            if (PlayerSetup.Instance.AvatarDescriptor != null && PlayerSetup.Instance.Animator != null) {
                var localHeadBonePos = PlayerSetup.Instance.Animator.GetBoneTransform(HumanBodyBones.Head).position;
                // There's only local player
                if (closestRemotePlayer == null) {
                    playerGuid = MetaPort.Instance.ownerId;
                    return true;
                }
                // Check whether local player is closer than closest remote player
                var localPlayerCloser = Vector3.Distance(localHeadBonePos, sourcePos) < Vector3.Distance(closestRemotePlayer.PuppetMaster.netIkController.avatarHeadPosition, sourcePos);
                playerGuid = localPlayerCloser ? MetaPort.Instance.ownerId : closestRemotePlayer.Uuid;
                return true;
            }

            // There's only remote players, if there's a valid closer one pick it
            playerGuid = closestRemotePlayer?.Uuid;
            return closestRemotePlayer == null;
        }

        [Serializable]
        public enum BehaviorType {
            FetchPickup,
            FollowPlayer,
            PlayFetch,
        }

        [Serializable]
        public enum TargetPlayerType {
            Spawner,
            ClosestPlayer,
        }

        [Serializable]
        public enum TargetPropType {
            ClosestProp,
            HeldProp,
        }

        [SerializeField] public BehaviorType behavior = BehaviorType.FollowPlayer;
        [SerializeField] public bool isEnabled;
        [SerializeField] public TargetPlayerType playerTarget = TargetPlayerType.Spawner;
        [SerializeField] public TargetPropType propTarget = TargetPropType.ClosestProp;
    }
}
