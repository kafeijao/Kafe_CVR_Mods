using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using FreedomFingers.Properties;
using HarmonyLib;
using MelonLoader;

namespace FreedomFingers;

public class FreedomFingers : MelonMod {

    private static MelonPreferences_Category melonCategoryFreedomFingers;
    internal static MelonPreferences_Entry<bool> melonEntryEnableNotification;

    public override void OnApplicationStart() {

        // Melon Config
        melonCategoryFreedomFingers = MelonPreferences.CreateCategory(nameof(FreedomFingers));
        melonEntryEnableNotification = melonCategoryFreedomFingers.CreateEntry("EnableNotifications", false,
            description: "Whether the mod should send notifications when toggling gestures.");

        // Create action menu entry
        if (MelonHandler.Mods.Exists(m => m.Assembly?.GetName().Name == AssemblyInfoParams.OptionalDependencyActionMenu)) {
			MelonLogger.Msg($"Action Menu mod found! Initializing integration.");
			ActionMenuEntryCreator.Create();
        }
        else {
	        MelonLogger.Msg($"Action Menu mod NOT found! Skipping integration...");
        }
    }


    [HarmonyPatch]
    private static class HarmonyPatches {

	    // Prevent the setting from working (yes it could be prettier xD)
	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(InputModuleSteamVR), nameof(InputModuleSteamVR.Start))]
	    private static void AfterInputModuleSteamVRStart(InputModuleSteamVR __instance) {
		    Traverse.Create(__instance).Field("_gestureAnimationsDuringFingerTracking").SetValue(false);
	    }

	    // Prevent the setting from working
	    [HarmonyPostfix]
	    [HarmonyPatch(typeof(InputModuleSteamVR), "SettingsBoolChanged")]
	    private static void AfterInputModuleSteamVRSettingBoolChanged(InputModuleSteamVR __instance, string name) {
		    if (name != "ControlEnableGesturesWhileFingerTracking") return;
		    Traverse.Create(__instance).Field("_gestureAnimationsDuringFingerTracking").SetValue(false);
	    }

	    private static readonly FieldInfo InputManager = AccessTools.Field(typeof(InputModuleSteamVR), "_inputManager");
	    private static readonly FieldInfo IndividualFingerTracking = AccessTools.Field(typeof(CVRInputManager), "individualFingerTracking");
	    private static readonly FieldInfo SteamVrGestureToggleValue = AccessTools.Field(typeof(InputModuleSteamVR), "_steamVrIndexGestureToggleValue");

	    private static readonly MethodInfo GestureToggleFunc = SymbolExtensions.GetMethodInfo((bool b) => OnGestureToggle(b));

	    private static void OnGestureToggle(bool gestureToggleValue) {
		    Api.OnGestureToggleByGame(gestureToggleValue);
	    }

	    [HarmonyTranspiler]
        [HarmonyPatch(typeof(InputModuleSteamVR), nameof(InputModuleSteamVR.UpdateInput))]
        private static IEnumerable<CodeInstruction> Transpiler_InputModuleSteamVR_UpdateInput(IEnumerable<CodeInstruction> instructions) {

	        var success = false;

            var patchPhase = 0;

            foreach (var instruction in instructions) {

	            if (!success) {

		            // Wait to enter knuckles IF statement
	                if (patchPhase == 0 && instruction.opcode == OpCodes.Ldstr && instruction.operand is string opStr) {
		                if (opStr == "knuckles") {
			                patchPhase = 1;
		                }
	                }

	                // When reaching the GestureToggle.stateDown IF statement
	                else if (patchPhase == 1 && instruction.opcode == OpCodes.Brfalse) {

		                // Before entering the GestureToggle.stateDown if statement

		                // Lets add here setting this._inputManager._steamVrIndexGestureToggleValue = true;

		                // Push this.
		                yield return new CodeInstruction(OpCodes.Ldarg_0);
		                // Push the field _inputManager
		                yield return new CodeInstruction(OpCodes.Ldfld, InputManager);
		                // Push the value true
		                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
		                // Set field individualFingerTracking with
		                yield return new CodeInstruction(OpCodes.Stfld, IndividualFingerTracking);


		                // Enter the if statement
		                yield return instruction;

		                patchPhase = 2;
		                continue;
	                }

	                // Wait until we set the _steamVrIndexGestureToggleValue
	                else if (patchPhase == 2 && instruction.opcode == OpCodes.Stfld &&
	                    instruction.operand is FieldInfo { Name: "_steamVrIndexGestureToggleValue" }) {
		                patchPhase = 3;
	                }

	                // Skip setting the individualFingerTracking
	                else if (patchPhase == 3) {
		                // Found the last instruction for setting the individualFingerTracking, lets move to next phase
		                if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo { Name: "individualFingerTracking" }) {
							patchPhase = 4;
		                }
		                // Skip the instructions
		                continue;
	                }

	                // Skip sending the notification for the skeletal input changed
	                else if (patchPhase == 4 && instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo { Name: nameof(CohtmlHud.Instance) } fi && fi.FieldType == typeof(CohtmlHud)) {
		                patchPhase = 5;
		                continue;
	                }

	                // Skip until we reach the end of the notification code
	                else if (patchPhase == 5) {
		                if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo { Name: nameof(CohtmlHud.ViewDropTextImmediate) }) {

			                // Add our own method to tell when the button was pressed

			                // Push .this
			                yield return new CodeInstruction(OpCodes.Ldarg_0);
			                // Push the field _steamVrIndexGestureToggleValue
			                yield return new CodeInstruction(OpCodes.Ldfld, SteamVrGestureToggleValue);
			                // Call our function
			                yield return new CodeInstruction(OpCodes.Call, GestureToggleFunc);
							patchPhase = 6;
							success = true;
		                }
		                // Skip all instructions
		                continue;
	                }
	            }

	            yield return instruction;
            }

            if (!success) MelonLogger.Error("We failed to inject our stuff :( Contact the mod author.");
        }
    }
}
