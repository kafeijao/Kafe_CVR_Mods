using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class ActionDetector : MonoBehaviour {

    public CVRAvatar avatarDescriptor;
    public Animator animator;

    public float timeThreshold = 1.5f;

    private Transform _head;
    private Transform _hips;

    public static Action<float, Vector3, float, Vector3> Flapped;

    private FlapHand _leftHand;
    private FlapHand _rightHand;

    private enum Hand {
        Left,
        Right,
    }

    private enum FlapState {
        Idle,
        Starting,
        Flapping,
        Flapped,
    }

    private class FlapHand {
        public Hand Side;
        public Transform HandTransform;
        public FlapState State = FlapState.Idle;
        public float FlapStartedTime;
        public Vector3 InitialPosition;
        public Vector3 FlapDirectionNormalized;
        public Vector3 PreviousPosition;
        public float DistanceFlapped;
        public float FlapVelocity;
    }

    private void Start() {

        _head = animator.GetBoneTransform(HumanBodyBones.Head);
        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);

        _leftHand = new FlapHand {
            Side = Hand.Left,
            HandTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand),
        };

        _rightHand = new FlapHand {
            Side = Hand.Right,
            HandTransform = animator.GetBoneTransform(HumanBodyBones.RightHand),
        };
    }

    private void Update() {

        if (!ModConfig.MeFlapToFly.Value) return;

        ProcessHand(_leftHand);
        ProcessHand(_rightHand);

        // Check if we flappin
        if (_leftHand.State == FlapState.Flapped && _rightHand.State == FlapState.Flapped) {
            Flapped?.Invoke(_leftHand.FlapVelocity, _leftHand.FlapDirectionNormalized, _rightHand.FlapVelocity, _rightHand.FlapDirectionNormalized);
            _leftHand.State = FlapState.Idle;
            _rightHand.State = FlapState.Idle;
        }

    }

    private void ProcessHand(FlapHand hand) {


        switch (hand.State) {
            case FlapState.Idle:
                // Check if our hands are above our head
                if (hand.HandTransform.position.y > _head.position.y) {
                    hand.State = FlapState.Starting;
                    MelonLogger.Msg($"Starting Hand: {hand.Side.ToString()}...");
                }
                break;

            case FlapState.Starting:
                // Check if our hands stop being above our head, which means we start the flap
                if (hand.HandTransform.position.y <= _head.position.y) {
                    hand.FlapStartedTime = Time.time;
                    var handPosition = hand.HandTransform.position;
                    hand.InitialPosition = handPosition;
                    hand.PreviousPosition = handPosition;
                    hand.DistanceFlapped = 0f;
                    hand.State = FlapState.Flapping;
                    MelonLogger.Msg($"Flapping Hand: {hand.Side.ToString()}...");
                }
                break;

            case FlapState.Flapping:
                // Keep checking if the movement is going down and the timer to timeout
                // If gucci keep checking for hand under the hips
                // If stop moving downwards or timed out
                if (hand.PreviousPosition.y < hand.HandTransform.position.y || Time.time > hand.FlapStartedTime + timeThreshold) {
                    hand.State = FlapState.Idle;
                    MelonLogger.Msg($"Failed! Hand: {hand.Side.ToString()}");
                    break;
                }
                // Add to the Distance Flapped
                var currentHandPosition = hand.HandTransform.position;
                hand.DistanceFlapped += Vector3.Distance(hand.PreviousPosition, currentHandPosition);
                hand.PreviousPosition = currentHandPosition;
                // Check if our hands are under our Hips
                if (hand.HandTransform.position.y < _hips.position.y) {
                    hand.State = FlapState.Flapped;
                    // Calculate the flap velocity by using the accumulated flapped distance across the time it took
                    hand.FlapVelocity = hand.DistanceFlapped / (Time.time - hand.FlapStartedTime);
                    hand.FlapDirectionNormalized = (hand.InitialPosition - currentHandPosition).normalized;
                    MelonLogger.Msg($"Flapped Hand: {hand.Side.ToString()}");
                }
                break;

            case FlapState.Flapped:
                // Keep checking the timer to cancel...
                if (Time.time > hand.FlapStartedTime + timeThreshold) {
                    hand.State = FlapState.Idle;
                    MelonLogger.Msg($"Failed! Hand: {hand.Side.ToString()}");
                }
                break;
        }

    }
}
