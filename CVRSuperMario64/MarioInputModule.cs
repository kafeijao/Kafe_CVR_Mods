using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.XR;
using UnityEngine;

namespace Kafe.CVRSuperMario64;

public class MarioInputModule : CVRInputModule {

    internal static MarioInputModule Instance;

    public int controllingMarios;
    public bool canMoveOverride = false;

    public float vertical;
    public float horizontal;
    public bool jump;
    public bool kick;
    public bool stomp;

    public float cameraRotation;
    public float cameraPitch;

    public override void ModuleAdded() {
        base.ModuleAdded();
        Instance = this;
        CVRSM64Context.UpdateMarioCount();
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

        // Normalize the movement vector, this is done after the modules run, but I'm saving the values before so...
        var movementVector = _inputManager.movementVector;
        if (movementVector.magnitude > movementVector.normalized.magnitude) {
            movementVector.Normalize();
        }

        // Normalize the look vector, this is done after the modules run, but I'm saving the values before so...
        var lookVector = _inputManager.lookVector;
        if (lookVector.magnitude > lookVector.normalized.magnitude) {
            lookVector.Normalize();
        }

        // Save current input
        horizontal = movementVector.x;
        vertical = movementVector.z;
        jump = _inputManager.jump;
        kick = _inputManager.interactRightValue > 0.25f;
        stomp = _inputManager.gripRightValue > 0.25f;

        cameraRotation = lookVector.x;
        cameraPitch = lookVector.y;

        // Prevent moving if we're controlling marios
        if (!CanMove()) {

            // Prevent the player from moving, doing the NotAKidoS way so he doesn't open Issues ;_;
            _inputManager.movementVector = Vector3.zero;
            _inputManager.jump = false;
            _inputManager.interactRightValue = 0f;
            _inputManager.gripRightValue = 0f;

            // Attempt to do a free look control, let's prevent our control from being able to rotate
            if (MarioCameraMod.IsControllingAMario(out var mario) && MarioCameraMod.IsFreeCamEnabled()) {
                _inputManager.lookVector = Vector2.zero;
                // Return because if we're doing the free control we don't want to be able to move around
                return;
            }

            // Thanks NotAKidS for finding the issue and suggesting the fix!
            if (!ViewManager.Instance.IsAnyMenuOpen) {

                // Lets attempt to do a left hand only movement (let's ignore vive wants because it messes the jump)

                if (MetaPort.Instance.isUsingVr) {

                    // No fun for the vive controllers
                    if (CVRInputManager._moduleXR == null ||
                        CVRInputManager._moduleXR._leftModule?.Type == eXRControllerType.Vive ||
                        CVRInputManager._moduleXR._rightModule?.Type == eXRControllerType.Vive) {
                        return;
                    }

                    _inputManager.movementVector.z = CVRTools.AxisDeadZone(
                        CVRInputManager._moduleXR._lookAxis.y,
                        MetaPort.Instance.settings.GetSettingsInt("ControlDeadZoneRight") / 100f);
                }
            }
        }

        base.UpdateInput();
    }

    public override void Update_Always() {
        // Prevent Mario from moving while we're using the menu
        if (!ViewManager.Instance.IsAnyMenuOpen) return;
        horizontal = 0;
        vertical = 0;
        jump = false;
        kick = false;
        stomp = false;

        cameraRotation = 0f;
        cameraPitch = 0f;
    }
}
