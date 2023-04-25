using ABI_RC.Core;
using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace System.Runtime.CompilerServices {
    // https://stackoverflow.com/questions/64749385/predefined-type-system-runtime-compilerservices-isexternalinit-is-not-defined
    internal static class IsExternalInit {}
}

namespace Kafe.OSC.Utils {
    public static class JsonConfigOsc {

        public static JsonConfigAvatar CurrentAvatarConfig { get; private set; }

        private static readonly HashSet<string> CoreParameters = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();

        internal static void ClearCurrentAvatarConfig() {
            CurrentAvatarConfig = null;
        }

        internal static string GetConfigFilePath(string userUuid, string avatarUuid) {
            var basePath = Application.persistentDataPath;

            // Replace the base path with the override
            if (OSC.Instance.meOSCJsonConfigOverridePathEnabled.Value) {
                basePath = OSC.Instance.meOSCJsonConfigOverridePath.Value;
            }

            return Path.Combine(basePath, "OSC", userUuid, "Avatars", avatarUuid + ".json");
        }

        public static void ProcessUserAndAvatarGuids(ref string userGuid, ref string avatarGuid) {
            var usePrefix = OSC.Instance.meOSCJsonConfigUuidPrefixes.Value;
            userGuid = usePrefix ? $"usr_{userGuid}" : userGuid;
            avatarGuid = usePrefix ? $"avtr_{avatarGuid}" : avatarGuid;
        }

        public static void CreateConfig(string userGuid, string avatarGuid, string avatarName, CVRAnimatorManager animatorManager) {
            try {
                List<JsonConfigParameter> parameters = new();
                foreach (var parameter in animatorManager.animator.parameters) {

                    // Ignore triggers if the triggers module is disabled
                    if (!OSC.Instance.meOSCAvatarModuleTriggers.Value &&
                        parameter.type == AnimatorControllerParameterType.Trigger) continue;

                    var input = new JsonConfigParameterEntry {
                        address = Handlers.OscModules.Avatar.AddressPrefixAvatarParametersLegacy + parameter.name,
                        type = parameter.type,
                    };
                    var output = new JsonConfigParameterEntry {
                        address = Handlers.OscModules.Avatar.AddressPrefixAvatarParametersLegacy + parameter.name,
                        type = parameter.type,
                    };

                    // Don't allow the core parameters to be inputs
                    var isCoreParameter = CoreParameters.Contains(parameter.name);
                    var jsonParameter = isCoreParameter
                        ? new JsonConfigParameter { name = parameter.name, output = output }
                        : new JsonConfigParameter { name = parameter.name, input = input, output = output };

                    parameters.Add(jsonParameter);
                }

                ProcessUserAndAvatarGuids(ref userGuid, ref avatarGuid);

                var avatarConfig = new JsonConfigAvatar {
                    id = avatarGuid,
                    name = avatarName,
                    parameters = parameters.ToArray(),
                };

                var jsonContent = JsonConvert.SerializeObject(avatarConfig, Formatting.Indented);
                var file = new FileInfo(GetConfigFilePath(userGuid, avatarGuid));

                // Prevent replacing if the setting says so
                if (file.Exists && !OSC.Instance.meOSCJsonConfigAlwaysReplace.Value) return;

                // Create directory if doesn't exist
                file.Directory?.Create();
                File.WriteAllText(file.FullName, jsonContent);

                CurrentAvatarConfig = avatarConfig;

                // MelonLogger.Msg($"[Config] {(file.Exists ? "Overwritten" : "Saved")} {avatarName}'s json config to: {file.FullName}");
            }
            catch (Exception e) {
                CurrentAvatarConfig = null;
                MelonLogger.Error($"Something went wrong when trying to create an OSC json config for the avatar {avatarName}");
                MelonLogger.Error(e);
                throw;
            }
        }

        public static JsonConfigAvatar GetConfig(string userGuid, string avatarGuid) {
            try {
                var file = new FileInfo(GetConfigFilePath(userGuid, avatarGuid));
                if (!file.Exists) return null;
                var currentAvatarConfig = JsonConvert.DeserializeObject<JsonConfigAvatar>(File.ReadAllText(file.FullName));
                CurrentAvatarConfig = currentAvatarConfig;
                // MelonLogger.Msg($"[Config] Loaded existing json config for the avatar: {currentAvatarConfig.name}");
                return currentAvatarConfig;
            }
            catch (Exception e) {
                CurrentAvatarConfig = null;
                MelonLogger.Error($"Something went wrong when trying to load an OSC json config for the avatar {avatarGuid}");
                MelonLogger.Error(e);
                throw;
            }
        }

        public static JsonConfigParameterEntry GetJsonConfigParameterEntry(string name, object value) {
            if (Converters.GetParameterType(value).HasValue) {
                return new JsonConfigParameterEntry {
                    address = Handlers.OscModules.Avatar.AddressPrefixAvatarParametersLegacy + name,
                    type = Converters.GetParameterType(value).Value,
                };
            }
            return null;
        }
    }

    public record JsonConfigAvatar {
        public string id { get; init; }
        public string name { get; init; }
        public JsonConfigParameter[] parameters { get; init; }
    }

    public record JsonConfigParameter {
        public string name { get; init; }
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JsonConfigParameterEntry input { get; init; }
        public JsonConfigParameterEntry output { get; init; }
    }

    public record JsonConfigParameterEntry {
        public string address { get; init; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AnimatorControllerParameterType type { get; init; }
    }
}
