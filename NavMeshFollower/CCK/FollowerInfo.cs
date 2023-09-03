using ABI.CCK.Components;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshFollower.CCK;

internal class FollowerInfo : MonoBehaviour {

    internal const string CurrentVersion = "0.0.1";

    [SerializeField] public string version = default;

    [SerializeField] public CVRSpawnable spawnable = default;
    [SerializeField] public NavMeshAgent navMeshAgent = default;
    [SerializeField] public Animator humanoidAnimator = default;

    [SerializeField] public bool hasLookAt = default;
    [SerializeField] public Transform headTransform = default;
    [SerializeField] public Transform lookAtTargetTransform = default;

    // VRIK Left Arm
    [SerializeField] public bool hasLeftArmIK = default;
    [SerializeField] public Transform vrikLeftArmTargetTransform = default;
    [SerializeField] public Transform leftHandAttachmentPoint = default;

    // VRIK Right Arm
    [SerializeField] public bool hasRightArmIK = default;
    [SerializeField] public Transform vrikRightArmTargetTransform = default;
    [SerializeField] public Transform rightHandAttachmentPoint = default;

}
