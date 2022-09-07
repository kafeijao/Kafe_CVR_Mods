using ABI_RC.Core.Util;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace OSC.Handlers.OscModules;

enum SpawnableOperation {
    parameters,
    location,
}

public class Spawnable : OscHandler {

    internal const string AddressPrefixSpawnable = "/prop/";

    private bool _enabled;

    private readonly Action<CVRSyncHelper.PropData> _spawnableCreated;
    private readonly Action<CVRSpawnable, CVRSpawnableValue> _spawnableParameterChanged;

    public Spawnable() {

        // Execute actions on spawnable created
        _spawnableCreated = propData => {
            // Send all parameter values when loads a new spawnable is created
            foreach (var syncedParams in propData.Spawnable.syncValues) {
                Events.Spawnable.OnSpawnableParameterChanged(propData.Spawnable, syncedParams);
            }
        };

        // Send spawnable parameter change events
        _spawnableParameterChanged = (spawnable, spawnableValue) => {
            // Spawnable instance id example: p+047576d5-e028-483a-9870-89e62f0ed3a4~FF00984F7C5A
            // p+<spawnable_id>~<instance_id>
            var instanceId = spawnable.instanceId.Substring(39);
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{spawnable.guid}/{instanceId}/{nameof(SpawnableOperation.parameters)}/{spawnableValue.name}", spawnableValue.currentValue);
        };

        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCSpawnableModule.Value) Enable();
        OSC.Instance.meOSCSpawnableModule.OnValueChanged += (oldValue, newValue) => {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        };
    }

    internal sealed override void Enable() {
        Events.Spawnable.SpawnableCreated += _spawnableCreated;
        Events.Spawnable.SpawnableParameterChanged += _spawnableParameterChanged;
        _enabled = true;
    }

    internal sealed override void Disable() {
        Events.Spawnable.SpawnableCreated -= _spawnableCreated;
        Events.Spawnable.SpawnableParameterChanged -= _spawnableParameterChanged;
        _enabled = false;
    }

    internal sealed override void ReceiveMessageHandler(string address, List<object> args) {
        if (!_enabled) return;

        var addressParts = address.Split('/');

        // Validations
        if (addressParts.Length < 5) {
            MelonLogger.Msg($"[Error] Attempted to interact with a prop but the address is invalid. " +
                            $"Address attempted: \"{address}\" " +
                            $"The correct format should start with: \"/prop/<prop_guid>/<prop_instance_id>/<op>\" " +
                            $"Allowed ops: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
            return;
        }
        if (!Guid.TryParse(addressParts[2], out _)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the prop guid is not a valid GUID. " +
                            $"Provided prop guid: {addressParts[2]}");
            return;
        }
        if (addressParts[3].Length != 12 || !long.TryParse(addressParts[3], System.Globalization.NumberStyles.HexNumber, null, out _)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the prop instance id is not a valid prop instance id. " +
                            $"It needs to be an hexadecimal value with a length of 12 characters. " +
                            $"Provided prop instance id: {addressParts[3]}");
            return;
        }
        var spawnableGuid = addressParts[2];
        var spawnableInstanceId = addressParts[3];


        Enum.TryParse<SpawnableOperation>(addressParts[4], true, out var spawnableOperation);

        switch (spawnableOperation) {
            case SpawnableOperation.parameters:
                ReceivedParameterHandler(address, addressParts, spawnableGuid, spawnableInstanceId, args);
                return;
            case SpawnableOperation.location:
                ReceivedLocationHandler(address, addressParts, spawnableGuid, spawnableInstanceId, args);
                break;
            default:
                MelonLogger.Msg($"[Error] Attempted to interact with a prop but the operation type is invalid. " +
                                $"Provided operation: \"{addressParts[4]}\" " +
                                $"Allowed ops: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
                return;
        }
    }

    private static void ReceivedParameterHandler(
        string address,
        IReadOnlyList<string> addressParts,
        string spawnableGuid,
        string spawnableInstanceId,
        List<object> args
        ) {

        if (addressParts.Count != 6) {
            MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the address is invalid. " +
                            $"Address attempted: \"{address}\" " +
                            $"The correct format should be: /{AddressPrefixSpawnable}/<prop_guid>/<prop_instance_id>/{nameof(SpawnableOperation.parameters)}/<parameter_name>");
            return;
        }

        var spawnableParameterName = addressParts[5];
        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        // Get only the first value and assume no values to be null
        var valueObj = args.Count > 0 ? args[0] : null;

        // Sort their types and call the correct handler
        if (valueObj is float floatValue)
            Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, floatValue);
        else if (valueObj is int intValue)
            Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, intValue);
        else if (valueObj is bool boolValue)
            Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, boolValue ? 1f : 0f);

        // Attempt to parse the string into their proper type and then call the correct handler
        else if (valueObj is string valueStr) {
            if (valueStr.ToLower().Equals("true"))
                Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, 1f);
            else if (valueStr.ToLower().Equals("false"))
                Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, 0f);
            else if (int.TryParse(valueStr, out int valueInt))
                Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, valueInt);
            else if (float.TryParse(valueStr, out float valueFloat))
                Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, valueFloat);
            else {
                MelonLogger.Msg(
                    $"[Error] Attempted to change the prop parameter {spawnableParameterName} to {valueObj}, but the string value {valueObj} is not supported. " +
                    $"Contact the mod creator if you think this is a bug.");
            }
        }

        // Well... erm... we tried
        else {
            MelonLogger.Msg(
                $"[Error] Attempted to change the prop parameter {spawnableParameterName} to {valueObj}, but the type {valueObj.GetType()} is not supported. " +
                $"Contact the mod creator if you think this is a bug.");
        }
    }

    private static void ReceivedLocationHandler(
        string address,
        IReadOnlyList<string> addressParts,
        string spawnableGuid,
        string spawnableInstanceId,
        List<object> args
        ) {

        if (addressParts.Count != 5) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location but the address is invalid. " +
                            $"Address attempted: \"{address}\" " +
                            $"The correct format should be: /{AddressPrefixSpawnable}/<prop_guid>/<prop_instance_id>/{nameof(SpawnableOperation.location)}");
            return;
        }

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var parsedFloats = ParseLocationValues(args);
        if (parsedFloats.Count != 6) return;

        var position = new Vector3(parsedFloats[0], parsedFloats[1], parsedFloats[2]);
        var rotation = new Vector3(parsedFloats[3], parsedFloats[4], parsedFloats[5]);

        Events.Spawnable.OnSpawnableLocationSet(spawnableFullId, position, rotation);
    }

    private static List<float> ParseLocationValues(List<object> args) {
        var result = new List<float>();

        if (args.Count != 6) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location but received an invalid number of values. " +
                            $"Amount of values received: \"{args.Count}\" " +
                            $"The required values are 6 floats corresponding to the position x,y,z (first three), and " +
                            $"rotation x,y,z (last three) (in euler angles).");
            return result;
        }

        // Iterate the arguments and try to parse them all as floats
        foreach (var valueObj in args) {
            if (valueObj is float floatValue) result.Add(floatValue);
            else if (valueObj is int intValue) result.Add(intValue);
            else if (valueObj is string valueStr) {
                if (float.TryParse(valueStr, out var valueFloat)) result.Add(valueFloat);
                else break;
            }
            // Well... erm... we tried
            else break;
        }

        if (result.Count != 6) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location but received invalid values. " +
                            $"Values received: \"{string.Join(", ", args)}\" " +
                            $"The required values are 6 floats corresponding to the position x,y,z (first three), and " +
                            $"rotation x,y,z (last three) (in euler angles).");
        }
        return result;
    }
}
