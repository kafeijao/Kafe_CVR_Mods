using System.Collections;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.AI;

namespace Kafe.NavMeshProp;

public class NavMeshProp : MelonMod {

    public const string MovementXName = "MovementX";
    public const string MovementYName = "MovementY";
    public const string GroundedName = "Grounded";
    public const string RandomFloatName = "RandomFloat";

    public override void OnInitializeMelon() {

        // Add NavMeshObstacle to the prop's whitelist
        SharedFilter._spawnableWhitelist.Add(typeof(NavMeshObstacle));

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
            0.2f,
            false,
            256
        );

        // Register the kyle
        PetController.AddPetBlueprint("8bd1b614-a07f-4e06-9e70-3756abf5c685", new PetBlueprint(
            kyleAgentSettings, 0.5f, 2f, 4f, 240f, 10f, 2f));


        // Create our nak agent to be used in the bakes
        var nakAgentSettings = new NavMeshTools.API.Agent(
            .15f,
            .8f,
            45f,
            0.79f,
            .5f,
            true,
            .15f/3/2,
            true,
            256*2
        );

        // Register the nak
        PetController.AddPetBlueprint("3e72cd62-31c5-458e-908d-5966a1b41c23", new PetBlueprint(
            nakAgentSettings, 0.15f, 0.8f, 3f, 240f, 7f, 2f));

        // Stop pets from following a player when they leave
        CVRGameEventSystem.Player.OnLeave.AddListener(descriptor => {
            foreach (var petController in PetController.Controllers) {
                if (petController.FollowingPlayer != null && petController.FollowingPlayer.PlayerDescriptor == descriptor) {
                    petController.FollowingPlayer = null;
                }
            }
        });

        #if DEBUG
        CVRGameEventSystem.Instance.OnConnected.AddListener(instanceID => {
            if (!CVRWorld.Instance.allowSpawnables || AuthManager.username != "Kafeijao") return;
            MelonLogger.Msg($"Connected to instance: {instanceID} Spawning in one seconds...");
            IEnumerator DelaySpawnProp() {
                yield return new WaitForSeconds(3f);
                PlayerSetup.Instance.DropProp("3e72cd62-31c5-458e-908d-5966a1b41c23");
            }
            MelonCoroutines.Start(DelaySpawnProp());
        });
        #endif
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

                    // Disable nav mesh obstacle locally so our nav mesh agents don't affect out agent
                    if (navMeshAgent.TryGetComponent<NavMeshObstacle>(out var navMeshObstacle)) navMeshObstacle.enabled = false;

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
                    if (lookAtTargetTransform != null && lookAtIKComponent != null
                                                      && lookAtIKComponent.solver is { head: not null }
                                                      && lookAtIKComponent.solver.head.transform != null
                                                      && lookAtIKComponent.solver.target != null) {

                        controller.HasLookAt = true;
                        controller.LookAtTargetTransform = lookAtTargetTransform;
                        controller.LookAtHeadTransform = lookAtIKComponent.solver.head.transform;

                        // #if DEBUG
                        // CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(lookAtTargetTransform.gameObject, "LookAtTransform");
                        // CCK.Debugger.Components.GameObjectVisualizers.LabeledVisualizer.Create(lookAtIKComponent.solver.target.gameObject, "LookAtSmoothedTransform");
                        // #endif
                    }

                    controller.SpawnableMovementYIndex = spawnable.syncValues.FindIndex(match => match.name is MovementYName or "Speed");
                    controller.SpawnableMovementXIndex = spawnable.syncValues.FindIndex(match => match.name == MovementXName);
                    controller.SpawnableGroundedIndex = spawnable.syncValues.FindIndex(match => match.name == GroundedName);
                    controller.SpawnableRandomFloatIndex = spawnable.syncValues.FindIndex(match => match.name == RandomFloatName);
                }, false);
            });
        }

        internal CVRSpawnable Spawnable;
        internal NavMeshAgent NavMeshAgent;

        internal bool HasLookAt;
        internal Transform LookAtTargetTransform;
        internal Transform LookAtHeadTransform;

        internal CVRPlayerEntity FollowingPlayer;
        internal Vector3 FollowingPlayerPreviousViewpointPos;

        internal int SpawnableMovementYIndex;
        internal int SpawnableMovementXIndex;
        internal int SpawnableGroundedIndex;
        internal int SpawnableRandomFloatIndex;

        private float timer = 0f;
        private float _delay = 1f;

        private void Update() {

            // Set the destination
            NavMeshAgent.SetDestination(FollowingPlayer == null || FollowingPlayer.AvatarHolder == null
                ? PlayerSetup.Instance.GetPlayerPosition()
                : FollowingPlayer.AvatarHolder.transform.position);

            // Handle the pet look at (it has been calculated late in the frame)
            if (HasLookAt) {
                LookAtTargetTransform.position = NavMeshAgent.hasPath && Vector3.Distance(NavMeshAgent.steeringTarget, NavMeshAgent.pathEndPosition) > 0.1f
                    ? NavMeshAgent.steeringTarget with { y = LookAtHeadTransform.position.y }
                    : FollowingPlayerPreviousViewpointPos;
            }

            var localVelocity = NavMeshAgent.transform.InverseTransformDirection(NavMeshAgent.velocity);

            // Set the spawnable parameters
            if (SpawnableMovementYIndex != -1) {
                Spawnable.SetValue(SpawnableMovementYIndex, localVelocity.z/NavMeshAgent.speed);
            }
            if (SpawnableMovementXIndex != -1) {
                Spawnable.SetValue(SpawnableMovementXIndex, localVelocity.x/NavMeshAgent.speed);
            }
            if (SpawnableGroundedIndex != -1) {
                Spawnable.SetValue(SpawnableGroundedIndex, !NavMeshAgent.isOnOffMeshLink && NavMeshAgent.isOnNavMesh ? 1f : 0f);
            }
            if (SpawnableRandomFloatIndex != -1) {
                timer += Time.deltaTime;
                if(timer >= _delay) {
                    Spawnable.SetValue(SpawnableRandomFloatIndex, UnityEngine.Random.value);
                    timer = 0f;
                }
            }

            // Keep the prop synced by us (mimic the attached)
            Spawnable.needsUpdate = true;
        }

        private void LateUpdate() {
            if (!HasLookAt) return;
            FollowingPlayerPreviousViewpointPos = FollowingPlayer == null || FollowingPlayer.AvatarHolder == null
                ? PlayerSetup.Instance._viewPoint.GetPointPosition()
                : FollowingPlayer.PuppetMaster._viewPoint.GetPointPosition();
        }

        private void Start() {
            Controllers.Add(this);
            ModConfig.UpdatePlayerPage();
        }

        private void OnDestroy() {
            Controllers.Remove(this);
            ModConfig.UpdatePlayerPage();
        }

        internal static void AddPetBlueprint(string spawnableGuid, PetBlueprint blueprint) {
            BlueprintHandlers[spawnableGuid] = blueprint;
        }
    }
}
