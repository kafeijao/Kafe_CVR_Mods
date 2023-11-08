using UnityEngine;

namespace Kafe.NavMeshFollower;

public static class SyncedParamNames {

    // Agent info
    public const string MovementX = "MovementX";
    public const string MovementY = "MovementY";
    public const string Grounded = "Grounded";
    public const string Idle = "Idle";

    // Mod info
    public const string HasMod = "HasNavMeshFollowerMod";
    public const string IsBakingNavMesh = "IsBakingNavMesh";
    public const string IsInitialized = "IsInitialized";

    // VRIK info
    public const string VRIKLeftArm = "VRIK/LeftArm/Weight";
    public const string VRIKRightArm = "VRIK/RightArm/Weight";

    // Behaviors
    public const string BehaviorCurrent = "Behavior/Current";
    public const string BehaviorState = "Behavior/State";
    public const string BehaviorHasTarget = "Behavior/HasTarget";
    public const string BehaviorIsTargetPlayer = "Behavior/IsTargetPlayer";
    public const string BehaviorIsTargetPlayerSpawner = "Behavior/IsTargetPlayerSpawner";
    public const string BehaviorTargetDistance = "Behavior/TargetDistance";
    public const string BehaviorDestinationDistance = "Behavior/DestinationDistance";
    public const string BehaviorIsHoldingPickup = "Behavior/IsHoldingPickup";
}

public class FollowerParamInfo {
    private readonly FollowerController _controller;

    private readonly int[] _spawnableIndexes;

    private readonly int _localParamHash;
    private readonly Dictionary<Animator, AnimatorControllerParameterType> _localParamTypes = new();

    private FollowerParamInfo(FollowerController controller, Animator[] allAnimators, string syncedValueName) {
        _controller = controller;

        // Cache all the spawnable indexes for this synced value name
        _spawnableIndexes = controller.Spawnable.syncValues
            .Select((value, index) => new { value, index })
            .Where(pair => pair.value.name == syncedValueName)
            .Select(pair => pair.index)
            .ToArray();

        // Calculate the animator parameter hash for the local parameter counterpart, and cache their types
        _localParamHash = Animator.StringToHash("#" + syncedValueName);
        foreach (var animator in allAnimators) {
            if (animator == null) continue;
            var foundParam = animator.parameters.FirstOrDefault(p => p.nameHash == _localParamHash);
            if (foundParam == null) continue;
            _localParamTypes[animator] = foundParam.type;
        }
    }

    internal static void InitializeParameter(FollowerController controller, Animator[] allAnimators,
        string syncedValueName) {
        controller.Parameters[syncedValueName] = new FollowerParamInfo(controller, allAnimators, syncedValueName);
    }

    internal static bool HasSyncedParameter(FollowerController controller, string syncedValueName) =>
        controller.Parameters.TryGetValue(syncedValueName, out var paramInfo) && paramInfo._spawnableIndexes.Length > 0;

    internal static void SetParameter(FollowerController controller, string syncedValueName, float value) {
        var paramInfo = controller.Parameters[syncedValueName];

        foreach (var spawnableIndex in paramInfo._spawnableIndexes) {
            paramInfo._controller.Spawnable.SetValue(spawnableIndex, value);
        }

        foreach (var controllerParameterType in paramInfo._localParamTypes) {
            switch (controllerParameterType.Value) {
                case AnimatorControllerParameterType.Float:
                    controllerParameterType.Key.SetFloat(paramInfo._localParamHash, value);
                    break;
                case AnimatorControllerParameterType.Int:
                    controllerParameterType.Key.SetInteger(paramInfo._localParamHash, (int)value);
                    break;
                case AnimatorControllerParameterType.Bool:
                    controllerParameterType.Key.SetBool(paramInfo._localParamHash, value >= 0.5);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    if (value >= 0.5) controllerParameterType.Key.SetTrigger(paramInfo._localParamHash);
                    break;
            }
        }
    }
}
