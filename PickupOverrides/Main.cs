﻿using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.PickupOverrides;

internal record PickupSettings {
    public bool AutoHold { get; set; }
    // public float MaxHoldDistance { get; set; }
    public float MaxGrabDistance { get; set; }
    public float ThrowForceMultiplier { get; set; }
}

public class PickupOverrides : MelonMod {

    private static MelonPreferences_Category _melonCategoryPickupOverrides;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideAutoHold;
    private static MelonPreferences_Entry<bool> _melonEntryAutoHoldValue;

    // private static MelonPreferences_Entry<bool> _melonEntryOverrideMaxHoldDistance;
    // private static MelonPreferences_Entry<float> _melonEntryMaxHoldDistanceValue;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideMaxGrabDistance;
    private static MelonPreferences_Entry<float> _melonEntryMaxGrabDistanceValue;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideThrowForceMultiplier;
    private static MelonPreferences_Entry<float> _melonEntryThrowForceMultiplierValue;

    private static readonly Dictionary<CVRPickupObject, PickupSettings> PickupSettings = new();

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategoryPickupOverrides = MelonPreferences.CreateCategory(nameof(PickupOverrides));

        // AutoHold
        _melonEntryOverrideAutoHold = _melonCategoryPickupOverrides.CreateEntry("OverrideAutoHold", true,
            description: "Whether this mod should override the pickup auto hold setting or not.",
            oldIdentifier: "OverrideSettings");
        _melonEntryOverrideAutoHold.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryAutoHoldValue = _melonCategoryPickupOverrides.CreateEntry("AutoHold", false,
            description: "The value for the Auto Hold setting. If true the pickup will stick to your hand." +
                         "And can only be dropped by pressing G (on desktop) or holding the right controller " +
                         "grip and pull the right thumbstick down (on VR).");
        _melonEntryAutoHoldValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

        // MaxHoldDistance
        // _melonEntryOverrideMaxHoldDistance = _melonCategoryPickupOverrides.CreateEntry("OverrideMaxHoldDistance",
        //     false, description: "Whether this mod should override the pickup max holding distance setting " +
        //                         "or not.");
        // _melonEntryOverrideMaxHoldDistance.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        // _melonEntryMaxHoldDistanceValue = _melonCategoryPickupOverrides.CreateEntry("MaxHoldDistance",
        //     3f, description: "The value for the max holding distance setting. This will set the max " +
        //                      "distance from which the object will be from your hand when you grab. Useful to prevent " +
        //                      "needing to drag your hands to bring the object closer. (default 3f)");
        // _melonEntryMaxHoldDistanceValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

        // MaxGrabDistance
        _melonEntryOverrideMaxGrabDistance = _melonCategoryPickupOverrides.CreateEntry("OverrideMaxGrabDistance",
            false, description: "Whether this mod should override the pickup maximum grabbing distance " +
                               "setting or not.");
        _melonEntryOverrideMaxGrabDistance.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryMaxGrabDistanceValue = _melonCategoryPickupOverrides.CreateEntry("MaxGrabDistance",
            0f, description: "The value for the max grabbing distance setting. This setting will define " +
                             "how far you can grab the pickups, this can be useful if you're tired of everything" +
                             "being grabbable from the other side of the world. 0 for no limit (default)");
        _melonEntryMaxGrabDistanceValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

        // ThrowForceMultiplier
        _melonEntryOverrideThrowForceMultiplier = _melonCategoryPickupOverrides.CreateEntry("OverrideThrowForceMultiplier",
        false, description: "Whether this mod should override the pickup throw force multiplier setting or not.");
        _melonEntryOverrideThrowForceMultiplier.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryThrowForceMultiplierValue = _melonCategoryPickupOverrides.CreateEntry("ThrowForceMultiplier",
            1.5f, description: "The value for the throw force multipler setting. This setting will be multiplied " +
                             "to the thrown force when throwing pickups. The CCK default is 1.5");
        _melonEntryThrowForceMultiplierValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
    }

    private static void UpdateAllPickups() {
        foreach (var pickup in PickupSettings.Keys) {
            UpdatePickup(pickup);
        }
    }

    private static void UpdatePickup(CVRPickupObject pickup) {

        if (pickup == null) return;

        // Update auto-hold
        pickup.autoHold = _melonEntryOverrideAutoHold.Value
            ? _melonEntryAutoHoldValue.Value
            : PickupSettings[pickup].AutoHold;

        // This was nuked from native game
        // // Update max hold distance
        // pickup.maxDistance = _melonEntryOverrideMaxHoldDistance.Value
        //     ? _melonEntryMaxHoldDistanceValue.Value
        //     : PickupSettings[pickup].MaxHoldDistance;

        // Update max grab distance
        pickup.maximumGrabDistance = _melonEntryOverrideMaxGrabDistance.Value
            ? _melonEntryMaxGrabDistanceValue.Value
            : PickupSettings[pickup].MaxGrabDistance;

        // Update throw force multiplier
        pickup.throwForceMultiplier = _melonEntryOverrideThrowForceMultiplier.Value
            ? _melonEntryThrowForceMultiplierValue.Value
            : PickupSettings[pickup].ThrowForceMultiplier;
    }

    private class PickupDestroyDetector : MonoBehaviour {

        private CVRPickupObject _pickup;

        private void Awake() {
            _pickup = GetComponent<CVRPickupObject>();
            if (!PickupSettings.ContainsKey(_pickup)) {
                PickupSettings.Add(_pickup, new PickupSettings {
                    AutoHold = _pickup.autoHold,
                    // MaxHoldDistance = _pickup.maxDistance,
                    MaxGrabDistance = _pickup.maximumGrabDistance,
                    ThrowForceMultiplier = _pickup.throwForceMultiplier,
                });
            }
            UpdatePickup(_pickup);
        }

        private void OnDestroy() {
            PickupSettings.Remove(_pickup);
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPickupObject), nameof(CVRPickupObject.Start))]
        private static void After_CVR_InteractableManager_AddPickup(CVRPickupObject __instance) {
            try {
                if (__instance == null) return;
                if (__instance.gameObject.GetComponent<PickupDestroyDetector>() == null)
                    __instance.gameObject.AddComponent<PickupDestroyDetector>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVR_InteractableManager_AddPickup)}");
                MelonLogger.Error(e);
            }
        }
    }
}
