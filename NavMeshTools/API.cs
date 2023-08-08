namespace Kafe.NavMeshTools;

public class API {
    /// <summary>
    /// Request a runtime Bake on the current world.
    /// </summary>
    /// <param name="onBakeFinish">Called when the bake finishes. The bool parameter indicates whether the bake was successful or not.</param>
    /// <param name="force">Whether to force the bake even if the current world was already baked or not.</param>
    public static void BakeCurrentWorldNavMesh(Action<bool> onBakeFinish, bool force = false) {

        // Fail the requests if we haven't initialized yet
        if (NavMeshTools.Instance == null) {
            NavMeshTools.CallResultsAction(onBakeFinish, false);
            return;
        }

        NavMeshTools.Instance.RequestWorldBake(onBakeFinish, force);
    }
}
