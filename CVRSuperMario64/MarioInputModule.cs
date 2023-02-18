using ABI_RC.Core.Savior;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class MarioInputModule : CVRInputModule {

    public static MarioInputModule Instance;

    private CVRInputManager _inputManager;

    public int controllingMarios;
    public bool canMoveOverride = false;

    public float vertical;
    public float horizontal;
    public bool jump;
    public bool kick;
    public bool stop;

    public new void Start() {
        _inputManager = CVRInputManager.Instance;
        Instance = this;
        base.Start();
    }

    private bool CanMove() {
        return controllingMarios == 0 || canMoveOverride;
    }

    public override void UpdateInput() {

        if (controllingMarios > 0 && Input.GetKeyDown(KeyCode.LeftShift)) {
            canMoveOverride = true;
        }

        if (Input.GetKeyUp(KeyCode.LeftShift)) {
            canMoveOverride = false;
        }

        // Save current input
        horizontal = _inputManager.movementVector.x;
        vertical = _inputManager.movementVector.z;
        jump = _inputManager.jump;
        kick = _inputManager.interactRightValue > 0.25f;
        stop = _inputManager.gripRightValue > 0.25f;

        // Prevent moving if we're controlling marios
        if (!CanMove()) {

            // Prevent the player from moving, doing the NotAKidoS way so he doesn't open Issues ;_;
            _inputManager.movementVector = Vector3.zero;
            _inputManager.jump = false;
            _inputManager.interactRightValue = 0f;
            _inputManager.gripRightValue = 0f;
        }
    }

    public override void UpdateImportantInput() {

        // Prevent Mario from moving while we're using the menu
        horizontal = 0;
        vertical = 0;
        jump = false;
        kick = false;
        stop = false;
    }
}
