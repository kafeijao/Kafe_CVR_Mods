using UnityEngine;

#if DEBUG
using MelonLoader;
#endif

namespace Kafe.BetterPlayerCollider; 

public class BetterPlayerColliderHelper : MonoBehaviour {

    private static BetterPlayerColliderHelper _instance;

    private CharacterController _characterController;

    private int _currentFrame;
    private bool _isCollidingWithWall;

    #if DEBUG
    private bool _isCollidingWithWallPrevious = true;
    #endif

    public static bool IsCollidingWithWall() {
        return _instance._isCollidingWithWall;
    }

    private void Start() {
        _characterController = GetComponent<CharacterController>();
        _instance = this;
    }

    private void Update() {
        #if DEBUG
        if (_isCollidingWithWallPrevious != _isCollidingWithWall) {
            MelonLogger.Msg($"{(_isCollidingWithWall ? "Started" : "Stopped")} Colliding with Wall");
            _isCollidingWithWallPrevious = _isCollidingWithWall;
        }
        #endif
    }

    private void OnControllerColliderHit(ControllerColliderHit hit) {

        // Reset the wall detection
        if (_currentFrame != Time.frameCount) {
            _isCollidingWithWall = false;
            _currentFrame = Time.frameCount;
        }

        // For every collision check if we're colliding with a wall
        if (Vector3.Angle(Vector3.up, hit.normal) > _characterController.slopeLimit) {
            _isCollidingWithWall = true;
        }
    }

}
