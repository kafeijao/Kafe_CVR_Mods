using System.Collections;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace Kafe.Instances;

public static class ModConfig {

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<bool> MeRejoinLastInstanceOnGameRestart;

    internal static MelonPreferences_Entry<bool> MeStartInAnOnlineInstance;
    internal static MelonPreferences_Entry<Region> MeStartingInstanceRegion;
    internal static MelonPreferences_Entry<InstancePrivacyType> MeStartingInstancePrivacyType;

    public enum Region {
        Europe,
        UnitedStates,
    }

    public enum InstancePrivacyType {
        Public,
        FriendsOfFriends,
        Friends,
        EveryoneCanInvite,
        OwnerMustInvite,
    }

    public static void InitializeMelonPrefs() {

        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(Instances));

        MeRejoinLastInstanceOnGameRestart = _melonCategory.CreateEntry("RejoinLastInstanceOnRestart", true,
            description: "Whether to join the last instance (if still available) when restarting the game or not.");

        MeStartInAnOnlineInstance = _melonCategory.CreateEntry("StartInAnOnlineInstance", true,
            description: "Whether to start the game in an online instance or not.");

        MeStartingInstanceRegion = _melonCategory.CreateEntry("StartingInstanceRegion", Region.Europe,
            description: "Which instance region to use when starting in an online instance.");

        MeStartingInstancePrivacyType = _melonCategory.CreateEntry("StartingInstancePrivacyType", InstancePrivacyType.OwnerMustInvite,
            description: "Which instance privacy type to use when starting the game in an online instance.");
    }


    // BTKUI Stuff
    private static readonly HashSet<string> LoadedWorldImages = new();
    private static readonly HashSet<string> LoadingWorldImages = new();

    public static void InitializeBTKUI() {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager) {
        BTKUILib.QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

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

        var joinLastInstanceButton = categorySettings.AddToggle("Join last instance after Restart",
            "Should we attempt to join the last instance you were in upon restarting the game? This takes " +
            "priority over starting in an Online Home World.",
            MeRejoinLastInstanceOnGameRestart.Value);
        joinLastInstanceButton.OnValueUpdated += b => {
            if (b == MeRejoinLastInstanceOnGameRestart.Value) return;
            MeRejoinLastInstanceOnGameRestart.Value = b;
        };
        MeRejoinLastInstanceOnGameRestart.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (joinLastInstanceButton.ToggleValue == newValue) return;
            joinLastInstanceButton.ToggleValue = newValue;
        });

        var joinInitialOnline = categorySettings.AddToggle("Start on Online Home World",
            "Should we create an online instance of your Home World when starting the game? Joining last " +
            "instance takes priority if active.",
            MeStartInAnOnlineInstance.Value);
        joinInitialOnline.OnValueUpdated += b => {
            if (b == MeStartInAnOnlineInstance.Value) return;
            MeStartInAnOnlineInstance.Value = b;
        };
        MeStartInAnOnlineInstance.OnEntryValueChanged.Subscribe((_, newValue) => {
            if (joinInitialOnline.ToggleValue == newValue) return;
            joinInitialOnline.ToggleValue = newValue;
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
            button.OnPress += () => Instances.OnInstanceSelected(instanceInfo.InstanceId);
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
        if (www.result != UnityWebRequest.Result.Success) {
            #if DEBUG
            MelonLogger.Error($"[LoadIconEnumerator] Error on the {worldImageUrl} image download request... Result: {www.result.ToString()}");
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
