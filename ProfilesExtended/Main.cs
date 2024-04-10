using System.Collections;
using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Util.AnimatorManager;
using ABI.CCK.Scripts;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.ProfilesExtended;

public class ProfilesExtended : MelonMod {

    private static string _paramTag;
    private static string _profileTag;

    private static bool _autoProfileEnabled;
    private static string _autoProfileName;
    private static bool _onlyLoadAASParams;

    private static object _coroutineCancellationToken;

    public override void OnInitializeMelon() {

        // Warn about CacheManager@1.0.1 issue
        if (RegisteredMelons.ToList().Exists(m => m.Info.Name == "CacheManager" && m.Info.Version == "1.0.1")) {
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
        ignoreTagParamConfig.OnEntryValueChanged.Subscribe((_, _) => _paramTag = ignoreTagParamConfig.Value);

        var ignoreTagProfileConfig = cat.CreateEntry("BlacklistBypassProfileTag", "*",
            description: "Which tag should be added to the profile name to bypass the blacklist (forcing all params" +
                         "to change. Requires restart.");
        _profileTag = ignoreTagProfileConfig.Value;
        ignoreTagProfileConfig.OnEntryValueChanged.Subscribe((_, _) => _profileTag = ignoreTagProfileConfig.Value);

        var useAutoProfile = cat.CreateEntry("RememberParams", true,
            description: "Whether the mod will remember the latest parameter settings across avatar changes or game " +
                         "restarts or not. It will create a new profile while keeping it updated with current values.");
        _autoProfileEnabled = useAutoProfile.Value;
        useAutoProfile.OnEntryValueChanged.Subscribe((_, _) => {
            _autoProfileEnabled = useAutoProfile.Value;
            QueueSaveAvatarSettingsProfile();
        });

        var autoProfileName = cat.CreateEntry("RememberParamsProfileName", "Auto",
            description: "Profile name save the parameters to remember. You can use the BlacklistBypassProfileTag on " +
                         "this profile name to indicate where you want load ALL parameters (including the blacklisted ones)." +
                         " Requires restart.");
        _autoProfileName = autoProfileName.Value;
        autoProfileName.OnEntryValueChanged.Subscribe((_, _) => {
            _autoProfileName = autoProfileName.Value;
            QueueSaveAvatarSettingsProfile();
        });

        var onlyLoadAASParams = cat.CreateEntry("OnlyLoadAASParams", true,
            description: "Whether it should load only AAS parameters when loading a profile, or all parameters in " +
                         "the animator.");
        _onlyLoadAASParams = onlyLoadAASParams.Value;
        onlyLoadAASParams.OnEntryValueChanged.Subscribe((_, newValue) => {
            _onlyLoadAASParams = newValue;
        });
    }

    private static void QueueSaveAvatarSettingsProfile() {
        if (_coroutineCancellationToken != null) {
            MelonCoroutines.Stop(_coroutineCancellationToken);
        }
        _coroutineCancellationToken = MelonCoroutines.Start(SaveAvatarSettingsProfile());
    }

    private static IEnumerator SaveAvatarSettingsProfile() {
        yield return new WaitForSeconds(1f);

        if (!_autoProfileEnabled) yield break;

        // Saves current parameters to profile and sets it as the default
        if (PlayerSetup.Instance._avatar != null && PlayerSetup.Instance._avatarDescriptor != null &&
            PlayerSetup.Instance._avatarDescriptor.avatarUsesAdvancedSettings) {
            PlayerSetup.Instance._avatarDescriptor.avatarSettings.SaveCurrentAvatarProfile(_autoProfileName, PlayerSetup.Instance.animatorManager);
        }

        _coroutineCancellationToken = null;
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
        [HarmonyPatch(typeof(AvatarAnimatorManager), nameof(AvatarAnimatorManager.ApplyAdvancedSettingsFileProfile))]
        private static void AfterApplyAdvancedSettingsFileProfile(ref List<CVRAdvancedSettingsFileProfileValue> values) {
            try {

                if (_loadingProfile.Contains(_profileTag)) {
                    // The profile name is forcing all parameters to change
                    MelonLogger.Msg($"Loaded profile {_loadingProfile} which contains a tag that forces all params to change.");
                    return;
                }

                var removedString = "";

                // Otherwise -> Check if there are tags to be ignored
                if (PlayerSetup.Instance._avatarDescriptor.avatarUsesAdvancedSettings) {
                    var settings = PlayerSetup.Instance._avatarDescriptor.avatarSettings.settings;

                    // Remove all values that are not AAS parameters
                    if (_onlyLoadAASParams) {
                        var removedAAS = values.RemoveAll(value =>
                            !settings.Exists(setting =>
                                setting.machineName == value.name));
                        if (removedAAS > 0) removedString += $"Ignored {removedAAS} animator params because they're not AAS parameters.";
                    }

                    // Remove all values which their AAS name includes the character *
                    var removedWildcard = values.RemoveAll(value =>
                        settings.Any(setting =>
                            setting.machineName == value.name && setting.name.Contains(_paramTag)));
                    if (removedWildcard > 0) removedString += $"Ignored {removedWildcard} animator params because their AAS contains the {_paramTag} wildcard.";
                }

                MelonLogger.Msg($"Loaded profile {_loadingProfile}. {removedString}");

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(AfterApplyAdvancedSettingsFileProfile)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.HandleSystemCall))]
        private static void AfterCVR_MenuManagerHandleSystemCall(string type, string param1, string param2) {
            // We're detecting the Quick Menu parameter change events here
            try {
                if (type is "AppChangeAnimatorParam" or "ChangeAnimatorParam") QueueSaveAvatarSettingsProfile();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(AfterApplyAdvancedSettingsFileProfile)}");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.changeAnimatorParam))]
        private static void After_PlayerSetup_changeAnimatorParam(PlayerSetup __instance, string name, float value, int source) {
            // We're detecting the Main Menu change parameter events here -> Save current changes
            try {
                // Source 1 and 2 are the menus, 0 is automatic stuff that we should ignore
                if (source != 0) QueueSaveAvatarSettingsProfile();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(AfterApplyAdvancedSettingsFileProfile)}");
                MelonLogger.Error(e);
            }
        }
    }
}
