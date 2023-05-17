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

        internal override float GetRadius(Transform childNode) {
            var index = _magicaBoneCloth.useTransformList.IndexOf(childNode);
            // Hack! Sometimes the use transform has more elements than the vertex depth :/
            // I'm assuming it's because it ignores some roots, so I rather keep the latest indexes
            index -= _magicaBoneCloth.useTransformList.Count - _magicaBoneCloth.ClothData.vertexDepthList.Count;
            if (index < 0 || index >= _magicaBoneCloth.ClothData.vertexDepthList.Count) {
                MelonLogger.Msg($"[GetRadiusMagica] not found: {childNode}");
                return 0f;
            }
            var depth = _magicaBoneCloth.ClothData.vertexDepthList[index];
            return _magicaBoneCloth.Params.GetRadius(depth);
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
        }

        internal override void RestorePhysics() {
            _dynamicBone.m_Gravity = _gravityDirection;
            _dynamicBone.m_Force = _forceDirection;
        }

        internal override float GetRadius(Transform childNode) {
            var index = _dynamicBone.transformsList.IndexOf(childNode);
            var radiusCurve = _dynamicBone.ParticlesList[index].m_Radius_curve;
            return radiusCurve * _dynamicBone.m_Radius * _dynamicBone.transform.lossyScale.x;
        }

        internal static float GetStiffness(DynamicBone dynamicBone, Transform childNode) {
            var index = dynamicBone.transformsList.IndexOf(childNode);
            var curve = dynamicBone.ParticlesList[index].m_Stiffness_curve;
            return curve * dynamicBone.m_Stiffness;
        }

        internal override bool HasInstance(MonoBehaviour script) => script == _dynamicBone;
    }

    internal abstract class GrabbyBoneInfo {

        internal class Root {
            internal readonly FABRIK IK;
            internal readonly Transform RootTransform;
            internal readonly HashSet<Transform> ChildTransforms;

            internal Root(FABRIK ik, Transform rootTransform, HashSet<Transform> childTransforms) {
                IK = ik;
                RootTransform = rootTransform;
                ChildTransforms = childTransforms;
            }
        }

        internal void AddRoot(FABRIK fabrik, Transform rootTransform, HashSet<Transform> childTransforms) {
            Roots.Add(new Root(fabrik, rootTransform, childTransforms));
        }

        internal readonly HashSet<Root> Roots = new();

        internal abstract bool IsEnabled();
        internal abstract void DisablePhysics();
        internal abstract void RestorePhysics();
        internal abstract float GetRadius(Transform childNode);
        internal abstract bool HasInstance(MonoBehaviour script);
    }

}
