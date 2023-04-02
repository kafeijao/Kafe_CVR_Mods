using ABI_RC.Core.Player;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class RealisticFlight : MelonMod {

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        // Check for BTKUILib
        if (RegisteredMelons.FirstOrDefault(m => m.Info.Name == "BTKUILib") != null) {
            MelonLogger.Msg($"Detected BTKUILib mod, we're adding the integration!");
            ModConfig.InitializeBTKUI();
        }
        else {
            MelonLogger.Warning($"BTKUILib mod NOT detected! You won't have access to the Instances History feature!");
        }


        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        private static readonly List<HumanBodyBones> BonesToCheck = new() {
            HumanBodyBones.Hips,
            HumanBodyBones.Head,
            // Left
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftHand,
            // Right
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightHand,
        };

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.SetupAvatar))]
        public static void After_PlayerSetup_SetupAvatar(PlayerSetup __instance) {
            try {

                if (__instance._avatar == null || __instance._animator == null || !__instance._animator.isHuman) {
                    MelonLogger.Warning($"[After_PlayerSetup_SetupAvatar] Loaded a null or non-human avatar...");
                    return;
                }

                // Check all bones existence
                var animator = __instance._animator;
                var missingBones = new List<HumanBodyBones>();
                foreach (var bone in BonesToCheck) {
                    if (animator.GetBoneTransform(bone) == null) {
                        missingBones.Add(bone);
                    }
                }
                if (missingBones.Count > 0) {
                    MelonLogger.Warning($"[After_PlayerSetup_SetupAvatar] Loaded an avatar without required bones: {missingBones.Join()}");
                    return;
                }

                MelonLogger.Msg($"Adding the Action Detector to the avatar...");

                var actionDetector = __instance.gameObject.AddComponent<ActionDetector>();
                actionDetector.avatarDescriptor = __instance._avatarDescriptor;
                actionDetector.animator = __instance._animator;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_SetupAvatar)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {
                MelonLogger.Msg($"Initializing the FlightController...");
                __instance.gameObject.AddComponent<FlightController>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}");
                MelonLogger.Error(e);
            }
        }


    }
}
