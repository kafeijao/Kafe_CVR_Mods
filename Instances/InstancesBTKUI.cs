using System.Collections;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace Kafe.Instances;

public class InstancesBTKUI {

    private static readonly HashSet<string> LoadedWorldImages = new();
    private static readonly HashSet<string> LoadingWorldImages = new();

    public InstancesBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += Initialize;
    }

    private static void Initialize(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= Initialize;

        #if DEBUG
        MelonLogger.Msg($"[InstancesBTKUI] Initializing...");
        #endif

        BTKUILib.QuickMenuAPI.PrepareIcon(nameof(Instances), "InstancesIcon",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));

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

    private static object _refreshButtonCancellationToken;

    private static void CallDelayedRefreshButtons(BTKUILib.UIObjects.Category categoryInstances) {
        if (_refreshButtonCancellationToken != null) {
            MelonCoroutines.Stop(_refreshButtonCancellationToken);
        }
        _refreshButtonCancellationToken = MelonCoroutines.Start(DelayedRefreshButtons(categoryInstances));
    }

    private static IEnumerator DelayedRefreshButtons(BTKUILib.UIObjects.Category categoryInstances) {
        yield return new WaitForSeconds(1);

        categoryInstances.ClearChildren();
        for (var index = 0; index < Instances.Config.RecentInstances.Count; index++) {
            var instanceInfo = Instances.Config.RecentInstances[index];
            // Only use the icons, if the image was loaded (it spams the CVR Logs if we load a non-existing image)
            var buttonIcon = LoadedWorldImages.Contains(instanceInfo.WorldId) ? instanceInfo.WorldId : "";
            var button = categoryInstances.AddButton($"{index}. {instanceInfo.InstanceName}",
                buttonIcon, $"Join {instanceInfo.InstanceName}!");
            button.OnPress += () => Instances.OnInstanceSelected(instanceInfo);
        }
    }


    private static void SetupInstancesButtons(BTKUILib.UIObjects.Category categoryInstances) {
        foreach (var instanceInfo in Instances.Config.RecentInstances) {
            if (instanceInfo.WorldImageUrl != null
                && !LoadingWorldImages.Contains(instanceInfo.WorldId)
                && !LoadedWorldImages.Contains(instanceInfo.WorldId)) {
                LoadingWorldImages.Add(instanceInfo.WorldId);
                MelonCoroutines.Start(LoadIconEnumerator(categoryInstances, instanceInfo.WorldId, instanceInfo.WorldImageUrl));
            }
        }

        CallDelayedRefreshButtons(categoryInstances);
    }

    private static IEnumerator LoadIconEnumerator(BTKUILib.UIObjects.Category categoryInstances, string worldGuid, string worldImageUrl) {

        // Sorry Bono ;_; I just wanted to mess around with it
        // Todo: Remove hacky code once a native implementation is supported

        const string btkuiImagesFolder = "ChilloutVR_Data\\StreamingAssets\\Cohtml\\UIResources\\GameUI\\mods\\BTKUI\\images\\" + nameof(Instances);
        var worldImagePath = btkuiImagesFolder + "\\" + worldGuid + ".png";

        // Icon already exists in cache, so there's nothing we need to do
        if (Directory.Exists(btkuiImagesFolder) && File.Exists(worldImagePath)) {
            LoadedWorldImages.Add(worldGuid);
            LoadingWorldImages.Remove(worldGuid);
            CallDelayedRefreshButtons(categoryInstances);
            yield break;
        }

        #if DEBUG
        MelonLogger.Msg($"[LoadIconEnumerator] Downloading world image url: {worldImageUrl}");
        #endif

        var www = UnityWebRequestTexture.GetTexture(worldImageUrl);
        yield return www.SendWebRequest();

        // Log and break if errors
        if (www.isNetworkError || www.isHttpError) {
            #if DEBUG
            MelonLogger.Error($"[LoadIconEnumerator] Error on the {worldImageUrl} image download request...");
            MelonLogger.Error(www.error);
            #endif
            LoadingWorldImages.Remove(worldGuid);
            yield break;
        }

        // Get the texture bytes and write to our mod's cache folder
        var texture = DownloadHandlerTexture.GetContent(www);
        if (!Directory.Exists(btkuiImagesFolder)) Directory.CreateDirectory(btkuiImagesFolder);
        File.WriteAllBytes(worldImagePath, texture.EncodeToPNG());

        LoadedWorldImages.Add(worldGuid);
        LoadingWorldImages.Remove(worldGuid);
        CallDelayedRefreshButtons(categoryInstances);
    }
}
