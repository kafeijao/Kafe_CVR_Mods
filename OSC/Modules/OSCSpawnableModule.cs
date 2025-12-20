using ABI_RC.Core.Util;
using ABI_RC.Helpers;
using ABI_RC.Systems.OSC;
using ABI_RC.Systems.OSC.Jobs;
using ABI.CCK.Components;
using Kafe.OSC.Utils;
using LucHeart.CoreOSC;
using MelonLoader;
using UnityEngine;

namespace Kafe.OSC.Modules;

public class OSCSpawnableModule : OSCModule
{
    public const string ModulePrefix = "/prop";

    enum SpawnableOperation {
        create,
        delete,
        available,
        parameter,
        location,
        location_sub,
    }

    private bool _enabled;
    private bool _debugConfigWarnings;

    private readonly Action<CVRSyncHelper.PropData> _spawnableCreated;
    private readonly Action<CVRSpawnable> _spawnableDeleted;
    private readonly Action<CVRSpawnable, bool> _spawnableAvailabilityChanged;
    private readonly Action<CVRSpawnable, CVRSpawnableValue> _spawnableParameterChanged;
    private readonly Action<CVRSpawnable> _spawnableLocationChanged;

    private OSCJobQueue<SpawnableCreatePayload> _spawnableCreateQueue = null!;
    private OSCJobQueue<SpawnableCreateAtPosPayload> _spawnableCreateAtPosQueue = null!;
    private OSCJobQueue<SpawnableDeletePayload> _spawnableDeleteQueue = null!;
    private OSCJobQueue<SpawnableParameterPayload> _spawnableParameterQueue = null!;
    private OSCJobQueue<SpawnableLocationPayload> _spawnableLocationQueue = null!;
    private OSCJobQueue<SpawnableLocationSubPayload> _spawnableLocationSubQueue = null!;

    public OSCSpawnableModule() : base(ModulePrefix)
    {
        // Execute actions on spawnable created
        _spawnableCreated = propData => {
            var spawnable = propData.Spawnable;

            // Send the create event
            Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.create)}",
                spawnable.guid,
                GetInstanceId(spawnable),
                spawnable.subSyncs.Count));

            // Send all parameter values when loads a new spawnable is created
            foreach (var syncedParams in spawnable.syncValues) {
                Events.Spawnable.OnSpawnableParameterChanged(spawnable, syncedParams);
            }
        };

        // Execute actions on spawnable deletion
        _spawnableDeleted = spawnable => {
            // Send the delete event
            Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.delete)}", GetGuid(spawnable), GetInstanceId(spawnable)));
        };

        // Send spawnable availability change events
        _spawnableAvailabilityChanged = (spawnable, available) => {
            Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.available)}", spawnable.guid, GetInstanceId(spawnable), available));
        };

        // Send spawnable parameter change events
        _spawnableParameterChanged = (spawnable, spawnableValue) => {
            Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.parameter)}", spawnable.guid, GetInstanceId(spawnable), spawnableValue.name, spawnableValue.currentValue));
        };

        // Send spawnable location change events
        _spawnableLocationChanged = spawnable => {

            // Handle main spawnable transform
            var transform = spawnable.transform;
            var pos = transform.position;
            var rot = transform.rotation.eulerAngles;
            Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.location)}",
                spawnable.guid, GetInstanceId(spawnable),
                pos.x, pos.y, pos.z, rot.x, rot.y, rot.z));

            // Handle sub_sync transforms
            for (var index = 0; index < spawnable.subSyncs.Count; index++) {
                var spawnableSubSync = spawnable.subSyncs[index];
                var sTransform = spawnableSubSync.transform;
                // If there are transforms defined but the prop was Enable Sync Values is turned off...
                if (sTransform == null) continue;
                var sPos = sTransform.position;
                var sRot = sTransform.rotation.eulerAngles;
                Server.DispatchMessage(new OscMessage($"{ModulePrefix}/{nameof(SpawnableOperation.location_sub)}",
                    spawnable.guid, GetInstanceId(spawnable),
                    index,
                    sPos.x, sPos.y, sPos.z, sRot.x, sRot.y, sRot.z));
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

    private void Enable()
    {
        if (_enabled || !OSCServer.IsRunning || !OSC.Instance.meOSCSpawnableModule.Value) return;

        Events.Spawnable.SpawnableCreated += _spawnableCreated;
        Events.Spawnable.SpawnableDeleted += _spawnableDeleted;
        Events.Spawnable.SpawnableAvailable += _spawnableAvailabilityChanged;
        Events.Spawnable.SpawnableParameterChanged += _spawnableParameterChanged;
        Events.Spawnable.SpawnableLocationTrackingTicked += _spawnableLocationChanged;
        _enabled = true;
    }

    private void Disable()
    {
        if (!_enabled) return;

        Events.Spawnable.SpawnableCreated -= _spawnableCreated;
        Events.Spawnable.SpawnableDeleted -= _spawnableDeleted;
        Events.Spawnable.SpawnableAvailable -= _spawnableAvailabilityChanged;
        Events.Spawnable.SpawnableParameterChanged -= _spawnableParameterChanged;
        Events.Spawnable.SpawnableLocationTrackingTicked -= _spawnableLocationChanged;
        _enabled = false;
    }

    #region Module Overrides

    public override void Initialize()
    {
        Enable();
        RegisterQueues();
    }

    public override void Cleanup()
    {
        Disable();
        FreeQueues();
    }

    public override bool HandleIncoming(OscMessage oscMsg)
    {
        if (!_enabled) {
            if (_debugConfigWarnings) {
                MelonLogger.Msg($"[Config] Sent an osc msg to {Prefix}/, but this module is disabled " +
                                $"in the configuration file, so this will be ignored.");
            }
            return false;
        }

        var addressParts = oscMsg.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to interact with a prop but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{Prefix}/<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}");
            return false;
        }

        Enum.TryParse<SpawnableOperation>(addressParts[2], true, out var spawnableOperation);

        switch (spawnableOperation) {
            case SpawnableOperation.parameter:
                return ReceivedParameterHandler(oscMsg);
            case SpawnableOperation.location:
                return ReceivedLocationHandler(oscMsg);
            case SpawnableOperation.location_sub:
                return ReceivedLocationSubHandler(oscMsg);
            case SpawnableOperation.delete:
                return ReceivedDeleteHandler(oscMsg);
            case SpawnableOperation.create:
                return ReceivedCreateHandler(oscMsg);
            case SpawnableOperation.available:
                MelonLogger.Msg($"[Error] Attempted set the availability for a prop, this is not allowed.");
                return false;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to interact with a prop but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{Prefix}/<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(SpawnableOperation)))}"
                    );
                return false;
        }
    }

    #endregion Module Overrides

    #region Queues

    private void RegisterQueues()
    {
        _spawnableCreateQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableCreatePayload>(128, payload =>
        {
            Events.Spawnable.OnSpawnableCreate(payload.SpawnableFullId.ToString());
        });

        _spawnableCreateAtPosQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableCreateAtPosPayload>(128, payload =>
        {
            Events.Spawnable.OnSpawnableCreate(payload.SpawnableFullId.ToString(), payload.X, payload.Y, payload.Z);
        });

        _spawnableDeleteQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableDeletePayload>(128, payload =>
        {
            Events.Spawnable.OnSpawnableDelete(payload.SpawnableFullId.ToString());
        });

        _spawnableParameterQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableParameterPayload>(8192, payload =>
        {
            Events.Spawnable.OnSpawnableParameterSet(payload.SpawnableFullId.ToString(), payload.SpawnableParameterName.ToString(), payload.ParsedFloat);
        });

        _spawnableLocationQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableLocationPayload>(8192, payload =>
        {
            Events.Spawnable.OnSpawnableLocationSet(payload.SpawnableFullId.ToString(), payload.Position, payload.Rotation);
        });

        _spawnableLocationSubQueue = OSCJobSystemExtensions.RegisterQueue<SpawnableLocationSubPayload>(8192, payload =>
        {
            Events.Spawnable.OnSpawnableLocationSet(payload.SpawnableFullId.ToString(), payload.Position, payload.Rotation, payload.SubIndex);
        });
    }

    private void FreeQueues()
    {
        OSCJobSystem.UnRegisterQueue(_spawnableCreateQueue);
        _spawnableCreateQueue = null!;
        OSCJobSystem.UnRegisterQueue(_spawnableCreateAtPosQueue);
        _spawnableCreateAtPosQueue = null!;
        OSCJobSystem.UnRegisterQueue(_spawnableDeleteQueue);
        _spawnableDeleteQueue = null!;
        OSCJobSystem.UnRegisterQueue(_spawnableParameterQueue);
        _spawnableParameterQueue = null!;
        OSCJobSystem.UnRegisterQueue(_spawnableLocationQueue);
        _spawnableLocationQueue = null!;
        OSCJobSystem.UnRegisterQueue(_spawnableLocationSubQueue);
        _spawnableLocationSubQueue = null!;
    }

    #endregion Queues

    #region Queue Handlers

     private bool ReceivedCreateHandler(OscMessage oscMessage) {

         var oscArguments = oscMessage.Arguments;

        if (oscArguments.Length is not (1 or 4)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid number of arguments. " +
                            $"Expected either 1 or 4 arguments, for the guid and optionally position coordinates.");
            return false;
        }

        var possibleGuid = oscArguments[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to create a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return false;
        }

        if (oscArguments.Length == 4 && TryParseVector3(oscArguments[1], oscArguments[2], oscArguments[3], out var floats)) {
            _spawnableCreateAtPosQueue.Enqueue(new SpawnableCreateAtPosPayload(spawnableGuid, floats.Item1, floats.Item2, floats.Item3));
            return true;
        }

        _spawnableCreateQueue.Enqueue(new SpawnableCreatePayload(spawnableGuid));
        return true;
    }

    private bool ReceivedDeleteHandler(OscMessage oscMessage) {
        var oscArguments = oscMessage.Arguments;

        if (oscArguments.Length != 2) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid number of arguments. " +
                            $"Expected 2 arguments, for the prop GUID and Instance ID.");
            return false;
        }

        var possibleGuid = oscArguments[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return false;
        }

        var possibleInstanceId = oscArguments[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to delete a prop, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return false;
        }

        _spawnableDeleteQueue.Enqueue(new SpawnableDeletePayload($"p+{spawnableGuid}~{spawnableInstanceId}"));
        return true;
    }

    private bool ReceivedParameterHandler(OscMessage oscMessage) {
        var oscArguments = oscMessage.Arguments;

        if (oscArguments.Length != 4) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid number of arguments. " +
                            $"Expected 4 arguments, for the prop GUID, and Instance ID, sync param name, and param value");
            return false;
        }

        var possibleGuid = oscArguments[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return false;
        }

        var possibleInstanceId = oscArguments[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return false;
        }

        var possibleParamName = oscArguments[2];
        if (possibleParamName is not string spawnableParameterName) {
            MelonLogger.Msg($"[Error] Attempted to set a prop synced param, but provided an invalid name. " +
                            $"Attempted: \"{possibleParamName}\" Type: {possibleParamName?.GetType()}" +
                            $"The synced parameter name has to be a string.");
            return false;
        }

        var possibleFloat = oscArguments[3];
        if (!Converters.TryHardToParseFloat(possibleFloat, out var parsedFloat)) {
            MelonLogger.Msg(
                $"[Error] Attempted to change a prop synced parameter {spawnableParameterName} to {possibleFloat}, " +
                $"but we failed to parse whatever you sent to a float (we tried really hard I promise). " +
                $"Attempted value type: {possibleFloat?.GetType()} " +
                $"Contact the mod creator if you think this is a bug.");
            return false;
        }

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";
        _spawnableParameterQueue.Enqueue(new SpawnableParameterPayload(spawnableFullId, spawnableParameterName, parsedFloat));
        return true;
    }

    private bool ReceivedLocationHandler(OscMessage oscMessage)
    {
        var oscArguments = oscMessage.Arguments;

        if (oscArguments.Length != 8) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid number of arguments. " +
                            $"Expected 8 arguments, for the prop GUID, and Instance ID, 3 floats for position, and 3" +
                            $"floats for the rotation (euler angles).");
            return false;
        }

        var possibleGuid = oscArguments[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return false;
        }

        var possibleInstanceId = oscArguments[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location, but provided an invalid Instance ID. " +
                            $"Prop Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return false;
        }

        // Validate position and rotation floats
        if (!TryParseVector3(oscArguments[2], oscArguments[3], oscArguments[4], out var posFloats)) return false;
        if (!TryParseVector3(oscArguments[5], oscArguments[6], oscArguments[7], out var rotFloats)) return false;

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var position = new Vector3(posFloats.Item1, posFloats.Item2, posFloats.Item3);
        var rotation = new Vector3(rotFloats.Item1, rotFloats.Item2, rotFloats.Item3);

        _spawnableLocationQueue.Enqueue(new SpawnableLocationPayload(spawnableFullId, position, rotation));
        return true;
    }

    private bool ReceivedLocationSubHandler(OscMessage oscMessage)
    {
        var oscArguments = oscMessage.Arguments;

        if (oscArguments.Length != 9) {
            MelonLogger.Msg($"[Error] Attempted to set a prop location with an invalid number of arguments. " +
                            $"Expected 8 arguments, for the prop GUID, and Instance ID, 3 floats for position, and 3" +
                            $"floats for the rotation (euler angles).");
            return false;
        }

        var possibleGuid = oscArguments[0];
        if (!TryParseSpawnableGuid(possibleGuid, out var spawnableGuid)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop sub-sync location, but provided an invalid GUID. " +
                            $"GUID attempted: \"{possibleGuid}\" Type: {possibleGuid?.GetType()}" +
                            $"Example of a valid GUID: 1aa10cac-36ba-4e01-b58d-a76dc35f61bb");
            return false;
        }

        var possibleInstanceId = oscArguments[1];
        if (!TryParseSpawnableInstanceId(possibleInstanceId, out var spawnableInstanceId)) {
            MelonLogger.Msg($"[Error] Attempted to set a prop sub-sync location, with an invalid Instance ID. " +
                            $"Instance ID attempted: \"{possibleInstanceId}\" Type: {possibleInstanceId?.GetType()}" +
                            $"Example of a valid GUID: 8E143EA45EE8");
            return false;
        }

        // Validate index
        var possibleIndex = oscArguments[2];
        if (!Converters.TryToParseInt(possibleIndex, out var subIndex) || subIndex < 0) {
            MelonLogger.Msg($"[Error] Attempted set the location of prop sub-sync with an invalid/negative index. " +
                            $"Value attempted to parse: {possibleIndex} [{possibleIndex?.GetType()}]");
            return false;
        }

        // Validate position and rotation floats
        if (!TryParseVector3(oscArguments[3], oscArguments[4], oscArguments[5], out var posFloats)) return false;
        if (!TryParseVector3(oscArguments[6], oscArguments[7], oscArguments[8], out var rotFloats)) return false;

        var spawnableFullId = $"p+{spawnableGuid}~{spawnableInstanceId}";

        var position = new Vector3(posFloats.Item1, posFloats.Item2, posFloats.Item3);
        var rotation = new Vector3(rotFloats.Item1, rotFloats.Item2, rotFloats.Item3);

        _spawnableLocationSubQueue.Enqueue(new SpawnableLocationSubPayload(spawnableFullId, position, rotation, subIndex));
        return true;
    }

    #endregion Queue Handlers

    #region Job Payloads

    private readonly struct SpawnableCreatePayload
    {
        public SpawnableCreatePayload(string spawnableFullId)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
        }
        public readonly FixedUtf8String256 SpawnableFullId;
    }

    private readonly struct SpawnableCreateAtPosPayload
    {
        public SpawnableCreateAtPosPayload(string spawnableFullId, float x, float y, float z)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
            X = x;
            Y = y;
            Z = z;
        }
        public readonly FixedUtf8String256 SpawnableFullId;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
    }

    private readonly struct SpawnableDeletePayload
    {
        public SpawnableDeletePayload(string spawnableFullId)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
        }
        public readonly FixedUtf8String256 SpawnableFullId;
    }

    private readonly struct SpawnableParameterPayload
    {
        public SpawnableParameterPayload(string spawnableFullId, string spawnableParameterName, float parsedFloat)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
            SpawnableParameterName = new FixedUtf8String256();
            SpawnableParameterName.Set(spawnableParameterName);
            ParsedFloat = parsedFloat;
        }
        public readonly FixedUtf8String256 SpawnableFullId;
        public readonly FixedUtf8String256 SpawnableParameterName;
        public readonly float ParsedFloat;
    }

    private readonly struct SpawnableLocationPayload
    {
        public SpawnableLocationPayload(string spawnableFullId, Vector3 position, Vector3 rotation)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
            Position = position;
            Rotation = rotation;
        }
        public readonly FixedUtf8String256 SpawnableFullId;
        public readonly Vector3 Position;
        public readonly Vector3 Rotation;
    }

    private readonly struct SpawnableLocationSubPayload
    {
        public SpawnableLocationSubPayload(string spawnableFullId, Vector3 position, Vector3 rotation, int subIndex)
        {
            SpawnableFullId = new FixedUtf8String256();
            SpawnableFullId.Set(spawnableFullId);
            Position = position;
            Rotation = rotation;
            SubIndex = subIndex;
        }
        public readonly FixedUtf8String256 SpawnableFullId;
        public readonly Vector3 Position;
        public readonly Vector3 Rotation;
        public readonly int SubIndex;
    }

    #endregion Job Payloads

    #region Helpers

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

        if (!Converters.TryParseFloat(x, out var parsedX) ||
            !Converters.TryParseFloat(y, out var parsedY) ||
            !Converters.TryParseFloat(z, out var parsedZ)) {
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

    #endregion Helpers

    // Todo: Add OSCQuery?
}
