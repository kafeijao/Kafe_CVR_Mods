using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshProp;

public class NavMeshProp : MelonMod {

    public override void OnInitializeMelon() {

        ModConfig.InitializeBTKUI();

        // Create our peeb agent to be used in the bakes
        var peebAgentSettings = new NavMeshTools.API.Agent(
            .25f,
            .5f,
            45f,
            0.45f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        // Register the peeb
        PetController.AddPetBlueprint("9eeb9eeb-9eeb-9eeb-9eeb-9eeb9eeb9eeb", new PetBlueprint(
            peebAgentSettings, 0.25f, 0.5f, 3f, 240f, 8f, 1f));

        // Create our knuckles agent to be used in the bakes
        var knucklesAgentSettings = new NavMeshTools.API.Agent(
            .25f,
            .5f,
            45f,
            0.45f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        // Register the knuckles
        PetController.AddPetBlueprint("35d70641-6a8f-4830-a2d3-01ff83c331f3", new PetBlueprint(
            knucklesAgentSettings, 0.25f, 0.5f, 3f, 240f, 8f, 1f));

        // Create our yipee agent to be used in the bakes
        var yipeeAgentSettings = new NavMeshTools.API.Agent(
            .25f,
            .5f,
            45f,
            0.45f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        // Register the yipee
        PetController.AddPetBlueprint("78490cce-0281-402f-a1b6-1ee83e248473", new PetBlueprint(
            yipeeAgentSettings, 0.25f, 0.5f, 3f, 240f, 8f, 1f));

        // Create our necoarc agent to be used in the bakes
        var necoarcAgentSettings = new NavMeshTools.API.Agent(
            .25f,
            .5f,
            45f,
            0.45f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        // Register the necoarc
        PetController.AddPetBlueprint("df35e685-0af4-42d3-8988-259b58f79d1b", new PetBlueprint(
            necoarcAgentSettings, 0.25f, 0.5f, 3f, 240f, 8f, 1f));

        // Create our shiggy agent to be used in the bakes
        var shiggyAgentSettings = new NavMeshTools.API.Agent(
            .15f,
            .3f,
            45f,
            0.29f,
            2f,
            false,
            0.2f,
            false,
            256
        );

        // Register the shiggy
        PetController.AddPetBlueprint("2befc448-01c7-47fe-87e7-6ed72e9b090b", new PetBlueprint(
            shiggyAgentSettings, 0.15f, 0.3f, 3f, 240f, 10f, 1.5f));

        // Create our kyle agent to be used in the bakes
        var kyleAgentSettings = new NavMeshTools.API.Agent(
            .5f,
            2f,
            45f,
            0.75f,
            2f,
            false,
            0.1667f,
            false,
            256
        );

        // Register the kyle
        PetController.AddPetBlueprint("8bd1b614-a07f-4e06-9e70-3756abf5c685", new PetBlueprint(
            kyleAgentSettings, 0.5f, 2f, 4f, 240f, 10f, 2f));

        // Stop pets from following a player when they leave
        CVRGameEventSystem.Player.OnLeave.AddListener(descriptor => {
            foreach (var petController in PetController.Controllers) {
                if (petController.FollowingPlayer != null && petController.FollowingPlayer.PlayerDescriptor == descriptor) {
                    petController.FollowingPlayer = null;
                }
            }
        });
    }

    internal class PetBlueprint {

        internal readonly NavMeshTools.API.Agent Agent;
        internal readonly float Radius;
        internal readonly float Height;
        internal readonly float Speed;
        internal readonly float AngularSpeed;
        internal readonly float Acceleration;
        internal readonly float StoppingDistance;

        public PetBlueprint(
            NavMeshTools.API.Agent agent,
            float radius = 0.25f,
            float height = 0.5f,
            float speed = 3f,
            float angularSpeed = 240f,
            float acceleration = 8f,
            float stoppingDistance = 1f) {
            Agent = agent;
            Radius = radius;
            Height = height;
            Speed = speed;
            AngularSpeed = angularSpeed;
            Acceleration = acceleration;
            StoppingDistance = stoppingDistance;
        }
    }

    [DefaultExecutionOrder(999999)]
    internal class PetController : MonoBehaviour {

        internal static readonly HashSet<PetController> Controllers = new();
        private static readonly Dictionary<string, PetBlueprint> BlueprintHandlers = new();

        static PetController() {

            CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((spawnerUserId, spawnable) => {

                // Ignore props spawned by other people
                if (spawnerUserId != MetaPort.Instance.ownerId) return;

                // Ignore props not in the handlers list
                if (!BlueprintHandlers.TryGetValue(spawnable.guid, out var blueprint)) return;

                var spawnableGuid = spawnable.guid;

                NavMeshTools.API.BakeCurrentWorldNavMesh(blueprint.Agent, (agentTypeID, success) => {

                    MelonLogger.Msg($"Finished baking the NavMeshData for {spawnableGuid}");

                    if (spawnable == null) {
                        MelonLogger.Warning($"The spawnable {spawnableGuid} doesn't exist anymore... Probably deleted while baking...");
                        return;
                    }

                    if (!success) {
                        MelonLogger.Warning($"The bake for {spawnableGuid} has failed for some reason... This Pet won't work as it should...");
                        return;
                    }

                    var navMeshAgent = spawnable.transform.GetComponentInChildren<NavMeshAgent>(true);
                    if (navMeshAgent == null) {
                        MelonLogger.Warning($"The spawnable {spawnableGuid} doesn't have a NavMeshAgent...");
                        return;
                    }

                    var lookAtTargetTransform = spawnable.transform.Find("[NavMeshProp] LookAtTarget");
                    var lookAtIKComponent = spawnable.GetComponentInChildren<LookAtIK>();

                    // We need to associate our Nav Mesh Agent with the agentTypeID we baked the mesh for
                    navMeshAgent.agentTypeID = agentTypeID;

                    // Set the NavMeshAgent settings (you should try matching with the ones using in the bake)
                    navMeshAgent.radius = blueprint.Radius;
                    navMeshAgent.height = blueprint.Height;
                    navMeshAgent.speed = blueprint.Speed;
                    navMeshAgent.angularSpeed = blueprint.AngularSpeed;
                    navMeshAgent.acceleration = blueprint.Acceleration;
                    navMeshAgent.stoppingDistance = blueprint.StoppingDistance;

                    // Enable the nav agent (because we're controlling it)
                    navMeshAgent.enabled = true;

                    // Initialize the controller
                    var controller = spawnable.gameObject.AddComponent<PetController>();
                    controller.Spawnable = spawnable;
                    controller.NavMeshAgent = navMeshAgent;

                    // Optionally initialize the look at
                    if (lookAtTargetTransform != null && lookAtIKComponent != null && lookAtIKComponent.solver is { head: not null } && lookAtIKComponent.solver.head.transform != null) {
                        controller.HasLookAt = true;
                        controller.LookAtTargetTransform = lookAtTargetTransform;
                        controller.LookAtHeadTransform = lookAtIKComponent.solver.head.transform;
                    }

                    controller.SpawnableSpeedIndex = spawnable.syncValues.FindIndex(match => match.name == "Speed");
                    controller.SpawnableRandomFloatIndex = spawnable.syncValues.FindIndex(match => match.name == "RandomFloat");
                    controller.SpawnableOffMeshLinkIndex = spawnable.syncValues.FindIndex(match => match.name == "OffMeshLink");
                }, true);
            });

        }

        internal CVRSpawnable Spawnable;
        internal NavMeshAgent NavMeshAgent;

        internal bool HasLookAt;
        internal Transform LookAtTargetTransform;
        internal Transform LookAtHeadTransform;

        internal CVRPlayerEntity FollowingPlayer;
        internal Vector3 FollowingPlayerPreviousViewpointPos;

        internal int SpawnableSpeedIndex;
        internal int SpawnableRandomFloatIndex;
        internal int SpawnableOffMeshLinkIndex;

        private float timer = 0f;
        private float _delay = 1f;

        private void Update() {

            // Set the destination
            NavMeshAgent.SetDestination(FollowingPlayer == null
                ? PlayerSetup.Instance.GetPlayerPosition()
                : FollowingPlayer.AvatarHolder.transform.position);

            // Handle the pet look at (it has been calculated late in the frame)
            if (HasLookAt) {
                LookAtTargetTransform.position = NavMeshAgent.hasPath && Vector3.Distance(NavMeshAgent.steeringTarget, NavMeshAgent.pathEndPosition) > 0.1f
                    ? NavMeshAgent.steeringTarget with { y = LookAtHeadTransform.position.y }
                    : FollowingPlayerPreviousViewpointPos;
            }

            // Set the spawnable parameters
            if (SpawnableSpeedIndex != -1) {
                Spawnable.SetValue(SpawnableSpeedIndex, NavMeshAgent.velocity.magnitude/NavMeshAgent.speed);
            }
            if (SpawnableOffMeshLinkIndex != -1) {
                Spawnable.SetValue(SpawnableOffMeshLinkIndex, NavMeshAgent.isOnOffMeshLink ? 1f : 0f);
            }
            if (SpawnableRandomFloatIndex != -1) {
                timer += Time.deltaTime;
                if(timer >= _delay) {
                    Spawnable.SetValue(SpawnableRandomFloatIndex, UnityEngine.Random.value);
                    timer = 0f;
                }
            }

            // Keep the prop synced by us
            Spawnable.needsUpdate = true;
        }

        private void LateUpdate() {
            if (!HasLookAt) return;
            FollowingPlayerPreviousViewpointPos = FollowingPlayer == null
                ? PlayerSetup.Instance._viewPoint.GetPointPosition()
                : FollowingPlayer.PuppetMaster._viewPoint.GetPointPosition();
        }

        private void Start() {
            Controllers.Add(this);
        }

        private void OnDestroy() {
            Controllers.Remove(this);
        }

        internal static void AddPetBlueprint(string spawnableGuid, PetBlueprint blueprint) {
            BlueprintHandlers[spawnableGuid] = blueprint;
        }
    }
}
