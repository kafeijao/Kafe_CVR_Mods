using System.ComponentModel;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class ActionDetector : MonoBehaviour {

    private static Action _onLateUpdate;

    public CVRAvatar avatarDescriptor;
    public Animator animator;
    private HumanPose _humanPose;
    private HumanPoseHandler _humanPoseHandler;

    public float timeThreshold = 0.3f;

    private const float ArmDownUpThreshold = 0.35f;
    private const float ArmDownUpStopThreshold = -0.40f;

    private const float ArmAngleMaxThreshold = 125f;
    private const float ArmAngleMinThreshold = 55f;

    private const float ArmAngleMinBreakThreshold = 30f;
    private const float ArmAngleMaxBreakThreshold = 150f;

    private const float ArmsAlignmentMinAngle = 135f;
    private const float WristTwistThreshold = 0.30f;

    private Transform _head;
    private Transform _neck;
    private Transform _hips;
    private Transform _spine;

    public static Action<float, Vector3, float, Vector3> Flapped;
    public static Action<bool, Vector2> Gliding;

    private HandInfo _leftHandInfo;
    private HandInfo _rightHandInfo;

    private enum Hand {
        Left,
        Right,
    }

    private enum FlapState {
        Idle,
        StartingFlap,
        Flapping,
        Flapped,
    }

    private enum GlideState {
        Idle,
        Gliding,
    }

    private class HandInfo {
        public Hand Side;
        public Transform HandTransform;
        public FlapState FlapState = FlapState.Idle;
        public float FlapStartedTime;
        public Vector3 InitialPositionFromAvatar;
        public Vector3 FlapDirection;
        public Vector3 PreviousPosition;
        public float DistanceFlapped;

        public float FlapVelocity;

        // Gliding
        public GlideState GlideState;
        public Transform AvatarRoot;
        public Transform HipsTransform;
        public Transform NeckTransform;
        public Transform ArmTransform;
        public Transform ElbowTransform;
        // public Transform ThumbTransform;
        public float ArmLength;

        public float GetCurrentArmSpan() => Vector3.Distance(ArmTransform.position, HandTransform.position) / Mathf.Max(AvatarRoot.transform.lossyScale.x, 0.00001f);
        //
        // public float GetCurrentArmAngleSum() {
        //     // First, calculate the global up vector of the parent transform (e.g., the body)
        //     var parentUp = ArmTransform.parent.up;
        //
        //     // Then, calculate the vectors that the arm, elbow and hand should point along
        //     var armVector = ElbowTransform.position - ArmTransform.position;
        //     var elbowVector = HandTransform.position - ElbowTransform.position;
        //     var handVector = HandTransform.forward; // assuming the hand points in its forward direction
        //
        //     // Next, calculate the angles between these vectors and the global up vector of the parent
        //     var armAngle = Vector3.Angle(parentUp, armVector);
        //     var elbowAngle = Vector3.Angle(armVector, elbowVector);
        //     var handAngle = Vector3.Angle(elbowVector, handVector);
        //
        //     // Now sum up the angles to get the total angle
        //     return armAngle + elbowAngle + handAngle;
        // }
        //
        // public float GetCurrentHandAngle() {
        //
        //     // You can get the direction in which the thumb is pointing by subtracting the position of the hand from the position of the thumb
        //     var thumbDirection = ThumbTransform.position - HandTransform.position;
        //
        //     // Normalize the direction
        //     thumbDirection.Normalize();
        //
        //     // Get the direction in which the character is pointing
        //     var characterDirection = AvatarRoot.forward;
        //
        //     // Project the thumb's direction onto the scene up axis
        //     var projectedThumbDirection = Vector3.ProjectOnPlane(thumbDirection, AvatarRoot.up);
        //
        //     // Normalize the projected direction
        //     projectedThumbDirection.Normalize();
        //
        //     // Get the angle between the thumb direction and the character direction in degrees
        //     var angle = Vector3.Angle(projectedThumbDirection, characterDirection);
        //
        //     return angle;
        // }
        //
        // public float GetCurrentArmAngle() {
        //
        //     // Get the direction in which the arm is pointing by subtracting the position of the shoulder from the position of the hand
        //     var armDirection = HandTransform.position - HandTransform.parent.position;
        //
        //     // Normalize the direction
        //     armDirection.Normalize();
        //
        //     // Project the arm's direction onto the scene up axis
        //     var projectedArmDirection = Vector3.ProjectOnPlane(armDirection, AvatarRoot.up);
        //
        //     // Normalize the projected direction
        //     projectedArmDirection.Normalize();
        //
        //     // Calculate the dot product between the scene's up vector and the arm's direction. This will give us the cosine of the angle between these vectors
        //     var cosineAngle = Vector3.Dot(AvatarRoot.up, armDirection);
        //
        //     // The acos function returns the angle in radians, so convert it to degrees
        //     var angleInDegrees = Mathf.Acos(cosineAngle) * Mathf.Rad2Deg;
        //
        //     // Since the range of acos is 0 to 180, we need to change it to -90 to 90. If the arm is pointing down, the z component of armDirection will be negative.
        //     if (armDirection.z < 0) {
        //         angleInDegrees = -angleInDegrees;
        //     }
        //
        //     return angleInDegrees;
        // }

        public float GetCurrentArmAngle() {
            var bottomDirection = NeckTransform.position - HipsTransform.parent.position;
            bottomDirection.Normalize();
            var armDirection = ArmTransform.position - HandTransform.parent.position;
            armDirection.Normalize();
            return Vector3.Angle(bottomDirection, armDirection);
        }
    }

    private int _spineLeftRightIdx;
    private int _chestLeftRightIdx;

    private int _leftShoulderUpDownIdx;
    private int _leftArmUpDownIdx;
    private int _leftHandUpDownIdx;

    private int _leftArmTwistIdx;
    private int _leftForearmTwistIdx;

    private int _rightShoulderUpDownIdx;
    private int _rightArmUpDownIdx;
    private int _rightHandUpDownIdx;

    private int _rightArmTwistIdx;
    private int _rightForearmTwistIdx;

    private float GetLeftArmDownUp() {
        return (_humanPose.muscles[_leftShoulderUpDownIdx] + _humanPose.muscles[_leftArmUpDownIdx] + _humanPose.muscles[_leftHandUpDownIdx]) / 3f;
        // arm down -30, limit -35, up 30 / limit 35
        // MelonLogger.Msg($"ArmUpDown: {humanPose.muscles[_spineLeftRightIdx]:F2} + {humanPose.muscles[_chestLeftRightIdx]:F2} + {humanPose.muscles[_leftShoulderUpDownIdx]:F2} + {humanPose.muscles[_leftArmUpDownIdx]:F2} + {humanPose.muscles[_leftHandUpDownIdx]:F2} + = {((humanPose.muscles[_leftShoulderUpDownIdx] + humanPose.muscles[_leftArmUpDownIdx] + humanPose.muscles[_leftHandUpDownIdx])/5f):F2}");
        // return (humanPose.muscles[_spineLeftRightIdx] + humanPose.muscles[_chestLeftRightIdx] + humanPose.muscles[_leftShoulderUpDownIdx] + humanPose.muscles[_leftArmUpDownIdx] + humanPose.muscles[_leftHandUpDownIdx]) / 5f;
    }

    private float GetLeftWristTwist() {
        return (_humanPose.muscles[_leftArmTwistIdx] + _humanPose.muscles[_leftForearmTwistIdx]) / 2f;
        // -0.25/-0.30 when thumb down, +0.25/0.30 when thumb up
    }

    private float GetRightArmDownUp() {
        return (_humanPose.muscles[_rightShoulderUpDownIdx] + _humanPose.muscles[_rightArmUpDownIdx] + _humanPose.muscles[_rightHandUpDownIdx]) / 3f;
        // arm down -30, limit -35, up 30 / limit 35
       }

    private float GetRightWristTwist() {
        return (_humanPose.muscles[_rightArmTwistIdx] + _humanPose.muscles[_rightForearmTwistIdx]) / 2f;
        // -0.25/-0.30 when thumb down, +0.25/0.30 when thumb up
    }

    private void Start() {

        _humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        _humanPose = new HumanPose();

        var muscleMap = new Dictionary<string, int>();
        for (var i = 0; i < HumanTrait.MuscleCount; i++) {
            muscleMap[HumanTrait.MuscleName[i]] = i;
        }

        _spineLeftRightIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.SpineLeftRight)];
        _chestLeftRightIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.ChestLeftRight)];

        _leftShoulderUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.LeftShoulderDownUp)];
        _leftArmUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.LeftArmDownUp)];
        _leftHandUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.LeftHandDownUp)];

        _leftArmTwistIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.LeftArmTwistInOut)];
        _leftForearmTwistIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.LeftForearmTwistInOut)];
        
        _rightShoulderUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.RightShoulderDownUp)];
        _rightArmUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.RightArmDownUp)];
        _rightHandUpDownIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.RightHandDownUp)];

        _rightArmTwistIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.RightArmTwistInOut)];
        _rightForearmTwistIdx = muscleMap[GetDescriptionFromEnumValue(MuscleBone.RightForearmTwistInOut)];

        _head = animator.GetBoneTransform(HumanBodyBones.Head);
        _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        _spine = animator.GetBoneTransform(HumanBodyBones.Spine);

        var lArmTransform = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var lElbowTransform = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var lHandTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        // var lThumbTransform = animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);

        _leftHandInfo = new HandInfo {
            Side = Hand.Left,
            AvatarRoot = avatarDescriptor.transform,
            HipsTransform = _hips,
            NeckTransform = _neck,
            ArmTransform = lArmTransform,
            ElbowTransform = lElbowTransform,
            HandTransform = lHandTransform,
            // ThumbTransform = lThumbTransform,
            ArmLength = (Vector3.Distance(lArmTransform.position, lElbowTransform.position) +
                        Vector3.Distance(lElbowTransform.position, lHandTransform.position))
                        / Mathf.Max(avatarDescriptor.transform.lossyScale.x, 0.00001f),
        };

        var rHandTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var rElbowTransform = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var rArmTransform = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        // var rThumbTransform = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);

        _rightHandInfo = new HandInfo {
            Side = Hand.Right,
            AvatarRoot = avatarDescriptor.transform,
            HipsTransform = _hips,
            NeckTransform = _neck,
            ArmTransform = rArmTransform,
            ElbowTransform = rElbowTransform,
            HandTransform = rHandTransform,
            // ThumbTransform = rThumbTransform,
            ArmLength = Vector3.Distance(rArmTransform.position, rElbowTransform.position) +
                        Vector3.Distance(rElbowTransform.position, rHandTransform.position),
        };

        _onLateUpdate += ProcessUpdate;
    }

    private void OnDestroy() {
        _onLateUpdate -= ProcessUpdate;
    }

    private void ProcessUpdate() {

        if (!ModConfig.MeCustomFlightInVR.Value && MetaPort.Instance.isUsingVr || !ModConfig.MeCustomFlightInDesktop.Value && !MetaPort.Instance.isUsingVr) return;

        // Ignore if the avatar is disabled
        if (ModConfig.MeUseAvatarOverrides.Value && !ConfigJson.GetCurrentAvatarEnabled()) return;

        // Process Flapping
        ProcessHandFlap(_leftHandInfo);
        ProcessHandFlap(_rightHandInfo);

        // Process Gliding
        ProcessHandsGlide(_leftHandInfo, _rightHandInfo);

        // Check if we flappin
        if (_leftHandInfo.FlapState == FlapState.Flapped && _rightHandInfo.FlapState == FlapState.Flapped) {
            Flapped?.Invoke(_leftHandInfo.FlapVelocity, _leftHandInfo.FlapDirection,
                _rightHandInfo.FlapVelocity, _rightHandInfo.FlapDirection);
            _leftHandInfo.FlapState = FlapState.Idle;
            _rightHandInfo.FlapState = FlapState.Idle;
        }

    }

    private void ProcessHandFlap(HandInfo handInfo) {

        switch (handInfo.FlapState) {
            case FlapState.Idle:
                // Check if our hands are above our head, or we're gliding
                if (handInfo.HandTransform.position.y > _head.position.y || (_isGliding && handInfo.HandTransform.position.y > _spine.position.y)) {
                    handInfo.FlapState = FlapState.StartingFlap;
                }

                break;

            case FlapState.StartingFlap:
                // Check if our hands stop being above our head, which means we start the flap
                if (handInfo.HandTransform.position.y <= _head.position.y || (_isGliding && handInfo.HandTransform.position.y > _spine.position.y)) {
                    handInfo.FlapStartedTime = Time.time;
                    var handPosition = handInfo.HandTransform.position;
                    handInfo.InitialPositionFromAvatar = PlayerSetup.Instance._avatar.transform.InverseTransformPoint(handPosition);
                    handInfo.PreviousPosition = handPosition;
                    handInfo.DistanceFlapped = 0f;
                    handInfo.FlapState = FlapState.Flapping;
                }

                break;

            case FlapState.Flapping:
                // Keep checking if the movement is going down and the timer to timeout
                // If gucci keep checking for hand under the hips
                // If stop moving downwards or timed out
                if (handInfo.PreviousPosition.y < handInfo.HandTransform.position.y ||
                    Time.time > handInfo.FlapStartedTime + timeThreshold) {
                    handInfo.FlapState = FlapState.Idle;
                    break;
                }

                // Add to the Distance Flapped
                var currentHandPosition = handInfo.HandTransform.position;
                handInfo.DistanceFlapped += Vector3.Distance(handInfo.PreviousPosition, currentHandPosition);
                handInfo.PreviousPosition = currentHandPosition;
                // Check if our hands are under our Hips
                if (handInfo.HandTransform.position.y < _hips.position.y || (_isGliding && handInfo.HandTransform.position.y < _spine.position.y)) {
                    handInfo.FlapState = FlapState.Flapped;
                    // Calculate the flap velocity by using the accumulated flapped distance across the time it took
                    handInfo.FlapVelocity = handInfo.DistanceFlapped / (Time.time - handInfo.FlapStartedTime);
                    // handInfo.FlapDirectionNormalized = (handInfo.InitialPosition - currentHandPosition).normalized;
                    handInfo.FlapDirection = handInfo.InitialPositionFromAvatar - PlayerSetup.Instance._avatar.transform.InverseTransformPoint(currentHandPosition);
                }

                break;

            case FlapState.Flapped:
                // Keep checking the timer to cancel...
                if (Time.time > handInfo.FlapStartedTime + timeThreshold) {
                    handInfo.FlapState = FlapState.Idle;
                }

                break;
        }
    }

    private bool _isGliding;

    private void ProcessHandsGlide(HandInfo leftHandInfo, HandInfo rightHandInfo) {

        // Update Human Pose
        _humanPoseHandler.GetHumanPose(ref _humanPose);

        var isGrounded = MovementSystem.Instance._isGrounded;

        // Conditions to check if the avatar is in gliding position
        var armsStretched = _leftHandInfo.GetCurrentArmSpan() > _leftHandInfo.ArmLength * 0.8f &&
                             _rightHandInfo.GetCurrentArmSpan() > _rightHandInfo.ArmLength * 0.8f;

        // var leftArmDownUp = GetLeftArmDownUp();
        // var rightArmDownUp = GetRightArmDownUp();

        var leftArmDownUp = leftHandInfo.GetCurrentArmAngle();
        var rightArmDownUp = rightHandInfo.GetCurrentArmAngle();

        var leftWristTwist = GetLeftWristTwist();
        var rightWristTwist = GetRightWristTwist();

        // Check if arms are mirrored or with a small difference
        var leftArmDirection = _leftHandInfo.HandTransform.position - _leftHandInfo.ArmTransform.position;
        var rightArmDirection = _rightHandInfo.HandTransform.position - _rightHandInfo.ArmTransform.position;
        var armsAligned = Vector3.Angle(leftArmDirection, rightArmDirection) >= ArmsAlignmentMinAngle;

        // float armsAngle = Vector3.Angle(leftArmDirection, rightArmDirection);
        // var areArmsOpposed = leftArmDownUp * rightArmDownUp < 0 || Math.Abs(leftArmDownUp + rightArmDownUp) < ArmDownUpDifferenceThreshold;

        float x;
        if (ModConfig.MeGlidingRotationLikeAirfoils.Value) {
            x = rightWristTwist - leftWristTwist;
            x *= ModConfig.MeRotationAirfoilsSensitivity.Value;
        }
        else {
            var leftArmNormalized = Mathf.Lerp(-1, 1, Mathf.InverseLerp(ArmAngleMinThreshold, ArmAngleMaxThreshold, Mathf.Clamp(leftArmDownUp, ArmAngleMinThreshold, ArmAngleMaxThreshold)));
            var rightArmNormalized = Mathf.Lerp(1, -1, Mathf.InverseLerp(ArmAngleMinThreshold, ArmAngleMaxThreshold, Mathf.Clamp(rightArmDownUp, ArmAngleMinThreshold, ArmAngleMaxThreshold)));
            x = (leftArmNormalized + rightArmNormalized) / 2f * 0.5f;
        }

        // Calculate normalized wrist twist. We can average the left and right values here too.
        var y = ((leftWristTwist / WristTwistThreshold) + (rightWristTwist / WristTwistThreshold)) / 2f;

        var glidingVector = new Vector2(x, y);

        var armsAngled = leftArmDownUp is <= ArmAngleMaxThreshold and >= ArmAngleMinThreshold
                   && rightArmDownUp is <= ArmAngleMaxThreshold and >= ArmAngleMinThreshold
                   && leftWristTwist is <= WristTwistThreshold and >= -WristTwistThreshold
                   && rightWristTwist is <= WristTwistThreshold and >= -WristTwistThreshold;

        var isProperGliding = armsStretched && armsAligned && armsAngled;

        // Handle the arms up/down settings
        var isJankyGliding = false;
        var bothArmsDown = leftArmDownUp < ArmAngleMinBreakThreshold && rightArmDownUp < ArmAngleMinBreakThreshold;
        var bothArmsUp = leftArmDownUp > ArmAngleMaxBreakThreshold && rightArmDownUp > ArmAngleMaxBreakThreshold;
        if (ModConfig.MeBothArmsDownToStopGliding.Value && ModConfig.MeBothArmsUpToStopGliding.Value) {
            isJankyGliding = _isGliding && !bothArmsDown && !bothArmsUp;
        }
        else if (ModConfig.MeBothArmsDownToStopGliding.Value) {
            isJankyGliding = _isGliding && !bothArmsDown;
        }
        else if (ModConfig.MeBothArmsUpToStopGliding.Value) {
            isJankyGliding = _isGliding && !bothArmsUp;
        }

        if (!isGrounded && (isProperGliding || isJankyGliding)) {
            Gliding?.Invoke(true, glidingVector);
            _isGliding = true;
            _leftHandInfo.GlideState = GlideState.Gliding;
            _rightHandInfo.GlideState = GlideState.Gliding;
        }
        else {
            Gliding?.Invoke(false, glidingVector);
            _isGliding = false;
            _leftHandInfo.GlideState = GlideState.Idle;
            _rightHandInfo.GlideState = GlideState.Idle;
        }

        #if DEBUG
        _armsStretched = armsStretched;
        _armsAligned = armsAligned;
        _leftArmDownUp = leftArmDownUp;
        _rightArmDownUp = rightArmDownUp;
        _leftWristTwist = leftWristTwist;
        _rightWristTwist = rightWristTwist;
        _isGliding = _leftHandInfo.GlideState == GlideState.Gliding && _rightHandInfo.GlideState == GlideState.Gliding;
        _glidingVector = glidingVector;
        _leftArmAngle = _leftHandInfo.GetCurrentArmAngle();
        _rightArmAngle = _rightHandInfo.GetCurrentArmAngle();
        #endif
    }

    #if DEBUG
    private const int LabelHeight = 24;
    private bool _armsStretched;
    private bool _armsAligned;
    private float _leftArmDownUp;
    private float _rightArmDownUp;
    private float _leftWristTwist;
    private float _rightWristTwist;
    private bool _isGliding;
    private Vector3 _glidingVector;
    private float _leftArmAngle;
    private float _rightArmAngle;

    private void OnGUI() {
        var i = 0;
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_armsStretched: {_armsStretched}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_armsAligned: {_armsAligned}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftArmDownUp: {_leftArmDownUp:F2}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightArmDownUp: {_rightArmDownUp:F2}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftWristTwist: {_leftWristTwist:F2}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightWristTwist: {_rightWristTwist:F2}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_isGliding: {_isGliding}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_glidingVector: {_glidingVector.ToString("F2")}");
        i++;
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftArmAngle: {_leftArmAngle:F2}");
        GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightArmAngle: {_rightArmAngle:F2}");
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftShoulderUpDownIdx: {_humanPose.muscles[_leftShoulderUpDownIdx]:F2}");
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftArmUpDownIdx: {_humanPose.muscles[_leftArmUpDownIdx]:F2}");
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_leftHandUpDownIdx: {_humanPose.muscles[_leftHandUpDownIdx]:F2}");
        // i++;
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightShoulderUpDownIdx: {_humanPose.muscles[_rightShoulderUpDownIdx]:F2}");
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightArmUpDownIdx: {_humanPose.muscles[_rightArmUpDownIdx]:F2}");
        // GUI.Label(new Rect(0, LabelHeight*i++, 256, LabelHeight), $"_rightHandUpDownIdx: {_humanPose.muscles[_rightHandUpDownIdx]:F2}");
    }
    #endif

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.LateUpdate))]
        public static void After_PlayerSetup_LateUpdate(PlayerSetup __instance) {
            try {
                _onLateUpdate?.Invoke();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }

    public enum MuscleBone {
        [Description("Spine Front-Back")]
        SpineFrontBack,
        [Description("Spine Left-Right")]
        SpineLeftRight,
        [Description("Spine Twist Left-Right")]
        SpineTwistLeftRight,
        [Description("Chest Front-Back")]
        ChestFrontBack,
        [Description("Chest Left-Right")]
        ChestLeftRight,
        [Description("Chest Twist Left-Right")]
        ChestTwistLeftRight,
        [Description("UpperChest Front-Back")]
        UpperChestFrontBack,
        [Description("UpperChest Left-Right")]
        UpperChestLeftRight,
        [Description("UpperChest Twist Left-Right")]
        UpperChestTwistLeftRight,
        [Description("Neck Nod Down-Up")]
        NeckNodDownUp,
        [Description("Neck Tilt Left-Right")]
        NeckTiltLeftRight,
        [Description("Neck Turn Left-Right")]
        NeckTurnLeftRight,
        [Description("Head Nod Down-Up")]
        HeadNodDownUp,
        [Description("Head Tilt Left-Right")]
        HeadTiltLeftRight,
        [Description("Head Turn Left-Right")]
        HeadTurnLeftRight,
        [Description("Left Eye Down-Up")]
        LeftEyeDownUp,
        [Description("Left Eye In-Out")]
        LeftEyeInOut,
        [Description("Right Eye Down-Up")]
        RightEyeDownUp,
        [Description("Right Eye In-Out")]
        RightEyeInOut,
        [Description("Jaw Close")]
        JawClose,
        [Description("Jaw Left-Right")]
        JawLeftRight,
        [Description("Left Upper Leg Front-Back")]
        LeftUpperLegFrontBack,
        [Description("Left Upper Leg In-Out")]
        LeftUpperLegInOut,
        [Description("Left Upper Leg Twist In-Out")]
        LeftUpperLegTwistInOut,
        [Description("Left Lower Leg Stretch")]
        LeftLowerLegStretch,
        [Description("Left Lower Leg Twist In-Out")]
        LeftLowerLegTwistInOut,
        [Description("Left Foot Up-Down")]
        LeftFootUpDown,
        [Description("Left Foot Twist In-Out")]
        LeftFootTwistInOut,
        [Description("Left Toes Up-Down")]
        LeftToesUpDown,
        [Description("Right Upper Leg Front-Back")]
        RightUpperLegFrontBack,
        [Description("Right Upper Leg In-Out")]
        RightUpperLegInOut,
        [Description("Right Upper Leg Twist In-Out")]
        RightUpperLegTwistInOut,
        [Description("Right Lower Leg Stretch")]
        RightLowerLegStretch,
        [Description("Right Lower Leg Twist In-Out")]
        RightLowerLegTwistInOut,
        [Description("Right Foot Up-Down")]
        RightFootUpDown,
        [Description("Right Foot Twist In-Out")]
        RightFootTwistInOut,
        [Description("Right Toes Up-Down")]
        RightToesUpDown,
        [Description("Left Shoulder Down-Up")]
        LeftShoulderDownUp,
        [Description("Left Shoulder Front-Back")]
        LeftShoulderFrontBack,
        [Description("Left Arm Down-Up")]
        LeftArmDownUp,
        [Description("Left Arm Front-Back")]
        LeftArmFrontBack,
        [Description("Left Arm Twist In-Out")]
        LeftArmTwistInOut,
        [Description("Left Forearm Stretch")]
        LeftForearmStretch,
        [Description("Left Forearm Twist In-Out")]
        LeftForearmTwistInOut,
        [Description("Left Hand Down-Up")]
        LeftHandDownUp,
        [Description("Left Hand In-Out")]
        LeftHandInOut,
        [Description("Right Shoulder Down-Up")]
        RightShoulderDownUp,
        [Description("Right Shoulder Front-Back")]
        RightShoulderFrontBack,
        [Description("Right Arm Down-Up")]
        RightArmDownUp,
        [Description("Right Arm Front-Back")]
        RightArmFrontBack,
        [Description("Right Arm Twist In-Out")]
        RightArmTwistInOut,
        [Description("Right Forearm Stretch")]
        RightForearmStretch,
        [Description("Right Forearm Twist In-Out")]
        RightForearmTwistInOut,
        [Description("Right Hand Down-Up")]
        RightHandDownUp,
        [Description("Right Hand In-Out")]
        RightHandInOut,
        [Description("Left Thumb 1 Stretched")]
        LeftThumb1Stretched,
        [Description("Left Thumb Spread")]
        LeftThumbSpread,
        [Description("Left Thumb 2 Stretched")]
        LeftThumb2Stretched,
        [Description("Left Thumb 3 Stretched")]
        LeftThumb3Stretched,
        [Description("Left Index 1 Stretched")]
        LeftIndex1Stretched,
        [Description("Left Index Spread")]
        LeftIndexSpread,
        [Description("Left Index 2 Stretched")]
        LeftIndex2Stretched,
        [Description("Left Index 3 Stretched")]
        LeftIndex3Stretched,
        [Description("Left Middle 1 Stretched")]
        LeftMiddle1Stretched,
        [Description("Left Middle Spread")]
        LeftMiddleSpread,
        [Description("Left Middle 2 Stretched")]
        LeftMiddle2Stretched,
        [Description("Left Middle 3 Stretched")]
        LeftMiddle3Stretched,
        [Description("Left Ring 1 Stretched")]
        LeftRing1Stretched,
        [Description("Left Ring Spread")]
        LeftRingSpread,
        [Description("Left Ring 2 Stretched")]
        LeftRing2Stretched,
        [Description("Left Ring 3 Stretched")]
        LeftRing3Stretched,
        [Description("Left Little 1 Stretched")]
        LeftLittle1Stretched,
        [Description("Left Little Spread")]
        LeftLittleSpread,
        [Description("Left Little 2 Stretched")]
        LeftLittle2Stretched,
        [Description("Left Little 3 Stretched")]
        LeftLittle3Stretched,
        [Description("Right Thumb 1 Stretched")]
        RightThumb1Stretched,
        [Description("Right Thumb Spread")]
        RightThumbSpread,
        [Description("Right Thumb 2 Stretched")]
        RightThumb2Stretched,
        [Description("Right Thumb 3 Stretched")]
        RightThumb3Stretched,
        [Description("Right Index 1 Stretched")]
        RightIndex1Stretched,
        [Description("Right Index Spread")]
        RightIndexSpread,
        [Description("Right Index 2 Stretched")]
        RightIndex2Stretched,
        [Description("Right Index 3 Stretched")]
        RightIndex3Stretched,
        [Description("Right Middle 1 Stretched")]
        RightMiddle1Stretched,
        [Description("Right Middle Spread")]
        RightMiddleSpread,
        [Description("Right Middle 2 Stretched")]
        RightMiddle2Stretched,
        [Description("Right Middle 3 Stretched")]
        RightMiddle3Stretched,
        [Description("Right Ring 1 Stretched")]
        RightRing1Stretched,
        [Description("Right Ring Spread")]
        RightRingSpread,
        [Description("Right Ring 2 Stretched")]
        RightRing2Stretched,
        [Description("Right Ring 3 Stretched")]
        RightRing3Stretched,
        [Description("Right Little 1 Stretched")]
        RightLittle1Stretched,
        [Description("Right Little Spread")]
        RightLittleSpread,
        [Description("Right Little 2 Stretched")]
        RightLittle2Stretched,
        [Description("Right Little 3 Stretched")]
        RightLittle3Stretched,
    }

    public static string GetDescriptionFromEnumValue(Enum value) {
        var valueStr = value.ToString();
        return value.GetType()
            .GetField(valueStr)
            .GetCustomAttributes(typeof (DescriptionAttribute), false)
            .SingleOrDefault() is not DescriptionAttribute attribute ? valueStr : attribute.Description;
    }
}
