using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.NavMeshFollower.Behaviors;
using Kafe.NavMeshFollower.CCK;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshFollower;

[DefaultExecutionOrder(999999)]
public class FollowerController : MonoBehaviour {

    private static Action<string, CVRSpawnable> _onSpawnableStart;

    internal static readonly HashSet<FollowerController> FollowerControllers = new();

    private static readonly Dictionary<string, NavMeshTools.API.Agent> AgentCache = new();

    private static readonly int AnimatorPropertySpawnedByMe = Animator.StringToHash("#SpawnedByMe");

    internal static void Initialize() {

        _onSpawnableStart += (spawnerUserId, spawnable) => {
            try {

                // Handle FollowerInfo
                if (spawnable.TryGetComponent<FollowerInfo>(out var followerInfo)) {

                    // Setup our nav mesh agents
                    if (spawnerUserId == MetaPort.Instance.ownerId) {
                        HandleFollowerInfo(spawnable, followerInfo);
                    }

                    // Setup other people's nav mesh agents
                    else {

                        // Place a nav mesh obstacle with the agent's size on remote followers
                        if (followerInfo.navMeshAgent != null) {
                            var obstacle = followerInfo.navMeshAgent.gameObject.AddComponent<NavMeshObstacle>();
                            var halfHeight = followerInfo.navMeshAgent.height / 2f;
                            var radius = followerInfo.navMeshAgent.radius;
                            obstacle.shape = NavMeshObstacleShape.Capsule;
                            obstacle.center = new Vector3(0f, halfHeight - followerInfo.navMeshAgent.baseOffset, 0f);
                            obstacle.size = new Vector3(radius, halfHeight, radius);
                        }
                    }

                }
            }
            catch (Exception e) {
                MelonLogger.Warning("Attempted to load a Follower but it has thrown an error. There might be something broken it its setup.");
                MelonLogger.Warning(e);
            }
        };
    }

    private static void HandleFollowerInfo(CVRSpawnable spawnable, FollowerInfo followerInfo) {

            var spawnableGuid = spawnable.guid;

            if (followerInfo.spawnable == null) {
                MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.spawnable)}, ignoring it.");
                return;
            }
            if (followerInfo.navMeshAgent == null) {
                MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.navMeshAgent)}, ignoring it.");
                return;
            }
            if (followerInfo.hasLookAt) {
                if (followerInfo.lookAtTargetTransform == null) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.lookAtTargetTransform)}, ignoring it.");
                    return;
                }
                if (followerInfo.headTransform == null) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.headTransform)}, ignoring it.");
                    return;
                }
            }
            if (followerInfo.hasLeftArmIK || followerInfo.hasRightArmIK) {
                if (followerInfo.humanoidAnimator == null) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.humanoidAnimator)}, ignoring it.");
                    return;
                }
                if (!followerInfo.humanoidAnimator.isHuman) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a non-humanoid {nameof(followerInfo.humanoidAnimator)}, ignoring it.");
                    return;
                }
            }
            if (followerInfo.hasLeftArmIK) {
                if (followerInfo.vrikLeftArmTargetTransform == null) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.vrikLeftArmTargetTransform)}, ignoring it.");
                    return;
                }
            }
            if (followerInfo.hasRightArmIK) {
                if (followerInfo.vrikRightArmTargetTransform == null) {
                    MelonLogger.Warning($"The prop {spawnableGuid} has a null {nameof(followerInfo.vrikRightArmTargetTransform)}, ignoring it.");
                    return;
                }
            }

            // Initialize local animator parameters in all animators
            var allAnimators = spawnable.GetComponentsInChildren<Animator>(true);
            foreach (var animator in allAnimators) {
                animator.SetBool(AnimatorPropertySpawnedByMe, true);
            }

            var navMeshAgent = followerInfo.navMeshAgent;

            // Create or get an agent from cache
            if (!AgentCache.TryGetValue(spawnableGuid, out var agent)) {
                var navMeshAgentHeight = navMeshAgent.height;
                agent = new NavMeshTools.API.Agent(
                    navMeshAgent.radius,
                    navMeshAgentHeight,
                    45f,
                    navMeshAgentHeight * .99f,
                    .5f,
                    false,
                    0.2f,
                    false,
                    256
                );
                AgentCache[spawnableGuid] = agent;
                MelonLogger.Msg($"Detected a new follower prop {spawnableGuid}, caching the agent settings...");
            }
            else {
                MelonLogger.Msg($"Detected previously used follower prop {spawnableGuid}, using the cached nav mesh agents settings...");
            }

            // We need to associate our Nav Mesh Agent with the agentTypeID we baked the mesh for
            navMeshAgent.agentTypeID = agent.AgentTypeID;

            // Enable the nav agent (because we're controlling it)
            navMeshAgent.enabled = true;

            // Initialize the controller
            var controller = spawnable.gameObject.AddComponent<FollowerController>();
            controller.enabled = false;
            controller.Spawnable = spawnable;
            controller.Agent = navMeshAgent;
            controller.BakeAgent = agent;
            controller._stoppingDistance = navMeshAgent.stoppingDistance;
            controller._humanoidAnimator = followerInfo.humanoidAnimator;
            controller._allAnimators = allAnimators;
            var navMeshTransform = navMeshAgent.transform;
            controller.RootControllerRay = Utils.GetFakeControllerRay(
                navMeshTransform,
                navMeshTransform.position + Vector3.up * navMeshAgent.height * 0.8f + navMeshTransform.forward * navMeshAgent.radius,
                out controller.RootControllerRayOffsetPos,
                out controller.RootControllerRayOffsetRot);

            // Initialize state machine behaviors for the animators
            foreach (var animator in allAnimators) {
                foreach (var followerStateMachine in animator.GetBehaviours<FollowerStateMachine>()) {
                    followerStateMachine.Initialize(controller);
                }
            }

            // Look at stuff
            controller.HasLookAt = followerInfo.hasLookAt;
            controller._lookAtTargetTransform = followerInfo.lookAtTargetTransform;
            controller._lookAtHeadTransform = followerInfo.headTransform;
            if (controller.HasLookAt) {
                var offset = navMeshTransform.position with { y = 0 } + (navMeshAgent.transform.forward * navMeshAgent.radius * 2f) with { y = controller._lookAtHeadTransform.position.y };
                controller.HeadControllerRay = Utils.GetFakeControllerRay(controller._lookAtHeadTransform, offset, out controller._headControllerRayOffsetPos, out controller._headControllerRayOffsetRot);
            }

            #if DEBUG
            Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(followerInfo.lookAtTargetTransform.gameObject, "LookAtTransform");
            Kafe.CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(followerInfo.headTransform.gameObject, "LookAtSmoothedTransform");
            #endif

            // Spawnable synced params
            controller.InitializeParameter(SyncedParamNames.MovementY);
            controller.InitializeParameter(SyncedParamNames.MovementX);
            controller.InitializeParameter(SyncedParamNames.Grounded);
            controller.InitializeParameter(SyncedParamNames.Idle);

            controller.InitializeParameter(SyncedParamNames.HasMod);
            controller.InitializeParameter(SyncedParamNames.IsBakingNavMesh);
            controller.InitializeParameter(SyncedParamNames.IsInitialized);

            controller.InitializeParameter(SyncedParamNames.VRIKLeftArm);
            controller.InitializeParameter(SyncedParamNames.VRIKRightArm);

            // VRIK Left Arm
            if (followerInfo.hasLeftArmIK && controller.HasSyncedParameter(SyncedParamNames.VRIKLeftArm)) {
                controller._vrikLeftArmTargetTransform = followerInfo.vrikLeftArmTargetTransform;
                controller.LeftHandAttachmentPoint = followerInfo.leftHandAttachmentPoint != null ? followerInfo.leftHandAttachmentPoint : controller._humanoidAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                controller.LeftArmControllerRay = Utils.GetFakeControllerRay(controller.LeftHandAttachmentPoint, controller.LeftHandAttachmentPoint.position, out controller._leftArmControllerRayOffsetPos, out controller._leftArmControllerRayOffsetRot);
                controller.HasLeftArmIK = true;
            }

            // VRIK Right Arm
            if (followerInfo.hasRightArmIK && controller.HasSyncedParameter(SyncedParamNames.VRIKRightArm)) {
                controller._vrikRightArmTargetTransform = followerInfo.vrikRightArmTargetTransform;
                controller._rightHandAttachmentPoint = followerInfo.rightHandAttachmentPoint != null ? followerInfo.rightHandAttachmentPoint : controller._humanoidAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                controller.RightArmControllerRay = Utils.GetFakeControllerRay(controller._rightHandAttachmentPoint, controller._rightHandAttachmentPoint.position, out controller._rightArmControllerRayOffsetPos, out controller._rightArmControllerRayOffsetRot);
                controller._hasRightArmIK = true;
            }

            // Set animator mod details
            controller.SetParameter(SyncedParamNames.HasMod, 1f);
            controller.SetParameter(SyncedParamNames.IsBakingNavMesh, 1f);

            var calculateMeshLinks = ModConfig.MeBakeNavMeshEverytimeFollowerSpawned.Value;

            NavMeshTools.API.BakeCurrentWorldNavMesh(agent, (agentTypeID, success) => {

                MelonLogger.Msg($"Finished baking the NavMeshData for {spawnableGuid}");

                if (spawnable == null || controller == null) {
                    MelonLogger.Warning($"The spawnable {spawnableGuid} doesn't exist anymore... Probably deleted while baking...");
                    return;
                }

                if (!success) {
                    MelonLogger.Warning($"The bake for {spawnableGuid} has failed for some reason... This Pet won't work as it should...");
                    return;
                }

                controller.enabled = true;

                // Add the behaviors
                controller.Behaviors.Add(new FetchPickup(controller, true, "Fetches a pickup for a player."));

                // Make it follow us behavior on by default
                var followController = new FollowPlayer(controller, true, "Follows a player.");
                followController.SetTarget(MetaPort.Instance.ownerId);
                controller.LastHandledBehavior = followController;

                controller.Behaviors.Add(followController);
                controller.Behaviors.Add(new PlayFetch(controller, true, "Plays fetch with a pickup."));

                // Last
                controller.Behaviors.Add(new LookAtClosesPlayer(controller, false, "Just idles looking at the closest player."));

                // Initialize behavior Parameters
                controller.InitializeParameter(SyncedParamNames.BehaviorCurrent);
                controller.InitializeParameter(SyncedParamNames.BehaviorState);
                controller.InitializeParameter(SyncedParamNames.BehaviorHasTarget);
                controller.InitializeParameter(SyncedParamNames.BehaviorIsTargetPlayer);
                controller.InitializeParameter(SyncedParamNames.BehaviorIsTargetPlayerSpawner);
                controller.InitializeParameter(SyncedParamNames.BehaviorTargetDistance);
                controller.InitializeParameter(SyncedParamNames.BehaviorDestinationDistance);
                controller.InitializeParameter(SyncedParamNames.BehaviorIsHoldingPickup);

                controller.SetParameter(SyncedParamNames.IsBakingNavMesh, 0f);

            }, calculateMeshLinks);
    }

    internal NavMeshTools.API.Agent BakeAgent;
    internal CVRSpawnable Spawnable;
    internal NavMeshAgent Agent;

    private bool _initialized;
    public bool Initialized {
        get => _initialized;
        private set {
            _initialized = value;
            SetParameter(SyncedParamNames.IsInitialized, _initialized ? 1f : 0f);
        }
    }

    internal readonly Dictionary<string, FollowerParamInfo> Parameters = new();

    private float _stoppingDistance;
    private Animator _humanoidAnimator;
    private Animator[] _allAnimators;
    internal ControllerRay RootControllerRay;
    internal Vector3 RootControllerRayOffsetPos;
    internal Quaternion RootControllerRayOffsetRot;

    internal readonly List<Behavior> Behaviors = new();
    internal Behavior LastHandledBehavior;

    internal readonly Dictionary<CVRSpawnableValue.UpdatedBy, float> UpdatedByValues = new() {
        { CVRSpawnableValue.UpdatedBy.OwnerCurrentGrip,     0f },
        { CVRSpawnableValue.UpdatedBy.OwnerCurrentTrigger,  0f },
        { CVRSpawnableValue.UpdatedBy.OwnerLeftGrip,        0f },
        { CVRSpawnableValue.UpdatedBy.OwnerLeftTrigger,     0f },
        { CVRSpawnableValue.UpdatedBy.OwnerOppositeGrip,    0f },
        { CVRSpawnableValue.UpdatedBy.OwnerOppositeTrigger, 0f },
        { CVRSpawnableValue.UpdatedBy.OwnerRightGrip,       0f },
        { CVRSpawnableValue.UpdatedBy.OwnerRightTrigger,    0f },
    };

    internal bool HasLookAt;
    internal ControllerRay HeadControllerRay;
    private Vector3 _headControllerRayOffsetPos;
    private Quaternion _headControllerRayOffsetRot;
    private Transform _lookAtTargetTransform;
    private Transform _lookAtHeadTransform;

    // VRIK Left Arm
    internal bool HasLeftArmIK;
    internal ControllerRay LeftArmControllerRay;
    private Vector3 _leftArmControllerRayOffsetPos;
    private Quaternion _leftArmControllerRayOffsetRot;
    internal Transform LeftHandAttachmentPoint;
    private Transform _vrikLeftArmTargetTransform;

    // VRIK Right Arm
    private bool _hasRightArmIK;
    internal ControllerRay RightArmControllerRay;
    private Vector3 _rightArmControllerRayOffsetPos;
    private Quaternion _rightArmControllerRayOffsetRot;
    private Transform _rightHandAttachmentPoint;
    private Transform _vrikRightArmTargetTransform;

    public static bool TryGetPlayerPos(string guid, out Vector3 pos) {
        if (guid == MetaPort.Instance.ownerId) {
            pos = PlayerSetup.Instance.GetPlayerPosition();
            return true;
        }
        var player = CVRPlayerManager.Instance.NetworkPlayers.Find(p => p.Uuid == guid);
        if (player != null && player.AvatarHolder != null) {
            pos = player.AvatarHolder.transform.position;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    public static bool TryGetPlayerViewPoint(string guid, out Vector3 pos) {
        if (guid == MetaPort.Instance.ownerId) {
            pos = NavMeshFollower.PlayerViewpoints[MetaPort.Instance.ownerId];
            return true;
        }
        if (NavMeshFollower.PlayerViewpoints.TryGetValue(guid, out var point)) {
            pos = point;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    public static string GetClosestPlayerGuid(Vector3 source) {
        var closestPlayerGuid = "";
        var closestDistance = float.MaxValue;
        foreach (var entry in NavMeshFollower.PlayerViewpoints) {
            var currentDistance = Vector3.Distance(source, entry.Value);
            if (!(currentDistance < closestDistance)) continue;
            closestDistance = currentDistance;
            closestPlayerGuid = entry.Key;
        }
        return closestPlayerGuid;
    }

    private void HandleLeftArm(Vector3? targetPos) {
        if (!HasLeftArmIK) return;
        SetParameter(SyncedParamNames.VRIKLeftArm, targetPos.HasValue ? 1f : 0f);
        if (targetPos.HasValue) _vrikLeftArmTargetTransform.position = targetPos.Value;
    }

    private void HandleRightArm(Vector3? targetPos) {
        if (!_hasRightArmIK) return;
        SetParameter(SyncedParamNames.VRIKRightArm, targetPos.HasValue ? 1f : 0f);
        if (targetPos.HasValue) _vrikRightArmTargetTransform.position = targetPos.Value;
    }

    public bool HasArrived() {
        if (Agent.pathPending) return false;
        if (!(Agent.remainingDistance <= Agent.stoppingDistance)) return false;
        return !Agent.hasPath || Agent.velocity.sqrMagnitude == 0f;
    }

    public ControllerRay GetRayController(bool isRightHand, out Vector3 offsetPos, out Quaternion offsetRot) {
        if (isRightHand && _hasRightArmIK) {
            offsetPos = _rightArmControllerRayOffsetPos;
            offsetRot = _rightArmControllerRayOffsetRot;
            return RightArmControllerRay;
        }
        if (!isRightHand && HasLeftArmIK) {
            offsetPos = _leftArmControllerRayOffsetPos;
            offsetRot = _leftArmControllerRayOffsetRot;
            return LeftArmControllerRay;
        }
        if (HasLookAt) {
            offsetPos = _headControllerRayOffsetPos;
            offsetRot = _headControllerRayOffsetRot;
            return HeadControllerRay;
        }
        offsetPos = RootControllerRayOffsetPos;
        offsetRot = RootControllerRayOffsetRot;
        return RootControllerRay;
    }

    private static void ResetRayControllerOffsets(ControllerRay controllerRay, Vector3 posOffset, Quaternion rotOffset) {
        var controllerRayTransform = controllerRay.transform;
        controllerRayTransform.localPosition = Vector3.zero;
        controllerRayTransform.localRotation = Quaternion.identity;
        controllerRay.attachmentPoint.transform.localPosition = posOffset;
        controllerRay.attachmentPoint.transform.localRotation = rotOffset;
    }

    private void GrabPickup(Pickups.PickupWrapper pickup, bool isRightHand) {
        if (pickup == null || pickup.pickupObject == null) return;
        var controllerRay = GetRayController(isRightHand, out var posOffset, out var rotOffset);
        ResetRayControllerOffsets(controllerRay, posOffset, rotOffset);
        var attachPoint = controllerRay.attachmentPoint.transform.position;
        pickup.pickupObject.transform.position = attachPoint;
        pickup.pickupObject.Grab(null, controllerRay, attachPoint);
        ResetRayControllerOffsets(controllerRay, posOffset, rotOffset);
        pickup.pickupObject.initialPositionOffset = Vector3.zero;
        pickup.pickupObject.initialRotationalOffset = Quaternion.identity;
        pickup.pickupObject.initialRayRotationOffset = Quaternion.identity;
        foreach (var key in Pickups.SpawnablePickupWrapper.OwnerUpdatedBy) {
            UpdatedByValues[key] = 1f;
        }
    }

    private void DropPickup(Pickups.PickupWrapper pickup, bool isRightHand) {
        foreach (var key in Pickups.SpawnablePickupWrapper.OwnerUpdatedBy) {
            UpdatedByValues[key] = 0f;
        }
        if (pickup == null || pickup.pickupObject == null || isRightHand && !IsGrabbedByMyRightHand(pickup) || (!isRightHand && !IsGrabbedByMyLeftHand(pickup))) return;
        pickup.pickupObject.Drop();
    }

    public void GrabPickupLeftHand(Pickups.PickupWrapper pickup) => GrabPickup(pickup, false);
    public void GrabPickupRightHand(Pickups.PickupWrapper pickup) => GrabPickup(pickup, true);
    public void DropPickupLeftHand(Pickups.PickupWrapper pickup) => DropPickup(pickup, false);
    public void DropPickupRightHand(Pickups.PickupWrapper pickup) => DropPickup(pickup, true);
    public static bool IsGrabbed(Pickups.PickupWrapper pickup) => pickup != null && pickup.pickupObject != null && pickup.pickupObject.grabbedBy != "";
    public bool IsGrabbedByMyLeftHand(Pickups.PickupWrapper pickup) => IsGrabbed(pickup) && GetRayController(false, out _, out _) == pickup.pickupObject._controllerRay;
    public bool IsGrabbedByMyRightHand(Pickups.PickupWrapper pickup) => IsGrabbed(pickup) && GetRayController(true, out _, out _) == pickup.pickupObject._controllerRay;


    internal void InitializeParameter(string syncedValueName) =>
        FollowerParamInfo.InitializeParameter(this, _allAnimators, syncedValueName);
    internal bool HasSyncedParameter(string syncedValueName) =>
        FollowerParamInfo.HasSyncedParameter(this, syncedValueName);
    internal void SetParameter(string syncedValueName, float value) =>
        FollowerParamInfo.SetParameter(this, syncedValueName, value);

    private void Update() {

        Vector3? destinationPos = null;
        Vector3? lookAtPos = null;
        Vector3? leftArmTargetPos = null;
        Vector3? rightArmTargetPos = null;
        bool? isIdle = null;

        // Fix Stopping Distance if it changed via some behavior
        if (!Mathf.Approximately(Agent.stoppingDistance, _stoppingDistance)) Agent.stoppingDistance = _stoppingDistance;

        // Iterate behaviors until one returns true (which means it ran and it was handled)
        foreach (var behavior in Behaviors) {
            if (!behavior.Handle(ref destinationPos, ref lookAtPos, ref leftArmTargetPos, ref rightArmTargetPos, ref isIdle)) continue;

            LastHandledBehavior = behavior;

            // Handle the destination
            if (destinationPos.HasValue) {
                // If the path is being calculated -> skip this frame. Otherwise set the destination!
                if (!Agent.pathPending) {
                    if (Agent.isStopped) Agent.isStopped = false;
                    Agent.SetDestination(destinationPos.Value);
                }
            }
            else if (!Agent.isStopped) {
                Agent.isStopped = true;
            }

            // Handle the look at
            if (HasLookAt) {
                if (lookAtPos.HasValue) {
                    // Stare at the target, or if walking a path star to the next corner of the path we need to cut
                    _lookAtTargetTransform.position = Agent.hasPath && Vector3.Distance(Agent.steeringTarget, Agent.pathEndPosition) > 0.1f
                        ? Agent.steeringTarget with { y = _lookAtHeadTransform.position.y }
                        : lookAtPos.Value;

                }
                else {
                    // Stare to the horizon!
                    var navMeshAgentTransform = Agent.transform;
                    _lookAtTargetTransform.position = (navMeshAgentTransform.position + navMeshAgentTransform.forward) with { y = _lookAtHeadTransform.position.y };
                }
            }

            // Handle Arms IK
            HandleLeftArm(leftArmTargetPos);
            HandleRightArm(rightArmTargetPos);

            // Handle Parameters
            SetParameter(SyncedParamNames.BehaviorCurrent, behavior.GetId());
            SetParameter(SyncedParamNames.BehaviorState, behavior.GetState());
            SetParameter(SyncedParamNames.BehaviorHasTarget, destinationPos.HasValue ? 1 : 0);
            SetParameter(SyncedParamNames.BehaviorIsTargetPlayer, behavior.IsTargetPlayer() ? 1 : 0);
            SetParameter(SyncedParamNames.BehaviorIsTargetPlayerSpawner, behavior.IsTargetPlayerSpawner() ? 1 : 0);
            var targetDistance = destinationPos.HasValue ? Vector3.Distance(Agent.transform.position, destinationPos.Value) : 0;
            SetParameter(SyncedParamNames.BehaviorTargetDistance, targetDistance);
            var destinationDistance = Math.Max(Agent.remainingDistance - Agent.stoppingDistance, 0);
            SetParameter(SyncedParamNames.BehaviorDestinationDistance, destinationDistance);
            SetParameter(SyncedParamNames.BehaviorIsHoldingPickup, behavior.IsHoldingPickup() ? 1 : 0);

            break;
        }

        var localVelocity = Agent.transform.InverseTransformDirection(Agent.velocity);

        // Set the spawnable parameters
        SetParameter(SyncedParamNames.MovementY, localVelocity.z / Agent.speed);
        SetParameter(SyncedParamNames.MovementX, localVelocity.x / Agent.speed);
        SetParameter(SyncedParamNames.Grounded, !Agent.isOnOffMeshLink && Agent.isOnNavMesh ? 1f : 0f);
        SetParameter(SyncedParamNames.Idle, Mathf.Approximately(localVelocity.magnitude, 0f) && (!isIdle.HasValue || isIdle.Value) ? 1f : 0f);

        // Keep the prop synced by us
        Spawnable.needsUpdate = true;
    }



    internal T GetBehavior<T>() {
        foreach (var behavior in Behaviors) {
            if (behavior is T b) return b;
        }
        throw new Exception($"All followers must have all behaviors. Requested behavior: {typeof(T)}");
    }

    private void Start() {
        FollowerControllers.Add(this);
        ModConfig.UpdateMainPage();
        Initialized = true;
    }

    private void OnDestroy() {
        FollowerControllers.Remove(this);
        foreach (var behavior in Behaviors) {
            behavior.OnDestroyed();
        }
        ModConfig.UpdateMainPage();
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRSpawnable), nameof(CVRSpawnable.Start))]
        public static void After_CVRSpawnable_Start(CVRSpawnable __instance) {
            // After the spawnable actually started
            try {
                _onSpawnableStart?.Invoke(__instance.ownerId, __instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRSpawnable_Start)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
