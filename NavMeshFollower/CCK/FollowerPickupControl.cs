using UnityEngine;

namespace Kafe.NavMeshFollower.CCK;

[Serializable]
public class FollowerPickupControl : FollowerStateMachine {

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (!IsInitialized) {
            Destroy(this);
            return;
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (!IsInitialized) {
            Destroy(this);
            return;
        }
    }

    [SerializeField] public bool isEnterEnabled;
    [SerializeField] public bool isExitEnabled;

    [SerializeField] public FollowerPickupControlTask enterTask = new();
    [SerializeField] public FollowerPickupControlTask exitTask = new();

    [Serializable]
    public class FollowerPickupControlTask {

        [Serializable]
        public enum InteractionType {
            InteractUp,
            InteractDown,
        }

        [SerializeField] public InteractionType behavior = InteractionType.InteractUp;
    }
}
