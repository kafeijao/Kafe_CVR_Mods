using ABI_RC.Core.Savior;
using Kafe.NavMeshFollower.InteractableWrappers;
using UnityEngine;

namespace Kafe.NavMeshFollower.Behaviors;

public class PlayFetch : Behavior {

    private enum State {
        Idle = 0,
        WaitingThrow = 1,
        Fetching = 2,
        Returning = 3,
    }

    private Pickups.PickupWrapper _target;

    public bool IsPlayingFetch { get; private set; }

    private string _lastGrabber;

    private State _currentState;

    // private State _previousState;

    private float _fetchingStartTime;
    private const float FetchingDelay = 3f;

    private float _droppingStartTime;
    private const float DroppingDelay = 3f;

    public void StartPlayingFetch(Pickups.PickupWrapper target) {
        IsPlayingFetch = true;
        _target = target;
    }

    public void StopPlayingFetch() {
        IsPlayingFetch = false;
        Controller.DropPickupRightHand(_target);
        _target = null;
    }

    private bool _wasDown;

    public override bool Handle(
        ref Vector3? destinationPos,
        ref Vector3? lookAtPos,
        ref Vector3? leftArmTargetPos,
        ref Vector3? rightArmTargetPos,
        ref bool? isIdle) {

        if (!IsPlayingFetch) return false;
        if (_target == null) {
            IsPlayingFetch = false;
            _target = null;
            return false;
        }
        var pickup = _target.pickupObject;

        var pickupPos = pickup.transform.position;

        lookAtPos = pickupPos;
        destinationPos = pickupPos;
        isIdle = false;

        switch (_currentState) {
            case State.Idle:

                isIdle = true;

                // Drop if we reached idle as still have it...
                if (Controller.IsGrabbedByMyRightHand(_target)) {
                    Controller.DropPickupRightHand(_target);
                }

                // If someone grabbed it -> next stage
                if (pickup.GrabbedBy != "") {
                    _lastGrabber = pickup.GrabbedBy;
                    _fetchingStartTime = Time.time;
                    _currentState = State.WaitingThrow;
                    break;
                }

                if (FollowerController.TryGetPlayerViewPoint(FollowerController.GetClosestPlayerGuid(pickupPos), out var possibleLookAt)) {
                    lookAtPos = possibleLookAt;
                }

                break;

            case State.WaitingThrow:

                // Update the last grabber, and reset the timer
                if (pickup.GrabbedBy != "") {
                    _lastGrabber = pickup.GrabbedBy;
                    _fetchingStartTime = Time.time;
                    if (FollowerController.TryGetPlayerPos(_lastGrabber, out var possiblePlayerPos)) {
                        destinationPos = possiblePlayerPos;
                    }
                }

                // If grabber releases it, start the timer to go fetch it!
                else {
                    if (Time.time - _fetchingStartTime >= FetchingDelay) {
                        _currentState = State.Fetching;
                    }
                }

                break;

            case State.Fetching:

                // We can't grab -> reset
                if (!_target.IsGrabbable()) {
                    _currentState = State.Idle;
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
                    Controller.GrabPickupRightHand(_target);
                    _currentState = State.Returning;
                    _droppingStartTime = Time.time;
                }

                break;

            case State.Returning:

                // Somehow we dropped it
                if (!Controller.IsGrabbedByMyRightHand(_target)) {
                    _currentState = State.Idle;
                    break;
                }

                // We failed to grab last grabber position...
                if (!FollowerController.TryGetPlayerPos(_lastGrabber, out var possibleLastGrabbedPos)) {
                    Controller.DropPickupRightHand(_target);
                    _currentState = State.Idle;
                    break;
                }

                // We failed to grab last grabber view point...
                if (!FollowerController.TryGetPlayerViewPoint(_lastGrabber, out var possibleLastGrabbedViewPointPos)) {
                    Controller.DropPickupRightHand(_target);
                    _currentState = State.Idle;
                    break;
                }

                destinationPos = possibleLastGrabbedPos;
                lookAtPos = possibleLastGrabbedViewPointPos;

                if (_target.hasInteractable && UnityEngine.Random.value >= 0.95) {
                    if (_wasDown) {
                        _target.interactable.InteractUp(Controller.GetRayController(true, out _, out _));
                    }
                    else {
                        _target.interactable.InteractDown(Controller.GetRayController(true, out _, out _));
                    }
                    _wasDown = !_wasDown;
                }

                // Start lifting the arm to deliver
                if (Vector3.Distance(Controller.Agent.transform.position, destinationPos.Value) <= 4) {
                    leftArmTargetPos = lookAtPos;
                    rightArmTargetPos = lookAtPos;
                }

                // Update the timer until we arrive
                if (!Controller.HasArrived()) {
                    _droppingStartTime = Time.time;
                }

                // We reached the destination and the delay elapsed, let's drop the item
                if (Controller.HasArrived() && Time.time - _droppingStartTime >= DroppingDelay) {
                    Controller.DropPickupRightHand(_target);
                }

                break;
        }

        return true;
    }

    public override void OnDestroyed() {

    }

    public override bool IsEnabled() {
        return IsPlayingFetch;
    }

    public PlayFetch(FollowerController controller, bool isToggleable, string description) : base(controller, isToggleable, description) {}


    public override string GetStatus() {
        return IsEnabled() ? "Playing Fetch" : "Play Fetch";
    }

    public override void Disable() => StopPlayingFetch();

    #region Parameters

    public override int GetId() => 3;
    public override int GetState() => (int) _currentState;
    public override bool IsHoldingPickup() => Controller.IsGrabbedByMyRightHand(_target);
    public override bool IsTargetPlayer() => _currentState is State.WaitingThrow or State.Returning;
    public override bool IsTargetPlayerSpawner() => IsTargetPlayer() && _lastGrabber == MetaPort.Instance.ownerId;

    #endregion
}
