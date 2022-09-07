using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using OSC.Utils;
using UnityEngine;

namespace OSC.Handlers.OscModules;

public class Avatar : OscHandler {

    internal const string AddressPrefixAvatar = "/avatar/";
    internal const string AddressPrefixAvatarParameters = $"{AddressPrefixAvatar}parameters/";
    private const string AddressPrefixAvatarChange = $"{AddressPrefixAvatar}change";

    private static readonly HashSet<string> CoreParameters = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();

    private bool _enabled;
    private bool _bypassJsonConfig;
    private readonly Dictionary<string, JsonConfigParameterEntry> _parameterAddressCache = new();

    private readonly Action<CVRAnimatorManager> _animatorManagerUpdated;
    private readonly Action<string, float> _parameterChangedFloat;
    private readonly Action<string, int> _parameterChangedInt;
    private readonly Action<string, bool> _parameterChangedBool;
    private readonly Action<string> _parameterChangedTrigger;

    public Avatar() {

        // Execute actions on local avatar changed
        _animatorManagerUpdated = InitializeNewAvatar;;

        // Send avatar float parameter change events
        _parameterChangedFloat = (parameter, value) => SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));

        // Send avatar int parameter change events
        _parameterChangedInt = (parameter, value) => SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));

        // Send avatar bool parameter change events
        _parameterChangedBool = (parameter, value) => SendAvatarParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));

        // Send avatar trigger parameter change events
        _parameterChangedTrigger = parameter => SendAvatarParamToConfigAddress(parameter);

        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCAvatarModule.Value) Enable();
        OSC.Instance.meOSCAvatarModule.OnValueChanged += (oldValue, newValue) => {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        };

        // Set whether should bypass json config or not, and handle the config change
        _bypassJsonConfig = OSC.Instance.meOSCAvatarModuleBypassJsonConfig.Value;
        OSC.Instance.meOSCAvatarModuleBypassJsonConfig.OnValueChanged += (oldValue, newValue) => {
            if (newValue == oldValue) return;
            if (PlayerSetup.Instance.animatorManager != null) {
                MelonLogger.Msg($"Changed the bypass json config Configuration to {newValue}, the avatar parameter are going to be reloaded...");
                InitializeNewAvatar(PlayerSetup.Instance.animatorManager);
            }
            _bypassJsonConfig = newValue;
        };
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

    internal sealed override void ReceiveMessageHandler(string address, List<object> args) {
        if (!_enabled) return;

        var addressLower = address.ToLower();

        // Get only the first value and assume no values to be null
        var valueObj = args.Count > 0 ? args[0] : null;

        // Handle the change parameter requests
        if (addressLower.StartsWith(AddressPrefixAvatarParameters)) {
            ParameterChangeHandler(address, valueObj);
        }

        // Handle the change avtar requests
        else if (addressLower.Equals(AddressPrefixAvatarChange) && valueObj is string valueStr) {
            Events.Avatar.OnAvatarSet(valueStr);
        }
    }

    private void InitializeNewAvatar(CVRAnimatorManager manager) {

        // Clear address cache
        _parameterAddressCache.Clear();

        // Send change avatar event
        HandlerOsc.SendMessage(AddressPrefixAvatarChange, MetaPort.Instance.currentAvatarGuid);

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

    private void ParameterChangeHandler(string address, object valueObj) {
        var parameter = address.Substring(AddressPrefixAvatarParameters.Length);

        // Reject core parameters
        if (CoreParameters.Contains(parameter)) {
            MelonLogger.Msg($"[Error] Attempted to change the core {parameter}. These parameters are set by the " +
                            $"game every frame, attempting to set them is pointless. If you want to change those use " +
                            $"the /input/ address instead, it allows to send inputs that actually change the core " +
                            $"parameters, like GestureRight, GestureLeft, Emote, Toggle, etc...");
            return;
        }

        // Reject non-config parameters
        if (!_bypassJsonConfig && !_parameterAddressCache.ContainsKey(parameter)) return;

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

    private void SendAvatarParamToConfigAddress(string paramName, params object[] data) {

        // If there is no config OR is not in the config but we're bypassing -> revert to default behavior
        if (JsonConfigOsc.CurrentAvatarConfig == null || (_bypassJsonConfig && !_parameterAddressCache.ContainsKey(paramName))) {
            HandlerOsc.SendMessage($"{AddressPrefixAvatarChange}{paramName}", data);
            return;
        }

        var paramEntity = _parameterAddressCache[paramName];

        // Ignore non-mapped addresses in the config
        if (paramEntity == null) return;

        HandlerOsc.SendMessage(paramEntity.address, data);
    }
}
