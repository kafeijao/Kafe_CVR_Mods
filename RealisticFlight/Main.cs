using ABI_RC.Core.Player;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.RealisticFlight;

public class RealisticFlight : MelonMod
{
    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();

        // Initialize the json config
        ConfigJson.LoadConfigJson();

        Integrations.UILibIntegration.InitializeUI();

        CVRGameEventSystem.Initialization.OnPlayerSetupStart.AddListener(OnPlayerSetupStart);
        CVRGameEventSystem.Avatar.OnLocalAvatarLoad.AddListener(OnLocalAvatarLoad);

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    private static readonly List<HumanBodyBones> BonesToCheck = new()
    {
        HumanBodyBones.Hips,
        HumanBodyBones.Neck,
        HumanBodyBones.Head,
        // Left
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftHand,
        // HumanBodyBones.LeftThumbProximal,
        // Right
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightHand,
        // HumanBodyBones.RightThumbProximal,
    };

    /// <summary>
    /// Adds the action detector to our avatar when it has every bone flying needs
    /// </summary>
    private static void OnLocalAvatarLoad(CVRAvatar avatar)
    {
        try
        {
            var playerSetup = PlayerSetup.Instance;
            if (playerSetup.AvatarObject == null || playerSetup.Animator == null || !playerSetup.Animator.isHuman)
            {
                MelonLogger.Warning($"[{nameof(OnLocalAvatarLoad)}] Loaded a null or non-human avatar...");
                return;
            }

            // Check all bones existence
            var animator = playerSetup.Animator;
            var missingBones = new List<HumanBodyBones>();
            foreach (var bone in BonesToCheck)
            {
                if (animator.GetBoneTransform(bone) == null)
                {
                    missingBones.Add(bone);
                }
            }

            if (missingBones.Count > 0)
            {
                MelonLogger.Warning($"[{nameof(OnLocalAvatarLoad)}] Loaded an avatar without required bones: {missingBones.Join()}");
                return;
            }

            MelonLogger.Msg($"Adding the Action Detector to the avatar {playerSetup.AvatarObject.name} game object...");

            var actionDetector = playerSetup.AvatarObject.AddComponent<ActionDetector>();
            actionDetector.avatarDescriptor = avatar;
            actionDetector.animator = animator;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(OnLocalAvatarLoad)}", e);
        }
    }

    /// <summary>
    /// Adds the flight controller to the local player
    /// </summary>
    private static void OnPlayerSetupStart()
    {
        try
        {
            MelonLogger.Msg($"Initializing the FlightController...");
            PlayerSetup.Instance.gameObject.AddComponent<FlightController>();
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(OnPlayerSetupStart)}", e);
        }
    }
}
