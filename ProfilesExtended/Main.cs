using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI.CCK.Components;
using ABI.CCK.Scripts;
using HarmonyLib;
using MelonLoader;

namespace ProfilesExtended;

public class ProfilesExtended : MelonMod {

    private static string[] _paramTags;
    private static string[] _profileTags;

    public override void OnApplicationStart() {

        // Warn about CacheManager@1.0.1 issue
        if (MelonHandler.Mods.Exists(m => m.Info.Name == "CacheManager" && m.Info.Version == "1.0.1")) {
            MelonLogger.Warning("[IMPORTANT] CacheManager@v1.0.1 mod detected!");
            MelonLogger.Msg("The mod CacheManager@v1.0.1 is bugged, and prevents the Avatar Profiles from " +
                                "working completely... And since this mod is all about Avatar Profiles it becomes " +
                                "kinda useless >.>");
        }

        var cat = MelonPreferences.CreateCategory(nameof(ProfilesExtended));

        var ignoreTagParamConfig = cat.CreateEntry("IgnoreTagParam", new[] {"*"},
            description: "Which tags should be added to the AAS param name (not animator param name) so it is" +
                         "ignored by the profile changes (default always overrides).");
        _paramTags = ignoreTagParamConfig.Value;
        ignoreTagParamConfig.OnValueChangedUntyped += () => _paramTags = ignoreTagParamConfig.Value;

        var ignoreTagProfileConfig = cat.CreateEntry("ForceTagProfile", new[] {"*"},
            description: "Which tags should be added to the profile name to force all params to change when chanted into.");
        _profileTags = ignoreTagProfileConfig.Value;
        ignoreTagProfileConfig.OnValueChangedUntyped += () => _profileTags = ignoreTagProfileConfig.Value;
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

            if (_profileTags.Any(tag => _loadingProfile.Contains(tag))) {
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
                    setting.machineName == value.name && _paramTags.Any(tag => setting.name.Contains(tag))));

            MelonLogger.Msg($"Loaded profile {_loadingProfile} while ignoring {removedCount} parameters.");
        }
    }
}
