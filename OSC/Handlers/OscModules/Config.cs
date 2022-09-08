using MelonLoader;

namespace OSC.Handlers.OscModules;

enum ConfigOperation {
    reset,
}

public class Config : OscHandler {

    internal const string AddressPrefixConfig = "/config/";

    private bool _enabled = true;

    internal sealed override void Enable() {
        _enabled = true;
    }

    internal sealed override void Disable() {
        _enabled = false;
    }

    internal sealed override void ReceiveMessageHandler(string address, List<object> args) {
        if (!_enabled) return;

        var addressParts = address.Split('/');

        // Validate Length
        if (addressParts.Length != 3) {
            MelonLogger.Msg($"[Error] Attempted to set a config but the address is invalid." +
                            $"\n\t\t\tAddress attempted: \"{address}\"" +
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
                    $"\n\t\t\tAddress attempted: \"{address}\"" +
                    $"\n\t\t\tThe correct format should be: \"{AddressPrefixConfig}<op>\"" +
                    $"\n\t\t\tAnd the allowed ops are: {string.Join(", ", Enum.GetNames(typeof(ConfigOperation)))}"
                );
                return;
        }
    }

}
