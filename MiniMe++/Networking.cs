using System.Collections;
using ABI_RC.Core;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.ContentClones;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.ModNetwork;
using ABI.CCK.Components;
using DarkRift;
using MelonLoader;
using UnityEngine;

namespace Kafe.MiniMePlusPlus;

public static class Networking
{
    private const string ModGuid = Properties.AssemblyInfoParams.Name;
    private const string ModGuid2 = Properties.AssemblyInfoParams.Name + "_Unreliable";

    public static Action<string> Hide;
    public static Action<bool, RemoteMiniMeInfo> Show;

    private static bool _isLocalMiniMeEnabled;
    private static ScheduledJob _sendUpdateJob;
    private static float _currentTickRate = MetaPort.SystemTickRate;

    public readonly struct RemoteMiniMeInfo(
        CVRPlayerEntity player,
        AttachType attachType,
        Vector3 offsetPosition,
        Quaternion offsetRotation,
        float localScale)
    {
        public CVRPlayerEntity Player { get; } = player;
        public AttachType AttachType { get; } = attachType;
        public Vector3 OffsetPosition { get; } = offsetPosition;
        public Quaternion OffsetRotation { get; } = offsetRotation;
        public float LocalScale { get; } = localScale;
    }

    public static void Initialize()
    {
        ModNetworkManager.Subscribe(ModGuid, msg => OnMessage(msg, false));
        ModNetworkManager.Subscribe(ModGuid2, msg => OnMessage(msg, true));

        // Send our state for late joiners
        CVRGameEventSystem.Player.OnJoinEntity.AddListener(player => SendUpdate(player.Uuid));
    }

    public static void MiniMeInitialize()
    {
        // Initialize listeners
        MiniMe._ourToggle.OnValueUpdated += isOn =>
        {
            _isLocalMiniMeEnabled = isOn;
            SendUpdate();
        };
        MiniMe._ourMultiSelection.OnOptionUpdated += _ => SendUpdate();
        CVRGameEventSystem.Instance.OnConnected.AddListener(_ => SendUpdate());

        // Update current values
        _isLocalMiniMeEnabled = MiniMe._ourToggle.ToggleValue;

        // Send updates
        SendUpdate();
        _sendUpdateJob = BetterScheduleSystem.AddJob(SendUnreliableUpdateIfNecessary, 0.0f, 1f / _currentTickRate, -1);
    }

    public static void UpdateTickRate(float tickRate)
    {
        _currentTickRate = tickRate;
        if (_sendUpdateJob == null) return;
        _sendUpdateJob.InvokeRate = tickRate;
    }

    public enum MsgType : byte
    {
        Show,
        Hide,
    }

    public enum AttachType : byte
    {
        Invalid = 0,
        World,
        PlaySpace,
        ViewPoint,
        LeftHand,
        RightHand,
    }

    public static void SendUpdate(string specificUserId = null)
    {
        if (_isLocalMiniMeEnabled
            && MiniMe._cloneHolderObject != null && MiniMe._cloneHolderObject.activeSelf
            && MiniMe._currentAnchorMode != MiniMe.AnchorMode.QuickMenu)
        {
            SendShowUpdate(false, specificUserId);
        }
        else
        {
            SendHideUpdate(specificUserId);
        }
    }

    public static void SendUnreliableUpdateIfNecessary()
    {
        if (MiniMe._cloneHolderObject != null && MiniMe._cloneHolderObject.activeSelf &&
            MiniMe._currentAnchorMode != MiniMe.AnchorMode.QuickMenu)
        {
            // In World Space only send updates if being grabbed
            if (MiniMe._currentAnchorMode == MiniMe.AnchorMode.WorldSpacePickup)
            {
                CVRPickupObject cloneHolderPickup = MiniMe._cloneHolderObject.GetComponent<CVRPickupObject>();
                if (!cloneHolderPickup.IsGrabbedByMe) return;
            }

            // Otherwise always send (queue to late in the frame so the IK and stuff are accurate)
            MetaPort.Instance.StartCoroutine(SendUnreliableUpdateIfNecessaryRoutine());
        }
    }

    public static IEnumerator SendUnreliableUpdateIfNecessaryRoutine()
    {
        yield return new WaitForEndOfFrame();
        SendShowUpdate(true, null);
    }

    private static void SendShowUpdate(bool isUpdate, string specificUserId)
    {
        if (!NetworkManager.Instance.IsConnectedToGameNetwork())
            return;

        CVRPickupObject cloneHolderPickup = MiniMe._cloneHolderObject.GetComponent<CVRPickupObject>();

        using var msg = specificUserId == null
            ? new ModNetworkMessage(ModGuid)
            : new ModNetworkMessage(ModGuid, specificUserId);

        msg.Write((byte)MsgType.Show);

        var controllerRay = cloneHolderPickup.ControllerRay;
        if (controllerRay != null && cloneHolderPickup.IsGrabbedByMe)
        {
            if (controllerRay.isDesktopRay)
                WriteOffsets(msg, AttachType.ViewPoint);
            else if (controllerRay.hand == CVRHand.Left)
                WriteOffsets(msg, AttachType.LeftHand);
            else if (controllerRay.hand == CVRHand.Right)
                WriteOffsets(msg, AttachType.RightHand);
            else
            {
                MelonLogger.Error("We somehow have an invalid case, there was a controller ray, but we couldn't tell which");
                return;
            }
        }
        else
        {
            if (MiniMe._currentAnchorMode == MiniMe.AnchorMode.PlaySpacePickup)
                WriteOffsets(msg, AttachType.PlaySpace);
            else if (MiniMe._currentAnchorMode == MiniMe.AnchorMode.WorldSpacePickup)
                WriteOffsets(msg, AttachType.World);
            else
            {
                MelonLogger.Error("We somehow have an invalid case :(");
                return;
            }
        }

        // Lol ModNetworkMessage doesn't support picking the sendMode
        msg._helper._sendMode = isUpdate ? SendMode.Unreliable : SendMode.Reliable;

        msg.Send();
    }

    private static void WriteOffsets(ModNetworkMessage msg, AttachType anchorType)
    {
        msg.Write((byte)anchorType);
        var miniMeTransform = MiniMe._cloneHolderObject.transform;
        switch (anchorType)
        {
            case AttachType.World:
                var worldPos = miniMeTransform.position;
                var worldRot = miniMeTransform.rotation;
                msg.Write(worldPos.x);
                msg.Write(worldPos.y);
                msg.Write(worldPos.z);
                msg.Write(worldRot.x);
                msg.Write(worldRot.y);
                msg.Write(worldRot.z);
                msg.Write(worldRot.w);
                break;
            // Old case when it was offset without transform
            // case AttachType.PlaySpace:
                // Vector3 worldPlaySpacePos = PlayerSetup.Instance.GetPlayerPosition();
                // Quaternion worldPlaySpaceRot = PlayerSetup.Instance.GetPlayerRotation();
                // Vector3 localPlaySpacePos = Quaternion.Inverse(worldPlaySpaceRot) * (miniMeTransform.position - worldPlaySpacePos);
                // Quaternion localPlaySpaceRot = Quaternion.Inverse(worldPlaySpaceRot) * miniMeTransform.rotation;
                // msg.Write(localPlaySpacePos.x);
                // msg.Write(localPlaySpacePos.y);
                // msg.Write(localPlaySpacePos.z);
                // msg.Write(localPlaySpaceRot.x);
                // msg.Write(localPlaySpaceRot.y);
                // msg.Write(localPlaySpaceRot.z);
                // msg.Write(localPlaySpaceRot.w);
                // break;
            case AttachType.PlaySpace:
            case AttachType.ViewPoint:
            case AttachType.LeftHand:
            case AttachType.RightHand:
                var offsetTransform = GetOffsetTransform(anchorType);
                if (!offsetTransform) return;
                // Position: world -> local
                Vector3 localPos = offsetTransform.InverseTransformPoint(miniMeTransform.position);
                // Rotation: world -> local
                Quaternion localRot = Quaternion.Inverse(offsetTransform.rotation) * miniMeTransform.rotation;
                msg.Write(localPos.x);
                msg.Write(localPos.y);
                msg.Write(localPos.z);
                msg.Write(localRot.x);
                msg.Write(localRot.y);
                msg.Write(localRot.z);
                msg.Write(localRot.w);
                break;
            case AttachType.Invalid:
                MelonLogger.Error("How???");
                return;
        }
        msg.Write(ModConfig.MeMiniMeScaleMultiplier.Value);
    }

    private static Transform GetOffsetTransform(AttachType anchorType)
    {
        switch (anchorType)
        {
            case AttachType.PlaySpace:
                return PlayerSetup.Instance.AvatarTransform;
            case AttachType.ViewPoint:
                return PlayerSetup.Instance.GetViewTransform();
            case AttachType.LeftHand:
                PlayerSetup.Instance.AnimatorManager.GetHumanBoneTransform(HumanBodyBones.LeftHand, out Transform leftHand);
                return leftHand;
            case AttachType.RightHand:
                PlayerSetup.Instance.AnimatorManager.GetHumanBoneTransform(HumanBodyBones.RightHand, out Transform rightHand);
                return rightHand;
        }
        return null;
    }

    private static void SendHideUpdate(string specificUserId)
    {
        if (!NetworkManager.Instance.IsConnectedToGameNetwork())
            return;

        using var msg = specificUserId == null
            ? new ModNetworkMessage(ModGuid)
            : new ModNetworkMessage(ModGuid, specificUserId);

        msg.Write((byte)MsgType.Hide);

        msg.Send();
    }

    private static void OnMessage(ModNetworkMessage msg, bool isUpdate)
    {
        msg.Read(out byte msgTypeByte);
        MsgType msgType = (MsgType)msgTypeByte;

        switch (msgType)
        {
            case MsgType.Show:
                msg.Read(out byte attachTypeByte);
                AttachType attachType = (AttachType)attachTypeByte;
                msg.Read(out float offsetPosX);
                msg.Read(out float offsetPosY);
                msg.Read(out float offsetPosZ);
                msg.Read(out float offsetRotX);
                msg.Read(out float offsetRotY);
                msg.Read(out float offsetRotZ);
                msg.Read(out float offsetRotW);
                msg.Read(out float localScale);
                if (CVRPlayerManager.Instance.UserIdToPlayerEntity.TryGetValue(msg.Sender, out CVRPlayerEntity player))
                {
                    Show?.Invoke(isUpdate, new RemoteMiniMeInfo(
                        player,
                        attachType,
                        new Vector3(offsetPosX, offsetPosY, offsetPosZ),
                        new Quaternion(offsetRotX, offsetRotY, offsetRotZ, offsetRotW),
                        localScale));
                }
                else
                {
                    MelonLogger.Warning($"Failed to find the CVRPlayerEntity for {msg.Sender}");
                }
                break;
            case MsgType.Hide:
                Hide?.Invoke(msg.Sender);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
