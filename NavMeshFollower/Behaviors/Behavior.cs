using UnityEngine;

namespace Kafe.NavMeshFollower.Behaviors;

public abstract class Behavior {

    protected readonly FollowerController Controller;
    public readonly bool IsToggleable;
    public readonly string Description;

    protected Behavior(FollowerController controller, bool isToggleable, string description) {
        Controller = controller;
        IsToggleable = isToggleable;
        Description = description;
    }

    public abstract bool Handle(
        ref Vector3? destinationPos,
        ref Vector3? lookAtPos,
        ref Vector3? leftArmTargetPos,
        ref Vector3? rightArmTargetPos,
        ref bool? isIdle);

    public abstract void OnDestroyed();

    public abstract bool IsEnabled();
    public abstract string GetStatus();
    public abstract void Disable();

    public void DisableAllBehaviorsExcept(Behavior behaviorToIgnore) {
        foreach (var controllerBehavior in Controller.Behaviors) {
            if (behaviorToIgnore == controllerBehavior) continue;
            controllerBehavior.Disable();
        }
    }

    // Behavior Parameters
    public abstract int GetId();
    public abstract int GetState();
    public abstract bool IsHoldingPickup();
    public abstract bool IsTargetPlayer();
    public abstract bool IsTargetPlayerSpawner();
}
