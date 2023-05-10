using System.Globalization;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.IK;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
using Valve.VR;

namespace Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;

public class MiscCohtmlHandler : ICohtmlHandler {

     // Finger Curls
    private static Section _fingerCurlSection;

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

                var name = tracker.deviceName == "" ? "Basestation?" : tracker.deviceName;

                LabeledVisualizer.Create(tracker.referenceGameObject, $"Ref - {name}");
                LabeledVisualizer.Create(tracker.offsetTransform.gameObject, $"Offset - {name}");
            }

            LabeledVisualizer.ToggleLabeledVisualizers(!button.IsOn);
        };

        // FingerCurls
        var im = CVRInputManager.Instance;
        if (CVRInputManager.Instance._inputModules.Find(module => module is InputModuleSteamVR) is InputModuleSteamVR steamVrIm) {

            _fingerCurlSection = core.AddSection("Finger Curls", true);

            _fingerCurlSection.AddSection("LeftTrigger").AddValueGetter(() => steamVrIm.vrTriggerValue.GetAxis(SteamVR_Input_Sources.LeftHand).ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("LeftGrip").AddValueGetter(() => steamVrIm.vrGripValue.GetAxis(SteamVR_Input_Sources.LeftHand).ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftThumb)).AddValueGetter(() => im.fingerCurlLeftThumb.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftIndex)).AddValueGetter(() => im.fingerCurlLeftIndex.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftMiddle)).AddValueGetter(() => im.fingerCurlLeftMiddle.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftRing)).AddValueGetter(() => im.fingerCurlLeftRing.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftPinky)).AddValueGetter(() => im.fingerCurlLeftPinky.ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection("RightTrigger").AddValueGetter(() => steamVrIm.vrTriggerValue.GetAxis(SteamVR_Input_Sources.RightHand).ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("RightGrip").AddValueGetter(() => steamVrIm.vrGripValue.GetAxis(SteamVR_Input_Sources.RightHand).ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightThumb)).AddValueGetter(() => im.fingerCurlRightThumb.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightIndex)).AddValueGetter(() => im.fingerCurlRightIndex.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightMiddle)).AddValueGetter(() => im.fingerCurlRightMiddle.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightRing)).AddValueGetter(() => im.fingerCurlRightRing.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlRightPinky)).AddValueGetter(() => im.fingerCurlRightPinky.ToString(CultureInfo.InvariantCulture));

        }
        else {
            _fingerCurlSection = null;
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
    }

}
