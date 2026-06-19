using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using MelonLoader;

namespace Kafe.RCCOverrides;

public static class ModConfig {

    // Melon Prefs
    internal static MelonPreferences_Category MelonCategory;
    // internal static MelonPreferences_Entry<bool> MePlacePortalsMidAir;

    public static void InitializeMelonPrefs() {

        // Melon Config
        MelonCategory = MelonPreferences.CreateCategory(nameof(RCCOverrides));
        //
        // MePlacePortalsMidAir = MelonCategory.CreateEntry("PlacePortalsMidAir", true,
        //     description: "Whether to be able to place portals mid air or not.");

    }

    public static void InitializeBTKUI() {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static Category _category;

    private static void SetupBTKUI(CVR_MenuManager manager) {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;
        _category = QuickMenuAPI.MiscTabPage.AddCategory(nameof(RCCOverrides), nameof(RCCOverrides));
        UpdateCars();
    }

    public static void UpdateCars() {

        _category.AddButton("MaxSpeed", "", "").OnPress += () => {
            QuickMenuAPI.OpenNumberInput("MaxSpeed", 0, newvalue => {
                foreach (var (spawnable, controller) in RCCOverrides.CurrentControllers) {
                    controller.maxspeed = newvalue;
                }
            });
        };

        _category.AddButton("MaxTorque", "", "").OnPress += () => {
            QuickMenuAPI.OpenNumberInput("MaxTorque", 0, newvalue => {
                foreach (var (spawnable, controller) in RCCOverrides.CurrentControllers) {
                    controller.maxEngineTorque = newvalue;
                }
            });
        };

        _category.AddButton("Slip", "", "").OnPress += () => {
            QuickMenuAPI.OpenNumberInput("Slip", 0, newvalue => {
                foreach (var (spawnable, controller) in RCCOverrides.CurrentControllers) {
                    controller.frontSlip = newvalue;
                    controller.rearSlip = newvalue;
                }
            });
        };

        _category.AddButton("Friction", "", "").OnPress += () => {
            QuickMenuAPI.OpenNumberInput("Friction", 0, newvalue => {
                foreach (var (spawnable, controller) in RCCOverrides.CurrentControllers) {
                    controller.engineFriction = newvalue;
                }
            });
        };

        // _category.ClearChildren();
        // foreach (var (spawnable, controller) in RCCOverrides.CurrentControllers) {
        //     _category
        // }
    }

}
