using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Kafe.NavMeshFollower.InteractableWrappers;
using MelonLoader;
using UnityEngine;

namespace Kafe.NavMeshFollower.Behaviors;

public class FetchPickup : Behavior {

    private enum State {
        Fetching = 0,
        Returning = 1,
    }

    private bool _isFetching;
    private string _targetPlayer;
    private Pickups.PickupWrapper _targetPickup;

    private State _currentState;

    public FetchPickup(FollowerController controller, bool isToggleable, string description) : base(controller, isToggleable, description) { }

    public void FetchPickupTo(Pickups.PickupWrapper pickup, string playerGuid) {
        _targetPlayer = playerGuid;
        _targetPickup = pickup;
        _isFetching = true;
    }

    public void FinishFetch() {
        _isFetching = false;
        // Drop if holding it
        Controller.DropPickupRightHand(_targetPickup);
        _targetPickup = null;
        // Update the menu after delivering
        ModConfig.UpdateFollowerControllerPage();
    }

    private bool _wasDown;

    public override bool Handle(
        ref Vector3? destinationPos,
        ref Vector3? lookAtPos,
        ref Vector3? leftArmTargetPos,
        ref Vector3? rightArmTargetPos,
        ref bool? isIdle) {

        if (!_isFetching) return false;
        if (_targetPickup == null ||
            (_targetPlayer != MetaPort.Instance.ownerId
             && !CVRPlayerManager.Instance.NetworkPlayers.Exists(p => p.Uuid == _targetPlayer))) {
            _isFetching = false;
            _targetPickup = null;
            return false;
        }
        var pickup = _targetPickup.pickupObject;

        var pickupPos = pickup.transform.position;

        lookAtPos = pickupPos;
        destinationPos = pickupPos;

        // We successfully delivered (the target player picked it up)
        if (!Controller.IsGrabbedByMyRightHand(_targetPickup) && _targetPickup.pickupObject.GrabbedBy == _targetPlayer) {
            FinishFetch();
            return true;
        }

        switch (_currentState) {

            case State.Fetching:

                // If we already have it
                if (Controller.IsGrabbedByMyRightHand(_targetPickup)) {
                    _currentState = State.Returning;
                    break;
                }

                // We can't grab -> Wait...
                if (!_targetPickup.IsGrabbable()) {
                    isIdle = true;
                    break;
                }

                // Let our agent to get closer to the pickup
                Controller.Agent.stoppingDistance = Controller.Agent.radius * 2f;

                // Start lifting the arm to grab
                if (Vector3.Distance(Controller.Agent.transform.position, pickupPos) <= 3) {
                    rightArmTargetPos = pickupPos;
                }

                // We reached the destination and it's still grabbable -> grab
                if (Controller.HasArrived()) {
                    Controller.GrabPickupRightHand(_targetPickup);
                    _currentState = State.Returning;
                }

                break;

            case State.Returning:

                // We lost the pickup
                if (!Controller.IsGrabbedByMyRightHand(_targetPickup)) {
                    // Something else happen, lets try to fetch it back...
                    _currentState = State.Fetching;
                    break;
                }

                // We failed to grab last grabber position...
                if (!FollowerController.TryGetPlayerPos(_targetPlayer, out var possibleLastGrabbedPos)) {
                    MelonLogger.Warning("[FetchPickup.Handle] Failed to find the position of the target player...");
                    FinishFetch();
                    break;
                }

                // We failed to grab last grabber view point...
                if (!FollowerController.TryGetPlayerViewPoint(_targetPlayer, out var possibleLastGrabbedViewPointPos)) {
                    MelonLogger.Warning("[FetchPickup.Handle] Failed to find the viewpoint of the target player...");
                    FinishFetch();
                    break;
                }

                destinationPos = possibleLastGrabbedPos;
                lookAtPos = possibleLastGrabbedViewPointPos;

                if (_targetPickup.hasInteractable && UnityEngine.Random.value >= 0.95) {
                    if (_wasDown) {
                        _targetPickup.interactable.InteractUp(Controller.GetRayController(true, out _, out _));
                    }
                    else {
                        _targetPickup.interactable.InteractDown(Controller.GetRayController(true, out _, out _));
                    }
                    _wasDown = !_wasDown;
                }

                // Start lifting the arm to deliver
                if (Vector3.Distance(Controller.Agent.transform.position, destinationPos.Value) <= 4) {
                    leftArmTargetPos = lookAtPos;
                    rightArmTargetPos = lookAtPos;
                }



                break;
        }

        return true;
    }

    public override void OnDestroyed() {

    }

    public override bool IsEnabled() => _isFetching;


    public override string GetStatus() {
        return IsEnabled() ? "Fetching a Pickup" : "Fetch a Pickup";
    }

    public override void Disable() => FinishFetch();

    #region Parameters

    public override int GetId() => 1;
    public override int GetState() => (int) _currentState;
    public override bool IsHoldingPickup() => Controller.IsGrabbedByMyRightHand(_targetPickup);
    public override bool IsTargetPlayer() => _currentState is State.Returning;
    public override bool IsTargetPlayerSpawner() => IsTargetPlayer() && _targetPlayer == MetaPort.Instance.ownerId;

    #endregion
}
