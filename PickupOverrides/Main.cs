using ABI_RC.Core.InteractionSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;

namespace Kafe.PickupOverrides;

internal record PickupOverrideSettings {
    public bool AutoHold { get; set; }
    public float MaxHoldDistance { get; set; }
    public float MaxGrabDistance { get; set; }
}

public class PickupOverrides : MelonMod {

    private static MelonPreferences_Category _melonCategoryPickupOverrides;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideAutoHold;
    private static MelonPreferences_Entry<bool> _melonEntryAutoHoldValue;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideMaxHoldDistance;
    private static MelonPreferences_Entry<float> _melonEntryMaxHoldDistanceValue;

    private static MelonPreferences_Entry<bool> _melonEntryOverrideMaxGrabDistance;
    private static MelonPreferences_Entry<float> _melonEntryMaxGrabDistanceValue;

    private static readonly Dictionary<CVRPickupObject, PickupOverrideSettings> PickupSettings = new();

    public override void OnInitializeMelon() {

        // Melon Config
        _melonCategoryPickupOverrides = MelonPreferences.CreateCategory(nameof(PickupOverrides));

        _melonEntryOverrideAutoHold = _melonCategoryPickupOverrides.CreateEntry("OverrideAutoHold", true,
            description: "Whether this mod should override the pickup auto hold setting or not.",
            oldIdentifier: "OverrideSettings");
        _melonEntryOverrideAutoHold.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryAutoHoldValue = _melonCategoryPickupOverrides.CreateEntry("AutoHold", false,
            description: "The value for the Auto Hold setting. If true the pickup will stick to your hand." +
                         "And can only be dropped by pressing G (on desktop) or holding the right controller " +
                         "grip and pull the right thumbstick down (on VR).");
        _melonEntryAutoHoldValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

        _melonEntryOverrideMaxHoldDistance = _melonCategoryPickupOverrides.CreateEntry("OverrideMaxHoldDistance",
            false, description: "Whether this mod should override the pickup max holding distance setting " +
                                "or not.");
        _melonEntryOverrideMaxHoldDistance.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryMaxHoldDistanceValue = _melonCategoryPickupOverrides.CreateEntry("MaxHoldDistance",
            3f, description: "The value for the max holding distance setting. This will set the max " +
                             "distance from which the object will be from your hand when you grab. Useful to prevent " +
                             "needing to drag your hands to bring the object closer. (default 3f)");
        _melonEntryMaxHoldDistanceValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

        _melonEntryOverrideMaxGrabDistance = _melonCategoryPickupOverrides.CreateEntry("OverrideMaxGrabDistance",
            false, description: "Whether this mod should override the pickup maximum grabbing distance " +
                               "setting or not.");
        _melonEntryOverrideMaxGrabDistance.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());
        _melonEntryMaxGrabDistanceValue = _melonCategoryPickupOverrides.CreateEntry("MaxGrabDistance",
            0f, description: "The value for the max grabbing distance setting. This setting will define " +
                             "how far you can grab the pickups, this can be useful if you're tired of everything" +
                             "being grabbable from the other side of the world. 0 for no limit (default)");
        _melonEntryMaxGrabDistanceValue.OnEntryValueChanged.Subscribe((_, _) => UpdateAllPickups());

    }

    private static void UpdateAllPickups() {
        foreach (var pickup in PickupSettings.Keys) {
            if (pickup != null) UpdatePickup(pickup);
        }
    }

    private static void UpdatePickup(CVRPickupObject pickup) {

        // Update auto-hold
        pickup.autoHold = _melonEntryOverrideAutoHold.Value
            ? _melonEntryAutoHoldValue.Value
            : PickupSettings[pickup].AutoHold;

        // Update max hold distance
        pickup.maxDistance = _melonEntryOverrideMaxHoldDistance.Value
            ? _melonEntryMaxHoldDistanceValue.Value
            : PickupSettings[pickup].MaxHoldDistance;

        // Update max grab distance
        pickup.maximumGrabDistance = _melonEntryOverrideMaxGrabDistance.Value
            ? _melonEntryMaxGrabDistanceValue.Value
            : PickupSettings[pickup].MaxGrabDistance;
    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_InteractableManager), nameof(CVR_InteractableManager.AddPickup))]
        private static void AfterAddPickup(ref CVRPickupObject pickupObject) {
            PickupSettings.Add(pickupObject, new PickupOverrideSettings {
                AutoHold = pickupObject.autoHold,
                MaxHoldDistance = pickupObject.maxDistance,
                MaxGrabDistance = pickupObject.maximumGrabDistance,
            });
            UpdatePickup(pickupObject);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_InteractableManager), nameof(CVR_InteractableManager.RemovePickup))]
        private static void AfterRemovePickup(ref CVRPickupObject pickupObject) {
            PickupSettings.Remove(pickupObject);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_InteractableManager), nameof(CVR_InteractableManager.OnSceneUnLoaded))]
        private static void AfterUnloadingScene() {
            PickupSettings.Clear();
        }
    }
}
