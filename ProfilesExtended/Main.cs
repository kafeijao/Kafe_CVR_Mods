using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI.CCK.Components;
using ABI.CCK.Scripts;
using HarmonyLib;
using MelonLoader;

namespace ProfilesExtended;

public class ProfilesExtended : MelonMod {

    private static string _paramTag;
    private static string _profileTag;

    private static bool _autoProfileEnabled;
    private static string _autoProfileName;

    public override void OnApplicationStart() {

        // Warn about CacheManager@1.0.1 issue
        if (MelonHandler.Mods.Exists(m => m.Info.Name == "CacheManager" && m.Info.Version == "1.0.1")) {
            MelonLogger.Warning("[IMPORTANT] CacheManager@v1.0.1 mod detected!");
            MelonLogger.Msg("The mod CacheManager@v1.0.1 is bugged, and prevents the Avatar Profiles from " +
                                "working completely... And since this mod is all about Avatar Profiles it becomes " +
                                "kinda useless >.>");
        }

        var cat = MelonPreferences.CreateCategory(nameof(ProfilesExtended));

        var ignoreTagParamConfig = cat.CreateEntry("BlacklistParamTag", "*",
            description: "Which tag should be added to the AAS param name (not animator param name) to blacklist" +
                         "the parameter from being affected by profile changes. Requires restart.");
        _paramTag = ignoreTagParamConfig.Value;
        ignoreTagParamConfig.OnValueChangedUntyped += () => _paramTag = ignoreTagParamConfig.Value;

        var ignoreTagProfileConfig = cat.CreateEntry("BlacklistBypassProfileTag", "*",
            description: "Which tag should be added to the profile name to bypass the blacklist (forcing all params" +
                         "to change. Requires restart.");
        _profileTag = ignoreTagProfileConfig.Value;
        ignoreTagProfileConfig.OnValueChangedUntyped += () => _profileTag = ignoreTagProfileConfig.Value;

        var useAutoProfile = cat.CreateEntry("RememberParams", true,
            description: "Whether the mod will remember the latest parameter settings across avatar changes or game " +
                         "restarts or not. It will create a new profile while keeping it updated with current values.");
        _autoProfileEnabled = useAutoProfile.Value;
        useAutoProfile.OnValueChangedUntyped += () => {
            _autoProfileEnabled = useAutoProfile.Value;
            OnParameterChangedViaMenu();
        };

        var autoProfileName = cat.CreateEntry("RememberParamsProfileName", "Auto",
            description: "Profile name save the parameters to remember. You can use the BlacklistBypassProfileTag on " +
                         "this profile name to indicate where you want load ALL parameters (including the blacklisted ones)." +
                         " Requires restart.");
        _autoProfileName = autoProfileName.Value;
        autoProfileName.OnValueChangedUntyped += () => {
            _autoProfileName = autoProfileName.Value;
            OnParameterChangedViaMenu();
        };
    }

    private static void OnParameterChangedViaMenu() {
        if (!_autoProfileEnabled) return;

        // Saves current parameters to profile and sets it as the default
        PlayerSetup.Instance.SaveCurrentAvatarSettingsProfile(_autoProfileName);
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        private static string _loadingProfile;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRAdvancedAvatarSettings), nameof(CVRAdvancedAvatarSettings.LoadProfile))]
        private static void AfterLoadProfile(string profileName, CVRAnimatorManager animatorManager) {
            _loadingProfile = profileName;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.ApplyAdvancedSettingsFileProfile))]
        private static void AfterApplyAdvancedSettingsFileProfile(ref List<CVRAdvancedSettingsFileProfileValue> values) {

            if (_loadingProfile.Contains(_profileTag)) {
                // The profile name is forcing all parameters to change
                MelonLogger.Msg($"Loaded profile {_loadingProfile} which contains a tag that forces all params to change.");
                return;
            }

            // Otherwise -> Check if there are tags to be ignored
            var avatarDescriptor = Traverse.Create(PlayerSetup.Instance).Field("_avatarDescriptor").GetValue<CVRAvatar>();
            var settings = avatarDescriptor.avatarSettings.settings;

            // Remove all values which their AAS name includes the character *
            var removedCount = values.RemoveAll(value =>
                settings.Any(setting =>
                    setting.machineName == value.name && setting.name.Contains(_paramTag)));

            MelonLogger.Msg($"Loaded profile {_loadingProfile} while ignoring {removedCount} parameters.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.HandleSystemCall))]
        private static void AfterCVR_MenuManagerHandleSystemCall(string type, string param1, string param2) {
            // We're detecting the Quick Menu parameter change events here
            if (type is "AppChangeAnimatorParam" or "ChangeAnimatorParam") OnParameterChangedViaMenu();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ViewManager), "RegisterEvents")]
        private static void BeforeViewManagerRegisterEvents(ViewManager __instance) {
            // We're detecting the Main Menu change parameter events here
            // Lets bind this before the game binds it, otherwise we can't overwrite it later
            __instance.gameMenuView.View.BindCall("CVRAppCallChangeAnimatorParam", (string name, float value) => {
                // Call the original function
                PlayerSetup.Instance.changeAnimatorParam(name, value);
                OnParameterChangedViaMenu();
            });
        }
    }
}
