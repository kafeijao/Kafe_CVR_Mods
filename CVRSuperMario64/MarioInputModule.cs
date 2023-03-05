using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using UnityEngine;

#if DEBUG
using Valve.VR;
#endif

namespace Kafe.CVRSuperMario64;

public class MarioInputModule : CVRInputModule {
    internal static MarioInputModule Instance;

    private CVRInputManager _inputManager;

    public int controllingMarios;
    public bool canMoveOverride = false;

    public float vertical;
    public float horizontal;
    public bool jump;
    public bool kick;
    public bool stomp;

    // VR Input stuff
    private Traverse<string> _rightHandControllerNameTraverse;

    public new void Start() {
        _inputManager = CVRInputManager.Instance;
        Instance = this;
        base.Start();

        // Traverse BS
        var vrInput = Traverse.Create(typeof(InputModuleOpenXR)).Field<InputModuleOpenXR>("Instance").Value;
        var vrInputTraverse = Traverse.Create(vrInput);
        _rightHandControllerNameTraverse = vrInputTraverse.Field<string>("_rightHandControllerName");

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

        // Save current input
        horizontal = movementVector.x;
        vertical = movementVector.z;
        jump = _inputManager.jump;
        kick = _inputManager.interactRightValue > 0.25f;
        stomp = _inputManager.gripRightValue > 0.25f;

        // Prevent moving if we're controlling marios
        if (!CanMove()) {

            // Prevent the player from moving, doing the NotAKidoS way so he doesn't open Issues ;_;
            _inputManager.movementVector = Vector3.zero;
            _inputManager.jump = false;
            _inputManager.interactRightValue = 0f;
            _inputManager.gripRightValue = 0f;

            // Lets attempt to do a right hand only movement
            var vrRightHand = InputModuleOpenXR.Controls.VRRightHand;
            var rightHandThumbstick = vrRightHand.Primary2DAxis.ReadValue<Vector2>();

            if (MetaPort.Instance.isUsingVr) {
                if (_rightHandControllerNameTraverse.Value != null && _rightHandControllerNameTraverse.Value.Contains("Vive")) {
                    var _viveAdvancedModeRight = false;
                    if (MetaPort.Instance.settings.GetSettingsBool("ControlViveAdvancedControls")) {
                        if (vrRightHand.Primary2DAxisClick.WasPressedThisFrame()) _viveAdvancedModeRight = true;
                        if (vrRightHand.Primary2DAxisTouch.WasReleasedThisFrame()) _viveAdvancedModeRight = false;
                    }
                    else {
                        _viveAdvancedModeRight = vrRightHand.Primary2DAxisClick.IsPressed();
                    }
                    var z = Mathf.Min(CVRTools.AxisDeadZone(vrRightHand.Primary2DAxis.ReadValue<Vector2>().y, MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneLeft") / 100f) * 1.25f, 1f) * (_viveAdvancedModeRight ? 1.0f : 0.0f) * 2.0f;
                    _inputManager.movementVector.z += z;
                }
                else {
                    _inputManager.movementVector.z += CVRTools.AxisDeadZone(rightHandThumbstick.y, MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneLeft") / 100f);
                }
            }
        }
    }

    public override void UpdateImportantInput() {
        // Prevent Mario from moving while we're using the menu
        horizontal = 0;
        vertical = 0;
        jump = false;
        kick = false;
        stomp = false;
    }
}
