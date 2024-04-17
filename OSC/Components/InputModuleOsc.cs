using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using MelonLoader;
using UnityEngine;

namespace Kafe.OSC.Components;

public enum AxisNames {
    Horizontal,
    Vertical,
    LookHorizontal,

    // === Waiting for features ===
    //UseAxisRight,
    //GrabAxisRight,
    MoveHoldFB,
    //SpinHoldCwCcw,
    //SpinHoldUD,
    //SpinHoldLR,

    // === New ===
    LookVertical,
    GripLeftValue,
    GripRightValue,
}

public enum ButtonNames {
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    LookLeft,
    LookRight,
    Jump,
    Run,
    ComfortLeft,
    ComfortRight,
    DropRight,
    UseRight,
    GrabRight,
    DropLeft,
    UseLeft,
    GrabLeft,
    PanicButton,
    QuickMenuToggleLeft,
    QuickMenuToggleRight,
    Voice,

    // === New ===
    Crouch,
    Prone,
    IndependentHeadTurn,
    Zoom,
    Reload,
    ToggleNameplates,
    ToggleHUD,
    SwitchMode,
    ToggleFlightMode,
    Respawn,
    ToggleCamera,
    ToggleSeated,
    QuitGame,
}

public enum ValueNames {
    // === New ===
    Emote,
    GestureLeft,
    GestureRight,
    Toggle,
}

public class InputModuleOSC : CVRInputModule {

    private float _independentHeadTurnDoubleTimer;
    private float _mainMenuTimer;
    private int _sensitivity;

    internal static readonly Dictionary<AxisNames, float> InputAxes = new();
    internal static readonly Dictionary<ButtonNames, bool> InputButtons = new();
    internal static readonly Dictionary<ValueNames, float> InputValues = new();

    private readonly Dictionary<AxisNames, float> InputAxesPrevious = new();
    private readonly Dictionary<ButtonNames, bool> InputButtonsPrevious = new();
    private readonly Dictionary<ValueNames, float> InputValuesPrevious = new();

    private CVRInputManager _thisInputManager;

    public override void ModuleAdded() {

        _thisInputManager = CVRInputManager.Instance;

        _sensitivity = MetaPort.Instance.settings.GetSettingInt("ControlMouseSensitivity");
        MetaPort.Instance.settings.settingIntChanged.AddListener((setting, value) => {
            if (setting != "ControlMouseSensitivity") return;
            _sensitivity = value;
        });

        foreach (var axisName in (AxisNames[])Enum.GetValues(typeof(AxisNames))) {
            InputAxes.Add(axisName, 0f);
            InputAxesPrevious.Add(axisName, 0f);
        }
        foreach (var buttonName in (ButtonNames[])Enum.GetValues(typeof(ButtonNames))) {
            InputButtons.Add(buttonName, false);
            InputButtonsPrevious.Add(buttonName, false);
        }
        foreach (var valueName in (ValueNames[])Enum.GetValues(typeof(ValueNames))) {
            var defaultValue = 0f;
            if (valueName == ValueNames.Emote) defaultValue = -1;
            InputValues.Add(valueName, defaultValue);
            InputValuesPrevious.Add(valueName, defaultValue);
        }
    }

    public override void Update_Always() {

        // Prepare for inline outs
        float floatValue;

        // Handle specials

        var moveLeft = InputButtons[ButtonNames.MoveLeft];
        var moveRight = InputButtons[ButtonNames.MoveRight];
        var moveHorizontalValue = moveLeft ^ moveRight ? (moveLeft ? -1f : 1f) : 0f;
        var horizontal = Mathf.Clamp(InputAxes[AxisNames.Horizontal] + moveHorizontalValue, -1f, 1f);

        var moveForward = InputButtons[ButtonNames.MoveForward];
        var moveBackward = InputButtons[ButtonNames.MoveBackward];
        var moveVerticalValue = moveForward ^ moveBackward ? (moveForward ? 1f : -1f) : 0f;
        var vertical = Mathf.Clamp(InputAxes[AxisNames.Vertical] + moveVerticalValue, -1f, 1f);

        var lookLeft = InputButtons[ButtonNames.LookLeft];
        var lookRight = InputButtons[ButtonNames.LookRight];
        var lookHorizontalValue = lookLeft ^ lookRight ? (lookLeft ? -1f : 1f) : 0f;
        var lookHorizontal = Mathf.Clamp(InputAxes[AxisNames.LookHorizontal] + lookHorizontalValue, -1f, 1f);

        var comfortLeft = InputButtons[ButtonNames.ComfortLeft];
        var comfortRight = InputButtons[ButtonNames.ComfortRight];
        var lookHorizontalComfortValue = comfortLeft ^ comfortRight ? (comfortLeft ? -1f : 1f) : 0f;
        lookHorizontal = Mathf.Clamp( lookHorizontal + InputAxes[AxisNames.LookHorizontal] + lookHorizontalComfortValue, -1f, 1f);

        // Handle inputs

        _thisInputManager.movementVector += new Vector3(horizontal, 0.0f, vertical);
        _thisInputManager.accelerate += Mathf.Clamp(vertical, -1f, 1f);
        _thisInputManager.brake += Mathf.Clamp01(vertical * -1f);

        if (!MetaPort.Instance.isUsingVr) {
            _thisInputManager.lookVector += new Vector2(
                lookHorizontal * (1 + _sensitivity * 0.1f) / 50.0f,
                InputAxes[AxisNames.LookVertical] * (1 + _sensitivity * 0.1f) / 50.0f);
            _thisInputManager.rawLookVector += new Vector2(
                lookHorizontal * (1 + _sensitivity * 0.1f) / 50.0f,
                InputAxes[AxisNames.LookVertical] * (1 + _sensitivity * 0.1f) / 50.0f);
        }

        _thisInputManager.jump |= InputButtons[ButtonNames.Jump];
        _thisInputManager.sprint |= InputButtons[ButtonNames.Run];
        _thisInputManager.crouchToggle |= GetKeyDown(ButtonNames.Crouch);
        _thisInputManager.proneToggle |= GetKeyDown(ButtonNames.Prone);
        _thisInputManager.floatDirection += (InputButtons[ButtonNames.Jump] ? 1.0f : 0.0f) + (InputButtons[ButtonNames.Crouch] ? -1.0f : 0.0f);
        _thisInputManager.independentHeadTurn |= InputButtons[ButtonNames.IndependentHeadTurn];
        _thisInputManager.zoom |= InputButtons[ButtonNames.Zoom];
        _independentHeadTurnDoubleTimer += Time.deltaTime;
        if (GetKeyDown(ButtonNames.IndependentHeadTurn)) {
            if (_independentHeadTurnDoubleTimer < CVRInputManager.doubleInteractThreshold) {
                _thisInputManager.independentHeadToggle = !_thisInputManager.independentHeadToggle;
                _independentHeadTurnDoubleTimer += CVRInputManager.doubleInteractThreshold;
            }
            else {
                _independentHeadTurnDoubleTimer = 0.0f;
            }
        }

        //_thisInputManager.objectPushPull += Input.mouseScrollDelta.y;
        _thisInputManager.scrollValue += InputAxes[AxisNames.MoveHoldFB];
        _thisInputManager.mainMenuButton |= GetKeyDown(ButtonNames.QuickMenuToggleRight);

        if (InputButtons[ButtonNames.QuickMenuToggleRight]) {
            _mainMenuTimer += Time.deltaTime;
            if (_mainMenuTimer > (double)CVRInputManager.buttonHoldThreshold && _mainMenuTimer < CVRInputManager.buttonHoldThreshold * 3.0) {
                _thisInputManager.mainMenuButtonHold = true;
                _mainMenuTimer += 2f * CVRInputManager.buttonHoldThreshold;
            }
            else {
                _thisInputManager.mainMenuButtonHold = false;
            }
        }
        else {
            _mainMenuTimer = 0.0f;
        }

        _thisInputManager.quickMenuButton |= GetKeyDown(ButtonNames.QuickMenuToggleLeft);

        _thisInputManager.voice |= GetKeyDown(ButtonNames.Voice);
        _thisInputManager.voiceDown |= InputButtons[ButtonNames.Voice];

        _thisInputManager.interactRightDown |= GetKeyDown(ButtonNames.UseRight);
        _thisInputManager.interactRightUp |= GetKeyUp(ButtonNames.UseRight);

        _thisInputManager.interactLeftDown |= GetKeyDown(ButtonNames.UseLeft);
        _thisInputManager.interactLeftUp |= GetKeyUp(ButtonNames.UseLeft);

        _thisInputManager.interactLeftValue = Mathf.Min(_thisInputManager.interactLeftValue + (InputButtons[ButtonNames.UseLeft] ? 1f : 0.0f), 1f);
        _thisInputManager.interactRightValue = Mathf.Min(_thisInputManager.interactRightValue + (InputButtons[ButtonNames.UseRight] ? 1f : 0.0f), 1f);

        _thisInputManager.gripRightDown |= GetKeyDown(ButtonNames.GrabRight);
        _thisInputManager.gripRightUp |= GetKeyUp(ButtonNames.GrabRight);

        if (MetaPort.Instance.isUsingVr) {
            _thisInputManager.gripLeftDown |= GetKeyDown(ButtonNames.GrabLeft);
            _thisInputManager.gripLeftUp |= GetKeyUp(ButtonNames.GrabLeft);
        }

        _thisInputManager.gripLeftValue = Mathf.Min(_thisInputManager.gripLeftValue + Mathf.Clamp(InputAxes[AxisNames.GripLeftValue], 0f, 1f), 1f);
        _thisInputManager.gripRightValue = Mathf.Min(_thisInputManager.gripRightValue + Mathf.Clamp(InputAxes[AxisNames.GripRightValue], 0f, 1f), 1f);

        if (GetKeyDown(ValueNames.Emote, out floatValue)) _thisInputManager.emote = floatValue;
        if (GetKeyDown(ValueNames.Toggle, out floatValue)) _thisInputManager.toggleState = floatValue;
        if (GetKeyDown(ValueNames.GestureLeft, out floatValue)) _thisInputManager.gestureLeft = floatValue;
        if (GetKeyDown(ValueNames.GestureRight, out floatValue)) _thisInputManager.gestureRight = floatValue;

        _thisInputManager.reload |= GetKeyDown(ButtonNames.Reload);
        _thisInputManager.switchMode |= GetKeyDown(ButtonNames.SwitchMode);
        _thisInputManager.toggleHud |= GetKeyDown(ButtonNames.ToggleHUD);
        _thisInputManager.toggleNameplates |= GetKeyDown(ButtonNames.ToggleNameplates);

        // Extras

        // Panic button, clear all avatars and props
        if (GetKeyDown(ButtonNames.PanicButton)) {
            MelonLogger.Msg("[Command] Clearing all player Avatars and Props... You might need to rejoin the instance to restore.");
            CVRPlayerManager.Instance.ClearPlayerAvatars();
            CVRSyncHelper.DeleteAllProps();
        }

        // Drop left controller
        if (GetKeyDown(ButtonNames.DropLeft)) {
            foreach (var pickup in CVR_InteractableManager.Instance.pickupList) {
                // pickup._controllerRay.hand == true -> Left hand
                if (!pickup.IsGrabbedByMe || pickup._controllerRay.hand == CVRHand.Left) return;
                MelonLogger.Msg("[Command] Dropping pickup held by left hand...");
                pickup.onDrop.Invoke();
            }
        }

        // Drop right controller
        if (GetKeyDown(ButtonNames.DropRight)) {
            foreach (var pickup in CVR_InteractableManager.Instance.pickupList) {
                // pickup._controllerRay.hand == false -> Right hand
                if (!pickup.IsGrabbedByMe || pickup._controllerRay.hand == CVRHand.Right) return;
                MelonLogger.Msg("[Command] Dropping pickup held by right hand...");
                pickup.onDrop.Invoke();
            }
        }

        // Toggle Flight mode
        if (GetKeyDown(ButtonNames.ToggleFlightMode)) {
            BetterBetterCharacterController.Instance.ToggleFlight();
        }

        // Respawn
        if (GetKeyDown(ButtonNames.Respawn)) {
            RootLogic.Instance.Respawn();
        }

        // Toggle Camera
        if (GetKeyDown(ButtonNames.ToggleCamera)) {
            CVRCamController.Instance.Toggle();
        }

        // Toggle Seated
        if (GetKeyDown(ButtonNames.ToggleSeated)) {
            PlayerSetup.Instance.SwitchSeatedPlay();
        }

        // Quit game
        if (GetKeyDown(ButtonNames.QuitGame)) {
            RootLogic.Instance.QuitApplication();
        }

        // Update the previous values (needs to be the last instruction of the loop)
        UpdatePreviousValues();
    }

    // public override void Update_Always() {
    //     _thisInputManager.mainMenuButton |= GetKeyDown(ButtonNames.QuickMenuToggleRight);
    //     _thisInputManager.quickMenuButton |= GetKeyDown(ButtonNames.QuickMenuToggleLeft);
    //     _thisInputManager.voice |= InputButtons[ButtonNames.Voice];
    //     _thisInputManager.voiceDown |= GetKeyDown(ButtonNames.Voice);
    //     _thisInputManager.scrollValue += InputAxes[AxisNames.MoveHoldFB];
    // }

    private void UpdatePreviousValues() {
        foreach (var axisName in (AxisNames[])Enum.GetValues(typeof(AxisNames))) InputAxesPrevious[axisName] = InputAxes[axisName];
        foreach (var buttonName in (ButtonNames[])Enum.GetValues(typeof(ButtonNames))) InputButtonsPrevious[buttonName] = InputButtons[buttonName];
        foreach (var valueName in (ValueNames[])Enum.GetValues(typeof(ValueNames))) InputValuesPrevious[valueName] = InputValues[valueName];
    }

    private bool GetKeyUp(ButtonNames buttonName) => !InputButtons[buttonName] && InputButtonsPrevious[buttonName];

    private bool GetKeyDown(ButtonNames buttonName) => InputButtons[buttonName] && !InputButtonsPrevious[buttonName];
    private bool GetKeyDown(ValueNames valueName, out float value) {
        value = InputValues[valueName];
        return !Mathf.Approximately(InputValues[valueName], InputValuesPrevious[valueName]);
    }
}
