using Unity.AI.Navigation;
using UnityEngine;


public class NavMeshLink_TBS : NavMeshLink {

    public string animation_FromStart = "";
    public string animation_FromEnd = "";


    public string GetAnimName(Vector3 charPos) {
        return IsTargetNearStart(charPos) ? animation_FromStart : animation_FromEnd;
    }

    private bool IsTargetNearStart(Vector3 pos) {
        return Vector3.Distance(pos, transform.position + startPoint) <
               Vector3.Distance(pos, transform.position + endPoint);
    }
}
