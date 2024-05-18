using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using MelonLoader;
using UnityEngine;

namespace Kafe.TheClapper;

public class ClappableAvatar : Clappable {

    private string _playerId;
    private string _playerUserName;
    private PuppetMaster _puppetMaster;

    protected override bool IsClappable() {
        var isHidden = _puppetMaster._isBlocked;
        var isFriend = TheClapper.PreventClappingFriends.Value && Friends.FriendsWith(_playerId);
        return TheClapper.EnableClappingAvatars.Value && !isHidden && !isFriend;
    }

    protected override void OnClapped(Vector3 clappablePosition) {

        var isHidden = _puppetMaster._isBlocked;

        MelonLogger.Msg($"{(isHidden ? "Unclapped" : "Clapped")} {_playerUserName}'s avatar!");

        MetaPort.Instance.SelfModerationManager.SetPlayerAvatarVisibility(_playerId, isHidden);

        // Emit particles on the clappable
        TheClapper.EmitParticles(clappablePosition, new Color(0f, 1f, 1f), 3f);
    }

    public static void Create(PuppetMaster target, string playerId, string username, Animator animator) {

        if (!target.gameObject.TryGetComponent(out ClappableAvatar clappableAvatar)) {
            clappableAvatar = target.gameObject.AddComponent<ClappableAvatar>();
        }

        clappableAvatar._playerId = playerId;
        clappableAvatar._playerUserName = username;
        clappableAvatar._puppetMaster = target;

        if (animator && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Head)) {
            clappableAvatar.TransformToFollow = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        clappableAvatar.UpdateVisualizerTransform();
    }
}
