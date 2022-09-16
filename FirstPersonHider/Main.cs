using System.Reflection;
using System.Reflection.Emit;
using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace FirstPersonHider;

public class FirstPersonHider : MelonMod {

    private static MelonPreferences_Category melonCategoryFirstPersonHider;
    private static MelonPreferences_Entry<List<string>> melonEntryTagsToHide;
    private static MelonPreferences_Entry<bool> melonEntryDebug;

    public override void OnApplicationStart() {

        // Melon Config
        melonCategoryFirstPersonHider = MelonPreferences.CreateCategory(nameof(FirstPersonHider));
        melonEntryTagsToHide = melonCategoryFirstPersonHider.CreateEntry("TagsToHide", new List<string> {"FPH"},
            description: "Tags that when contained in a gameobject name, will force them to be hidden locally " +
                         "(like it happens with the player head), also affects the gameobject children.");

        melonEntryDebug = melonCategoryFirstPersonHider.CreateEntry("Debug", false,
            description: "Whether to enable debug mode or not.");

        melonCategoryFirstPersonHider.SaveToFile(false);

        // React to live changes of the config
        melonEntryTagsToHide.OnValueChangedUntyped += () => {
            if (PlayerSetup.Instance._avatar == null) return;
            TransformHiderForMainCamera.ProcessHierarchy(PlayerSetup.Instance._avatar);
        };
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        private static readonly MethodInfo ManageTransforms = SymbolExtensions.GetMethodInfo((HashSet<Transform> transformsToHide) => AppendTransformsToHide(transformsToHide));

        private static void AppendTransformsToHide(HashSet<Transform> transformsToHide) {
            if (melonEntryDebug.Value) {
                MelonLogger.Msg($"Initially the game was going to hide {transformsToHide.Count} transforms.");
            }
            var avatarBones = PlayerSetup.Instance._avatar.GetComponentsInChildren<Transform>(true);
            foreach (var avatarBone in avatarBones) {
                if (melonEntryTagsToHide.Value.Any(avatarBone.name.Contains)) {
                    foreach( var boneToHide in avatarBone.GetComponentsInChildren<Transform>(true)) {
                        transformsToHide.Add(boneToHide);
                    }
                }
            }
            if (melonEntryDebug.Value) {
                MelonLogger.Msg($"\tAnd after our changes it's going to hide {transformsToHide.Count} transforms.");
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(TransformHiderForMainCamera), nameof(TransformHiderForMainCamera.ProcessHierarchy))]
        private static IEnumerable<CodeInstruction> Transpiler_TransformHiderForMainCamera_ProcessHierarchy(IEnumerable<CodeInstruction> instructions) {

            var success = false;

            foreach (var instruction in instructions) {

                yield return instruction;

                if (instruction.opcode == OpCodes.Stfld &&
                    instruction.operand is FieldInfo fieldInfo &&
                    fieldInfo.Name == "headChildrenSet") {
                    //if MelonLogger.Msg("Attempting to inject our good stuff ooooh...");
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, fieldInfo);
                    yield return new CodeInstruction(OpCodes.Call, ManageTransforms);
                    success = true;
                    //if MelonLogger.Msg("\tInjected our method successfully!");
                }
            }

            if (!success) MelonLogger.Error("We failed to inject out method :( Contact the mod author.");
        }
    }
}
