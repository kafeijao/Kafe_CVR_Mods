using ABI_RC.Core.Player;
using MagicaCloth;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;

namespace Kafe.GrabbyBones;

internal static class Data {

    internal class GrabbedInfo {

        internal readonly bool IsLocalPlayer;
        internal readonly PuppetMaster PuppetMasterComponent;
        internal readonly Transform SourceHand;
        internal readonly Transform SourceHandOffset;
        internal readonly GrabbyBoneInfo Info;
        internal readonly GrabbyBoneInfo.Root Root;
        internal readonly Transform TargetChildBone;

        public GrabbedInfo(PuppetMaster puppetMaster, Transform sourceHand, Transform sourceHandOffset, GrabbyBoneInfo info, GrabbyBoneInfo.Root root, Transform targetChildBone) {
            IsLocalPlayer = puppetMaster == null;
            PuppetMasterComponent = puppetMaster;
            SourceHand = sourceHand;
            SourceHandOffset = sourceHandOffset;
            Info = info;
            Root = root;
            TargetChildBone = targetChildBone;
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

            internal void Grab(Transform closestChildTransform, Transform sourceTransformOffset) {
                foreach (var rotationLimitAngle in RotationLimits) {
                    if (rotationLimitAngle == null) continue;
                    rotationLimitAngle.enabled = true;
                }
                IK.solver.SetChain(GetIkBones(RootTransform, closestChildTransform), RootTransform);
                IK.solver.target = sourceTransformOffset;
                IK.enabled = true;
            }

            internal void Release() {
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
