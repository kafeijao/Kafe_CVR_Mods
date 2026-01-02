using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.IO;
using ABI_RC.Systems.ContentClones;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using Kafe.MiniMePlusPlus.Properties;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kafe.MiniMePlusPlus;

public class RemoteMiniMe
{
    public static float NetworkInterval = 0.05f;

    private static readonly Dictionary<string, RemoteMiniMe> MiniMes = new Dictionary<string, RemoteMiniMe>();

    private Networking.RemoteMiniMeInfo _currentInfo;
    private Networking.RemoteMiniMeInfo _previousInfo;

    private ContentCloneManager.CloneData _remotePlayerClone;

    private GameObject _cloneHolderObject;

    private CVRAvatar _avatar;

    private float _interpolationTimer;

    public static void Initialize()
    {
        // Update the clone when detecting avatar updates
        CVRGameEventSystem.Avatar.OnRemoteAvatarLoad.AddListener((player, avatar) =>
        {
            if (!MiniMes.TryGetValue(player.Uuid, out var miniMe)) return;
            // Re-create avatar if we loaded a new avatar, and there is available info about the MiniMe
            if (miniMe._currentInfo.AttachType != Networking.AttachType.Invalid && avatar != miniMe._avatar)
                ShowFromNetwork(false, miniMe._currentInfo);
        });

        // Nuke clone without nuking data
        CVRGameEventSystem.Avatar.OnRemoteAvatarClear.AddListener((player, avatar) =>
        {
            if (!MiniMes.TryGetValue(player.Uuid, out var miniMe)) return;
            // Nuke clone without nuking data if the avatar that is being cloned was cleared
            if (avatar == miniMe._avatar)
                miniMe.DestroyClone();
        });

        LateEventsManager.OnPostLateUpdate += VeryLateUpdate;
    }

    private static void VeryLateUpdate()
    {
        foreach (RemoteMiniMe miniMe in MiniMes.Values)
            miniMe.UpdatePosition(false);
    }

    public static void NukeAll()
    {
        foreach (RemoteMiniMe miniMe in MiniMes.Values)
            miniMe.DestroyClone();
        MiniMes.Clear();
    }

    public static void ShowFromNetwork(bool isUnreliableUpdate, Networking.RemoteMiniMeInfo remoteMiniMeInfo)
    {
        bool changedAttachType = false;
        if (!MiniMes.TryGetValue(remoteMiniMeInfo.Player.Uuid, out var miniMe))
        {
            // Let's not use unreliable updates to initialize it
            if (isUnreliableUpdate) return;

            miniMe = new RemoteMiniMe
            {
                _currentInfo = remoteMiniMeInfo,
                _previousInfo = remoteMiniMeInfo,
                _avatar = remoteMiniMeInfo.Player.PuppetMaster.AvatarDescriptor,
            };
            MiniMes[remoteMiniMeInfo.Player.Uuid] = miniMe;
        }
        else
        {
            // Skip interpolation if changed attach type
            if (remoteMiniMeInfo.AttachType != miniMe._currentInfo.AttachType)
                changedAttachType = true;

            // Ignore unreliable updates if we changed attach type
            if (isUnreliableUpdate && changedAttachType)
                return;

            // Cycle the current and previous and reset the interpolation (unless we changed attach type)
            miniMe._previousInfo = changedAttachType ? remoteMiniMeInfo : miniMe._currentInfo;
            miniMe._currentInfo = remoteMiniMeInfo;
            miniMe._interpolationTimer = 0f;

            miniMe._avatar = remoteMiniMeInfo.Player.PuppetMaster.AvatarDescriptor;
        }

        // Only create mini me on reliable packets
        if (!isUnreliableUpdate)
            miniMe.CreateMiniMeIfNeeded();

        // Skip interpolation if we're changing attach type
        miniMe.UpdatePosition(changedAttachType);
    }

    public static void Hide(string playerId)
    {
        if (!MiniMes.TryGetValue(playerId, out var miniMe))
            return;

        miniMe.DestroyClone();
        MiniMes.Remove(playerId);
    }

    private void UpdatePosition(bool skipInterpolation)
    {
        if (!_cloneHolderObject || _remotePlayerClone == null || _remotePlayerClone.IsDestroyed)
            return;

        if (skipInterpolation)
        {
            ApplyTransform(
                _currentInfo.OffsetPosition,
                _currentInfo.OffsetRotation,
                _currentInfo.LocalScale
            );
        }
        else
        {
            _interpolationTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_interpolationTimer / NetworkInterval);

            ApplyTransform(
                Vector3.Lerp(_previousInfo.OffsetPosition, _currentInfo.OffsetPosition, t),
                Quaternion.Slerp(_previousInfo.OffsetRotation, _currentInfo.OffsetRotation, t),
                Mathf.Lerp(_previousInfo.LocalScale, _currentInfo.LocalScale, t)
            );
        }
    }

    private void ApplyTransform(Vector3 offsetPos, Quaternion offsetRot, float scale)
    {
        Transform transform = _cloneHolderObject.transform;

        switch (_currentInfo.AttachType)
        {
            case Networking.AttachType.World:
                transform.position = offsetPos;
                transform.rotation = offsetRot;
                break;

            case Networking.AttachType.PlaySpace:
                ApplyAttachedTransform(_currentInfo.Player.PuppetMaster.AvatarTransform, offsetPos, offsetRot);
                break;

            case Networking.AttachType.ViewPoint:
                ApplyAttachedTransform(_currentInfo.Player.PuppetMaster.GetViewTransform(), offsetPos, offsetRot);
                break;

            case Networking.AttachType.LeftHand:
                ApplyBoneTransform(HumanBodyBones.LeftHand, offsetPos, offsetRot);
                break;

            case Networking.AttachType.RightHand:
                ApplyBoneTransform(HumanBodyBones.RightHand, offsetPos, offsetRot);
                break;

            case Networking.AttachType.Invalid:
                #if DEBUG
                MelonLogger.Warning("Invalid attach mode!");
                #endif
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }

        transform.localScale = Vector3.one * scale;
    }


    private void ApplyAttachedTransform(Transform target, Vector3 pos, Quaternion rot)
    {
        if (!target) return;

        Transform t = _cloneHolderObject.transform;
        t.position = target.TransformPoint(pos);
        t.rotation = target.rotation * rot;
    }

    private void ApplyBoneTransform(HumanBodyBones bone, Vector3 pos, Quaternion rot)
    {
        var animator = _currentInfo.Player.PuppetMaster.Animator;

        if (!animator.isHuman)
        {
            #if DEBUG
            MelonLogger.Warning($"Attempted to attach to {bone}, but the animator is not human");
            #endif
            return;
        }

        Transform boneTransform = animator.GetBoneTransform(bone);
        if (!boneTransform)
        {
            #if DEBUG
            MelonLogger.Warning($"Attempted to attach to {bone}, but the bone was not found");
            #endif
            return;
        }

        ApplyAttachedTransform(boneTransform, pos, rot);
    }

    private void DestroyClone()
    {
        if (_remotePlayerClone != null && !_remotePlayerClone.IsDestroyed)
        {
            ContentCloneManager.DestroyClone(_remotePlayerClone);
            #if DEBUG
            MelonLogger.Msg($"Deleted MiniMe for {_currentInfo.Player?.Username}");
            #endif
        }
        _remotePlayerClone = null;

        if (_cloneHolderObject)
            Object.Destroy(_cloneHolderObject);
        _cloneHolderObject = null;
    }

    private void CreateMiniMeIfNeeded()
    {
        var player = _currentInfo.Player;
        if (player == null)
            return;

        var avatarObject = player.PuppetMaster.AvatarObject;
        if (!avatarObject)
            return;

        // Create the clone if needed
        if (!_cloneHolderObject || _remotePlayerClone == null || _remotePlayerClone.IsDestroyed)
        {
            DestroyClone();

            _remotePlayerClone = ContentCloneManager.CreateClone(avatarObject, ContentCloneManager.CloneOptions.WasmDefault);

            if (_remotePlayerClone == null)
            {
                #if DEBUG
                MelonLogger.Warning($"Failed to create MiniMe, failed to Create Clone for {player.Username}");
                #endif
                return;
            }

            // Create holder
            _cloneHolderObject = new GameObject($"{AssemblyInfoParams.Name}_{player.Uuid}");
            SceneManager.MoveGameObjectToScene(_cloneHolderObject, SceneManager.GetSceneByName(CVRObjectLoader.AdditiveContentSceneName));
            _cloneHolderObject.SetActive(false);

            Transform cloneRootTransform = _remotePlayerClone.CloneRootTransform;
            Transform holderTransform = _cloneHolderObject.transform;

            float avatarHeight = player.PuppetMaster.netIkController.GetRemoteHeight();
            Vector3 lossyScale = avatarObject.transform.lossyScale;
            float num = 0.5f / avatarHeight;

            cloneRootTransform.localScale = lossyScale * num;
            cloneRootTransform.SetParent(holderTransform, false);

            _cloneHolderObject.SetLayerRecursive(CVRLayers.UI);
            _cloneHolderObject.transform.SetParent(null, true);
            _cloneHolderObject.SetActive(true);

            #if DEBUG
            MelonLogger.Msg($"Created MiniMe for {player.Username}");
            #endif
        }
    }
}
