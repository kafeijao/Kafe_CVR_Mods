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

    public float timeThreshold = 0.5f;

    private const float ArmDownUpThreshold = 0.35f;
    private const float ArmsAlignmentMinAngle = 135f;
    private const float WristTwistThreshold = 0.30f;

    private Transform _head;
    private Transform _hips;

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
        public Vector3 InitialPosition;
        public Vector3 FlapDirectionNormalized;
        public Vector3 PreviousPosition;
        public float DistanceFlapped;

        public float FlapVelocity;

        // Gliding
        public GlideState GlideState;
        public Transform AvatarRoot;
        public Transform ArmTransform;
        public Transform ElbowTransform;
        public Transform ThumbTransform;
        public float ArmLength;

        public float GetCurrentArmSpan() => Vector3.Distance(ArmTransform.position, HandTransform.position);

        public float GetCurrentArmAngleSum() {
            // First, calculate the global up vector of the parent transform (e.g., the body)
            var parentUp = ArmTransform.parent.up;

            // Then, calculate the vectors that the arm, elbow and hand should point along
            var armVector = ElbowTransform.position - ArmTransform.position;
            var elbowVector = HandTransform.position - ElbowTransform.position;
            var handVector = HandTransform.forward; // assuming the hand points in its forward direction

            // Next, calculate the angles between these vectors and the global up vector of the parent
            var armAngle = Vector3.Angle(parentUp, armVector);
            var elbowAngle = Vector3.Angle(armVector, elbowVector);
            var handAngle = Vector3.Angle(elbowVector, handVector);

            // Now sum up the angles to get the total angle
            return armAngle + elbowAngle + handAngle;
        }

        public float GetCurrentHandAngle() {

            // You can get the direction in which the thumb is pointing by subtracting the position of the hand from the position of the thumb
            var thumbDirection = ThumbTransform.position - HandTransform.position;

            // Normalize the direction
            thumbDirection.Normalize();

            // Get the direction in which the character is pointing
            var characterDirection = AvatarRoot.forward;

            // Project the thumb's direction onto the scene up axis
            var projectedThumbDirection = Vector3.ProjectOnPlane(thumbDirection, AvatarRoot.up);

            // Normalize the projected direction
            projectedThumbDirection.Normalize();

            // Get the angle between the thumb direction and the character direction in degrees
            var angle = Vector3.Angle(projectedThumbDirection, characterDirection);

            return angle;
        }

        public float GetCurrentArmAngle() {

            // Get the direction in which the arm is pointing by subtracting the position of the shoulder from the position of the hand
            var armDirection = HandTransform.position - HandTransform.parent.position;

            // Normalize the direction
            armDirection.Normalize();

            // Project the arm's direction onto the scene up axis
            var projectedArmDirection = Vector3.ProjectOnPlane(armDirection, AvatarRoot.up);

            // Normalize the projected direction
            projectedArmDirection.Normalize();

            // Calculate the dot product between the scene's up vector and the arm's direction. This will give us the cosine of the angle between these vectors
            var cosineAngle = Vector3.Dot(AvatarRoot.up, armDirection);

            // The acos function returns the angle in radians, so convert it to degrees
            var angleInDegrees = Mathf.Acos(cosineAngle) * Mathf.Rad2Deg;

            // Since the range of acos is 0 to 180, we need to change it to -90 to 90. If the arm is pointing down, the z component of armDirection will be negative.
            if (armDirection.z < 0) {
                angleInDegrees = -angleInDegrees;
            }

            return angleInDegrees;
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
        #if DEBUG
        if (Time.time > _lastUpdate) MelonLogger.Msg($"LeftArmUpDown: {_humanPose.muscles[_leftShoulderUpDownIdx]:F2} + {_humanPose.muscles[_leftArmUpDownIdx]:F2} + {_humanPose.muscles[_leftHandUpDownIdx]:F2} + = {((_humanPose.muscles[_leftShoulderUpDownIdx] + _humanPose.muscles[_leftArmUpDownIdx] + _humanPose.muscles[_leftHandUpDownIdx])/3f):F2}");
        #endif
        return (_humanPose.muscles[_leftShoulderUpDownIdx] + _humanPose.muscles[_leftArmUpDownIdx] + _humanPose.muscles[_leftHandUpDownIdx]) / 3f;
        // arm down -30, limit -35, up 30 / limit 35
        // MelonLogger.Msg($"ArmUpDown: {humanPose.muscles[_spineLeftRightIdx]:F2} + {humanPose.muscles[_chestLeftRightIdx]:F2} + {humanPose.muscles[_leftShoulderUpDownIdx]:F2} + {humanPose.muscles[_leftArmUpDownIdx]:F2} + {humanPose.muscles[_leftHandUpDownIdx]:F2} + = {((humanPose.muscles[_leftShoulderUpDownIdx] + humanPose.muscles[_leftArmUpDownIdx] + humanPose.muscles[_leftHandUpDownIdx])/5f):F2}");
        // return (humanPose.muscles[_spineLeftRightIdx] + humanPose.muscles[_chestLeftRightIdx] + humanPose.muscles[_leftShoulderUpDownIdx] + humanPose.muscles[_leftArmUpDownIdx] + humanPose.muscles[_leftHandUpDownIdx]) / 5f;
    }

    private float GetLeftWristTwist() {
        #if DEBUG
        if (Time.time > _lastUpdate) MelonLogger.Msg($"LeftHandTwist: {_humanPose.muscles[_leftArmTwistIdx]:F2} + {_humanPose.muscles[_leftForearmTwistIdx]:F2} ={((_humanPose.muscles[_leftArmTwistIdx]+_humanPose.muscles[_leftForearmTwistIdx])/2f):F2}");
        #endif
        return (_humanPose.muscles[_leftArmTwistIdx] + _humanPose.muscles[_leftForearmTwistIdx]) / 2f;
        // -0.25/-0.30 when thumb down, +0.25/0.30 when thumb up
    }

    private float GetRightArmDownUp() {
        #if DEBUG
        if (Time.time > _lastUpdate) MelonLogger.Msg($"RightArmUpDown: {_humanPose.muscles[_rightShoulderUpDownIdx]:F2} + {_humanPose.muscles[_rightArmUpDownIdx]:F2} + {_humanPose.muscles[_rightHandUpDownIdx]:F2} + = {((_humanPose.muscles[_rightShoulderUpDownIdx] + _humanPose.muscles[_rightArmUpDownIdx] + _humanPose.muscles[_rightHandUpDownIdx])/3f):F2}");
        #endif
        return (_humanPose.muscles[_rightShoulderUpDownIdx] + _humanPose.muscles[_rightArmUpDownIdx] + _humanPose.muscles[_rightHandUpDownIdx]) / 3f;
        // arm down -30, limit -35, up 30 / limit 35
       }

    private float GetRightWristTwist() {
        #if DEBUG
        if (Time.time > _lastUpdate) MelonLogger.Msg($"RightHandTwist: {_humanPose.muscles[_rightArmTwistIdx]:F2} + {_humanPose.muscles[_rightForearmTwistIdx]:F2} ={((_humanPose.muscles[_rightArmTwistIdx]+_humanPose.muscles[_rightForearmTwistIdx])/2f):F2}");
        #endif
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
        _hips = animator.GetBoneTransform(HumanBodyBones.Hips);

        var lArmTransform = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var lElbowTransform = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        var lHandTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var lThumbTransform = animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal);

        _leftHandInfo = new HandInfo {
            Side = Hand.Left,
            AvatarRoot = avatarDescriptor.transform,
            ArmTransform = lArmTransform,
            ElbowTransform = lElbowTransform,
            HandTransform = lHandTransform,
            ThumbTransform = lThumbTransform,
            ArmLength = Vector3.Distance(lArmTransform.position, lElbowTransform.position) +
                        Vector3.Distance(lElbowTransform.position, lHandTransform.position),
        };

        var rHandTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var rElbowTransform = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var rArmTransform = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var rThumbTransform = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);

        _rightHandInfo = new HandInfo {
            Side = Hand.Right,
            AvatarRoot = avatarDescriptor.transform,
            ArmTransform = rArmTransform,
            ElbowTransform = rElbowTransform,
            HandTransform = rHandTransform,
            ThumbTransform = rThumbTransform,
            ArmLength = Vector3.Distance(rArmTransform.position, rElbowTransform.position) +
                        Vector3.Distance(rElbowTransform.position, rHandTransform.position),
        };

        _onLateUpdate += ProcessUpdate;
    }

    private void OnDestroy() {
        _onLateUpdate -= ProcessUpdate;
    }

    private bool isGlidingOverride = false;

    private void ProcessUpdate() {

        if (!ModConfig.MeFlapToFly.Value || !MetaPort.Instance.isUsingVr) return;

        if (Input.GetKeyDown(KeyCode.PageUp)) {
            isGlidingOverride = true;
        }
        if (Input.GetKeyDown(KeyCode.PageDown)) {
            isGlidingOverride = false;
        }

        // Process Flapping
        ProcessHandFlap(_leftHandInfo);
        ProcessHandFlap(_rightHandInfo);

        // Process Gliding
        ProcessHandsGlide();

        // Check if we flappin
        if (_leftHandInfo.FlapState == FlapState.Flapped && _rightHandInfo.FlapState == FlapState.Flapped) {
            Flapped?.Invoke(_leftHandInfo.FlapVelocity, _leftHandInfo.FlapDirectionNormalized,
                _rightHandInfo.FlapVelocity, _rightHandInfo.FlapDirectionNormalized);
            _leftHandInfo.FlapState = FlapState.Idle;
            _rightHandInfo.FlapState = FlapState.Idle;
        }

    }

    private void ProcessHandFlap(HandInfo handInfo) {

        switch (handInfo.FlapState) {
            case FlapState.Idle:
                // Check if our hands are above our head
                if (handInfo.HandTransform.position.y > _head.position.y) {
                    handInfo.FlapState = FlapState.StartingFlap;
                    #if DEBUG
                    MelonLogger.Msg($"Starting Hand: {handInfo.Side.ToString()}...");
                    #endif
                }

                break;

            case FlapState.StartingFlap:
                // Check if our hands stop being above our head, which means we start the flap
                if (handInfo.HandTransform.position.y <= _head.position.y) {
                    handInfo.FlapStartedTime = Time.time;
                    var handPosition = handInfo.HandTransform.position;
                    handInfo.InitialPosition = handPosition;
                    handInfo.PreviousPosition = handPosition;
                    handInfo.DistanceFlapped = 0f;
                    handInfo.FlapState = FlapState.Flapping;
                    #if DEBUG
                    MelonLogger.Msg($"Flapping Hand: {handInfo.Side.ToString()}...");
                    #endif
                }

                break;

            case FlapState.Flapping:
                // Keep checking if the movement is going down and the timer to timeout
                // If gucci keep checking for hand under the hips
                // If stop moving downwards or timed out
                if (handInfo.PreviousPosition.y < handInfo.HandTransform.position.y ||
                    Time.time > handInfo.FlapStartedTime + timeThreshold) {
                    handInfo.FlapState = FlapState.Idle;

                    #if DEBUG
                    MelonLogger.Msg($"Failed! Hand: {handInfo.Side.ToString()}");
                    #endif
                    break;
                }

                // Add to the Distance Flapped
                var currentHandPosition = handInfo.HandTransform.position;
                handInfo.DistanceFlapped += Vector3.Distance(handInfo.PreviousPosition, currentHandPosition);
                handInfo.PreviousPosition = currentHandPosition;
                // Check if our hands are under our Hips
                if (handInfo.HandTransform.position.y < _hips.position.y) {
                    handInfo.FlapState = FlapState.Flapped;
                    // Calculate the flap velocity by using the accumulated flapped distance across the time it took
                    handInfo.FlapVelocity = handInfo.DistanceFlapped / (Time.time - handInfo.FlapStartedTime);
                    handInfo.FlapDirectionNormalized = (handInfo.InitialPosition - currentHandPosition).normalized;

                    #if DEBUG
                    MelonLogger.Msg($"Flapped Hand: {handInfo.Side.ToString()}");
                    #endif
                }

                break;

            case FlapState.Flapped:
                // Keep checking the timer to cancel...
                if (Time.time > handInfo.FlapStartedTime + timeThreshold) {
                    handInfo.FlapState = FlapState.Idle;
                    #if DEBUG
                    MelonLogger.Msg($"Failed! Hand: {handInfo.Side.ToString()}");
                    #endif
                }

                break;
        }

    }

    private float _lastUpdate = 0f;

    private void ProcessHandsGlide() {
        // // leftArmSceneUpAngle: 71.04 | rightArmSceneUpAngle: 113.31 | armsAngle: 169.87 | armStretchLeft: 0.42/0.42=1.00 | armStretchRight: 0.42/0.42=1.00 | armsStretched: True | armStretch: 0.42/0.42=1.00
        //
        //
        // // Get the angles between arms and scene up
        // float leftArmSceneUpAngle = Vector3.Angle(_leftHandInfo.ArmTransform.up, Vector3.up);
        // float rightArmSceneUpAngle = Vector3.Angle(_rightHandInfo.ArmTransform.up, Vector3.up);
        //
        // // Get the angle between arms
        // float armsAngle = Vector3.Angle(_leftHandInfo.ArmTransform.up, _rightHandInfo.ArmTransform.up);
        //
        //
        // // Calculate angle in degrees between hand's right direction (thumb direction) and reference direction
        // // rightThump up = 0, rightThumb down = 180, left is the opposite
        // float leftThumbSceneUpAngle = Vector3.Angle(_leftHandInfo.HandTransform.right, Vector3.up);
        // float rightThumbSceneUpAngle = Vector3.Angle(_rightHandInfo.HandTransform.right, Vector3.up);
        //
        // bool handsAreKindaFlat =
        //     leftThumbSceneUpAngle is >= 25f and <= 155f && rightThumbSceneUpAngle is >= 25f and <= 155f;
        //
        // // Conditions to check if the avatar is in gliding position
        // bool armsStretched = _leftHandInfo.GetCurrentArmSpan() > _leftHandInfo.ArmLength * 0.9f &&
        //                      _rightHandInfo.GetCurrentArmSpan() > _rightHandInfo.ArmLength * 0.9f;
        //
        // bool armsPointingOpposite = armsAngle > 150f;
        // bool armsNotTooVertical =
        //     leftArmSceneUpAngle is >= 40f and <= 140f && rightArmSceneUpAngle is >= 40f and <= 140f;
        //
        // if (armsStretched && armsPointingOpposite && armsNotTooVertical && handsAreKindaFlat) {
        //     if (_leftHandInfo.GlideState != GlideState.Gliding || _rightHandInfo.GlideState != GlideState.Gliding) {
        //         _leftHandInfo.GlideState = GlideState.Gliding;
        //         _rightHandInfo.GlideState = GlideState.Gliding;
        //         #if DEBUG
        //         MelonLogger.Msg("Gliding started...");
        //         #endif
        //     }
        // }
        // else {
        //     if (_leftHandInfo.GlideState != GlideState.Idle || _rightHandInfo.GlideState != GlideState.Idle) {
        //         _leftHandInfo.GlideState = GlideState.Idle;
        //         _rightHandInfo.GlideState = GlideState.Idle;
        //         #if DEBUG
        //         MelonLogger.Msg("Gliding stopped...");
        //         #endif
        //     }
        // }

        // Update Human Pose
        _humanPoseHandler.GetHumanPose(ref _humanPose);

        var isGrounded = MovementSystem.Instance._isGrounded;

        // Conditions to check if the avatar is in gliding position
        var armsStretched = _leftHandInfo.GetCurrentArmSpan() > _leftHandInfo.ArmLength * 0.9f &&
                             _rightHandInfo.GetCurrentArmSpan() > _rightHandInfo.ArmLength * 0.9f;

        var leftArmDownUp = GetLeftArmDownUp();
        var rightArmDownUp = GetRightArmDownUp();
        var leftWristTwist = GetLeftWristTwist();
        var rightWristTwist = GetRightWristTwist();

        // Check if arms are mirrored or with a small difference
        var leftArmDirection = _leftHandInfo.HandTransform.position - _leftHandInfo.ArmTransform.position;
        var rightArmDirection = _rightHandInfo.HandTransform.position - _rightHandInfo.ArmTransform.position;
        var armsAligned = Vector3.Angle(leftArmDirection, rightArmDirection) >= ArmsAlignmentMinAngle;
        #if DEBUG
        if (Time.time > _lastUpdate) MelonLogger.Msg($"ArmsAngle: {Vector3.Angle(leftArmDirection, rightArmDirection):F2}, aligned: {armsAligned}");
        #endif
        // float armsAngle = Vector3.Angle(leftArmDirection, rightArmDirection);
        // var areArmsOpposed = leftArmDownUp * rightArmDownUp < 0 || Math.Abs(leftArmDownUp + rightArmDownUp) < ArmDownUpDifferenceThreshold;

        // Calculate normalized arm down up.
        var leftArmNormalized = leftArmDownUp / ArmDownUpThreshold; // values range from -1 to +1
        var rightArmNormalized = -rightArmDownUp / ArmDownUpThreshold; // values range from -1 to +1, inverted for right arm
        var x = (leftArmNormalized + rightArmNormalized) / 2f;

        // Calculate normalized wrist twist. We can average the left and right values here too.
        var y = ((leftWristTwist / WristTwistThreshold) + (rightWristTwist / WristTwistThreshold)) / 2f;

        var glidingVector = new Vector2(x, y);

        if (isGlidingOverride || !isGrounded && armsStretched && armsAligned
            && leftArmDownUp is <= ArmDownUpThreshold and >= -ArmDownUpThreshold
            && rightArmDownUp is <= ArmDownUpThreshold and >= -ArmDownUpThreshold
            && leftWristTwist is <= WristTwistThreshold and >= -WristTwistThreshold
            && rightWristTwist is <= WristTwistThreshold and >= -WristTwistThreshold) {

            Gliding?.Invoke(true, glidingVector);
            if (_leftHandInfo.GlideState != GlideState.Gliding || _rightHandInfo.GlideState != GlideState.Gliding) {
                _leftHandInfo.GlideState = GlideState.Gliding;
                _rightHandInfo.GlideState = GlideState.Gliding;
            }
        }
        else {
            Gliding?.Invoke(false, glidingVector);
            if (_leftHandInfo.GlideState != GlideState.Idle || _rightHandInfo.GlideState != GlideState.Idle) {
                _leftHandInfo.GlideState = GlideState.Idle;
                _rightHandInfo.GlideState = GlideState.Idle;
            }
        }


        if (Time.time > _lastUpdate) {

            #if DEBUG
            MelonLogger.Msg($"armsStretched: {armsStretched} | " +
                            $"armsAligned: {armsAligned} | " +
                            $"leftArmDownUp: {leftArmDownUp:F2} | " +
                            $"rightArmDownUp: {rightArmDownUp:F2} | " +
                            $"leftWristTwist: {leftWristTwist:F2} | " +
                            $"rightWristTwist: {rightWristTwist:F2} | " +
                            $"isGliding: {isGliding} | " +
                            $"glidingVector: {glidingVector.ToString("F2")} | " +
                            "");
            #endif

            // MelonLogger.Msg($"leftArmSceneUpAngle: {leftArmSceneUpAngle:F2} | " +
            //                 $"rightArmSceneUpAngle: {rightArmSceneUpAngle:F2} | " +
            //                 $"armsAngle: {armsAngle:F2} | " +
            //                 $"leftThumbSceneUpAngle: {leftThumbSceneUpAngle:F2} | " +
            //                 $"rightThumbSceneUpAngle: {rightThumbSceneUpAngle:F2} | " +
            //
            //                 $"leftArmSum: {_leftHandInfo.GetCurrentArmAngleSum():F2} | " +
            //                 $"leftArmAngle: {_leftHandInfo.GetCurrentArmAngle():F2} | " +
            //                 $"leftHandAngle: {_leftHandInfo.GetCurrentHandAngle():F2} | " +
            //                 "");
            _lastUpdate = Time.time + 1f;
        }
    }

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
