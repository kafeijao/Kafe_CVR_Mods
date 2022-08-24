using System;
using System.Collections.Generic;
using ABI_RC.Core;
using ABI_RC.Core.Savior;
using HarmonyLib;
using MelonLoader;
using SharpOSC;
using System.Linq;
using OSC.Components;
using OSC.Utils;
using UnityEngine;


namespace OSC.Handlers; 

internal class HandlerOsc {
    private static readonly Dictionary<string, JsonConfigParameterEntry> ParameterAddressCache = new(); 

    internal const string AddressParametersPrefix = "/avatar/parameters/";
    private const string AddressInputPrefix = "/input/";
    private const string AddressAvatarChange = "/avatar/change";
    
    private static readonly HashSet<string> CoreParameters = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();
    
    
    public HandlerOsc() {
        
        // Start server
        var listener = new UDPListener(OSC.Instance.meOSCInPort.Value, ReceiveMessageHandler);
        
        MelonLogger.Msg($"[Server] OSC Server started listening on the port {OSC.Instance.meOSCInPort.Value}.");

        // Handle config listener port changing
        Events.Config.InPortChanged += (oldPort, newPort) => {
            if (oldPort == newPort) return;
            MelonLogger.Msg("[Server] OSC server port config has changed. Restarting server...");
            listener.Close();
            listener = new UDPListener(newPort, ReceiveMessageHandler);
            MelonLogger.Msg($"[Server] OSC Server started listening on the port {newPort}.");
        };
        
        // Setup sending messages
        
        // Execute actions on local avatar changed
        Events.Avatar.AnimatorManagerUpdated += manager => {
            
            // Clear address cache
            ParameterAddressCache.Clear();
            
            // Send change avatar event
            SendMessage(AddressAvatarChange, MetaPort.Instance.currentAvatarGuid);
            
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
        };

        // Send avatar float parameter change events
        Events.Avatar.ParameterChangedFloat += (parameter, value) => {
            SendParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };
        
        // Send avatar int parameter change events
        Events.Avatar.ParameterChangedInt += (parameter, value) => {
            SendParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };
        
        // Send avatar bool parameter change events
        Events.Avatar.ParameterChangedBool += (parameter, value) => {
            SendParamToConfigAddress(parameter, ConvertToConfigType(parameter, value));
        };
        
        // Send avatar trigger parameter change events
        Events.Avatar.ParameterChangedTrigger += parameter => {
            SendParamToConfigAddress(parameter);
        };
    }
    
    private static System.Object ConvertToConfigType(string paramName, float value) {
        if (ParameterAddressCache.ContainsKey(paramName)) {
            switch (ParameterAddressCache[paramName].type) {
                case AnimatorControllerParameterType.Int: return Math.Round(value);
                case AnimatorControllerParameterType.Bool: return value > 0.5;
            }
        }
        return value;
    }
    private static System.Object ConvertToConfigType(string paramName, int value) {
        if (ParameterAddressCache.ContainsKey(paramName) && 
            ParameterAddressCache[paramName].type == AnimatorControllerParameterType.Bool) {
            return value == 1;
        }
        return value;
    }
    
    private static System.Object ConvertToConfigType(string paramName, bool value) {
        if (ParameterAddressCache.ContainsKey(paramName)) {
            switch (ParameterAddressCache[paramName].type) {
                case AnimatorControllerParameterType.Float: return value ? 1f : 0f;
                case AnimatorControllerParameterType.Int: return value ? 1 : 0;
            }
        }
        return value;
    }
    
    private static void CacheAddress(string paramName) {
        // Cache addresses for all parameters (if there is a config)
        if (JsonConfigOsc.CurrentAvatarConfig == null) return;
        // If there is no address on our cache, resolve it with the config
        if (!ParameterAddressCache.ContainsKey(paramName)) {
            var paramConfig = JsonConfigOsc.CurrentAvatarConfig.parameters.FirstOrDefault(param => param.name.Equals(paramName));
            // If we can't find an address we add a null value, so we can ignore later
            ParameterAddressCache[paramName] = paramConfig?.output;
        }
    }

    private static void SendParamToConfigAddress(string paramName, params object[] data) {
        
        // If there is no config, revert to default behavior
        if (JsonConfigOsc.CurrentAvatarConfig == null) {
            SendMessage($"{AddressParametersPrefix}{paramName}", data);
            return;
        }

        // If there is no address on our cache, resolve it with the config (should be already cached but hey)
        CacheAddress(paramName);

        var paramEntity = ParameterAddressCache[paramName];
        
        // Ignore non-mapped addresses in the config
        if (paramEntity == null) return;

        SendMessage(paramEntity.address, data);
    }
    
    private static void SendMessage(string address, params object[] data) {
        var message = new OscMessage(address, data);
        var sender = new UDPSender(OSC.Instance.meOSCOutIp.Value, OSC.Instance.meOSCOutPort.Value);
        sender.Send(message);
    }

    private static void ReceiveMessageHandler(OscPacket packet) {

        // Ignore packets that had errors
        if (packet == null) return;
        var oscMessage = (OscMessage) packet;

        var address = oscMessage.Address;
        var addressLower = oscMessage.Address.ToLower();
        
        // Get only the first value and assume no values to be null
        var valueObj = oscMessage.Arguments.Count > 0 ? oscMessage.Arguments[0] : null;
        
        // Handle the change parameter requests
        if (addressLower.StartsWith(AddressParametersPrefix)) {
            ParameterChangeHandler(address, valueObj);
        }
        
        // Handle the input requests
        else if (addressLower.StartsWith(AddressInputPrefix)) {
            InputHandler(address, valueObj);
        }
        
        // Handle the change avtar requests
        else if (addressLower.Equals(AddressAvatarChange) && valueObj is string valueStr) {
            Events.Avatar.OnAvatarSet(valueStr);
        }
    }

    private static void ParameterChangeHandler(string address, object valueObj) {
        var parameter = address.Substring(AddressParametersPrefix.Length);
        
        // Reject core parameters
        if (CoreParameters.Contains(parameter)) {
            MelonLogger.Msg($"[Error] Attempted to change the core {parameter}. These parameters are set by the " +
                            $"game every frame, attempting to set them is pointless. If you want to change those use " +
                            $"the /input/ address instead, it allows to send inputs that actually change the core " +
                            $"parameters, like GestureRight, GestureLeft, Emote, Toggle, etc...");  
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
            else if (int.TryParse(valueStr, out int valueInt)) Events.Avatar.OnParameterSetInt(parameter, valueInt);
            else if (float.TryParse(valueStr, out float valueFloat)) Events.Avatar.OnParameterSetFloat(parameter, valueFloat);
        }
            
        // Well... erm... we tried
        else {
            MelonLogger.Msg($"[Error] Attempted to change {parameter} to {valueObj}, but the type {valueObj.GetType()} is not supported. " +
                            $"Contact the mod creator if you think this is a bug.");   
        }
    }

    private static void InputHandler(string address, object valueObj) {
        var inputName = address.Substring(AddressInputPrefix.Length);

        // Reject blacklisted inputs (ignore case)
        if (OSC.Instance.meOSCInputBlacklist.Value.Contains(inputName, StringComparer.OrdinalIgnoreCase)) {
            MelonLogger.Msg($"[Info] The OSC config has {inputName} blacklisted. Edit the config to allow.");
            return;
        }
        
        // Axes
        if (Enum.TryParse<AxisNames>(inputName, true, out var axisName)) {
            void UpdateAxisValue(AxisNames axis, float value) {
                if (value is > 1f or < -1f) {
                    MelonLogger.Msg($"[Error] The input name {inputName} is an Axis, so the allowed values " +
                                    $"are floats between -1f and 1f (inclusive). Value provided: {valueObj}");
                    return;
                }
                InputModuleOSC.InputAxes[axis] = value;
            }
            if (valueObj is float floatValue) UpdateAxisValue(axisName, floatValue);
            else if (valueObj is int intValue) UpdateAxisValue(axisName, intValue);
            else if (valueObj is string valueStr && float.TryParse(valueStr, out var valueFloat)) UpdateAxisValue(axisName, valueFloat);
            else UpdateAxisValue(axisName, float.NaN);
        }
        
        // Buttons
        else if (Enum.TryParse<ButtonNames>(inputName, true, out var buttonName)) {
            void UpdateButtonValue(ButtonNames button, bool? value) {
                if (!value.HasValue) {
                    MelonLogger.Msg($"[Error] The input name {inputName} is a Button, so the allowed values " +
                                    $"are booleans, that can be represented with bool values, 0 and 1, or " +
                                    $"true/false as a string. Value provided: {valueObj}");
                    return;
                }
                InputModuleOSC.InputButtons[button] = value.Value;
            }
            if (valueObj is bool boolValue) UpdateButtonValue(buttonName, boolValue);
            else if (valueObj is int intValue) UpdateButtonValue(buttonName, intValue == 1 ? true : intValue == 0 ? false : null);
            else if (valueObj is string valueStr) {
                if (int.TryParse(valueStr, out var valueInt)) UpdateButtonValue(buttonName, valueInt == 1 ? true : valueInt == 0 ? false : null);
                else if (bool.TryParse(valueStr, out var valueBool)) UpdateButtonValue(buttonName, valueBool);
                else UpdateButtonValue(buttonName, null);
            }
            else UpdateButtonValue(buttonName, null);
        }
        
        // Values
        else if (Enum.TryParse<ValueNames>(inputName, true, out var valueName)) {
            void UpdateValueValue(ValueNames valName, float value) {
                if (float.IsNaN(value)) {
                    MelonLogger.Msg($"[Error] The input name {inputName} is a Value, so the allowed values " +
                                    $"are any numbers floats/ints. Value provided: {valueObj}");
                    return;
                }
                InputModuleOSC.InputValues[valName] = value;
            }
            if (valueObj is float floatValue) UpdateValueValue(valueName, floatValue);
            else if (valueObj is int intValue) UpdateValueValue(valueName, intValue);
            else if (valueObj is string valueStr && float.TryParse(valueStr, out var valueFloat)) UpdateValueValue(valueName, valueFloat);
            else UpdateValueValue(valueName, float.NaN);
        }
        
        // Yes
        else {
            MelonLogger.Msg($"[Error] The input name {inputName} is not supported! \n" +
                            $"Supported Axis Names: {Enum.GetNames(typeof(AxisNames))}, \n" +
                            $"Supported Button Names: {Enum.GetNames(typeof(ButtonNames))}, \n" +
                            $"Supported Value Names: {Enum.GetNames(typeof(ValueNames))}.");
        }
    }

}