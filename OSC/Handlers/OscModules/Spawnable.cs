using ABI_RC.Core.Util;
using ABI.CCK.Components;
using MelonLoader;
using UnityEngine;

namespace OSC.Handlers.OscModules;

enum SpawnableOperation {
    create,
    delete,
    available,
    parameters,
    location,
}

public class Spawnable : OscHandler {

    internal const string AddressPrefixSpawnable = "/prop/";

    private bool _enabled;

    private readonly Action<CVRSyncHelper.PropData> _spawnableCreated;
    private readonly Action<CVRSyncHelper.PropData> _spawnableDeleted;
    private readonly Action<CVRSpawnable, bool> _spawnableAvailabilityChanged;
    private readonly Action<CVRSpawnable, CVRSpawnableValue> _spawnableParameterChanged;
    private readonly Action<CVRSpawnable> _spawnableLocationChanged;

    public Spawnable() {

        // Execute actions on spawnable created
        _spawnableCreated = propData => {
            var spawnable = propData.Spawnable;

            // Send the create event
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.create)}", spawnable.guid, GetInstanceId(spawnable));

            // Send all parameter values when loads a new spawnable is created
            foreach (var syncedParams in spawnable.syncValues) {
                Events.Spawnable.OnSpawnableParameterChanged(spawnable, syncedParams);
            }
        };

        // Execute actions on spawnable deletion
        _spawnableDeleted = propData => {
            var spawnable = propData.Spawnable;

            // Send the delete event
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.delete)}", spawnable.guid, GetInstanceId(spawnable));
        };

        // Send spawnable availability change events
        _spawnableAvailabilityChanged = (spawnable, available) => {
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.available)}", spawnable.guid, GetInstanceId(spawnable), available);
        };

        // Send spawnable parameter change events
        _spawnableParameterChanged = (spawnable, spawnableValue) => {
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.parameters)}", spawnable.guid, GetInstanceId(spawnable), spawnableValue.name, spawnableValue.currentValue);
        };

        // Send spawnable location change events
        _spawnableLocationChanged = spawnable => {
            var transform = spawnable.transform;
            var pos = transform.position;
            var rot = transform.rotation.eulerAngles;
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.location)}",
                spawnable.guid, GetInstanceId(spawnable),
                pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);
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
        Events.Spawnable.SpawnableDeleted += _spawnableDeleted;
        Events.Spawnable.SpawnableAvailable += _spawnableAvailabilityChanged;
        Events.Spawnable.SpawnableParameterChanged += _spawnableParameterChanged;
        Events.Spawnable.SpawnableLocationTrackingTicked += _spawnableLocationChanged;
        _enabled = true;
    }

    internal sealed override void Disable() {
        Events.Spawnable.SpawnableCreated -= _spawnableCreated;
        Events.Spawnable.SpawnableDeleted -= _spawnableDeleted;
        Events.Spawnable.SpawnableAvailable -= _spawnableAvailabilityChanged;
        Events.Spawnable.SpawnableParameterChanged -= _spawnableParameterChanged;
        Events.Spawnable.SpawnableLocationTrackingTicked -= _spawnableLocationChanged;
        _enabled = false;
    }

    internal sealed override void ReceiveMessageHandler(string address, List<object> args) {
        if (!_enabled) return;

        var addressParts = address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to interact with a prop but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{AddressPrefixSpawnable}<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
            return;
        }

        Enum.TryParse<SpawnableOperation>(addressParts[2], true, out var spawnableOperation);

        switch (spawnableOperation) {
            case SpawnableOperation.parameters:
                ReceivedParameterHandler(args);
                return;
            case SpawnableOperation.location:
                ReceivedLocationHandler(args);
                return;
            case SpawnableOperation.delete:
                ReceivedDeleteHandler(args);
                return;
            case SpawnableOperation.create:
                ReceivedCreateHandler(args);
                return;
            case SpawnableOperation.available:
                MelonLogger.Msg($"[Error] Attempted set the availability for a prop, this is not allowed.");
                return;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to interact with a prop but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{AddressPrefixSpawnable}<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}"
                    );
                return;
        }
    }

    private static void ReceivedCreateHandler(List<object> args) {

        if (args.Count is not (1 or 4)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid number of arguments. " +
                            $"Expected either 1 or 4 arguments, for the guid and optionally position coordinates.");
            return;
        }

        var possibleGuid = args[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        if (TryParseVector3(args[1], args[2], args[3], out var floats)) {
            Events.Spawnable.OnSpawnableCreate(spawnableGuid, floats.Item1, floats.Item2, floats.Item3);
        }
        else {
            Events.Spawnable.OnSpawnableCreate(spawnableGuid);
        }
    }

    private static void ReceivedDeleteHandler(List<object> args) {

        if (args.Count != 2) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid number of arguments. " +
                            $"Expected 2 arguments, for the prop GUID and Instance ID.");
            return;
        }

        var possibleGuid = args[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = args[1];
        if (!TryParseSpawnableGuid(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        Events.Spawnable.OnSpawnableDelete($"p+{spawnableGuid}~{spawnableInstanceId}");
    }

    private static void ReceivedParameterHandler(List<object> args) {

        if (args.Count != 4) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid number of arguments. " +
                            $"Expected 4 arguments, for the prop GUID, and Instance ID, sync param name, and param value");
            return;
        }

        var possibleGuid = args[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = args[1];
        if (!TryParseSpawnableGuid(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        var possibleParamName = args[2];
        if (possibleParamName is not string spawnableParameterName) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid name. " +
                            $"Attempted: \"{possibleParamName}\" Type: {possibleParamName?.GetType()}" +
                            $"The synced parameter name has to be a string.");
            return;
        }

        var possibleFloat = args[3];
        if (!Utils.Parameters.TryHardToParseFloat(possibleFloat, out var parsedFloat)) {
            MelonLogger.Msg(
                $"[Error] Attempted to change a prop synced parameter {spawnableParameterName} to {possibleFloat}, " +
                $"but we failed to parse whatever you sent to a float (we tried really hard I promise). " +
                $"Attempted value type: {possibleFloat?.GetType()} " +
                $"Contact the mod creator if you think this is a bug.");
            return;
        }

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";
        Events.Spawnable.OnSpawnableParameterSet(spawnableFullId, spawnableParameterName, parsedFloat);
    }

    private static void ReceivedLocationHandler(List<object> args) {

        if (args.Count != 8) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid number of arguments. " +
                            $"Expected 8 arguments, for the prop GUID, and Instance ID, 3 floats for position, and 3" +
                            $"floats for the rotation (euler angles).");
            return;
        }

        var possibleGuid = args[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = args[1];
        if (!TryParseSpawnableGuid(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        // Validate position and rotation floats
        if (!TryParseVector3(args[2], args[3], args[4], out var posFloats)) return;
        if (!TryParseVector3(args[5], args[6], args[7], out var rotFloats)) return;

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var position = new Vector3(posFloats.Item1, posFloats.Item2, posFloats.Item3);
        var rotation = new Vector3(rotFloats.Item1, rotFloats.Item2, rotFloats.Item3);

        Events.Spawnable.OnSpawnableLocationSet(spawnableFullId, position, rotation);
    }

    private static string GetInstanceId(CVRSpawnable spawnable) {
        // Spawnable instance id example: p+047576d5-e028-483a-9870-89e62f0ed3a4~FF00984F7C5A
        // p+<spawnable_id>~<instance_id>
        return spawnable.instanceId.Substring(39);
    }

    private static bool TryParseVector3(object x, object y, object z, out (float, float, float) result) {

        bool TryParseFloat(object valueObj, out float parsedFloat) {
            switch (valueObj) {
                case float floatValue:
                    parsedFloat = floatValue;
                    return true;
                case int intValue:
                    parsedFloat = intValue;
                    return true;
                case string valueStr when float.TryParse(valueStr, out var valueFloat):
                    parsedFloat = valueFloat;
                    return true;
                default:
                    parsedFloat = float.NaN;
                    MelonLogger.Msg($"[Error] Attempted interact with a prop using an invalid position or rotation. " +
                                    $"Value attempted to parse to a float: {valueObj} [{valueObj?.GetType()}]");
                    return false;
            }
        }

        result = (0,0,0);
        if (!TryParseFloat(x, out var parsedX)) return false;
        if (!TryParseFloat(y, out var parsedY)) return false;
        if (!TryParseFloat(z, out var parsedZ)) return false;
        result = (parsedX, parsedY, parsedZ);

        return true;
    }

    private static bool TryParseSpawnableGuid(object possibleGuid, out string guid) {
        if (possibleGuid is string possibleGuidStr && Guid.TryParse(possibleGuidStr, out var guidValue)) {
            guid = guidValue.ToString("D");
            return true;
        }
        MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the prop guid is not a valid GUID. " +
                        $"Provided prop guid: {possibleGuid}");
        guid = null;
        return false;
    }

    private static bool TryParseSpawnableInstanceId(string possibleInstanceId, out string instanceId) {
        if (possibleInstanceId.Length != 12 ||
            !long.TryParse(possibleInstanceId, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the prop instance id is not a valid prop instance id. " +
                            $"It needs to be an hexadecimal value with a length of 12 characters. " +
                            $"Provided prop instance id: {possibleInstanceId}");
            instanceId = null;
            return false;
        }
        instanceId = hexValue.ToString("X");
        return true;
    }
}
