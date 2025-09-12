using ABI_RC.Systems.OSC;
using ABI_RC.Systems.OSC.Jobs;
using Kafe.OSC.Utils;
using LucHeart.CoreOSC;
using MelonLoader;

namespace Kafe.OSC.Modules;

public class OSCConfigModule : OSCModule
{
    public const string ModulePrefix = "/config";

    private enum ConfigOperation {
        Reset,
    }

    private OSCJobQueue<ConfigResetPayload> _configResetQueue = null!;

    public OSCConfigModule() : base(ModulePrefix) { }

    #region Module Overrides

    public override void Initialize()
    {
        RegisterQueues();
    }

    public override void Cleanup()
    {
        FreeQueues();
    }

    public override bool HandleIncoming(OscMessage packet)
    {
        var addressParts = packet.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to set a config but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{packet.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{ModulePrefix}/<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ConfigOperation)))}");
            return false;
        }

        Enum.TryParse<ConfigOperation>(addressParts[2], true, out var configOperation);

        switch (configOperation) {
            case ConfigOperation.Reset:
                _configResetQueue.Enqueue(new ConfigResetPayload());
                return true;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to set a config but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{packet.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{Prefix}/<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ConfigOperation)))}"
                );
                break;
        }

        return false;
    }

    #endregion Module Overrides

    #region Queues

    private void RegisterQueues()
    {
        _configResetQueue = OSCJobSystemExtensions.RegisterQueue<ConfigResetPayload>(512, _ =>
        {
            Events.Scene.ResetAll();
        });
    }

    private void FreeQueues()
    {
        OSCJobSystem.UnRegisterQueue(_configResetQueue);
        _configResetQueue = null!;
    }

    #endregion Queues

    #region Job Payloads

    public readonly struct ConfigResetPayload;

    #endregion Job Payloads
}
