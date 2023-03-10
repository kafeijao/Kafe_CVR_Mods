using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;

namespace Kafe.Instances;

public class InstancesBTKUI {

    public InstancesBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += Initialize;
    }

    private void Initialize(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= Initialize;

        #if DEBUG
        MelonLogger.Msg($"[InstancesBTKUI] Initializing...");
        #endif

        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(Instances), "InstancesIcon",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("Instances.Resources.InstancesBTKUIIcon.png"));

        var page = new BTKUILib.UIObjects.Page(nameof(Instances), nameof(Instances), true, "InstancesIcon") {
            MenuTitle = nameof(Instances),
            MenuSubtitle = "Rejoin previous instances",
        };

        var categoryInstances = page.AddCategory("Recent Instances");
        SetupInstancesButtons(categoryInstances);
        Instances.InstancesConfigChanged += () => SetupInstancesButtons(categoryInstances);

        var categorySettings = page.AddCategory("Settings");
        var toggleButton = categorySettings.AddToggle("Join last instance after Restart",
            "Should we attempt to join the last instance you were in upon restarting the game?",
            Instances.MeRejoinLastInstanceOnGameRestart.Value);

        toggleButton.OnValueUpdated += b => {
            if (b == Instances.MeRejoinLastInstanceOnGameRestart.Value) return;
            Instances.MeRejoinLastInstanceOnGameRestart.Value = b;
        };

        Instances.MeRejoinLastInstanceOnGameRestart.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (toggleButton.ToggleValue == newValue) return;
            toggleButton.ToggleValue = newValue;
        });
    }

    private void SetupInstancesButtons(BTKUILib.UIObjects.Category categoryInstances) {
        categoryInstances.ClearChildren();
        for (var index = 0; index < Instances.Config.RecentInstances.Count; index++) {
            var instanceInfo = Instances.Config.RecentInstances[index];
            var button = categoryInstances.AddButton($"{index}. {instanceInfo.InstanceName}",
                "", $"Join {instanceInfo.InstanceName}!");
            button.OnPress += () => Instances.OnInstanceSelected(instanceInfo);
        }
    }
}
