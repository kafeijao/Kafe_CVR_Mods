using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using MagicaCloth;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;

namespace Kafe.GrabbyBones;

internal static class Data
{

    internal enum GrabState
    {
        None = 0,
        Grab,
        // Pose,
    }

    internal class AvatarHandInfo
    {

        private const string HandName = $"[{nameof(GrabbyBones)} Mod] GrabbingPoint";
        private const string HandOffsetName = "GrabbedOffset";
        public const string LocalPlayerId = "[LocalPlayer]";

        internal static readonly Dictionary<string, AvatarHandInfo> Hands = new();

        internal readonly bool IsLocalPlayerHand;
        private readonly string _playerGuid;
        internal readonly CVRAvatar Avatar;
        public readonly bool IsLeftHand;
        internal readonly PuppetMaster PuppetMaster;

        internal readonly Transform GrabbingPoint;
        internal readonly Transform GrabbingOffset;

        internal GrabState PreviousGrabState = GrabState.None;

        internal bool IsGrabbing = false;
        internal GrabbedInfo GrabbedBoneInfo = null;

        private static readonly Dictionary<GrabbedInfo, AvatarHandInfo> GrabbedBones = new();

        internal static void SetSkipIKSolver()
        {
            foreach (var grabbedInfo in GrabbedBones.Keys) grabbedInfo.Root.IK.skipSolverUpdate = true;
        }

        internal static void ExecuteIKSolver()
        {
            foreach (var grabbedInfo in GrabbedBones.Keys) grabbedInfo.Root.IK.UpdateSolverExternal();
        }

        internal void Grab(GrabbedInfo grabbedInfo)
        {

            grabbedInfo.SetGrabbed(GrabbingOffset);

            // Handle caches
            GrabbedBones[grabbedInfo] = this;

            IsGrabbing = true;
            GrabbedBoneInfo = grabbedInfo;
        }

        internal void Release()
        {
            if (!IsGrabbing) return;

            GrabbedBoneInfo.SetReleased();

            // Handle Caches
            GrabbedBones.Remove(GrabbedBoneInfo);

            IsGrabbing = false;
            GrabbedBoneInfo = null;
        }

        internal static void ReleaseAll()
        {
            foreach (var grabbingHand in GrabbedBones.Values.ToList())
            {
                grabbingHand.Release();
            }
        }

        internal static void ReleaseAllWithBehavior(MonoBehaviour instance)
        {
            foreach (var grabbingInfo in GrabbedBones.Where(gb => gb.Key.Info.HasInstance(instance)).ToList())
            {
                grabbingInfo.Value.Release();
            }
        }

        private static readonly HashSet<AvatarHandInfo> HandsToRelease = new();

        internal static void CheckGrabbedBones()
        {

            HandsToRelease.Clear();

            // Look for hands that should release the bones they're holding
            foreach (var grabbedInfo in GrabbedBones)
            {

                // Look for remote player bones being grabbed
                if (!grabbedInfo.Key.IsGrabbedByLocalPlayer)
                {
                    var puppetMaster = grabbedInfo.Key.GrabberHandPuppetMaster;

                    // // Handle player's avatar being hidden/blocked/blocked_alt
                    // if (puppetMaster._isHidden || puppetMaster._isBlocked || puppetMaster._isBlockedAlt) {
                    //     #if DEBUG
                    //     MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(puppetMaster)}] Broken by being hidden/blocked/blocked_alt: {grabbedInfo.Key.Root.RootTransform.name}. " +
                    //                     $"_isHidden: {puppetMaster._isHidden}, _isBlocked: {puppetMaster._isBlocked}, _isBlockedAlt: {puppetMaster._isBlockedAlt}");
                    //     #endif
                    //     HandsToRelease.Add(grabbedInfo.Value);
                    //     continue;
                    // }

                    // Handle player being too far
                    if (ModConfig.MeMaxPlayerDistance.Value > 0 && Vector3.Distance(puppetMaster.transform.position, PlayerSetup.Instance.transform.position) > ModConfig.MeMaxPlayerDistance.Value)
                    {
#if DEBUG
                        MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(puppetMaster)}] Broken by grabbing player being too far: {grabbedInfo.Key.Root.RootTransform.name}.");
#endif
                        HandsToRelease.Add(grabbedInfo.Value);
                        continue;
                    }
                }

                // Handle grabbing hand being too far
                if (grabbedInfo.Key.ShouldBreak())
                {
                    HandsToRelease.Add(grabbedInfo.Value);
                    continue;
                }
            }

            foreach (var handInfo in HandsToRelease)
            {
                handInfo.Release();
            }
        }

        internal static void UpdateAngleParameters()
        {
            foreach (var grabbedBone in GrabbedBones.Keys)
            {
                grabbedBone.UpdateCurrentAngle();
            }
        }

        internal static void Create(CVRAvatar avatar, PuppetMaster puppetMaster)
        {
            var animator = avatar.GetComponent<Animator>();
            if (!animator.isHuman)
            {
#if DEBUG
                MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(puppetMaster)}] Avatar is not Human...");
#endif
                return;
            }

            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftHandInfo = leftHand != null ? new AvatarHandInfo(avatar, puppetMaster, true, leftHand, animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal)) : null;
            var rightHandInfo = rightHand != null ? new AvatarHandInfo(avatar, puppetMaster, false, rightHand, animator.GetBoneTransform(HumanBodyBones.RightIndexProximal)) : null;

            string handId = null;
            if (puppetMaster == null)
            {
                handId = LocalPlayerId;
            }
            else if (puppetMaster.AvatarDescriptor != null)
            {
                handId = puppetMaster.PlayerId;
            }

            if (handId != null)
            {
                if (leftHandInfo != null) Hands[$"{handId}_L"] = leftHandInfo;
#if DEBUG
                else MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(puppetMaster)}] Avatar doesn't have a LeftHand...");
#endif
                if (rightHandInfo != null) Hands[$"{handId}_R"] = rightHandInfo;
#if DEBUG
                else MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(puppetMaster)}] Avatar doesn't have a RightHand...");
#endif
            }
#if DEBUG
            else
            {
                MelonLogger.Msg($"[AvatarHandInfo.Create] [{GrabbyBones.GetPlayerName(puppetMaster)}] Couldn't determine a valid guid for the avatar...");
            }
#endif
        }

        internal static void Delete(CVRAvatar avatar)
        {
            // Release all bones this avatar is grabbing
            var removeHands = Hands.Where(x =>
            {
                if (x.Value.Avatar != avatar) return false;
                x.Value.Release();
                return true;
            }).Select(x => x.Key).ToArray();
            foreach (var k in removeHands)
            {
                Hands.Remove(k);
            }
            // Release all bones that are being grabbed on this avatar
            foreach (var grabbingInfo in GrabbedBones.Where(gb => gb.Key.BoneOwnerAvatar == avatar).ToList())
            {
                grabbingInfo.Value.Release();
            }
        }

        private AvatarHandInfo(CVRAvatar avatar, PuppetMaster puppetMaster, bool isLeftHand, Transform handTransform, Transform indexTransform)
        {
            Avatar = avatar;
            PuppetMaster = puppetMaster;
            IsLeftHand = isLeftHand;
            IsLocalPlayerHand = PuppetMaster == null;
            _playerGuid = IsLocalPlayerHand ? PlayerSetup.Instance.PlayerId : puppetMaster.PlayerId;

            GrabbingPoint = new GameObject(HandName).transform;
            GrabbingPoint.SetParent(handTransform, false);
            // If we got an index transform, set the grabbing point between the wrist and the index
            GrabbingPoint.position = indexTransform == null ? handTransform.position : (handTransform.position + indexTransform.position) / 2;

            GrabbingOffset = new GameObject(HandOffsetName).transform;
            GrabbingOffset.SetParent(GrabbingPoint);
        }

        private PlayerAvatarMovementData GetMovementData()
        {
            if (IsLocalPlayerHand)
            {
                return PlayerSetup.Instance._playerAvatarMovementData;
            }
            else
            {
                return PuppetMaster._playerAvatarMovementDataCurrent;
            }
        }

        private const float OpenCurl = 0.7f;
        private const float CloseCurl = -1f;
        private const float OpenCurlThumb = 0.85f;
        private const float CloseCurlThumb = -0.85f;

        private GrabState GetGrabStateHand(int gesture, float thumb, float index, float middle, float ring, float pinky)
        {

            // Check for the grabbing gesture
            var isGrabbingWithGesture = ModConfig.MeUseFistGestureToGrab.Value && Mathf.Approximately(gesture, 1);
            if (isGrabbingWithGesture) return GrabState.Grab;

            // Check for the grabbing via finger curls
            var isGrabbingWithCurls = ModConfig.MeUseFingerCurlsToGrab.Value
                                      && Mathf.InverseLerp(OpenCurlThumb, CloseCurlThumb, thumb) >= ModConfig.MeThumbMinFingerCurl.Value
                                      && Mathf.InverseLerp(OpenCurl, CloseCurl, index) >= ModConfig.MeIndexMinFingerCurl.Value
                                      && Mathf.InverseLerp(OpenCurl, CloseCurl, middle) >= ModConfig.MeMiddleMinFingerCurl.Value
                                      && Mathf.InverseLerp(OpenCurl, CloseCurl, ring) >= ModConfig.MeRingMinFingerCurl.Value
                                      && Mathf.InverseLerp(OpenCurl, CloseCurl, pinky) >= ModConfig.MePinkyMinFingerCurl.Value;

            if (isGrabbingWithCurls) return GrabState.Grab;

            return GrabState.None;
        }

        internal GrabState GetGrabState()
        {
            var data = GetMovementData();
            return IsLeftHand
                ? GetGrabStateHand(
                    (int) data.AnimatorGestureLeft,
                    data.MuscleValues[MuscleIndex.LeftThumb1Stretched],
                    data.MuscleValues[MuscleIndex.LeftIndex1Stretched],
                    data.MuscleValues[MuscleIndex.LeftMiddle1Stretched],
                    data.MuscleValues[MuscleIndex.LeftRing1Stretched],
                    data.MuscleValues[MuscleIndex.LeftLittle1Stretched])
                : GetGrabStateHand(
                    (int) data.AnimatorGestureRight,
                    data.MuscleValues[MuscleIndex.RightThumb1Stretched],
                    data.MuscleValues[MuscleIndex.RightIndex1Stretched],
                    data.MuscleValues[MuscleIndex.RightMiddle1Stretched],
                    data.MuscleValues[MuscleIndex.RightRing1Stretched],
                    data.MuscleValues[MuscleIndex.RightLittle1Stretched]);
        }

        internal bool IsAllowed()
        {
            if (IsLocalPlayerHand) return true;
            if (PuppetMaster.IsAvatarHidden || PuppetMaster.IsAvatarBlocked || PuppetMaster.IsAvatarBlockedAlt) return false;
            if (ModConfig.MeOnlyFriends.Value && !Friends.FriendsWith(_playerGuid)) return false;
            if (ModConfig.MeMaxPlayerDistance.Value > 0 &&
                Vector3.Distance(PuppetMaster.transform.position, PlayerSetup.Instance.transform.position) >
                ModConfig.MeMaxPlayerDistance.Value) return false;
            return true;
        }

        internal float GetAvatarHeight()
        {
            return IsLocalPlayerHand ? PlayerSetup.Instance.AvatarHeight : PuppetMaster.netIkController.GetRemoteHeight();
        }

        internal static bool IsRootGrabbed(GrabbyBoneInfo.Root root)
        {
            return GrabbedBones.Any(gb => gb.Key.Root == root);
        }
    }

    internal class GrabbedInfo
    {

        private const float AvatarSizeToBreakDistance = 2f;

        internal readonly bool IsGrabbedByLocalPlayer;
        internal readonly PuppetMaster GrabberHandPuppetMaster;

        internal readonly bool IsBoneFromLocalPlayer;
        internal readonly PuppetMaster BoneOwnerPuppetMaster;
        internal readonly CVRAvatar BoneOwnerAvatar;

        internal readonly AvatarHandInfo HandInfo;
        internal readonly GrabbyBoneInfo Info;
        internal readonly GrabbyBoneInfo.Root Root;
        internal readonly Transform TargetChildBone;

        public GrabbedInfo(AvatarHandInfo handInfo, GrabbyBoneInfo info, GrabbyBoneInfo.Root root, Transform targetChildBone)
        {
            IsGrabbedByLocalPlayer = handInfo.IsLocalPlayerHand;
            GrabberHandPuppetMaster = handInfo.PuppetMaster;
            IsBoneFromLocalPlayer = info.PuppetMaster == null;
            BoneOwnerPuppetMaster = info.PuppetMaster;
            BoneOwnerAvatar = info.Avatar;
            HandInfo = handInfo;
            Info = info;
            Root = root;
            TargetChildBone = targetChildBone;
        }

        internal bool ShouldBreak()
        {
            var boneOwnerAvatarHeight = IsBoneFromLocalPlayer ? PlayerSetup.Instance.AvatarHeight : BoneOwnerPuppetMaster.netIkController.GetRemoteHeight();
            var grabbedAvatarHeight = IsGrabbedByLocalPlayer ? PlayerSetup.Instance.AvatarHeight : GrabberHandPuppetMaster.netIkController.GetRemoteHeight();
            var breakDistance = (boneOwnerAvatarHeight + grabbedAvatarHeight) * AvatarSizeToBreakDistance;
            var currentDistance = Vector3.Distance(HandInfo.GrabbingOffset.position, TargetChildBone.position);
            if (currentDistance > breakDistance)
            {
#if DEBUG
                MelonLogger.Msg($"[{GrabbyBones.GetPlayerName(BoneOwnerPuppetMaster)}] Broken by distance: {Root.RootTransform.name}. " +
                                $"Current Distance: {currentDistance}, Breaking Distance: {breakDistance}");
#endif
                return true;
            }
            return false;
        }

        // Animator Parameters
        private const string ParameterAngleSuffix = "_Angle";
        private const string ParameterGrabbedSuffix = "_IsGrabbed";
        private string _currentAngleParameterName;
        private int _currentAngleParameterNameLocal;
        private Quaternion _initialRotation;
        private float _oldAngle;

        internal void SetGrabbed(Transform grabbingOffset)
        {

            // Component setup
            Info.DisablePhysics();
            Root.SetupIKChain(TargetChildBone, grabbingOffset);

            // Animator parameters
            if (IsBoneFromLocalPlayer)
            {
                // Set Grabbed parameter to true (both synced and local params)
                PlayerSetup.Instance.AnimatorManager.SetParameter(Info.GetName() + ParameterGrabbedSuffix, 1.0f);
                PlayerSetup.Instance.AnimatorManager.SetParameter("#" + Info.GetName() + ParameterGrabbedSuffix, 1.0f);
            }
            else
            {
                // Set Grabbed parameter on remotes to true (local params only)
                BoneOwnerPuppetMaster.Animator.SetFloat("#" + Info.GetName() + ParameterGrabbedSuffix, 1.0f);
            }
            // Initialize stuff to update the angles
            _currentAngleParameterName = Info.GetName() + ParameterAngleSuffix;
            _currentAngleParameterNameLocal = Animator.StringToHash("#" + _currentAngleParameterName);
            _initialRotation = TargetChildBone.parent.localRotation;
            _oldAngle = -1;
        }

        internal void SetReleased()
        {

            // Component setup
            Root.DisableIKChain();
            Info.RestorePhysics();

            // Animator parameters
            if (IsBoneFromLocalPlayer)
            {
                // Set Grabbed parameter to false (both synced and local params)
                PlayerSetup.Instance.AnimatorManager.SetParameter(Info.GetName() + ParameterGrabbedSuffix, 0.0f);
                // Reset the Angle parameter to 0 (both synced and local params)
                PlayerSetup.Instance.AnimatorManager.SetParameter(_currentAngleParameterName, 0.0f);
                // Do the same for the local parameters
                if (PlayerSetup.Instance.Animator != null)
                {
                    PlayerSetup.Instance.Animator.SetBool("#" + Info.GetName() + ParameterGrabbedSuffix, false);
                    PlayerSetup.Instance.Animator.SetFloat(_currentAngleParameterNameLocal, 0.0f);
                }
            }
            else
            {
                if (BoneOwnerPuppetMaster != null && BoneOwnerPuppetMaster.Animator != null)
                {
                    // Set Grabbed and Angle parameter on remotes (local params only)
                    BoneOwnerPuppetMaster.Animator.SetBool("#" + Info.GetName() + ParameterGrabbedSuffix, false);
                    BoneOwnerPuppetMaster.Animator.SetFloat(_currentAngleParameterNameLocal, 0f);
                }
            }
        }

        internal void UpdateCurrentAngle()
        {
            var newAngle = Mathf.Clamp01(Quaternion.Angle(_initialRotation, TargetChildBone.parent.localRotation) / 180f);
            // MelonLogger.Msg($"Angle: {Quaternion.Angle(_initialRotation, TargetChildBone.parent.localRotation)} / 180 = {newAngle} | {TargetChildBone.parent.localRotation.ToString("F3")}");
            if (Mathf.Approximately(newAngle, _oldAngle)) return;
            _oldAngle = newAngle;
            if (IsBoneFromLocalPlayer)
            {
                PlayerSetup.Instance.AnimatorManager.SetParameter(_currentAngleParameterName, newAngle);
                PlayerSetup.Instance.Animator.SetFloat(_currentAngleParameterNameLocal, newAngle);
            }
            else
            {
                BoneOwnerPuppetMaster.Animator.SetFloat(_currentAngleParameterNameLocal, newAngle);
            }
        }
    }

    internal class GrabbyMagicaBoneInfo : GrabbyBoneInfo
    {

        private readonly MagicaBoneCloth _magicaBoneCloth;
        private readonly Vector3 _gravityDirection;

        public GrabbyMagicaBoneInfo(CVRAvatar avatar, PuppetMaster puppetMaster, MagicaBoneCloth magicaBoneCloth, Vector3 gravityDirection) : base(puppetMaster, avatar)
        {
            _magicaBoneCloth = magicaBoneCloth;
            _gravityDirection = gravityDirection;
            _networkPath = GetHierarchyIndexPath(magicaBoneCloth, avatar.transform);
        }

        string _networkPath;
        internal override string NetworkPath => _networkPath;

        internal override bool IsEnabled() => _magicaBoneCloth.isActiveAndEnabled && !Mathf.Approximately(_magicaBoneCloth.BlendWeight, 0f);

        internal override void DisablePhysics()
        {
            MagicaPhysicsManager.Instance.Team.SetGravityDirection(_magicaBoneCloth.TeamId, Vector3.zero);
        }

        internal override void RestorePhysics()
        {
            MagicaPhysicsManager.Instance.Team.SetGravityDirection(_magicaBoneCloth.TeamId, _gravityDirection);
        }

        internal override string GetName() => _magicaBoneCloth.name;

        internal override float GetRadius(Transform childNode) => GetRadius(_magicaBoneCloth, childNode);

        internal override float GetLength(Transform childNode) => GetLength(_magicaBoneCloth, childNode);

        internal static float GetRadius(MagicaBoneCloth magicaBone, Transform childNode)
        {
            var transformIndex = magicaBone.useTransformList.IndexOf(childNode);
            var clothDataIndex = magicaBone.clothData.useVertexList.IndexOf(transformIndex);

            // In magica some transforms have no cloth data for some reason, let's give it a 0 radius
            if (clothDataIndex == -1) return 0f;

            var depth = magicaBone.ClothData.vertexDepthList[clothDataIndex];
            return magicaBone.Params.GetRadius(depth);
        }

        internal static float GetLength(MagicaBoneCloth magicaBone, Transform childNode)
        {
            // Unfortunately magica doesn't have a total length property (that I could find)
            var totalLength = 0f;
            var currentBone = childNode;
            while (currentBone.parent != null)
            {
                var nextBone = currentBone.parent;
                totalLength += Vector3.Distance(currentBone.position, nextBone.position);
                currentBone = nextBone;
                if (magicaBone.clothTarget.rootList.Contains(currentBone))
                {
                    return totalLength;
                }
            }
            return 1f;
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _magicaBoneCloth;
    }

    internal class GrabbyMagica2BoneInfo : GrabbyBoneInfo
    {

        private readonly MagicaCloth2.MagicaCloth _magicaBoneCloth;
        private readonly float _originalGravity;

        public GrabbyMagica2BoneInfo(CVRAvatar avatar, PuppetMaster puppetMaster, MagicaCloth2.MagicaCloth magicaBoneCloth) : base(puppetMaster, avatar)
        {
            _magicaBoneCloth = magicaBoneCloth;
            _originalGravity = _magicaBoneCloth.process.cloth.SerializeData.gravity;
            _networkPath = GetHierarchyIndexPath(magicaBoneCloth, avatar.transform);
        }

        string _networkPath;
        internal override string NetworkPath => _networkPath;

        internal override bool IsEnabled() => _magicaBoneCloth.isActiveAndEnabled && !Mathf.Approximately(_magicaBoneCloth.serializeData.blendWeight, 0);

        internal override void DisablePhysics()
        {
            _magicaBoneCloth.SerializeData.gravity = 0f;
            _magicaBoneCloth.process.SyncParameters();
        }

        internal override void RestorePhysics()
        {
            _magicaBoneCloth.SerializeData.gravity = _originalGravity;
            _magicaBoneCloth.process.SyncParameters();
        }

        internal override string GetName() => _magicaBoneCloth.name;

        internal override float GetRadius(Transform childNode) => GetRadius(_magicaBoneCloth, childNode);

        internal override float GetLength(Transform childNode) => GetLength(_magicaBoneCloth, childNode);

        internal static float GetRadius(MagicaCloth2.MagicaCloth magicaBone, Transform childNode)
        {
            float depth = 0.5f;
            // If the bone was pre-build magicaBone.process.boneClothSetupData doesn't exist
            // Todo: Find a way to get the depth of a transform for pre-built data
            if (magicaBone.process.boneClothSetupData != null)
            {
                var idx = magicaBone.process.boneClothSetupData.GetTransformIndexFromId(childNode.GetInstanceID());
                // var depth = magicaBone.process.HasProxyMesh.vertexDepths[idx];
                // var depth = magicaBone.process.ProxyMesh.vertexDepths[idx];
                depth = magicaBone.process.ProxyMeshContainer.shareVirtualMesh.vertexDepths[idx];
            }
            var radius = magicaBone.SerializeData.radius.Evaluate(depth);
            return radius;
        }

        internal static float GetLength(MagicaCloth2.MagicaCloth magicaBone, Transform childNode)
        {
            // Unfortunately magica doesn't have a total length property (that I could find)
            var totalLength = 0f;
            var currentBone = childNode;
            while (currentBone.parent != null)
            {
                var nextBone = currentBone.parent;
                totalLength += Vector3.Distance(currentBone.position, nextBone.position);
                currentBone = nextBone;
                if (magicaBone.SerializeData.rootBones.Contains(currentBone))
                {
                    return totalLength;
                }
            }
            return 1f;
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _magicaBoneCloth;
    }

    internal class GrabbyDynamicBoneInfo : GrabbyBoneInfo
    {

        private readonly DynamicBone _dynamicBone;
        private readonly Vector3 _gravityDirection;
        private readonly Vector3 _forceDirection;

        public GrabbyDynamicBoneInfo(CVRAvatar avatar, PuppetMaster puppetMaster, DynamicBone dynamicBone, Vector3 gravityDirection, Vector3 forceDirection) : base(puppetMaster, avatar)
        {
            _dynamicBone = dynamicBone;
            _gravityDirection = gravityDirection;
            _forceDirection = forceDirection;
            _networkPath = GetHierarchyIndexPath(dynamicBone, avatar.transform);
        }

        string _networkPath;
        internal override string NetworkPath => _networkPath;
        internal override bool IsEnabled() => _dynamicBone.isActiveAndEnabled;

        internal override void DisablePhysics()
        {
            _dynamicBone.m_Gravity = Vector3.zero;
            _dynamicBone.m_Force = Vector3.zero;
            _dynamicBone.OnDidApplyAnimationProperties();
        }

        internal override void RestorePhysics()
        {
            _dynamicBone.m_Gravity = _gravityDirection;
            _dynamicBone.m_Force = _forceDirection;
            _dynamicBone.OnDidApplyAnimationProperties();
        }

        internal override float GetRadius(Transform childNode) => GetRadius(_dynamicBone, childNode);

        internal override float GetLength(Transform childNode) => _dynamicBone.m_BoneTotalLength;

        internal override string GetName() => _dynamicBone.name;

        internal static float GetRadius(DynamicBone dynamicBone, Transform childNode)
        {
            var index = dynamicBone.transformsList.IndexOf(childNode);
            var radiusCurve = dynamicBone.ParticlesList[index].m_Radius_curve;
            return radiusCurve * dynamicBone.m_Radius * dynamicBone.transform.lossyScale.x;
        }

        internal static float GetStiffness(DynamicBone dynamicBone, Transform childNode)
        {
            var index = dynamicBone.transformsList.IndexOf(childNode);
            var curve = dynamicBone.ParticlesList[index].m_Stiffness_curve;
            return curve * dynamicBone.m_Stiffness;
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _dynamicBone;
    }

    private static Transform[] GetIkBones(Transform root, Transform child)
    {
        var path = new Stack<Transform>();
        var current = child;
        while (current != null)
        {
            path.Push(current);
            if (current == root)
            {
                break;
            }
            current = current.parent;
        }
        return path.ToArray();
    }

    // Returns the hierarchy path of a component
    // based on indices relative to avatar root
    // last index is component index (e.g. "0/1/2/3/1/0")
    public static string GetHierarchyIndexPath(Component c, Transform root)
    {
        var path = c.GetComponentIndex().ToString();
        var parent = c.transform;
        while (parent != root && parent != null)
        {
            path = parent.GetSiblingIndex() + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
    // Returns the hierarchy path of a component's transform (e.g. "Hips/Spine/Chest")
    public static string GetHierarchyNamePath(Component c, Transform root)
    {
        var path = c.transform.name;
        var parent = c.transform.parent;
        while (parent != root && parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    internal abstract class GrabbyBoneInfo
    {
        internal class ChildTransform
        {
            public string NetworkPath;
            public Transform Transform;

            public ChildTransform(Transform trs, int idx)
            {
                Transform = trs;
                NetworkPath = idx.ToString();
            }

            public static implicit operator Transform(ChildTransform ct) => ct.Transform;
        }
        internal class Root
        {
            internal readonly FABRIK IK;
            internal readonly Transform RootTransform;
            internal readonly List<ChildTransform> ChildTransforms;
            internal readonly HashSet<RotationLimitAngle> RotationLimits;
            public readonly string NetworkPath;

            internal Root(FABRIK ik, Transform rootTransform, List<ChildTransform> childTransforms, HashSet<RotationLimitAngle> rotationLimits, string networkPath)
            {
                IK = ik;
                RootTransform = rootTransform;
                ChildTransforms = childTransforms;
                RotationLimits = rotationLimits;
                NetworkPath = networkPath;
            }

            internal void SetupIKChain(Transform closestChildTransform, Transform sourceTransformOffset)
            {
                foreach (var rotationLimitAngle in RotationLimits)
                {
                    if (rotationLimitAngle == null) continue;
                    rotationLimitAngle.enabled = true;
                }
                if (IK == null || IK.solver == null) return;
                IK.solver.SetChain(GetIkBones(RootTransform, closestChildTransform), RootTransform);
                IK.solver.target = sourceTransformOffset;
                IK.enabled = true;
            }

            internal void DisableIKChain()
            {
                if (IK == null) return;
                IK.enabled = false;
                foreach (var rotationLimitAngle in RotationLimits)
                {
                    if (rotationLimitAngle == null) continue;
                    rotationLimitAngle.enabled = false;
                }
            }
        }

        internal void AddRoot(FABRIK fabrik, Transform rootTransform, List<Transform> childTransforms, HashSet<RotationLimitAngle> rotationLimits)
        {
            Roots.Add(new Root(fabrik, rootTransform, childTransforms.Select((x,i) => new ChildTransform(x,i)).ToList(), rotationLimits, Roots.Count.ToString()));
        }

        internal readonly List<Root> Roots = new();

        internal GrabbyBoneInfo(PuppetMaster puppetMaster, CVRAvatar avatar)
        {
            PlayerGuid = puppetMaster == null ? MetaPort.Instance.ownerId : puppetMaster.PlayerId;
            PuppetMaster = puppetMaster;
            AvatarGuid = avatar.GetComponent<CVRAssetInfo>()?.objectId;
            Avatar = avatar;
        }

        internal readonly PuppetMaster PuppetMaster;
        internal readonly CVRAvatar Avatar;
        internal readonly string PlayerGuid;
        internal readonly string AvatarGuid;

        internal abstract string NetworkPath { get; }
        internal abstract bool IsEnabled();
        internal abstract void DisablePhysics();
        internal abstract void RestorePhysics();
        internal abstract float GetRadius(Transform childNode);
        internal abstract float GetLength(Transform childNode);
        internal abstract string GetName();
        internal abstract bool HasInstance(MonoBehaviour script);
    }

}
