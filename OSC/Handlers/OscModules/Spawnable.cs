using ABI_RC.Core.Util;
using ABI.CCK.Components;
using MelonLoader;
using Rug.Osc.Core;
using UnityEngine;

namespace Kafe.OSC.Handlers.OscModules;

enum SpawnableOperation {
    create,
    delete,
    available,
    parameter,
    location,
    location_sub,
}

public class Spawnable : OscHandler {

    internal const string AddressPrefixSpawnable = "/prop/";

    private bool _enabled;
    private bool _debugConfigWarnings;

    private readonly Action<CVRSyncHelper.PropData> _spawnableCreated;
    private readonly Action<CVRSpawnable> _spawnableDeleted;
    private readonly Action<CVRSpawnable, bool> _spawnableAvailabilityChanged;
    private readonly Action<CVRSpawnable, CVRSpawnableValue> _spawnableParameterChanged;
    private readonly Action<CVRSpawnable> _spawnableLocationChanged;

    public Spawnable() {

        // Execute actions on spawnable created
        _spawnableCreated = propData => {
            var spawnable = propData.Spawnable;

            // Send the create event
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.create)}",
                spawnable.guid,
                GetInstanceId(spawnable),
                spawnable.subSyncs.Count);

            // Send all parameter values when loads a new spawnable is created
            foreach (var syncedParams in spawnable.syncValues) {
                Events.Spawnable.OnSpawnableParameterChanged(spawnable, syncedParams);
            }
        };

        // Execute actions on spawnable deletion
        _spawnableDeleted = spawnable => {
            // Send the delete event
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.delete)}", GetGuid(spawnable), GetInstanceId(spawnable));
        };

        // Send spawnable availability change events
        _spawnableAvailabilityChanged = (spawnable, available) => {
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.available)}", spawnable.guid, GetInstanceId(spawnable), available);
        };

        // Send spawnable parameter change events
        _spawnableParameterChanged = (spawnable, spawnableValue) => {
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.parameter)}", spawnable.guid, GetInstanceId(spawnable), spawnableValue.name, spawnableValue.currentValue);
        };

        // Send spawnable location change events
        _spawnableLocationChanged = spawnable => {

            // Handle main spawnable transform
            var transform = spawnable.transform;
            var pos = transform.position;
            var rot = transform.rotation.eulerAngles;
            HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.location)}",
                spawnable.guid, GetInstanceId(spawnable),
                pos.x, pos.y, pos.z, rot.x, rot.y, rot.z);

            // Handle sub_sync transforms
            for (var index = 0; index < spawnable.subSyncs.Count; index++) {
                var spawnableSubSync = spawnable.subSyncs[index];
                var sTransform = spawnableSubSync.transform;
                // If there are transforms defined but the prop was Enable Sync Values is turned off...
                if (sTransform == null) continue;
                var sPos = sTransform.position;
                var sRot = sTransform.rotation.eulerAngles;
                HandlerOsc.SendMessage($"{AddressPrefixSpawnable}{nameof(SpawnableOperation.location_sub)}",
                    spawnable.guid, GetInstanceId(spawnable),
                    index,
                    sPos.x, sPos.y, sPos.z, sRot.x, sRot.y, sRot.z);
                    //sPos.x + pos.x, sPos.y + pos.y, sPos.z + pos.z, sRot.x + rot.x, sRot.y + rot.y, sRot.z + rot.z);
            }
        };


        // Enable according to the config and setup the config listeners
        if (OSC.Instance.meOSCSpawnableModule.Value) Enable();
        OSC.Instance.meOSCSpawnableModule.OnEntryValueChanged.Subscribe((oldValue, newValue) => {
            if (newValue && !oldValue) Enable();
            else if (!newValue && oldValue) Disable();
        });

        // Handle the warning when blocked osc command by config
        _debugConfigWarnings = OSC.Instance.meOSCDebugConfigWarnings.Value;
        OSC.Instance.meOSCDebugConfigWarnings.OnEntryValueChanged.Subscribe((_, enabled) => _debugConfigWarnings = enabled);
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

    internal sealed override void ReceiveMessageHandler(OscMessage oscMsg) {
        if (!_enabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg($"[Config] Sent an osc msg to {AddressPrefixSpawnable}, but this module is disabled " +
                                $"in the configuration file, so this will be ignored.");
            }
            return;
        }

        var addressParts =oscMsg.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to interact with a prop but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{AddressPrefixSpawnable}<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
            return;
        }

        Enum.TryParse<SpawnableOperation>(addressParts[2], true, out var spawnableOperation);

        switch (spawnableOperation) {
            case SpawnableOperation.parameter:
                ReceivedParameterHandler(oscMsg);
                return;
            case SpawnableOperation.location:
                ReceivedLocationHandler(oscMsg);
                return;
            case SpawnableOperation.location_sub:
                ReceivedLocationSubHandler(oscMsg);
                return;
            case SpawnableOperation.delete:
                ReceivedDeleteHandler(oscMsg);
                return;
            case SpawnableOperation.create:
                ReceivedCreateHandler(oscMsg);
                return;
            case SpawnableOperation.available:
                MelonLogger.Msg($"[Error] Attempted set the availability for a prop, this is not allowed.");
                return;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to interact with a prop but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{AddressPrefixSpawnable}<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}"
                    );
                return;
        }
    }

    private static void ReceivedCreateHandler(OscMessage oscMessage) {

        if (oscMessage.Count is not (1 or 4)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid number of arguments. " +
                            $"Expected either 1 or 4 arguments, for the guid and optionally position coordinates.");
            return;
        }

        var possibleGuid = oscMessage[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        if (oscMessage.Count == 4 && TryParseVector3(oscMessage[1], oscMessage[2], oscMessage[3], out var floats)) {
            Events.Spawnable.OnSpawnableCreate(spawnableGuid, floats.Item1, floats.Item2, floats.Item3);
        }
        else {
            Events.Spawnable.OnSpawnableCreate(spawnableGuid);
        }
    }

    private static void ReceivedDeleteHandler(OscMessage oscMessage) {

        if (oscMessage.Count != 2) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid number of arguments. " +
                            $"Expected 2 arguments, for the prop GUID and Instance ID.");
            return;
        }

        var possibleGuid = oscMessage[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = oscMessage[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        Events.Spawnable.OnSpawnableDelete($"p+{spawnableGuid}~{spawnableInstanceId}");
    }

    private static void ReceivedParameterHandler(OscMessage oscMessage) {

        if (oscMessage.Count != 4) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid number of arguments. " +
                            $"Expected 4 arguments, for the prop GUID, and Instance ID, sync param name, and param value");
            return;
        }

        var possibleGuid = oscMessage[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = oscMessage[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        var possibleParamName = oscMessage[2];
        if (possibleParamName is not string spawnableParameterName) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid name. " +
                            $"Attempted: \"{possibleParamName}\" Type: {possibleParamName?.GetType()}" +
                            $"The synced parameter name has to be a string.");
            return;
        }

        var possibleFloat = oscMessage[3];
        if (!Utils.Converters.TryHardToParseFloat(possibleFloat, out var parsedFloat)) {
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

    private static void ReceivedLocationHandler(OscMessage oscMessage) {

        if (oscMessage.Count != 8) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid number of arguments. " +
                            $"Expected 8 arguments, for the prop GUID, and Instance ID, 3 floats for position, and 3" +
                            $"floats for the rotation (euler angles).");
            return;
        }

        var possibleGuid = oscMessage[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = oscMessage[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        // Validate position and rotation floats
        if (!TryParseVector3(oscMessage[2], oscMessage[3], oscMessage[4], out var posFloats)) return;
        if (!TryParseVector3(oscMessage[5], oscMessage[6], oscMessage[7], out var rotFloats)) return;

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var position = new Vector3(posFloats.Item1, posFloats.Item2, posFloats.Item3);
        var rotation = new Vector3(rotFloats.Item1, rotFloats.Item2, rotFloats.Item3);

        Events.Spawnable.OnSpawnableLocationSet(spawnableFullId, position, rotation);
    }

    private static void ReceivedLocationSubHandler(OscMessage oscMessage) {

        if (oscMessage.Count != 9) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location with an invalid number of arguments. " +
                            $"Expected 8 arguments, for the prop GUID, and Instance ID, 3 floats for position, and 3" +
                            $"floats for the rotation (euler angles).");
            return;
        }

        var possibleGuid = oscMessage[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop sub-sync location, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return;
        }

        var possibleInstanceId = oscMessage[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop sub-sync location, with an invalid Instance ID. " +
                            $"Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return;
        }

        // Validate index
        var possibleIndex = oscMessage[2];
        if (!Utils.Converters.TryToParseInt(possibleIndex, out var subIndex) || subIndex < 0) {
            MelonLogger.Msg($"[Error] Attempted set the location of prop sub-sync with an invalid/negative index. " +
                            $"Value attempted to parse: {possibleIndex} [{possibleIndex?.GetType()}]");
            return;
        }

        // Validate position and rotation floats
        if (!TryParseVector3(oscMessage[3], oscMessage[4], oscMessage[5], out var posFloats)) return;
        if (!TryParseVector3(oscMessage[6], oscMessage[7], oscMessage[8], out var rotFloats)) return;

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var position = new Vector3(posFloats.Item1, posFloats.Item2, posFloats.Item3);
        var rotation = new Vector3(rotFloats.Item1, rotFloats.Item2, rotFloats.Item3);

        Events.Spawnable.OnSpawnableLocationSet(spawnableFullId, position, rotation, subIndex);
    }

    private static string GetGuid(CVRSpawnable spawnable) {
        // Spawnable instance id example: p+047576d5-e028-483a-9870-89e62f0ed3a4~FF00984F7C5A
        // p+<spawnable_id>~<instance_id>
        return spawnable.instanceId.Substring(3, 36);
    }

    private static string GetInstanceId(CVRSpawnable spawnable) {
        // Spawnable instance id example: p+047576d5-e028-483a-9870-89e62f0ed3a4~FF00984F7C5A
        // p+<spawnable_id>~<instance_id>
        return spawnable.instanceId.Substring(39);
    }

    private static bool TryParseVector3(object x, object y, object z, out (float, float, float) result) {

        if (!Utils.Converters.TryParseFloat(x, out var parsedX) ||
            !Utils.Converters.TryParseFloat(y, out var parsedY) ||
            !Utils.Converters.TryParseFloat(z, out var parsedZ)) {
            MelonLogger.Msg($"[Error] Attempted interact with a prop using an invalid position or rotation. " +
                            $"Values attempted to parse to a float: " +
                            $"{x} [{x?.GetType()}], {y} [{y?.GetType()}], {z} [{z?.GetType()}]");
            result = default;
            return false;
        }
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

    private static bool TryParseSpawnableInstanceId(object possibleInstanceId, out string instanceId) {
        if (possibleInstanceId is string { Length: 12 } possibleInstanceIdStr && long.TryParse(possibleInstanceIdStr, System.Globalization.NumberStyles.HexNumber, null, out var hexValue)) {
            instanceId = hexValue.ToString("X");
            return true;
        }
        MelonLogger.Msg($"[Error] Attempted to set a prop parameter but the prop instance id is not a valid prop instance id. " +
                        $"It needs to be an hexadecimal value with a length of 12 characters. " +
                        $"Provided prop instance id: {possibleInstanceId}");
        instanceId = null;
        return false;
    }
}
