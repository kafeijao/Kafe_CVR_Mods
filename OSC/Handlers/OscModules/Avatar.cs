using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using Kafe.OSC.Utils;
using MelonLoader;
using Rug.Osc.Core;
using UnityEngine;

namespace Kafe.OSC.Handlers.OscModules;

public class Avatar : OscHandler {

    internal const string AddressPrefixAvatar = "/avatar/";
    internal const string AddressPrefixAvatarParameters = $"{AddressPrefixAvatar}parameter";
    internal const string AddressPrefixAvatarParametersLegacy = $"{AddressPrefixAvatar}parameters/";
    private const string AddressPrefixAvatarChange = $"{AddressPrefixAvatar}change";

    private static readonly HashSet<string> CoreParameters = CVRAnimatorManager.coreParameters;

    private bool _enabled;
    private bool _bypassJsonConfig;
    private bool _debugConfigWarnings;
    private readonly Dictionary<string, JsonConfigParameterEntry> _parameterAddressCache = new();

    private readonly Action<CVRAnimatorManager> _animatorManagerUpdated;
    private readonly Action<string, float> _parameterChangedFloat;
    private readonly Action<string, int> _parameterChangedInt;
    private readonly Action<string, bool> _parameterChangedBool;
    private readonly Action<string> _parameterChangedTrigger;

    public Avatar() {

        // Execute actions on local avatar changed
        _animatorManagerUpdated = InitializeNewAvatar;

        // Send avatar float parameter change events
        _parameterChangedFloat = (parameter, value) => {
            if (!_parameterAddressCache.ContainsKey(parameter)) return;
            SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };

        // Send avatar int parameter change events
        _parameterChangedInt = (parameter, value) => {
            if (!_parameterAddressCache.ContainsKey(parameter)) return;
            SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };

        // Send avatar bool parameter change events
        _parameterChangedBool = (parameter, value) => {
            if (!_parameterAddressCache.ContainsKey(parameter)) return;
            SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };

        // Send avatar trigger parameter change events
        _parameterChangedTrigger = parameter => {
            if (!_parameterAddressCache.ContainsKey(parameter)) return;
            SendAvatarParamToConfigAddress(parameter, null);
        };

        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCAvatarModule.Value) Enable();
        OSC.Instance.meOSCAvatarModule.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        });

        // Set whether should bypass json config or not, and handle the config change
        _bypassJsonConfig = OSC.Instance.meOSCAvatarModuleBypassJsonConfig.Value;
        OSC.Instance.meOSCAvatarModuleBypassJsonConfig.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue == oldValue) return;
            if (PlayerSetup.Instance.animatorManager != null) {
                MelonLogger.Msg($"Changed the bypass json config Configuration to {newValue}, the avatar parameter are going to be reloaded...");
                InitializeNewAvatar(PlayerSetup.Instance.animatorManager);
            }
            _bypassJsonConfig = newValue;
        });

        // Handle the warning when blocked osc command by config
        _debugConfigWarnings = OSC.Instance.meOSCDebugConfigWarnings.Value;
        OSC.Instance.meOSCDebugConfigWarnings.OnEntryValueChanged.Subscribe((_, enabled) => _debugConfigWarnings = enabled);
    }

    internal sealed override void Enable() {
        Events.Avatar.AnimatorManagerUpdated += _animatorManagerUpdated;
        Events.Avatar.ParameterChangedFloat += _parameterChangedFloat;
        Events.Avatar.ParameterChangedInt += _parameterChangedInt;
        Events.Avatar.ParameterChangedBool += _parameterChangedBool;
        Events.Avatar.ParameterChangedTrigger += _parameterChangedTrigger;
        _enabled = true;
    }

    internal sealed override void Disable() {
        Events.Avatar.AnimatorManagerUpdated -= _animatorManagerUpdated;
        Events.Avatar.ParameterChangedFloat -= _parameterChangedFloat;
        Events.Avatar.ParameterChangedInt -= _parameterChangedInt;
        Events.Avatar.ParameterChangedBool -= _parameterChangedBool;
        Events.Avatar.ParameterChangedTrigger -= _parameterChangedTrigger;
        _enabled = false;
    }

    internal sealed override void ReceiveMessageHandler(OscMessage oscMsg) {
        if (!_enabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg($"[Config] Sent an osc msg to {AddressPrefixAvatar}, but this module is disabled " +
                                $"in the configuration file, so this will be ignored.");
            }
            return;
        }

        var addressLower= oscMsg.Address.ToLower();

        // Get only the first value and assume no values to be null
        var valueObj = oscMsg.Count > 0 ? oscMsg[0] : null;
        var valueObj2 = oscMsg.Count > 1 ? oscMsg[1] : null;

        // Handle the change parameter requests
        if (addressLower.Equals(AddressPrefixAvatarParameters)) {
            if (valueObj2 is not string paramName) {
                MelonLogger.Msg($"[Error] Attempted to change an avatar parameter, but the parameter name " +
                                $"provided is not a string :( you sent a {valueObj2?.GetType()} type argument.");
                return;
            }
            ParameterChangeHandler(paramName, valueObj);
        }

        // Handle the change avtar requests
        else if (addressLower.Equals(AddressPrefixAvatarChange)) {
            if (valueObj is not string valueStr) {
                MelonLogger.Msg($"[Error] Attempted to change the avatar, but the guid provided is not a string " +
                                $":( you sent a {valueObj?.GetType()} type argument.");
                return;
            }
            // Get rid of the optional avtr_ prefix
            if (valueStr.StartsWith("avtr_", StringComparison.InvariantCultureIgnoreCase)) {
                valueStr = valueStr.Substring("avtr_".Length);
            }
            Events.Avatar.OnAvatarSet(valueStr);
        }

        // Handle the change parameter requests [Deprecated]
        else if (addressLower.StartsWith(AddressPrefixAvatarParametersLegacy)) {
            var parameter = oscMsg.Address.Substring(AddressPrefixAvatarParametersLegacy.Length);
            ParameterChangeHandler(parameter, valueObj);
        }
    }

    private void InitializeNewAvatar(CVRAnimatorManager manager) {

        // Clear address cache
        _parameterAddressCache.Clear();

        var userGuid = MetaPort.Instance.ownerId;
        var avatarGuid = MetaPort.Instance.currentAvatarGuid;
        JsonConfigOsc.ProcessUserAndAvatarGuids(ref userGuid, ref avatarGuid);

        // Send change avatar event
        HandlerOsc.SendMessage(AddressPrefixAvatarChange, avatarGuid,
            JsonConfigOsc.GetConfigFilePath(userGuid, avatarGuid));

        // Send all parameter values when loads a new avatar
        foreach (var param in manager.animator.parameters) {
            // Cache addresses
            CacheAddress(param.name);

            switch (param.type) {
                case AnimatorControllerParameterType.Float:
                    var fValue = manager.GetAnimatorParameterFloat(param.name);
                    if (!fValue.HasValue) continue;
                    Events.Avatar.OnParameterChangedFloat(manager, param.name, fValue.Value);
                    break;
                case AnimatorControllerParameterType.Int:
                    var iValue = manager.GetAnimatorParameterInt(param.name);
                    if (!iValue.HasValue) continue;
                    Events.Avatar.OnParameterChangedInt(manager, param.name, iValue.Value);
                    break;
                case AnimatorControllerParameterType.Bool:
                    var bValue = manager.GetAnimatorParameterBool(param.name);
                    if (!bValue.HasValue) continue;
                    Events.Avatar.OnParameterChangedBool(manager, param.name, bValue.Value);
                    break;
                case AnimatorControllerParameterType.Trigger:
                default:
                    break;
            }
        }
    }

    private void ParameterChangeHandler(string parameter, object valueObj) {

        // Reject core parameters
        if (CoreParameters.Contains(parameter)) {
            MelonLogger.Msg($"[Error] Attempted to change the core {parameter}. These parameters are set by the " +
                            $"game every frame, attempting to set them is pointless. If you want to change those use " +
                            $"the /input/ address instead, it allows to send inputs that actually change the core " +
                            $"parameters, like GestureRight, GestureLeft, Emote, Toggle, etc...");
            return;
        }

        // Reject non-config parameters
        if (!_bypassJsonConfig && !_parameterAddressCache.ContainsKey(parameter)) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg($"[Config] Ignoring the {parameter} change because it's not present in the json " +
                                $"config file, and you set on the configure file to not bypass checking if the " +
                                $"address is present in the json config.");
            }
            return;
        }

        // Sort their types and call the correct handler
        if (valueObj is float floatValue) Events.Avatar.OnParameterSetFloat(parameter, floatValue);
        else if (valueObj is int intValue) Events.Avatar.OnParameterSetInt(parameter, intValue);
        else if (valueObj is bool boolValue) Events.Avatar.OnParameterSetBool(parameter, boolValue);
        else if (valueObj is null) Events.Avatar.OnParameterSetTrigger(parameter);

        // Attempt to parse the string into their proper type and then call the correct handler
        else if (valueObj is string valueStr) {
            if (string.IsNullOrEmpty(valueStr)) Events.Avatar.OnParameterSetTrigger(parameter);
            else if (valueStr.ToLower().Equals("true")) Events.Avatar.OnParameterSetBool(parameter, true);
            else if (valueStr.ToLower().Equals("false")) Events.Avatar.OnParameterSetBool(parameter, false);
            else if (int.TryParse(valueStr, out var valueInt)) Events.Avatar.OnParameterSetInt(parameter, valueInt);
            else if (float.TryParse(valueStr, out var valueFloat)) Events.Avatar.OnParameterSetFloat(parameter, valueFloat);
        }

        // Well... erm... we tried
        else {
            MelonLogger.Msg($"[Error] Attempted to change {parameter} to {valueObj}, but the type {valueObj.GetType()} is not supported. " +
                            $"Contact the mod creator if you think this is a bug.");
        }
    }

    private object ConvertToConfigType(string paramName, float value) {
        if (_parameterAddressCache.ContainsKey(paramName)) {
            switch (_parameterAddressCache[paramName].type) {
                case AnimatorControllerParameterType.Int: return Math.Round(value);
                case AnimatorControllerParameterType.Bool: return value > 0.5;
            }
        }
        return value;
    }
    private object ConvertToConfigType(string paramName, int value) {
        if (_parameterAddressCache.ContainsKey(paramName) &&
            _parameterAddressCache[paramName].type == AnimatorControllerParameterType.Bool) {
            return value == 1;
        }
        return value;
    }
    private object ConvertToConfigType(string paramName, bool value) {
        if (_parameterAddressCache.ContainsKey(paramName)) {
            switch (_parameterAddressCache[paramName].type) {
                case AnimatorControllerParameterType.Float: return value ? 1f : 0f;
                case AnimatorControllerParameterType.Int: return value ? 1 : 0;
            }
        }
        return value;
    }
    private void CacheAddress(string paramName) {
        // Cache addresses for all parameters (if there is a config)
        if (JsonConfigOsc.CurrentAvatarConfig == null) return;
        // If there is no address on our cache, resolve it with the config
        if (!_parameterAddressCache.ContainsKey(paramName)) {
            var paramConfig = JsonConfigOsc.CurrentAvatarConfig.parameters.FirstOrDefault(param => param.name.Equals(paramName));

            // If we can't find an address -> ignore
            if (paramConfig?.output == null) return;

            _parameterAddressCache[paramName] = paramConfig.output;
        }
    }

    private void SendAvatarParamToConfigAddress(string paramName, object data) {

        // If there is no config OR is not in the config but we're bypassing -> revert to default behavior
        if (JsonConfigOsc.CurrentAvatarConfig == null || (_bypassJsonConfig && !_parameterAddressCache.ContainsKey(paramName))) {
            // Send to both endpoints to support vrc endpoint
            HandlerOsc.SendMessage($"{AddressPrefixAvatarParameters}", data, paramName);
            HandlerOsc.SendMessage($"{AddressPrefixAvatarParametersLegacy}{paramName}", data);
            return;
        }

        var paramEntity = _parameterAddressCache[paramName];

        // Ignore non-mapped addresses in the config
        if (paramEntity == null) return;

        // Send the parameter name as the second argument so it is compatible with both legacy and new way
        HandlerOsc.SendMessage(paramEntity.address, data, paramName);
    }
}
