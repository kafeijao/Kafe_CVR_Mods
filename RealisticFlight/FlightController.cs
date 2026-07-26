using System.Collections;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using HarmonyLib;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class FlightController : MonoBehaviour
{
    private void Start()
    {
        ActionDetector.Flapped += Flap;
        ActionDetector.Gliding += HandleGliding;
    }

    private void Flap(float leftVelocity, Vector3 leftFlapDirection, float rightVelocity, Vector3 rightFlapDirection)
    {
        var controller = BetterBetterCharacterController.Instance;

        if (!controller.CanFly()
            || !controller.CanMove()
            || controller.IsFlying()
            || controller._holoPortEnabled) return;

        var avatarTransform = PlayerSetup.Instance.AvatarObject.transform;

        // Convert the flap directions from local to global space
        var globalLeftFlapDirection = avatarTransform.TransformDirection(leftFlapDirection);
        globalLeftFlapDirection.Normalize();
        globalLeftFlapDirection.x *= ModConfig.MeFlapMultiplierHorizontal.Value;
        globalLeftFlapDirection.z *= ModConfig.MeFlapMultiplierHorizontal.Value;

        var globalRightFlapDirection = avatarTransform.TransformDirection(rightFlapDirection);
        globalRightFlapDirection.Normalize();
        globalRightFlapDirection.x *= ModConfig.MeFlapMultiplierHorizontal.Value;
        globalRightFlapDirection.z *= ModConfig.MeFlapMultiplierHorizontal.Value;

        // Set the inherited velocity to be this opposite movement direction
        var leftVelocityVector = globalLeftFlapDirection * leftVelocity;
        var rightVelocityVector = globalRightFlapDirection * rightVelocity;

        var totalVelocityVector = leftVelocityVector + rightVelocityVector;
        var flapMultiplier = ConfigJson.GetCurrentAvatarFlapModifier();

        var velocityToAdd = totalVelocityVector * flapMultiplier * (MetaPort.Instance.isUsingVr ? 2.5f : 1f);

        // If we're not gliding launch off the floor
        if (!_previousGlide)
        {
            // Un-ground the player so we can leave the floor
            controller.PauseGroundConstraint();
            // Use half a jump's impulse as a baseline upwards boost (jumpImpulse is sqrt(2 * g * jumpHeight))
            var verticalImpulse = controller.GetUpVector() * (controller.jumpImpulse / Mathf.Sqrt(2f));
            controller.LaunchCharacter(velocityToAdd + verticalImpulse, true);
        }
        // If it's already gliding, add the forward component to the glide speed
        else
        {
            var localVelocity = avatarTransform.InverseTransformDirection(velocityToAdd);
            _lastVelocity += localVelocity.z;
        }

        // Handle the parameter IsGliding
        if (_triggerJustFlappedCoroutine != null) StopCoroutine(_triggerJustFlappedCoroutine);
        _triggerJustFlappedCoroutine = StartCoroutine(TriggerJustFlapped(totalVelocityVector.magnitude));
    }

    private Coroutine _triggerJustFlappedCoroutine;

    private static IEnumerator TriggerJustFlapped(float velocity)
    {
        var animatorManager = PlayerSetup.Instance.AnimatorManager;
        animatorManager.SetParameter("JustFlapped", 1f);
        animatorManager.SetParameter("FlapVelocity", velocity);
        yield return new WaitForSeconds(0.2f);
        animatorManager.SetParameter("JustFlapped", 0f);
        animatorManager.SetParameter("FlapVelocity", 0f);
    }

    private static bool _previousGlide;
    private static float _rotationAmount;

    private static Vector3 _lastPosition = Vector3.zero;
    private static float _lastVelocity;

    // Glide velocity computed in the LateUpdate, applied during the controller simulation
    private static Vector3 _glideVelocity;

    private static void HandleGliding(bool isGliding, Vector2 glideVector)
    {
        var controller = BetterBetterCharacterController.Instance;
        var actualGliding = isGliding && controller.CanFly() && !controller.IsFlying() && !controller._holoPortEnabled;

        var avatarTransform = PlayerSetup.Instance.AvatarObject.transform;
        var currentPosition = avatarTransform.position;

        if (_previousGlide != actualGliding)
        {
            if (actualGliding)
            {
                // Set the initial velocity when start gliding
                _lastVelocity = ((currentPosition - _lastPosition) / Time.deltaTime).magnitude;
            }

            // Set the parameter IsGliding
            PlayerSetup.Instance.AnimatorManager.SetParameter("IsGliding", actualGliding ? 1f : 0f);

            _previousGlide = actualGliding;
        }

        _lastPosition = currentPosition;

        // If not gliding we can stop here
        if (!actualGliding) return;

        // Set the rotation to be handled later during the UpdateInput of the input manager
        _rotationAmount = glideVector.x * Mathf.Clamp(_lastVelocity, -0.5f, 1f);

        var glideFactor = Mathf.Clamp(-glideVector.y, -1f, 1f);
        var currentGravity = controller.GetGravityMagnitude() * 0.5f;

        // SDraw funny math code, don't ask me how it works (it's magic)
        _lastVelocity = Mathf.Clamp(_lastVelocity + glideFactor * currentGravity * Time.deltaTime, 0f, float.MaxValue);
        var currentVelocityVector =
            Quaternion.Euler(Mathf.Asin(glideFactor) * Mathf.Rad2Deg, avatarTransform.rotation.eulerAngles.y, 0f) *
            new Vector3(0f, 0f, _lastVelocity);

        // Set the parameter GlidingVelocity
        PlayerSetup.Instance.AnimatorManager.SetParameter("GlidingVelocity", _lastVelocity);

        // Store the velocity to override during the simulation, and keep the player un-grounded
        _glideVelocity = currentVelocityVector;
        controller.PauseGroundConstraint();
    }

    [HarmonyPatch]
    private class HarmonyPatches
    {
        // Override the falling velocity with our glide velocity while gliding (runs just before the controller Move)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BetterBetterCharacterController), "FallingMovementMode")]
        public static void After_BetterBetterCharacterController_FallingMovementMode(
            BetterBetterCharacterController __instance)
        {
            if (!_previousGlide) return;
            __instance.CharacterMovement.velocity = _glideVelocity;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRInputManager), nameof(CVRInputManager.UpdateInput))]
        public static void After_InputModuleMouseKeyboard_Update(CVRInputManager __instance)
        {
            // Update the input module to rotate accordingly
            if (_previousGlide)
            {
                __instance.lookVector.x += _rotationAmount;
                _rotationAmount = 0;
            }
        }
    }
}
