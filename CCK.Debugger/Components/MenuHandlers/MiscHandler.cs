using ABI_RC.Core.Savior;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Valve.VR;

namespace CCK.Debugger.Components.MenuHandlers;

public class MiscHandler : MenuHandler {

     // Finger Curls
    private static GameObject _categoryFingerCurls;
    private static Dictionary<Func<float>, TextMeshProUGUI> FingerCurlValues;

    public override void Load(Menu menu) {

        menu.AddNewDebugger("Misc");

        menu.ToggleCategories(true);

        // FingerCurls
        var im = CVRInputManager.Instance;
        if (Traverse.Create(CVRInputManager.Instance).Field<List<CVRInputModule>>("_inputModules").Value.Find(module => module is InputModuleSteamVR) is InputModuleSteamVR steamVrIm) {

            _categoryFingerCurls = menu.AddCategory("Finger Curls");

            var triggerValue = Traverse.Create(steamVrIm).Field<SteamVR_Action_Single>("vrTriggerValue").Value;
            var gripValue = Traverse.Create(steamVrIm).Field<SteamVR_Action_Single>("vrGripValue").Value;

            FingerCurlValues = new Dictionary<Func<float>, TextMeshProUGUI>() {
                { () => triggerValue.GetAxis(SteamVR_Input_Sources.LeftHand), menu.AddCategoryEntry(_categoryFingerCurls, "LeftTrigger") },
                { () => gripValue.GetAxis(SteamVR_Input_Sources.LeftHand), menu.AddCategoryEntry(_categoryFingerCurls, "LeftGrip") },
                { () => im.fingerCurlLeftThumb, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlLeftThumb)) },
                { () => im.fingerCurlLeftIndex, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlLeftIndex)) },
                { () => im.fingerCurlLeftMiddle, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlLeftMiddle)) },
                { () => im.fingerCurlLeftRing, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlLeftRing)) },
                { () => im.fingerCurlLeftPinky, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlLeftPinky)) },
                { () => triggerValue.GetAxis(SteamVR_Input_Sources.RightHand), menu.AddCategoryEntry(_categoryFingerCurls, "RightTrigger") },
                { () => gripValue.GetAxis(SteamVR_Input_Sources.RightHand), menu.AddCategoryEntry(_categoryFingerCurls, "RightGrip") },
                { () => im.fingerCurlRightThumb, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlRightThumb)) },
                { () => im.fingerCurlRightIndex, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlRightIndex)) },
                { () => im.fingerCurlRightMiddle, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlRightMiddle)) },
                { () => im.fingerCurlRightRing, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlRightRing)) },
                { () => im.fingerCurlRightPinky, menu.AddCategoryEntry(_categoryFingerCurls, nameof(im.fingerCurlRightPinky)) },
            };
        }
        else {
            FingerCurlValues = new Dictionary<Func<float>, TextMeshProUGUI>();
        }
    }
    public override void Unload() { }


    public override void Update(Menu menu) {
        // Update the finger curl values
        foreach (var fingerCurlValue in FingerCurlValues) {
            // Ignore if the value didn't change
            if (!Menu.HasValueChanged(fingerCurlValue.Value, fingerCurlValue.Key())) continue;
            fingerCurlValue.Value.SetText(fingerCurlValue.Key().ToString());
        }
    }

}
