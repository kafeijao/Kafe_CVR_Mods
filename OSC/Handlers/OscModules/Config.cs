using MelonLoader;
using Rug.Osc;

namespace Kafe.OSC.Handlers.OscModules;

enum ConfigOperation {
    reset,
}

public class Config : OscHandler {

    internal const string AddressPrefixConfig = "/config/";

    internal sealed override void Enable() {}

    internal sealed override void Disable() {}

    internal sealed override void ReceiveMessageHandler(OscMessage oscMsg) {

        var addressParts = oscMsg.Address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to set a config but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                            $"\n\t\t\tThe correct format should be: \"{AddressPrefixConfig}<op>\"" +
                            $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ConfigOperation)))}");
            return;
        }

        Enum.TryParse<ConfigOperation>(addressParts[2], true, out var configOperation);

        switch (configOperation) {
            case ConfigOperation.reset:
                Events.Scene.ResetAll();
                return;
            default:
                MelonLogger.Msg(
                    "[Error] Attempted to set a config but the address is invalid." +
                    $"\n\t\t\tAddress attempted: \"{oscMsg.Address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{AddressPrefixConfig}<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ConfigOperation)))}"
                );
                return;
        }
    }

}
