using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.ContentClones;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using Kafe.MiniMePlusPlus.Properties;
using MelonLoader;
using UnityEngine;

namespace Kafe.MiniMePlusPlus;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static MelonPreferences_Entry<float> MeMiniMeScaleMultiplier;
    // public static MelonPreferences_Entry<bool> MeMiniSynced;

    // public static ToggleButton IsSynced;
    public static SliderFloat ScaledMultiplier;

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(AssemblyInfoParams.Name);

        MeMiniMeScaleMultiplier = _melonCategory.CreateEntry("MiniMeScaleMultiplier", 1.0f,
            description: "The scale multiplier to apply to the MiniMe when not on the menu.");
        MeMiniMeScaleMultiplier.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (Mathf.Approximately(oldValue, newValue)) return;
            if (ScaledMultiplier != null)
                ScaledMultiplier.SliderValue = newValue;
            MiniMe.UpdateAnchorMode();
            Networking.SendUpdate();
        });

        // MeMiniSynced = _melonCategory.CreateEntry("MiniSynced", true,
        //     description: "Whether should sync all MiniMe or not.");
        // MeMiniSynced.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        // {
        //     if (oldValue == newValue) return;
        //     if (IsSynced != null)
        //         IsSynced.ToggleValue = newValue;
        // });
    }

    public static void InitializeUILibMenu()
    {
        // This button is stupid
        // IsSynced = CVR_MenuManager.CVRUtilsCategory.AddToggle(
        //     "MiniMe Synced",
        //     "Whether should sync all MiniMe or not",
        //     MeMiniSynced.Value);
        // IsSynced.OnValueUpdated += newValue =>
        // {
        //     MeMiniSynced.Value = newValue;
        // };

        ScaledMultiplier = CVR_MenuManager.CVRUtilsCategory.AddSlider(
            "MiniMe Scale",
            "",
            MeMiniMeScaleMultiplier.Value,
            0.1f,
            5f,
            2,
            1f,
            true);
        // ScaledMultiplier.ColumnCount = 2;
        ScaledMultiplier.SliderValue = MeMiniMeScaleMultiplier.Value;
        ScaledMultiplier.OnValueUpdated += newValue =>
        {
            MeMiniMeScaleMultiplier.Value = newValue;
        };
    }
}
