using System.Globalization;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.FaceTracking;
using ABI_RC.Systems.IK;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.InputManagement.InputModules;
using ABI.CCK.Components;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
using Valve.VR;

namespace Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;

public class MiscCohtmlHandler : ICohtmlHandler {

     // Finger Curls
    private static Section _fingerCurlSection;

    // VR Inputs
    private static Section _vrInputsSection;

    // VR Inputs
    private static Section _faceTrackingSection;

    protected override void Load() {

        var core = new Core("Misc");

        var trackerButton = core.AddButton(new Button(Button.ButtonType.Tracker, false, false));
        trackerButton.StateUpdater = button => {
            var hasTrackersActive = TrackerVisualizer.HasTrackersActive();
            button.IsOn = hasTrackersActive;
            button.IsVisible = MetaPort.Instance.isUsingVr;
        };
        trackerButton.ClickHandler = button => TrackerVisualizer.ToggleTrackers(!button.IsOn);

        var advancedButton = core.AddButton(new Button(Button.ButtonType.Advanced, false, true));
        advancedButton.StateUpdater = button => {
            var hasLabeledVisualizersActive = LabeledVisualizer.HasLabeledVisualizersActive();
            button.IsOn = hasLabeledVisualizersActive;
        };
        advancedButton.ClickHandler = button => {

            // Create (if not created) the default label visualizers

            // Player stuff
            LabeledVisualizer.Create(PlayerSetup.Instance.gameObject, PlayerSetup.Instance.name);
            LabeledVisualizer.Create(PlayerSetup.Instance.PlayerAvatarParent, PlayerSetup.Instance.PlayerAvatarParent.name);
            if (PlayerSetup.Instance._avatar != null) {
                LabeledVisualizer.Create(PlayerSetup.Instance._avatar, PlayerSetup.Instance._avatar.name);
            }

            // Update trackers visualizers
            foreach (var tracker in IKSystem.Instance.AllTrackingPoints) {
                if (!tracker.isActive || !tracker.isValid || tracker.suggestedRole == TrackingPoint.TrackingRole.Invalid) continue;

                var name = tracker.deviceName == "" ? "N/A" : tracker.deviceName;

                LabeledVisualizer.Create(tracker.referenceGameObject, $"Ref - {name}");
                LabeledVisualizer.Create(tracker.offsetTransform.gameObject, $"Offset - {name}");
            }

            LabeledVisualizer.ToggleLabeledVisualizers(!button.IsOn);
        };

        // FingerCurls
        var im = CVRInputManager.Instance;

        _fingerCurlSection = core.AddSection("Finger Curls", true);

        // Left Finger Curls
        _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftThumb)).AddValueGetter(() => im.fingerCurlLeftThumb.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftIndex)).AddValueGetter(() => im.fingerCurlLeftIndex.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftMiddle)).AddValueGetter(() => im.fingerCurlLeftMiddle.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftRing)).AddValueGetter(() => im.fingerCurlLeftRing.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftPinky)).AddValueGetter(() => im.fingerCurlLeftPinky.ToString(CultureInfo.InvariantCulture));

        // Right Finger Curls
        _fingerCurlSection.AddSection(nameof(im.fingerCurlRightThumb)).AddValueGetter(() => im.fingerCurlRightThumb.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlRightIndex)).AddValueGetter(() => im.fingerCurlRightIndex.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlRightMiddle)).AddValueGetter(() => im.fingerCurlRightMiddle.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlRightRing)).AddValueGetter(() => im.fingerCurlRightRing.ToString(CultureInfo.InvariantCulture));
        _fingerCurlSection.AddSection(nameof(im.fingerCurlRightPinky)).AddValueGetter(() => im.fingerCurlRightPinky.ToString(CultureInfo.InvariantCulture));

        // VR Inputs
        if (CVRInputManager.Instance._inputModules.Find(module => module is CVRInputModule_XR) is CVRInputModule_XR xrInputModule) {
            _vrInputsSection = core.AddSection("VR Inputs", true);
            _vrInputsSection.AddSection("LeftTrigger").AddValueGetter(() => xrInputModule._leftModule.Trigger.ToString(CultureInfo.InvariantCulture));
            _vrInputsSection.AddSection("LeftGrip").AddValueGetter(() => xrInputModule._leftModule.Grip.ToString(CultureInfo.InvariantCulture));
            _vrInputsSection.AddSection("RightTrigger").AddValueGetter(() => xrInputModule._rightModule.Trigger.ToString(CultureInfo.InvariantCulture));
            _vrInputsSection.AddSection("RightGrip").AddValueGetter(() => xrInputModule._rightModule.Grip.ToString(CultureInfo.InvariantCulture));
        }
        else {
            _vrInputsSection = null;
        }

        // Face tracking
        _faceTrackingSection = core.AddSection("Face Tracking", true);

        var hasFaceTracking = FaceTrackingManager.Instance.IsEyeDataAvailable;
        var eyeSection = _faceTrackingSection.AddSection("Eye Tracking");
        eyeSection.AddSection("Data Available").AddValueGetter(() => ToString(hasFaceTracking()));
        eyeSection.AddSection("Gaze Direction").AddValueGetter(() => hasFaceTracking() ? FaceTrackingManager.Instance.GetEyeTrackingData().GazeDirection.ToString("F2") : "N/A");

        var hasLipTracking = FaceTrackingManager.Instance.IsLipDataAvailable;
        var lipSection = _faceTrackingSection.AddSection("Lip Tracking");
        lipSection.AddSection("Data Available").AddValueGetter(() => ToString(hasLipTracking()));
        for (var i = 0; i < 37; i++) {
            var shapeKeyIdx = i;
            lipSection.AddSection($"BlendShape {i}").AddValueGetter(() => hasLipTracking() ? FaceTrackingManager.Instance.GetFacialTrackingData().LipShapeData[shapeKeyIdx].ToString(CultureInfo.InvariantCulture) : "N/A");
        }

        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);
    }

    protected override void Unload() { }

    protected override void Reset() { }

    public override void Update() {

        // Update button's states
        Core.UpdateButtonsState();

        // Update the finger curl values
        _fingerCurlSection?.UpdateFromGetter(true);

        // Update vr inputs
        _vrInputsSection?.UpdateFromGetter(true);

        // Update Face Tracking
        _fingerCurlSection?.UpdateFromGetter(true);
    }

}
