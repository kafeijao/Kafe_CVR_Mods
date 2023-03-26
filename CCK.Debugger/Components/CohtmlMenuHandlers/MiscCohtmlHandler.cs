using System.Globalization;
using ABI_RC.Core.Savior;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;

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

        // FingerCurls
        var im = CVRInputManager.Instance;
        if (CVRInputManager.Instance._inputModules.Find(module => module is InputModuleOpenXR) is InputModuleOpenXR openXRIm) {

            _fingerCurlSection = core.AddSection("Finger Curls", true);

            _fingerCurlSection.AddSection("LeftTrigger").AddValueGetter(() => openXRIm.lastInteractLeft.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("LeftGrip").AddValueGetter(() => openXRIm.lastGripLeft.ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftThumb)).AddValueGetter(() => im.fingerCurlLeftThumb.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftIndex)).AddValueGetter(() => im.fingerCurlLeftIndex.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftMiddle)).AddValueGetter(() => im.fingerCurlLeftMiddle.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftRing)).AddValueGetter(() => im.fingerCurlLeftRing.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection(nameof(im.fingerCurlLeftPinky)).AddValueGetter(() => im.fingerCurlLeftPinky.ToString(CultureInfo.InvariantCulture));

            _fingerCurlSection.AddSection("RightTrigger").AddValueGetter(() => openXRIm.lastInteractRight.ToString(CultureInfo.InvariantCulture));
            _fingerCurlSection.AddSection("RightGrip").AddValueGetter(() => openXRIm.lastGripRight.ToString(CultureInfo.InvariantCulture));

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
