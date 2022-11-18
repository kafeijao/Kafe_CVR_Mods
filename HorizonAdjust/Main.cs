using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Base;
using ABI_RC.Core.Player;
using ABI_RC.Systems.MovementSystem;
using ActionMenu;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace HorizonAdjust;

public class HorizonAdjust : MelonMod {

    private Menu _lib;

    private static Quaternion _targetQuaternion;
    private static bool _isRotating;
    private static bool _isResetting;
    private static bool _groundPosChanged;
    private static Vector3 _groundPos;

    private static bool _created;

    private static float _degreesPerSecond = 180f;
    private static float _angleIncrement = 90f;

    public override void OnApplicationStart() {
        _lib = new Menu();
    }

    public override void OnUpdate() {

        var playerTransform = PlayerSetup.Instance.transform;

        // MelonLogger.Msg($"Rotating {playerTransform.localRotation.eulerAngles.ToString()} to {_targetQuaternion.eulerAngles.ToString()}");

        var groundCheck = Traverse.Create(MovementSystem.Instance).Field(nameof(MovementSystem.groundCheck)).GetValue<Transform>();
        var groundDistance = Traverse.Create(MovementSystem.Instance).Field(nameof(MovementSystem.groundDistance)).GetValue<float>();
        //var colliderCenter = Traverse.Create(MovementSystem.Instance).Field(nameof(MovementSystem._colliderCenter)).GetValue<Vector3>();


        // Apply previous frame global position if available
        if (_groundPosChanged) {
	        groundCheck.position = _groundPos;
	        _groundPosChanged = false;
        }

        if (!_created && MovementSystem.Instance != null && groundCheck != null) {
	        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
	        sphere.transform.SetParent(groundCheck);
	        sphere.transform.localScale = Vector3.one * groundDistance;
	        sphere.transform.localPosition = Vector3.zero;
	        sphere.GetComponent<Collider>().enabled = false;
	        _created = true;
        }

        if (!_isRotating) return;

        // Ignore if we reached the target angle
	    if (Mathf.Approximately(Quaternion.Angle(playerTransform.localRotation, _targetQuaternion), 0f)) {

		    // Consume Reset (and reset the ground check position)
		    if (_isResetting) {
			    // Execute MovementSystem.UpdateCollider() to reset the collider
			    Traverse.Create(MovementSystem.Instance).Method(nameof(MovementSystem.UpdateCollider)).GetValue();
			    _isResetting = false;
		    }

		    _isRotating = false;
		    return;
	    }

	    // Save global position
	    _groundPos = groundCheck.position;
	    _groundPosChanged = true;

        // Rotate player
        playerTransform.localRotation = Quaternion.RotateTowards(playerTransform.localRotation, _targetQuaternion, _degreesPerSecond * Time.deltaTime);
        //playerTransform.localRotation = Quaternion.RotateTowards(playerTransform.localRotation, _targetQuaternion, 50000);

        //var groundTraverse = Traverse.Create(MovementSystem.Instance).Field(nameof(MovementSystem.groundCheck));

        //groundTraverse.GetValue<Transform>().position = playerTransform.position;
    }

    private static void SetRotateTarget(Vector3 rotationDirection) {
	    rotationDirection = rotationDirection.normalized * _angleIncrement;
	    SetRotation(PlayerSetup.Instance.transform.localRotation * Quaternion.Euler(rotationDirection));
    }

    private static void SetRotation(Quaternion localRotation, bool reset = false) {
	    // Finish rotating before accepting another
	    if (_isRotating || _isResetting) return;
	    _targetQuaternion = localRotation;
	    _isRotating = true;
	    if (reset) _isResetting = true;
    }

    private class Menu : ActionMenuMod.Lib {
        protected override string modName => "Horizon Adjust";
        protected override string modIcon => "data:image/png;base64,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";

        protected override List<MenuItem> modMenuItems() {

	        return new List<MenuItem>() {
                new MenuItem("Back", BuildButtonItem("Back", () => SetRotateTarget(Vector3.left))),
                new MenuItem("Reset", BuildButtonItem("Reset", () => {
	                SetRotation(Quaternion.Euler(0f, PlayerSetup.Instance.transform.localRotation.eulerAngles.y, 0f), true);
                })),
                new MenuItem("Left", BuildButtonItem("Left", () => SetRotateTarget(Vector3.forward))),
                new MenuItem("Rotation Speed", BuildRadialItem("Rotation Speed", v => _degreesPerSecond = v, minValue: 1f, maxValue: 360f, defaultValue: _degreesPerSecond)),
                new MenuItem("Front", BuildButtonItem("Front", () => SetRotateTarget(Vector3.right))),
                new MenuItem("Rotation Increments", BuildRadialItem("Rotation Increments", v => _angleIncrement = v, minValue: 1f, maxValue: 180f, defaultValue: _angleIncrement)),
                new MenuItem("Right", BuildButtonItem("Right", () => SetRotateTarget(Vector3.back))),
                new MenuItem("Set Horizon", BuildButtonItem("Set Horizon", () => {
	                SetRotation(Quaternion.Euler(PlayerSetup.Instance.GetActiveCamera().transform.rotation.eulerAngles));
                })),
            };
        }
    }

   // [HarmonyPatch]
   // private static class HarmonyPatches {
	  //  //
	  //  // [HarmonyPostfix]
	  //  // [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateCollider))]
	  //  // private static void After_MovementSystem_UpdateCollider(MovementSystem __instance) {
		 //  //  var _colliderCenter = Traverse.Create(__instance).Field(nameof(MovementSystem._colliderCenter)).GetValue<Vector3>();
		 //  //  __instance.groundCheck.localPosition = _colliderCenter;
		 //  //  MelonLogger.Msg(_colliderCenter.ToString());
	  //  // }
   //
	  // //   private static readonly FieldInfo _isGroundedRaw = AccessTools.Field(typeof(MovementSystem), nameof(MovementSystem._isGroundedRaw));
   // //
	  // //   [HarmonyTranspiler]
   // //      [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.Update))]
   // //      private static IEnumerable<CodeInstruction> Transpiler_MovementSystem_Update(IEnumerable<CodeInstruction> instructions) {
   // //
	  // //       var _isGroundedRawPatchCount = 0;
   // //
	  // //       foreach (var instruction in instructions) {
   // //
		 // //        // Always overwrite _isGroundedRaw to true
		 // //        if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo { Name: "_isGroundedRaw" }) {
			// //         yield return instruction;
   // //
			// //         // Push this.
			// //         yield return new CodeInstruction(OpCodes.Ldarg_0);
			// //         // Push the value true
			// //         yield return new CodeInstruction(OpCodes.Ldc_I4_1);
			// //         // Set field _isGroundedRaw to true
			// //         yield return new CodeInstruction(OpCodes.Stfld, _isGroundedRaw);
   // //
			// //         _isGroundedRawPatchCount++;
   // //
			// //         continue;
		 // //        }
   // //
		 // //        yield return instruction;
   // //          }
   // //
			// // MelonLogger.Msg($"[Transpiler] Patched MovementSystem._isGroundedRaw {_isGroundedRawPatchCount} times.");
   // //      }
   // }
}
