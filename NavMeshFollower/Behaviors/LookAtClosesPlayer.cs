using UnityEngine;

namespace Kafe.NavMeshFollower.Behaviors;

public class LookAtClosesPlayer : Behavior {

    public LookAtClosesPlayer(FollowerController controller, bool isToggleable, string description) : base(controller, isToggleable, description) {}

    public override bool Handle(
        ref Vector3? destinationPos,
        ref Vector3? lookAtPos,
        ref Vector3? leftArmTargetPos,
        ref Vector3? rightArmTargetPos,
        ref bool? isIdle) {

        var currentPos = Controller.Agent.transform.position;
        if (!FollowerController.TryGetPlayerViewPoint(FollowerController.GetClosestPlayerGuid(currentPos), out var target)) return false;
        if (Vector3.Distance(currentPos, target) > 15f) return false;
        lookAtPos = target;
        return true;
    }

    public override void OnDestroyed() {}

    public override bool IsEnabled() => true;


    public override string GetStatus() {
        return "Looking at Closest Players";
    }
}
