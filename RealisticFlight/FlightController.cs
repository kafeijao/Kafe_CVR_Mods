using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class FlightController : MonoBehaviour {

    private void Start() {
        ActionDetector.Flapped += Flap;
        ActionDetector.Gliding += HandleGliding;
    }

    private void Flap(float leftVelocity, Vector3 leftFlapDirection, float rightVelocity, Vector3 rightFlapDirection) {

        #if DEBUG
        MelonLogger.Msg($"Flapped {leftVelocity} | {rightVelocity}");
        #endif

        var movementSystem = MovementSystem.Instance;

        // Clamp ->

        // if ((double) this.proxyPhysics.velocity.magnitude > (double) this.minAppliedVelocity)
        // this._velocity = Vector3.ClampMagnitude(this.proxyPhysics.velocity, this.maxAppliedVelocity);
        // Do stuff before clamping so if we go over the limit it wont be an issue

        //
        // this.appliedVelocityFriction = 0.9

        // Grounded = divide by 5 friction
        // this.appliedVelocityFriction

        // Use this to scale to the avatar
        //movementSystem.movementScale;

        if (movementSystem.canFly && movementSystem.canMove && !movementSystem.flying &&
            !movementSystem._holoPortEnabled) {
            movementSystem._isGrounded = false;
            movementSystem._isGroundedRaw = false;

            if (movementSystem._currentParent != null)
                movementSystem._inheritVelocity = movementSystem._currentParent.GetVelocity();
            var avgVelocity = (leftVelocity + rightVelocity) / 2f;
            movementSystem._appliedGravity.y = Mathf.Sqrt(movementSystem.jumpHeight * 2f * movementSystem.gravity * avgVelocity);
        }
    }

    private static bool _previousGlide = true;
    private static float _rotationAmount;
    private float _initialVelMagnitude;
    private Vector3 _lastAvatarPosition;

    private void HandleGliding(bool isGliding, Vector2 glideVector) {
        var currentPos = PlayerSetup.Instance._avatar.transform.position;
        var currentFlyDirectionRaw = (_lastAvatarPosition - currentPos) / Time.deltaTime;
        _lastAvatarPosition = currentPos;

        var movementSystem = MovementSystem.Instance;

        var actualGliding = isGliding && movementSystem.canFly && movementSystem.canMove && !movementSystem.flying && !movementSystem._holoPortEnabled;

        if (_previousGlide != actualGliding) {
            if (actualGliding) {
                _initialVelMagnitude = currentFlyDirectionRaw.magnitude;
            }
            else {
                movementSystem._appliedGravity = Vector3.zero;
            }
            movementSystem.gravity = actualGliding ? 0f : 10f;
            _previousGlide = actualGliding;
        }

        _rotationAmount = 0;

        if (actualGliding) {

            //var velocity = currentFlyDirectionRaw.magnitude;

            //MelonLogger.Msg($"Velocity: {_initialVelMagnitude}");

            _rotationAmount = glideVector.x * Mathf.Clamp(_initialVelMagnitude, -1f, 1f);

            //var forwardDirection = Quaternion.Euler(0, movementSystem._headingAngle, 0) * Vector3.forward;
            var forwardDirection = PlayerSetup.Instance._avatar.transform.forward;

            forwardDirection.y = glideVector.y;
            forwardDirection.Normalize();
            movementSystem._appliedGravity = forwardDirection * _initialVelMagnitude;
            // movementSystem._velocity = Vector3.zero;

            if (Input.GetKey(KeyCode.Keypad4)) {
                // CVRInputManager.Instance.lookVector.x -= 0.1f;
                _rotationAmount = -1f * Time.deltaTime * 60;
            }
            else if (Input.GetKey(KeyCode.Keypad6)) {
                _rotationAmount = 1f * Time.deltaTime * 60;
                // CVRInputManager.Instance.lookVector.x += 0.1f;
            }
            if (Input.GetKey(KeyCode.Keypad8)) {
                forwardDirection.y = -1f * Time.deltaTime * 60;
            }
            else if (Input.GetKey(KeyCode.Keypad2)) {
                forwardDirection.y = 1f * Time.deltaTime * 60;
            }

            // // movementSystem._appliedGravity = forwardDirection * _totalVelocity;
            // movementSystem._appliedGravity = forwardDirection * velocity;
            // MelonLogger.Msg(movementSystem._appliedGravity.ToString("F2"));

            // When gliding, gravity doesn't affect the character
            //movementSystem.gravity = 0;

            // Manipulate y of appliedGravity for going up and down
            // GlideFactor could be a predefined constant to adjust the rate of altitude change
            // float GlideFactor = 0.1f;
            // movementSystem._appliedGravity.y += glideVector.y * GlideFactor;
            //
            // // Depending on the upward or downward movement, increase or decrease forward speed
            // // SpeedFactor could be a predefined constant to adjust the rate of speed change
            // float SpeedFactor = 0.2f;
            // movementSystem._appliedGravity.z += -movementSystem._appliedGravity.y * SpeedFactor;
            //
            // // You might want to clamp the velocity to prevent it going too high or too low
            // movementSystem._appliedGravity.y = Mathf.Clamp(movementSystem._appliedGravity.y, -1f, 1f);
            // movementSystem._appliedGravity.z = Mathf.Clamp(movementSystem._appliedGravity.z, 0, 10f);
        }

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

        //
        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Update))]
        // public static void After_MovementSystem_Update(MovementSystem __instance) {
        //     try {
        //
        //     }
        //     catch (Exception e) {
        //         MelonLogger.Error($"Error during {nameof(After_MovementSystem_Update)} patch.");
        //         MelonLogger.Error(e);
        //         throw;
        //     }
        // }
    }
}
