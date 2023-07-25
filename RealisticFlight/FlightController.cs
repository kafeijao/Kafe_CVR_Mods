using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using ABI.CCK.Components;
using HarmonyLib;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class FlightController : MonoBehaviour {

    private void Start() {
        ActionDetector.Flapped += Flap;
        ActionDetector.Gliding += HandleGliding;
    }

    private void Flap(float leftVelocity, Vector3 leftFlapDirection, float rightVelocity, Vector3 rightFlapDirection) {

        var movementSystem = MovementSystem.Instance;

        if (movementSystem.canFly && movementSystem.canMove && !movementSystem.flying && !movementSystem._holoPortEnabled) {

            movementSystem._isGrounded = false;
            movementSystem._isGroundedRaw = false;

            if (movementSystem._currentParent != null) movementSystem._inheritVelocity = movementSystem._currentParent.GetVelocity();

            // Convert the flap directions from local to global space
            var globalLeftFlapDirection = PlayerSetup.Instance._avatar.transform.TransformDirection(leftFlapDirection);
            globalLeftFlapDirection.Normalize();
            globalLeftFlapDirection.x *= ModConfig.MeFlapMultiplierHorizontal.Value;
            globalLeftFlapDirection.z *= ModConfig.MeFlapMultiplierHorizontal.Value;

            var globalRightFlapDirection = PlayerSetup.Instance._avatar.transform.TransformDirection(rightFlapDirection);
            globalRightFlapDirection.Normalize();
            globalRightFlapDirection.x *= ModConfig.MeFlapMultiplierHorizontal.Value;
            globalRightFlapDirection.z *= ModConfig.MeFlapMultiplierHorizontal.Value;

            // Set the inherited velocity to be this opposite movement direction
            var leftVelocityVector = globalLeftFlapDirection * leftVelocity;
            var rightVelocityVector = globalRightFlapDirection * rightVelocity;

            var totalVelocityVector = leftVelocityVector + rightVelocityVector;
            var flapMultiplier = ConfigJson.GetCurrentAvatarFlapModifier();

            var velocityToAdd = totalVelocityVector * flapMultiplier * (MetaPort.Instance.isUsingVr ? 2.5f : 1f);

            // If we're not gliding make it go up
            if (!_previousGlide) {
                // Use gravity to take off the floor, use half a jump height as a baseline
                MovementSystem.Instance._appliedGravity.y = Mathf.Sqrt(movementSystem.jumpHeight * movementSystem.gravity);
                MovementSystem.Instance._gravityVelocity = 0f;
            }
            // If it's already gliding, add to the last velocity
            else {
                var localVelocity = PlayerSetup.Instance._avatar.transform.InverseTransformDirection(velocityToAdd);
                _lastVelocity += localVelocity.z;
            }

            movementSystem._inheritVelocity += velocityToAdd;

            // Handle the parameter IsGliding
            if (_triggerJustFlappedCoroutine != null) StopCoroutine(_triggerJustFlappedCoroutine);
            _triggerJustFlappedCoroutine = StartCoroutine(TriggerJustFlapped(totalVelocityVector.magnitude));
        }
    }

    private Coroutine _triggerJustFlappedCoroutine;

    private static IEnumerator TriggerJustFlapped(float velocity) {
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter("JustFlapped", 1f);
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter("FlapVelocity", velocity);
        yield return new WaitForSeconds(0.2f);
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter("JustFlapped", 0f);
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter("FlapVelocity", 0f);
    }

    private static bool _previousGlide = true;
    private static float _rotationAmount;

    private static Vector3 _lastPosition = Vector3.zero;
    private static float _lastVelocity = 0f;

    private static void HandleGliding(bool isGliding, Vector2 glideVector) {

        var movementSystem = MovementSystem.Instance;
        var actualGliding = isGliding && movementSystem.canFly && movementSystem.canMove && !movementSystem.flying && !movementSystem._holoPortEnabled;

        var currentPosition = PlayerSetup.Instance._avatar.transform.position;

        if (_previousGlide != actualGliding) {
            if (actualGliding) {
                // Set the initial velocity when start gliding
                _lastVelocity = ((currentPosition - _lastPosition) / Time.deltaTime).magnitude;
            }

            // Set the parameter IsGliding
            PlayerSetup.Instance.animatorManager.SetAnimatorParameter("IsGliding", actualGliding ? 1f : 0f);

            _previousGlide = actualGliding;
        }

        _lastPosition = currentPosition;

        // If not gliding we can stop here
        if (!actualGliding) return;

        // Set the rotation to be handled later during the UpdateInput of MouseAndKeyboard input module
        _rotationAmount = glideVector.x * Mathf.Clamp(_lastVelocity, -0.5f, 1f);

        var glideFactor = Mathf.Clamp(-glideVector.y, -1f, 1f);
        var currentGravity = Mathf.Abs(CVRWorld.Instance.gravity) * 0.5f;

        // SDraw funny math code, don't ask me how it works (it's magic)
        _lastVelocity = Mathf.Clamp(_lastVelocity + glideFactor * currentGravity * Time.deltaTime, 0f, float.MaxValue);
        var currentVelocityVector = Quaternion.Euler(Mathf.Asin(glideFactor) * Mathf.Rad2Deg, PlayerSetup.Instance._avatar.transform.rotation.eulerAngles.y, 0f) * new Vector3(0f, 0f, _lastVelocity);

        // Set the parameter GlidingVelocity
        PlayerSetup.Instance.animatorManager.SetAnimatorParameter("GlidingVelocity", _lastVelocity);

        MovementSystem.Instance._appliedGravity = Vector3.zero;
        MovementSystem.Instance._gravityVelocity = 0f;
        MovementSystem.Instance._inheritVelocity = currentVelocityVector;
    }

    [HarmonyPatch]
    private class HarmonyPatches {
        private static float _originalMaxAppliedVelocity;
        private static float _originalAppliedVelocityFriction;

        private static void UpdateAppliedVelocityChanges() {
            var movementSystem = MovementSystem.Instance;
            movementSystem.maxAppliedVelocity = ModConfig.MeOverrideMaxAppliedVelocity.Value
                ? ModConfig.MeMaxAppliedVelocity.Value
                : _originalMaxAppliedVelocity;
            movementSystem.appliedVelocityFriction = ModConfig.MeOverrideAppliedVelocityFriction.Value
                ? ModConfig.MeAppliedVelocityFriction.Value
                : _originalAppliedVelocityFriction;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.FixedUpdate))]
        public static void Before_MovementSystem_FixedUpdater(MovementSystem __instance) {
            __instance.proxyPhysics.velocity *= ModConfig.MePreClampVelocityMultiplier.Value;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputModuleMouseKeyboard), nameof(InputModuleMouseKeyboard.UpdateInput))]
        public static void After_InputModuleMouseKeyboard_Update(InputModuleMouseKeyboard __instance) {
            // Update the input module to rotate accordingly
            if (_previousGlide) {
                __instance._inputManager.lookVector.x += _rotationAmount;
                _rotationAmount = 0;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Start))]
        public static void After_MovementSystem_Start(MovementSystem __instance) {

            // Save original values
            _originalMaxAppliedVelocity = __instance.maxAppliedVelocity;
            _originalAppliedVelocityFriction = __instance.appliedVelocityFriction;

            UpdateAppliedVelocityChanges();

            // Handle config updates
            ModConfig.MeOverrideMaxAppliedVelocity.OnEntryValueChanged.Subscribe((_, _) => UpdateAppliedVelocityChanges());
            ModConfig.MeMaxAppliedVelocity.OnEntryValueChanged.Subscribe((_, _) => UpdateAppliedVelocityChanges());
            ModConfig.MeOverrideAppliedVelocityFriction.OnEntryValueChanged.Subscribe((_, _) => UpdateAppliedVelocityChanges());
            ModConfig.MeAppliedVelocityFriction.OnEntryValueChanged.Subscribe((_, _) => UpdateAppliedVelocityChanges());
        }

        private static float SmoothDampCustom(float current, float target, ref float currentVelocity, float smoothTime) {
            var movementSystem = MovementSystem.Instance;
            if (movementSystem._isGroundedRaw) smoothTime *= ModConfig.MeGroundedMultiplier.Value;
            return Mathf.SmoothDamp(current, target, ref currentVelocity, smoothTime);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Update))]
        private static IEnumerable<CodeInstruction> Transpiler_MovementSystem_Update(
            IEnumerable<CodeInstruction> instructions, ILGenerator il) {

            // Match the 3 call of smooth damp (x, y, and z) of velocity:
            // Mathf.SmoothDamp(this._velocity.x, 0.0f, ref this._deltaVelocityX, this.appliedVelocityFriction);
            var matcher = new CodeMatcher(instructions).MatchForward(true, new CodeMatch(i =>
                    i.opcode == OpCodes.Ldflda && i.operand is FieldInfo fi && fi.Name.StartsWith("_deltaVelocity")),
                    OpCodes.Ldarg_0,
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo { Name: "appliedVelocityFriction" }),
                    new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo { Name: "SmoothDamp" }));

            // Call our custom smooth damp instead for all 3 axis
            return matcher.Repeat(matched => {
                matched.SetOperandAndAdvance(AccessTools.Method(typeof(HarmonyPatches), nameof(SmoothDampCustom)));
            }).InstructionEnumeration();
        }
    }
}
