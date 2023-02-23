using UnityEngine;

namespace Kafe.CVRSuperMario64;

public abstract class CVRSM64Input : MonoBehaviour {
    
    public enum Button {
        Jump,
        Kick,
        Stomp,
    }

    public abstract Vector3 GetCameraLookDirection();
    public abstract Vector2 GetJoystickAxes();
    public abstract bool GetButtonHeld(Button button);
    public virtual bool IsMine() => true;
    public virtual bool IsPositionOverriden() => false;
}
