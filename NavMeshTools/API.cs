using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.AI;

namespace Kafe.NavMeshTools;

public static class API {

    private static uint _jobWorkerCount = (uint) Math.Max(1, Math.Min(JobsUtility.JobWorkerMaximumCount, Environment.ProcessorCount) - 2);

    public static uint JobWorkerCount {
        get => _jobWorkerCount;
        set => _jobWorkerCount = (uint) Math.Clamp(value, 1, JobsUtility.JobWorkerMaximumCount);
    }

    /// <summary>
    /// Request a runtime Bake on the current world.
    /// </summary>
    /// <param name="agent">Agent that will provide the settings and AgentTypeID for the bake.</param>
    /// <param name="onBakeFinish">Called when the bake finishes.
    /// - The int parameter is the AgentTypeID of the agent, you should use it to assign to your NavMeshAgent.
    /// - The bool parameter indicates whether the bake was successful or not.
    /// </param>
    /// <param name="force">Whether to force the bake even if the current world was already baked or not.</param>
    public static void BakeCurrentWorldNavMesh(Agent agent, Action<int, bool> onBakeFinish, bool force = false) {

        // Fail the requests if we haven't initialized yet
        if (NavMeshTools.Instance == null) {
            NavMeshTools.CallResultsAction(onBakeFinish, agent.AgentTypeID, false);
            return;
        }

        NavMeshTools.Instance.RequestWorldBake(agent, onBakeFinish, force);
    }

    /// <summary>
    /// Represents the settings for an agent navigating within a NavMesh.
    /// </summary>
    public class Agent {

        /// <summary>
        /// Agent Type ID for this agent. You must use this on the NavMeshAgent instances you want to navigate on bakes you did with this Agent.
        /// </summary>
        public readonly int AgentTypeID;

        /// <summary>
        /// Settings to be used during bakes.
        /// </summary>
        internal readonly NavMeshBuildSettings Settings;

        /// <summary>
        /// Initializes a new instance of the Agent class.
        /// </summary>
        /// <param name="agentRadius">The radius of the agent for baking in world units.</param>
        /// <param name="agentHeight">The height of the agent for baking in world units.</param>
        /// <param name="agentSlope">The maximum slope angle which is walkable (angle in degrees).</param>
        /// <param name="agentClimb">The maximum vertical step size an agent can take.</param>
        /// <param name="minRegionArea">The approximate minimum area of individual NavMesh regions.</param>
        /// <param name="overrideVoxelSize">Enables overriding the default voxel size.</param>
        /// <param name="voxelSize">Sets the voxel size in world length units.</param>
        /// <param name="overrideTileSize">Enables overriding the default tile size.</param>
        /// <param name="tileSize">Sets the tile size in voxel units.</param>
        public Agent(
            float agentRadius = 0.5f,
            float agentHeight = 2f,
            float agentSlope = 45f,
            float agentClimb = 0.4f,
            float minRegionArea = 2f,
            bool overrideVoxelSize = false,
            float voxelSize = 0.2f,
            bool overrideTileSize = false,
            int tileSize = 256
            ) {
            Settings = NavMesh.CreateSettings() with {
                agentRadius = agentRadius,
                agentHeight = agentHeight,
                agentSlope = agentSlope,
                agentClimb = agentClimb,
                minRegionArea = minRegionArea,
                overrideVoxelSize = overrideVoxelSize,
                voxelSize = voxelSize,
                overrideTileSize = overrideTileSize,
                tileSize = tileSize,
                maxJobWorkers = JobWorkerCount,
            };
            AgentTypeID = Settings.agentTypeID;
        }
    }
}
