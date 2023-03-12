using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using UnityEngine;

using Valve.VR;

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
    private SteamVR_Action_Vector2 _vrLookAction;

    public new void Start() {
        _inputManager = CVRInputManager.Instance;
        Instance = this;
        base.Start();

        // Traverse BS
        var vrInput = Traverse.Create(typeof(InputModuleSteamVR)).Field<InputModuleSteamVR>("Instance").Value;
        var vrInputTraverse = Traverse.Create(vrInput);
        _vrLookAction = vrInputTraverse.Field<SteamVR_Action_Vector2>("vrLookAction").Value;

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

            // Thanks NotAKidS for finding the issue and suggesting the fix!
            if (!ViewManager.Instance.isGameMenuOpen() &&
                !Traverse.Create(CVR_MenuManager.Instance).Field<bool>("_quickMenuOpen").Value) {

                // Lets attempt to do a left hand only movement
                if (MetaPort.Instance.isUsingVr && !PlayerSetup.Instance._trackerManager.TrackedObjectsContains("vive_controller")) {
                    _inputManager.movementVector.z = CVRTools.AxisDeadZone(
                        _vrLookAction.GetAxis(SteamVR_Input_Sources.Any).y,
                        MetaPort.Instance.settings.GetSettingInt("ControlDeadZoneRight") / 100f);
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
