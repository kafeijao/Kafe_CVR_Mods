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

    protected override bool IsClappable()
    {
        if (TheClapper.DisableClappingAvatars.Value) return false;

        // When the avatar is hidden bypass the friend check
        bool avatarIsHidden = _puppetMaster.IsAvatarBlocked;
        if (TheClapper.AlwaysAllowClappingHiddenAvatars.Value && avatarIsHidden) return true;

        if (TheClapper.PreventClappingFriendsAvatars.Value && Friends.FriendsWith(_playerId)) return false;

        return true;
    }

    protected override void OnClapped(Vector3 clappablePosition) {

        var isHidden = _puppetMaster.IsAvatarBlocked;

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
