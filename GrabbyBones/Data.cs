using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using MagicaCloth;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;

namespace Kafe.GrabbyBones;

internal static class Data {

    internal enum GrabState {
        None = 0,
        Grab,
        // Pose,
    }

    internal class AvatarHandInfo {

        private const string HandName = $"[{nameof(GrabbyBones)} Mod] GrabbingPoint";
        private const string HandOffsetName = "GrabbedOffset";

        internal static readonly HashSet<AvatarHandInfo> Hands = new();

        internal readonly bool IsLocal;
        private readonly string _playerGuid;
        internal readonly CVRAvatar Avatar;
        private readonly bool _isLeftHand;
        internal readonly PuppetMaster PuppetMaster;

        internal readonly Transform GrabbingPoint;
        internal readonly Transform GrabbingOffset;

        internal GrabState PreviousGrabState = GrabState.None;

        internal bool IsGrabbing = false;
        internal GrabbedInfo GrabbedBoneInfo = null;

        internal static readonly HashSet<GrabbedInfo> LocalGrabbedBones = new();
        private static readonly Dictionary<GrabbedInfo, AvatarHandInfo> GrabbedBones = new();

        internal void Grab(GrabbedInfo grabbedInfo) {
            grabbedInfo.Info.DisablePhysics();
            grabbedInfo.Root.SetupIKChain(grabbedInfo.TargetChildBone, GrabbingOffset);

            // Handle caches
            GrabbedBones[grabbedInfo] = this;
            if (grabbedInfo.IsLocalPlayer) LocalGrabbedBones.Add(grabbedInfo);

            IsGrabbing = true;
            GrabbedBoneInfo = grabbedInfo;
        }

        internal void Release() {
            if (!IsGrabbing) return;

            GrabbedBoneInfo.Root.DisableIKChain();
            GrabbedBoneInfo.Info.RestorePhysics();

            // Handle Caches
            GrabbedBones.Remove(GrabbedBoneInfo);
            LocalGrabbedBones.Remove(GrabbedBoneInfo);

            IsGrabbing = false;
            GrabbedBoneInfo = null;
        }

        internal static void ReleaseAll() {
            foreach (var grabbingHand in GrabbedBones.Values.ToList()) {
                grabbingHand.Release();
            }
        }

        internal static void ReleaseAllWithBehavior(MonoBehaviour instance) {
            foreach (var grabbingInfo in GrabbedBones.Where(gb => gb.Key.Info.HasInstance(instance)).ToList()) {
                grabbingInfo.Value.Release();
            }
        }


        private static readonly HashSet<AvatarHandInfo> HandsToRelease = new();

        internal static void CheckGrabbedBones() {

            HandsToRelease.Clear();

            // Look for hands that should release the bones they're holding
            foreach (var grabbedInfo in GrabbedBones) {
                if (!grabbedInfo.Key.IsLocalPlayer) {
                    var puppetMaster = grabbedInfo.Key.PuppetMasterComponent;

                    // Handle player's avatar being hidden/blocked/blocked_alt
                    if (puppetMaster._isHidden || puppetMaster._isBlocked || puppetMaster._isBlockedAlt) {
                        #if DEBUG
                        MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(puppetMaster)}] Broken by being hidden/blocked/blocked_alt: {grabbedInfo.Key.Root.RootTransform.name}. " +
                                        $"_isHidden: {puppetMaster._isHidden}, _isBlocked: {puppetMaster._isBlocked}, _isBlockedAlt: {puppetMaster._isBlockedAlt}");
                        #endif
                        HandsToRelease.Add(grabbedInfo.Value);
                        continue;
                    }

                    // Handle player being too far
                    if (ModConfig.MeMaxPlayerDistance.Value > 0 && Vector3.Distance(puppetMaster.transform.position, PlayerSetup.Instance.transform.position) > ModConfig.MeMaxPlayerDistance.Value) {
                        #if DEBUG
                        MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(puppetMaster)}] Broken by grabbing player being too far: {grabbedInfo.Key.Root.RootTransform.name}.");
                        #endif
                        HandsToRelease.Add(grabbedInfo.Value);
                        continue;
                    }
                }

                // Handle grabbing hand being too far
                if (grabbedInfo.Key.ShouldBreak()) {
                    HandsToRelease.Add(grabbedInfo.Value);
                    continue;
                }
            }

            foreach (var handInfo in HandsToRelease) {
                handInfo.Release();
            }
        }

        internal static void Create(CVRAvatar avatar, PuppetMaster puppetMaster) {
            var animator = avatar.GetComponent<Animator>();
            if (!animator.isHuman) {
                #if DEBUG
                MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(avatar.puppetMaster)}] Avatar is not Human...");
                #endif
                return;
            }

            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftHand != null) {
                Hands.Add(new AvatarHandInfo(avatar, puppetMaster, true, leftHand, animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal)));
            }
            else {
                #if DEBUG
                MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(avatar.puppetMaster)}] Avatar doesn't have a LeftHand...");
                #endif
            }

            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null) {
                Hands.Add(new AvatarHandInfo(avatar, puppetMaster, false, rightHand, animator.GetBoneTransform(HumanBodyBones.RightIndexProximal)));
            }
            else {
                #if DEBUG
                MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(avatar.puppetMaster)}] Avatar doesn't have a RightHand...");
                #endif
            }

        }

        internal static void Delete(CVRAvatar avatar) {
            Hands.RemoveWhere(h => {
                if (h.Avatar != avatar) return false;
                h.Release();
                return true;
            });
        }

        private AvatarHandInfo(CVRAvatar avatar, PuppetMaster puppetMaster, bool isLeftHand, Transform handTransform, Transform indexTransform) {
            Avatar = avatar;
            PuppetMaster = puppetMaster;
            _isLeftHand = isLeftHand;
            IsLocal = PuppetMaster == null;
            _playerGuid = IsLocal ? MetaPort.Instance.ownerId : puppetMaster._playerDescriptor.ownerId;

            GrabbingPoint = new GameObject(HandName).transform;
            GrabbingPoint.SetParent(handTransform);
            // If we got an index transform, set the grabbing point between the wrist and the index
            GrabbingPoint.position = indexTransform == null ? handTransform.position : (handTransform.position + indexTransform.position) / 2;

            GrabbingOffset = new GameObject(HandOffsetName).transform;
            GrabbingOffset.SetParent(GrabbingPoint);
        }

        private PlayerAvatarMovementData GetMovementData() {
            if (IsLocal) {
                return PlayerSetup.Instance._playerAvatarMovementData;
            }
            else {
                return PuppetMaster._playerAvatarMovementDataCurrent;
            }
        }

        private GrabState GetGrabStateHand(int gesture, float thumb, float index, float middle, float ring, float pinky) {
            if (Mathf.Approximately(gesture, 1) || thumb > 0.5f && index > 0.5f && middle > 0.5f && ring > 0.5f && pinky > 0.5f) return GrabState.Grab;
            // if (Mathf.Approximately(gesture, 2) || thumb < 0.4f && middleFingerCurl > 0.5f) return GrabState.Pose;
            return GrabState.None;
        }

        internal GrabState GetGrabState() {
            var data = GetMovementData();
            return _isLeftHand
                ? GetGrabStateHand((int)data.AnimatorGestureLeft, data.LeftThumbCurl, data.LeftIndexCurl, data.LeftMiddleCurl, data.LeftRingCurl, data.LeftPinkyCurl)
                : GetGrabStateHand((int)data.AnimatorGestureRight, data.RightThumbCurl, data.RightIndexCurl, data.RightMiddleCurl, data.RightRingCurl, data.RightPinkyCurl);
        }

        internal bool IsAllowed() {
            if (IsLocal) return true;
            if (PuppetMaster._isHidden || PuppetMaster._isBlocked || PuppetMaster._isBlockedAlt) return false;
            if (ModConfig.MeOnlyFriends.Value && !Friends.FriendsWith(_playerGuid)) return false;
            if (ModConfig.MeMaxPlayerDistance.Value > 0 &&
                Vector3.Distance(PuppetMaster.transform.position, PlayerSetup.Instance.transform.position) >
                ModConfig.MeMaxPlayerDistance.Value) return false;
            return true;
        }

        internal float GetAvatarHeight() {
            return IsLocal ? PlayerSetup.Instance._avatarHeight : PuppetMaster._avatarHeight;
        }

        internal string GetPlayerName() {
            return GrabbyBones.GetPlayerName(PuppetMaster);
        }

        internal static bool IsRootGrabbed(GrabbyBoneInfo.Root root) {
            return GrabbedBones.Any(gb => gb.Key.Root == root);
        }
    }

    internal class GrabbedInfo {

        private const float AvatarSizeToBreakDistance = 1f;

        internal readonly bool IsLocalPlayer;
        internal readonly PuppetMaster PuppetMasterComponent;
        // internal readonly Transform SourceHand;
        // internal readonly Transform SourceHandOffset;
        internal readonly AvatarHandInfo HandInfo;
        internal readonly GrabbyBoneInfo Info;
        internal readonly GrabbyBoneInfo.Root Root;
        internal readonly Transform TargetChildBone;

        public GrabbedInfo(AvatarHandInfo handInfo, GrabbyBoneInfo info, GrabbyBoneInfo.Root root, Transform targetChildBone) {
            IsLocalPlayer = handInfo.IsLocal;
            PuppetMasterComponent = handInfo.PuppetMaster;
            // SourceHand = sourceHand;
            // SourceHandOffset = sourceHandOffset;
            HandInfo = handInfo;
            Info = info;
            Root = root;
            TargetChildBone = targetChildBone;
        }

        internal bool ShouldBreak() {
            var avatarHeight = IsLocalPlayer ? PlayerSetup.Instance._avatarHeight : PuppetMasterComponent._avatarHeight;
            var breakDistance = avatarHeight * AvatarSizeToBreakDistance;
            var currentDistance = Vector3.Distance(HandInfo.GrabbingOffset.position, TargetChildBone.position);
            if (currentDistance > breakDistance) {
                #if DEBUG
                MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(PuppetMasterComponent)}] Broken by distance: {Root.RootTransform.name}. " +
                                $"Current Distance: {currentDistance}, Breaking Distance: {breakDistance}");
                #endif
                return true;
            }
            return false;
        }
    }

    internal class GrabbyMagicaBoneInfo : GrabbyBoneInfo {

        private readonly MagicaBoneCloth _magicaBoneCloth;
        private readonly Vector3 _gravityDirection;

        public GrabbyMagicaBoneInfo(MagicaBoneCloth magicaBoneCloth, Vector3 gravityDirection) {
            _magicaBoneCloth = magicaBoneCloth;
            _gravityDirection = gravityDirection;
        }

        internal override bool IsEnabled() => _magicaBoneCloth.isActiveAndEnabled && !Mathf.Approximately(_magicaBoneCloth.BlendWeight, 0f);

        internal override void DisablePhysics() {
            MagicaPhysicsManager.Instance.Team.SetGravityDirection(_magicaBoneCloth.TeamId, Vector3.zero);
        }

        internal override void RestorePhysics() {
            MagicaPhysicsManager.Instance.Team.SetGravityDirection(_magicaBoneCloth.TeamId, _gravityDirection);
        }

        internal override float GetRadius(Transform childNode) => GetRadius(_magicaBoneCloth, childNode);

        internal static float GetRadius(MagicaBoneCloth magicaBone, Transform childNode) {
            var transformIndex = magicaBone.useTransformList.IndexOf(childNode);
            var clothDataIndex = magicaBone.clothData.useVertexList.IndexOf(transformIndex);

            // In magica some transforms have no cloth data for some reason, let's give it a 0 radius
            if (clothDataIndex == -1) return 0f;

            var depth = magicaBone.ClothData.vertexDepthList[clothDataIndex];
            return magicaBone.Params.GetRadius(depth);
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _magicaBoneCloth;
    }

    internal class GrabbyDynamicBoneInfo : GrabbyBoneInfo {

        private readonly DynamicBone _dynamicBone;
        private readonly Vector3 _gravityDirection;
        private readonly Vector3 _forceDirection;

        public GrabbyDynamicBoneInfo(DynamicBone dynamicBone, Vector3 gravityDirection, Vector3 forceDirection) {
            _dynamicBone = dynamicBone;
            _gravityDirection = gravityDirection;
            _forceDirection = forceDirection;
        }

        internal override bool IsEnabled() => _dynamicBone.isActiveAndEnabled;

        internal override void DisablePhysics() {
            _dynamicBone.m_Gravity = Vector3.zero;
            _dynamicBone.m_Force = Vector3.zero;
            _dynamicBone.OnDidApplyAnimationProperties();
        }

        internal override void RestorePhysics() {
            _dynamicBone.m_Gravity = _gravityDirection;
            _dynamicBone.m_Force = _forceDirection;
            _dynamicBone.OnDidApplyAnimationProperties();
        }

        internal override float GetRadius(Transform childNode) => GetRadius(_dynamicBone, childNode);

        internal static float GetRadius(DynamicBone dynamicBone, Transform childNode) {
            var index = dynamicBone.transformsList.IndexOf(childNode);
            var radiusCurve = dynamicBone.ParticlesList[index].m_Radius_curve;
            return radiusCurve * dynamicBone.m_Radius * dynamicBone.transform.lossyScale.x;
        }

        internal static float GetStiffness(DynamicBone dynamicBone, Transform childNode) {
            var index = dynamicBone.transformsList.IndexOf(childNode);
            var curve = dynamicBone.ParticlesList[index].m_Stiffness_curve;
            return curve * dynamicBone.m_Stiffness;
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _dynamicBone;
    }

    private static Transform[] GetIkBones(Transform root, Transform child) {
        var path = new Stack<Transform>();
        var current = child;
        while (current != null) {
            path.Push(current);
            if (current == root) {
                break;
            }
            current = current.parent;
        }
        return path.ToArray();
    }

    internal abstract class GrabbyBoneInfo {

        internal class Root {
            internal readonly FABRIK IK;
            internal readonly Transform RootTransform;
            internal readonly HashSet<Transform> ChildTransforms;
            internal readonly HashSet<RotationLimitAngle> RotationLimits;

            internal Root(FABRIK ik, Transform rootTransform, HashSet<Transform> childTransforms, HashSet<RotationLimitAngle> rotationLimits) {
                IK = ik;
                RootTransform = rootTransform;
                ChildTransforms = childTransforms;
                RotationLimits = rotationLimits;
            }

            internal void SetupIKChain(Transform closestChildTransform, Transform sourceTransformOffset) {
                foreach (var rotationLimitAngle in RotationLimits) {
                    if (rotationLimitAngle == null) continue;
                    rotationLimitAngle.enabled = true;
                }
                IK.solver.SetChain(GetIkBones(RootTransform, closestChildTransform), RootTransform);
                IK.solver.target = sourceTransformOffset;
                IK.enabled = true;
            }

            internal void DisableIKChain() {
                IK.enabled = false;
                foreach (var rotationLimitAngle in RotationLimits) {
                    if (rotationLimitAngle == null) continue;
                    rotationLimitAngle.enabled = false;
                }
            }
        }

        internal void AddRoot(FABRIK fabrik, Transform rootTransform, HashSet<Transform> childTransforms, HashSet<RotationLimitAngle> rotationLimits) {
            Roots.Add(new Root(fabrik, rootTransform, childTransforms, rotationLimits));
        }

        internal readonly HashSet<Root> Roots = new();

        internal abstract bool IsEnabled();
        internal abstract void DisablePhysics();
        internal abstract void RestorePhysics();
        internal abstract float GetRadius(Transform childNode);
        internal abstract bool HasInstance(MonoBehaviour script);
    }

}
