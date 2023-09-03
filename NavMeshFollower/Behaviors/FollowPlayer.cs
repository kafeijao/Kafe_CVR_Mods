using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using UnityEngine;

namespace Kafe.NavMeshFollower.Behaviors;

public class FollowPlayer : Behavior {

    public static readonly List<FollowPlayer> FollowPlayerInstances = new();

    private string _playerGuidToFollow;

    public bool IsFollowing { get; private set; }

    static FollowPlayer() {
        // Stop from following a player when they leave
        CVRGameEventSystem.Player.OnLeave.AddListener(descriptor => {
            foreach (var followPlayerInstance in FollowPlayerInstances) {
                if (followPlayerInstance.IsFollowing && descriptor.ownerId == followPlayerInstance._playerGuidToFollow) {
                    followPlayerInstance.IsFollowing = false;
                }
            }
        });
    }

    public FollowPlayer(FollowerController controller, bool isToggleable, string description) : base(controller, isToggleable, description) {
        FollowPlayerInstances.Add(this);
    }

    public string GetFollowingPlayerGuid() => _playerGuidToFollow;

    public string GetSpawnableGuid() => Controller.Spawnable.guid;

    public void ClearTarget() {
        IsFollowing = false;
    }

    public void SetTarget(string guid) {
        if (guid == MetaPort.Instance.ownerId) {
            IsFollowing = true;
            _playerGuidToFollow = MetaPort.Instance.ownerId;
            return;
        }
        var player = CVRPlayerManager.Instance.NetworkPlayers.Find(p => p.Uuid == guid);
        if (player != null) {
            IsFollowing = true;
            _playerGuidToFollow = player.Uuid;
            return;
        }
        IsFollowing = false;
    }

    public override bool Handle(
        ref Vector3? destinationPos,
        ref Vector3? lookAtPos,
        ref Vector3? leftArmTargetPos,
        ref Vector3? rightArmTargetPos,
        ref bool? isIdle) {

        if (!IsFollowing) return false;

        if (!FollowerController.TryGetPlayerPos(_playerGuidToFollow, out var possibleDestPos)) {
            IsFollowing = false;
            return false;
        }

        if (!FollowerController.TryGetPlayerViewPoint(_playerGuidToFollow, out var possibleLookAtPos)) {
            IsFollowing = false;
            return false;
        }

        destinationPos = possibleDestPos;
        lookAtPos = possibleLookAtPos;

        return true;
    }

    public override void OnDestroyed() {
        FollowPlayerInstances.Remove(this);
    }

    public override bool IsEnabled() {
        return IsFollowing;
    }

    public override string GetStatus() {
        if (!IsEnabled()) return "Follow Player";
        var targetName = MetaPort.Instance.ownerId == _playerGuidToFollow
            ? AuthManager.username
            : CVRPlayerManager.Instance.TryGetPlayerName(_playerGuidToFollow);
        return $"Following {targetName}";
    }
}
