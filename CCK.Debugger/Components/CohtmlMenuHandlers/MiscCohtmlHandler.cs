using System.Globalization;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using CCK.Debugger.Components.GameObjectVisualizers;
using HarmonyLib;
using UnityEngine;
using Valve.VR;

namespace CCK.Debugger.Components.CohtmlMenuHandlers;

public class MiscCohtmlHandler : ICohtmlHandler {

     // Finger Curls
    private static Section _fingerCurlSection;

    // Eye Movement
    private static Section _eyeMovementSection;

    protected override void Load(CohtmlMenuController menu) {

        var core = new Core("Misc");

        var trackerButton = core.AddButton(new Button(Button.ButtonType.Tracker, false, false));
        var eyeButton = core.AddButton(new Button(Button.ButtonType.Eye, false, true));

        trackerButton.StateUpdater = button => {
            var handsActive = IKSystem.Instance.leftHandModel.activeSelf && IKSystem.Instance.rightHandModel.activeSelf;
            button.IsOn = handsActive && CurrentEntityTrackerList.All(vis => vis.enabled);
            button.IsVisible = MetaPort.Instance.isUsingVr;
        };
        trackerButton.ClickHandler = ClickTrackersButtonHandler;

        eyeButton.StateUpdater = button => {

            var eyeManager = CVREyeControllerManager.Instance;
            var localController = CVREyeControllerManager.Instance.controllerList.First(controller => controller.isLocal);
            var targetGuid = Traverse.Create(localController).Field<string>("targetGuid").Value;

            CurrentEyeCandidateList.Clear();
            foreach (var candidate in eyeManager.targetCandidates) {

                // Create visualizer (if doesn't exist yet)
                if (EyeTargetVisualizer.Create(eyeManager.gameObject, out var trackerVisualizer, candidate.Key, candidate.Value)) {
                    CurrentEyeCandidateList.Add(trackerVisualizer);
                }
            }

            // Update the visualizer states
            EyeTargetVisualizer.UpdateActive(button.IsOn, eyeManager.targetCandidates.Values, targetGuid);
        };
        eyeButton.ClickHandler = button => button.IsOn = !button.IsOn;

        // FingerCurls
        var im = CVRInputManager.Instance;
        if (Traverse.Create(CVRInputManager.Instance).Field<List<CVRInputModule>>("_inputModules").Value.Find(module => module is InputModuleSteamVR) is InputModuleSteamVR steamVrIm) {

            _fingerCurlSection = core.AddSection("Finger Curls", true);

            var triggerValue = Traverse.Create(steamVrIm).Field<SteamVR_Action_Single>("vrTriggerValue").Value;
            var gripValue = Traverse.Create(steamVrIm).Field<SteamVR_Action_Single>("vrGripValue").Value;

            _fingerCurlSection.AddSection("LeftTrigger").AddValueGetter(() => triggerValue.GetAxis(SteamVR_Input_Sources.LeftHand).ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("LeftGrip").AddValueGetter(() => gripValue.GetAxis(SteamVR_Input_Sources.LeftHand).ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftThumb)).AddValueGetter(() => im.fingerCurlLeftThumb.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftIndex)).AddValueGetter(() => im.fingerCurlLeftIndex.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftMiddle)).AddValueGetter(() => im.fingerCurlLeftMiddle.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftRing)).AddValueGetter(() => im.fingerCurlLeftRing.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftPinky)).AddValueGetter(() => im.fingerCurlLeftPinky.ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection("RightTrigger").AddValueGetter(() => triggerValue.GetAxis(SteamVR_Input_Sources.RightHand).ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("RightGrip").AddValueGetter(() => gripValue.GetAxis(SteamVR_Input_Sources.RightHand).ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightThumb)).AddValueGetter(() => im.fingerCurlRightThumb.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightIndex)).AddValueGetter(() => im.fingerCurlRightIndex.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightMiddle)).AddValueGetter(() => im.fingerCurlRightMiddle.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightRing)).AddValueGetter(() => im.fingerCurlRightRing.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightPinky)).AddValueGetter(() => im.fingerCurlRightPinky.ToString(CultureInfo.InvariantCulture));

        }
        else {
            _fingerCurlSection = null;
        }

        // Eye movement target
        _eyeMovementSection = core.AddSection("Eye Movement", true);
        var eyeManager = CVREyeControllerManager.Instance;
        var localController = eyeManager.controllerList.FirstOrDefault(controller => controller.isLocal);
        var controllerTraverse = Traverse.Create(localController);

        var targetGuidField = controllerTraverse.Field<string>("targetGuid");
        _eyeMovementSection.AddSection("Target Guid").AddValueGetter(() => targetGuidField.Value);

        var eyeAngleField = controllerTraverse.Field<Vector2>("eyeAngle");
        _eyeMovementSection.AddSection("Eye Angle").AddValueGetter(() => eyeAngleField.Value.ToString("F3"));

        var leftEyeField = controllerTraverse.Field<Transform>("EyeLeft");
        if (leftEyeField != null) _eyeMovementSection.AddSection("Left Eye Rotation").AddValueGetter(() => leftEyeField.Value.localRotation.ToString("F3"));

        var leftEyeBaseRotField = controllerTraverse.Field<Quaternion>("EyeLeftBaseRot");
        if (leftEyeBaseRotField != null) _eyeMovementSection.AddSection("Left Eye Rotation Base").AddValueGetter(() => leftEyeBaseRotField.Value.ToString("F3"));

        var rightEyeField = controllerTraverse.Field<Transform>("EyeRight");
        if (rightEyeField != null) _eyeMovementSection.AddSection("Right Eye Rotation").AddValueGetter(() => rightEyeField.Value.localRotation.ToString("F3"));

        var rightEyeBaseRotField = controllerTraverse.Field<Quaternion>("EyeRightBaseRot");
        if (rightEyeBaseRotField != null) _eyeMovementSection.AddSection("Right Eye Rotation Base").AddValueGetter(() => rightEyeBaseRotField.Value.ToString("F3"));

        _eyeMovementSection.AddSection("Candidates").AddValueGetter(() => eyeManager.targetCandidates.Values.Join(candidate => candidate.Guid));

        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);
    }

    protected override void Unload() { }

    public override void Reset() { }

    public override void Update(CohtmlMenuController menu) {

        // Update button's states
        Core.UpdateButtonsState();

        // Update the finger curl values
        _fingerCurlSection?.UpdateFromGetter(true);

        // Update eye movement values
        _eyeMovementSection?.UpdateFromGetter(true);
    }

}
