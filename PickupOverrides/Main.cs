using ABI_RC.Core.InteractionSystem;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;

namespace PickupOverrides;

public class PickupOverrides : MelonMod {
    
    private static MelonPreferences_Category melonCategoryPickupOverrides;
    private static MelonPreferences_Entry<bool> melonEntryOverride;
    private static MelonPreferences_Entry<bool> melonEntryAutoHold;
    
    public override void OnApplicationStart() {
        
        // Melon Config
        melonCategoryPickupOverrides = MelonPreferences.CreateCategory(nameof(PickupOverrides));
        melonEntryOverride = melonCategoryPickupOverrides.CreateEntry("OverrideSettings", true,
            description: "Whether this mod should override the pickup settings or not.");
        melonEntryAutoHold = melonCategoryPickupOverrides.CreateEntry("AutoHold", false,
            description: "The value for the Auto Hold setting. If true the pickup will stick to your hand." +
                         "And can only be dropped by pressing G (on desktop) or holding the right controller " +
                         "grip and pull the right thumbstick down (on VR).");
        melonCategoryPickupOverrides.SaveToFile(false);
        melonEntryAutoHold.OnValueChangedUntyped += UpdateAllPickups;
    }

    private static void UpdateAllPickups() {
        if (!melonEntryOverride.Value) return;
        var pickups = Traverse.Create(CVR_InteractableManager.Instance).Field("pickupList").GetValue<List<CVRPickupObject>>();
        foreach (var pickup in pickups) {
            pickup.autoHold = melonEntryAutoHold.Value;
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_InteractableManager), nameof(CVR_InteractableManager.AddPickup))]
        private static void AfterAddPickup(ref CVRPickupObject pickupObject) {
            if (!melonEntryOverride.Value) return;
            pickupObject.autoHold = melonEntryAutoHold.Value;
        }
    }
}