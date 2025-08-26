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
    private static Section _fingerNormalizedCurlSection;
    private static Section _fingerCurlsAndSpreadSection;

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
            if (PlayerSetup.Instance.AvatarObject != null) {
                LabeledVisualizer.Create(PlayerSetup.Instance.AvatarObject, PlayerSetup.Instance.AvatarObject.name);
            }

            // Update trackers visualizers
            foreach (var tracker in IKSystem.Instance.TrackingSystem.AllTrackingPoints) {
                if (!tracker.isActive || !tracker.isValid || tracker.suggestedRole == TrackingPoint.TrackingRole.Invalid) continue;

                var name = tracker.deviceName == "" ? "N/A" : tracker.deviceName;

                LabeledVisualizer.Create(tracker.referenceGameObject, $"Ref - {name}");
                LabeledVisualizer.Create(tracker.offsetTransform.gameObject, $"Offset - {name}");
            }

            LabeledVisualizer.ToggleLabeledVisualizers(!button.IsOn);
        };

        // FingerCurls
        var im = CVRInputManager.Instance;

        // Normalized finger curls
        _fingerNormalizedCurlSection = core.AddSection("Finger Curls Normalized", true);

        // Left Hand
        var leftHandNormalized = _fingerNormalizedCurlSection.AddSection("Left Hand");

        // Left Finger Curls Normalized
        leftHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedLeftThumb)).AddValueGetter(() => im.fingerFullCurlNormalizedLeftThumb.ToString(CultureInfo.InvariantCulture));
        leftHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedLeftIndex)).AddValueGetter(() => im.fingerFullCurlNormalizedLeftIndex.ToString(CultureInfo.InvariantCulture));
        leftHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedLeftMiddle)).AddValueGetter(() => im.fingerFullCurlNormalizedLeftMiddle.ToString(CultureInfo.InvariantCulture));
        leftHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedLeftRing)).AddValueGetter(() => im.fingerFullCurlNormalizedLeftRing.ToString(CultureInfo.InvariantCulture));
        leftHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedLeftPinky)).AddValueGetter(() => im.fingerFullCurlNormalizedLeftPinky.ToString(CultureInfo.InvariantCulture));

        // Left Hand
        var rightHandNormalized = _fingerNormalizedCurlSection.AddSection("Right Hand");

        // Right Finger Curls Normalized
        rightHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedRightThumb)).AddValueGetter(() => im.fingerFullCurlNormalizedRightThumb.ToString(CultureInfo.InvariantCulture));
        rightHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedRightIndex)).AddValueGetter(() => im.fingerFullCurlNormalizedRightIndex.ToString(CultureInfo.InvariantCulture));
        rightHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedRightMiddle)).AddValueGetter(() => im.fingerFullCurlNormalizedRightMiddle.ToString(CultureInfo.InvariantCulture));
        rightHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedRightRing)).AddValueGetter(() => im.fingerFullCurlNormalizedRightRing.ToString(CultureInfo.InvariantCulture));
        rightHandNormalized.AddSection(nameof(im.fingerFullCurlNormalizedRightPinky)).AddValueGetter(() => im.fingerFullCurlNormalizedRightPinky.ToString(CultureInfo.InvariantCulture));

        _fingerCurlsAndSpreadSection = core.AddSection("Finger Curls & Spread", true);

        // Left Hand
        var leftHand = _fingerCurlsAndSpreadSection.AddSection("Left Hand");

        var leftThumb = leftHand.AddSection("Thumb");
        leftThumb.AddSection(nameof(im.fingerSpreadLeftThumb)).AddValueGetter(() => im.fingerSpreadLeftThumb.ToString(CultureInfo.InvariantCulture));
        leftThumb.AddSection(nameof(im.finger1StretchedLeftThumb)).AddValueGetter(() => im.finger1StretchedLeftThumb.ToString(CultureInfo.InvariantCulture));
        leftThumb.AddSection(nameof(im.finger2StretchedLeftThumb)).AddValueGetter(() => im.finger2StretchedLeftThumb.ToString(CultureInfo.InvariantCulture));
        leftThumb.AddSection(nameof(im.finger3StretchedLeftThumb)).AddValueGetter(() => im.finger3StretchedLeftThumb.ToString(CultureInfo.InvariantCulture));

        var leftIndex = leftHand.AddSection("Index");
        leftIndex.AddSection(nameof(im.fingerSpreadLeftIndex)).AddValueGetter(() => im.fingerSpreadLeftIndex.ToString(CultureInfo.InvariantCulture));
        leftIndex.AddSection(nameof(im.finger1StretchedLeftIndex)).AddValueGetter(() => im.finger1StretchedLeftIndex.ToString(CultureInfo.InvariantCulture));
        leftIndex.AddSection(nameof(im.finger2StretchedLeftIndex)).AddValueGetter(() => im.finger2StretchedLeftIndex.ToString(CultureInfo.InvariantCulture));
        leftIndex.AddSection(nameof(im.finger3StretchedLeftIndex)).AddValueGetter(() => im.finger3StretchedLeftIndex.ToString(CultureInfo.InvariantCulture));

        var leftMiddle = leftHand.AddSection("Middle");
        leftMiddle.AddSection(nameof(im.fingerSpreadLeftMiddle)).AddValueGetter(() => im.fingerSpreadLeftMiddle.ToString(CultureInfo.InvariantCulture));
        leftMiddle.AddSection(nameof(im.finger1StretchedLeftMiddle)).AddValueGetter(() => im.finger1StretchedLeftMiddle.ToString(CultureInfo.InvariantCulture));
        leftMiddle.AddSection(nameof(im.finger2StretchedLeftMiddle)).AddValueGetter(() => im.finger2StretchedLeftMiddle.ToString(CultureInfo.InvariantCulture));
        leftMiddle.AddSection(nameof(im.finger3StretchedLeftMiddle)).AddValueGetter(() => im.finger3StretchedLeftMiddle.ToString(CultureInfo.InvariantCulture));

        var leftRing = leftHand.AddSection("Ring");
        leftRing.AddSection(nameof(im.fingerSpreadLeftRing)).AddValueGetter(() => im.fingerSpreadLeftRing.ToString(CultureInfo.InvariantCulture));
        leftRing.AddSection(nameof(im.finger1StretchedLeftRing)).AddValueGetter(() => im.finger1StretchedLeftRing.ToString(CultureInfo.InvariantCulture));
        leftRing.AddSection(nameof(im.finger2StretchedLeftRing)).AddValueGetter(() => im.finger2StretchedLeftRing.ToString(CultureInfo.InvariantCulture));
        leftRing.AddSection(nameof(im.finger3StretchedLeftRing)).AddValueGetter(() => im.finger3StretchedLeftRing.ToString(CultureInfo.InvariantCulture));

        var leftPinky = leftHand.AddSection("Pinky");
        leftPinky.AddSection(nameof(im.fingerSpreadLeftPinky)).AddValueGetter(() => im.fingerSpreadLeftPinky.ToString(CultureInfo.InvariantCulture));
        leftPinky.AddSection(nameof(im.finger1StretchedLeftPinky)).AddValueGetter(() => im.finger1StretchedLeftPinky.ToString(CultureInfo.InvariantCulture));
        leftPinky.AddSection(nameof(im.finger2StretchedLeftPinky)).AddValueGetter(() => im.finger2StretchedLeftPinky.ToString(CultureInfo.InvariantCulture));
        leftPinky.AddSection(nameof(im.finger3StretchedLeftPinky)).AddValueGetter(() => im.finger3StretchedLeftPinky.ToString(CultureInfo.InvariantCulture));

        // Right Hand
        var rightHand = _fingerCurlsAndSpreadSection.AddSection("Right Hand");

        var rightThumb = rightHand.AddSection("Thumb");
        rightThumb.AddSection(nameof(im.fingerSpreadRightThumb)).AddValueGetter(() => im.fingerSpreadRightThumb.ToString(CultureInfo.InvariantCulture));
        rightThumb.AddSection(nameof(im.finger1StretchedRightThumb)).AddValueGetter(() => im.finger1StretchedRightThumb.ToString(CultureInfo.InvariantCulture));
        rightThumb.AddSection(nameof(im.finger2StretchedRightThumb)).AddValueGetter(() => im.finger2StretchedRightThumb.ToString(CultureInfo.InvariantCulture));
        rightThumb.AddSection(nameof(im.finger3StretchedRightThumb)).AddValueGetter(() => im.finger3StretchedRightThumb.ToString(CultureInfo.InvariantCulture));

        var rightIndex = rightHand.AddSection("Index");
        rightIndex.AddSection(nameof(im.fingerSpreadRightIndex)).AddValueGetter(() => im.fingerSpreadRightIndex.ToString(CultureInfo.InvariantCulture));
        rightIndex.AddSection(nameof(im.finger1StretchedRightIndex)).AddValueGetter(() => im.finger1StretchedRightIndex.ToString(CultureInfo.InvariantCulture));
        rightIndex.AddSection(nameof(im.finger2StretchedRightIndex)).AddValueGetter(() => im.finger2StretchedRightIndex.ToString(CultureInfo.InvariantCulture));
        rightIndex.AddSection(nameof(im.finger3StretchedRightIndex)).AddValueGetter(() => im.finger3StretchedRightIndex.ToString(CultureInfo.InvariantCulture));

        var rightMiddle = rightHand.AddSection("Middle");
        rightMiddle.AddSection(nameof(im.fingerSpreadRightMiddle)).AddValueGetter(() => im.fingerSpreadRightMiddle.ToString(CultureInfo.InvariantCulture));
        rightMiddle.AddSection(nameof(im.finger1StretchedRightMiddle)).AddValueGetter(() => im.finger1StretchedRightMiddle.ToString(CultureInfo.InvariantCulture));
        rightMiddle.AddSection(nameof(im.finger2StretchedRightMiddle)).AddValueGetter(() => im.finger2StretchedRightMiddle.ToString(CultureInfo.InvariantCulture));
        rightMiddle.AddSection(nameof(im.finger3StretchedRightMiddle)).AddValueGetter(() => im.finger3StretchedRightMiddle.ToString(CultureInfo.InvariantCulture));

        var rightRing = rightHand.AddSection("Ring");
        rightRing.AddSection(nameof(im.fingerSpreadRightRing)).AddValueGetter(() => im.fingerSpreadRightRing.ToString(CultureInfo.InvariantCulture));
        rightRing.AddSection(nameof(im.finger1StretchedRightRing)).AddValueGetter(() => im.finger1StretchedRightRing.ToString(CultureInfo.InvariantCulture));
        rightRing.AddSection(nameof(im.finger2StretchedRightRing)).AddValueGetter(() => im.finger2StretchedRightRing.ToString(CultureInfo.InvariantCulture));
        rightRing.AddSection(nameof(im.finger3StretchedRightRing)).AddValueGetter(() => im.finger3StretchedRightRing.ToString(CultureInfo.InvariantCulture));

        var rightPinky = rightHand.AddSection("Pinky");
        rightPinky.AddSection(nameof(im.fingerSpreadRightPinky)).AddValueGetter(() => im.fingerSpreadRightPinky.ToString(CultureInfo.InvariantCulture));
        rightPinky.AddSection(nameof(im.finger1StretchedRightPinky)).AddValueGetter(() => im.finger1StretchedRightPinky.ToString(CultureInfo.InvariantCulture));
        rightPinky.AddSection(nameof(im.finger2StretchedRightPinky)).AddValueGetter(() => im.finger2StretchedRightPinky.ToString(CultureInfo.InvariantCulture));
        rightPinky.AddSection(nameof(im.finger3StretchedRightPinky)).AddValueGetter(() => im.finger3StretchedRightPinky.ToString(CultureInfo.InvariantCulture));

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
        eyeSection.AddSection("Gaze Direction").AddValueGetter(() => hasFaceTracking() ? FaceTrackingManager.Instance.GetEyeTrackingData().gazePoint.ToString("F2") : "N/A");

        // Todo: Add the new ft stuff
        // var hasLipTracking = FaceTrackingManager.Instance.IsLipDataAvailable;
        // var lipSection = _faceTrackingSection.AddSection("Lip Tracking");
        // lipSection.AddSection("Data Available").AddValueGetter(() => ToString(hasLipTracking()));
        // for (var i = 0; i < 37; i++) {
        //     var shapeKeyIdx = i;
        //     lipSection.AddSection($"BlendShape {i}").AddValueGetter(() => hasLipTracking() ? FaceTrackingManager.Instance.GetFacialTrackingData().LipShapeData[shapeKeyIdx].ToString(CultureInfo.InvariantCulture) : "N/A");
        // }

        Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(core);
    }

    protected override void Unload() { }

    protected override void Reset() { }

    public override void Update() {

        // Update button's states
        Core.UpdateButtonsState();

        // Update the finger curl values
        _fingerNormalizedCurlSection?.UpdateFromGetter(true);
        _fingerCurlsAndSpreadSection?.UpdateFromGetter(true);

        // Update vr inputs
        _vrInputsSection?.UpdateFromGetter(true);

        // Update Face Tracking
        _faceTrackingSection?.UpdateFromGetter(true);
    }

}
