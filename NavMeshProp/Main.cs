using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshProp;

public class NavMeshProp : MelonMod {

    private static NavMeshProp _instance;

    private CVRSpawnable _currentPeeb;
    private NavMeshAgent _currentPeebAgent;
    private Transform _currentPeebLookAtTarget;
    private Transform _currentPeebHeadTransform;

    private static Vector3 _previousViewPoint;

    public override void OnInitializeMelon() {

        ModConfig.InitializeBTKUI();

        _instance = this;

        // Create our peeb settings to be used in the bakes
        var peebAgentSettings = new NavMeshTools.API.Agent(
            .25f,
            .5f,
            45f,
            0.5f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((spawnerUserId, spawnable)  => {

            if (spawnerUserId != MetaPort.Instance.ownerId) return;
            MelonLogger.Msg($"Spawned {spawnable.guid}");
            if (spawnable.guid != "9eeb9eeb-9eeb-9eeb-9eeb-9eeb9eeb9eeb") return;

            MelonLogger.Msg($"Requesting Bake...");

            NavMeshTools.API.BakeCurrentWorldNavMesh(peebAgentSettings, (agentTypeID, success) => {

                if (!success) {
                    MelonLogger.Warning("The bake has failed for some reason :( The peeb won't have a NavMeshAgent.");
                }

                // Bake complete! Let's setup and enable our NavMeshAgent

                var agentSc = spawnable.subSyncs.Find(sc => sc.transform != null && sc.transform.name.StartsWith("[NavMeshAgent]"));
                if (agentSc == null) {
                    MelonLogger.Warning("Attempted to load a peeb but it didn't have a syb-sync transform with the name starting in [NavMeshAgent]");
                    return;
                }

                _currentPeebLookAtTarget = spawnable.transform.Find("LookAtTarget");
                _currentPeebHeadTransform = spawnable.transform.Find("Peeb").GetComponent<LookAtIK>().solver.head.transform;

                if (!agentSc.transform.TryGetComponent(out _currentPeebAgent)) {
                    _currentPeebAgent = agentSc.transform.gameObject.AddComponent<NavMeshAgent>();
                }

                // We need to associate our Nav Mesh Agent with the agentTypeID we baked the mesh for
                _currentPeebAgent.agentTypeID = agentTypeID;

                // Set the NavMeshAgent settings (you should try matching with the ones using in the bake)
                _currentPeebAgent.radius = 0.25f;
                _currentPeebAgent.height = 0.5f;
                _currentPeebAgent.speed = 2f;
                _currentPeebAgent.angularSpeed = 240f;
                _currentPeebAgent.acceleration = 8f;
                _currentPeebAgent.stoppingDistance = 2f;
                _currentPeebAgent.enabled = true;

                _currentPeeb = spawnable;

            }, true);
        });

    }

    public static CVRPlayerEntity FollowingPlayer;

    // private bool _lastFailed = false;

    public override void OnUpdate() {

        if (_currentPeeb == null) return;

        // This was calculated late in the previous frame, so it should be up to date after all the IK bs
        var playerHeadTarget = _previousViewPoint;

        _currentPeebAgent.SetDestination(playerHeadTarget);

        _currentPeebLookAtTarget.position = _currentPeebAgent.hasPath && Vector3.Distance(_currentPeebAgent.steeringTarget, _currentPeebAgent.pathEndPosition) > 0.1f
            ? _currentPeebAgent.steeringTarget with { y = _currentPeebHeadTransform.position.y }
            : playerHeadTarget;

        _currentPeeb.SetValue(_currentPeeb.syncValues.FindIndex( match => match.name == "Speed"), _currentPeebAgent.velocity.magnitude/_currentPeebAgent.speed);
        _currentPeeb.needsUpdate = true;
    }

    [HarmonyPatch]
    internal class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ControllerRay), nameof(ControllerRay.LateUpdate))]
        public static void After_ControllerRay_LateUpdate() {
            try {
                if (_instance._currentPeeb == null) return;

                // Save the viewpoints position
                _previousViewPoint = FollowingPlayer == null ? PlayerSetup.Instance._viewPoint.GetPointPosition() : FollowingPlayer.PuppetMaster._viewPoint.GetPointPosition();

                // // Handle following local player
                // if (FollowingPlayer == null) {
                //     var animator = PlayerSetup.Instance._animator;
                //     if (animator == null || !animator.isHuman || animator.GetBoneTransform(HumanBodyBones.Head) != null) {
                //         if (PlayerSetup.Instance._viewPoint == null) return;
                //         _previousViewPoint = PlayerSetup.Instance._viewPoint.GetPointPosition();
                //         return;
                //     }
                //     _previousViewPoint = animator.GetBoneTransform(HumanBodyBones.Head).position;
                // }
                // // Handle remote players
                // else {
                //     var animator = FollowingPlayer.PuppetMaster._animator;
                //     if (animator == null || !animator.isHuman || animator.GetBoneTransform(HumanBodyBones.Head) != null) {
                //         if (FollowingPlayer.PuppetMaster._viewPoint == null) return;
                //         _previousViewPoint = FollowingPlayer.PuppetMaster._viewPoint.GetPointPosition();
                //         return;
                //     }
                //     _previousViewPoint = animator.GetBoneTransform(HumanBodyBones.Head).position;
                // }
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_ControllerRay_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }
}
