using System;
using MelonLoader;

namespace OSC.Utils; 

internal static class EnvVariables {
    
    private const string OscCommandScheme = "--osc=inPort:senderIP:outPort";
    private const string OscEnvPrefix = "--osc=";
    
    public static void Load() {
        foreach (var commandLineArg in Environment.GetCommandLineArgs()) {
            if (!commandLineArg.StartsWith(OscEnvPrefix)) continue;
            var oscConfigs = commandLineArg.Substring(OscEnvPrefix.Length).Split(':');
            
            // Validations
            if (oscConfigs.Length != 3) {
                MelonLogger.Error($"The OSC config in the environment variable is malformed ({commandLineArg}). " +
                                  $"It should follow the scheme: {OscCommandScheme}");
                return;
            }
            if (!int.TryParse(oscConfigs[0], out var inPort)) {
                MelonLogger.Error($"The OSC config inPort needs to be an int, value provided: {oscConfigs[0]}");
                return;
            }
            if (!int.TryParse(oscConfigs[2], out var outPort)) {
                MelonLogger.Error($"The OSC config outPort needs to be an int, value provided: {oscConfigs[2]}");
                return;
            }
            
            var senderIp = oscConfigs[1];
            
            MelonLogger.Msg($"[Server] OSC config loaded from the environment variable successfully! It will overwrite " +
                            $"melon loader config with the settings: inPort: {inPort}, senderIP: {senderIp}, " +
                            $"outPort: {outPort}.");

            // Update melon config
            OSC.Instance.meOSCInPort.Value = inPort;
            OSC.Instance.meOSCOutIp.Value = senderIp;
            OSC.Instance.meOSCOutPort.Value = outPort;
            
            break;
        }
    }
}