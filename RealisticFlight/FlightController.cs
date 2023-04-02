using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class FlightController : MonoBehaviour {

    private void Start() {
        ActionDetector.Flapped += Flap;
    }

    private void Flap(float leftVelocity, Vector3 leftFlapDirection, float rightVelocity, Vector3 rightFlapDirection) {
        MelonLogger.Msg($"Flapped {leftVelocity} | {rightVelocity}");

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
            movementSystem._appliedGravity.y =
                Mathf.Sqrt(movementSystem.jumpHeight * 2f * movementSystem.gravity * avgVelocity);
        }
    }

    private void HandleGliding() {
    }

    private void Update() {
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
