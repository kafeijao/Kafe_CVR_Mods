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

        CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((spawnerUserId, spawnable)  => {

            if (spawnerUserId != MetaPort.Instance.ownerId) return;
            MelonLogger.Msg($"Spawned {spawnable.guid}");
            if (spawnable.guid != "9eeb9eeb-9eeb-9eeb-9eeb-9eeb9eeb9eeb") return;

            MelonLogger.Msg($"Requesting Bake...");

            NavMeshTools.API.BakeCurrentWorldNavMesh(success => {

                // Bake complete! Let's enable the nav mesh

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

                _currentPeebAgent.radius = 0.25f;
                _currentPeebAgent.height = 0.5f;
                _currentPeebAgent.speed = 3f;
                _currentPeebAgent.angularSpeed = 120f;
                _currentPeebAgent.stoppingDistance = 2f;
                _currentPeebAgent.enabled = true;
                _currentPeeb = spawnable;

            }, false);
        });

    }

    public static CVRPlayerEntity FollowingPlayer;

    // private bool _lastFailed = false;

    public override void OnUpdate() {

        if (_currentPeeb == null) return;

        // This was calculated late in the previous frame, so it should be up to date after all the IK bs
        var playerHeadTarget = _previousViewPoint;

        _currentPeebAgent.SetDestination(playerHeadTarget);

        _currentPeebLookAtTarget.position = _currentPeebAgent.hasPath
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
                // Save the viewpoints position
                _previousViewPoint = FollowingPlayer == null ? PlayerSetup.Instance._viewPoint.GetPointPosition() : FollowingPlayer.PuppetMaster._viewPoint.GetPointPosition();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_ControllerRay_LateUpdate)}");
                MelonLogger.Error(e);
            }
        }
    }
}
