using UnityEngine;

namespace Kafe.NavMeshFollower;

[Serializable]
public abstract class FollowerStateMachine : StateMachineBehaviour {

    [NonSerialized] protected bool IsInitialized;
    [NonSerialized] protected FollowerController Controller;

    internal void Initialize(FollowerController controller) {
        IsInitialized = true;
        Controller = controller;
    }
}
