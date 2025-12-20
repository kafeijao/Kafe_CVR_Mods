using ABI_RC.Systems.OSC;
using Kafe.OSC.Modules;
using MelonLoader;

namespace Kafe.OSC;

public class OSC : MelonMod
{
    public static OSC Instance { get; private set; }

    private MelonPreferences_Category _mcOsc;

    // Json config
    public MelonPreferences_Entry<bool> meOSCJsonConfigUuidPrefixes;
    public MelonPreferences_Entry<bool> meOSCJsonConfigAlwaysReplace;
    public MelonPreferences_Entry<bool> meOSCJsonConfigOverridePathEnabled;
    public MelonPreferences_Entry<string> meOSCJsonConfigOverridePath;

    // Spawnable Module
    public MelonPreferences_Entry<bool> meOSCSpawnableModule;

    // Tracking Module
    public MelonPreferences_Entry<bool> meOSCTrackingModule;
    public MelonPreferences_Entry<float> meOSCTrackingModuleUpdateInterval;

    // Chat Box Module
    public MelonPreferences_Entry<bool> meOSCChatBoxModule;

    // Misc
    public MelonPreferences_Entry<bool> meOSCDebug;
    public MelonPreferences_Entry<bool> meOSCDebugConfigWarnings;
    public MelonPreferences_Entry<bool> meOSCPerformanceMode;

    public override void OnInitializeMelon()
    {
        Instance = this;

        // Melon Config
        _mcOsc = MelonPreferences.CreateCategory(nameof(OSC));

        // Avatar OSC json Configs
        meOSCJsonConfigUuidPrefixes = _mcOsc.CreateEntry("JsonConfigUuidPrefixes", true,
            description: "Whether the mod should prefix the OSC config file path/name with usr_ and avtr_",
            oldIdentifier: "addUuidPrefixes");

        meOSCJsonConfigAlwaysReplace = _mcOsc.CreateEntry("JsonConfigReplaceEvenIfExists", true,
            description:
            "Whether the mod should replace the avatar OSC config (if already exists) when loading into the avatar.",
            oldIdentifier: "replaceConfigIfExists");

        meOSCJsonConfigOverridePathEnabled = _mcOsc.CreateEntry("JsonConfigOverridePathEnable", false,
            description: "Whether the mode should use the override path to store/read OSC configs.",
            oldIdentifier: "enableOverridePath");

        var defaultVrcFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "LocalLow"),
            "VRChat", "VRChat");
        meOSCJsonConfigOverridePath = _mcOsc.CreateEntry("JsonConfigOverrideFolder", defaultVrcFolder,
            description:
            "The override path to store/read configs. Note: Only used if JsonConfigOverridePathEnable = true");

        // Spawnable
        meOSCSpawnableModule = _mcOsc.CreateEntry("PropModule", true,
            description: "Whether the mod will listen/write data for props on the /prop/ address or not.");


        // Tracking
        meOSCTrackingModule = _mcOsc.CreateEntry("TrackingModule", true,
            description: "Whether the mod will send/receive tracking data updates on the /tracking/ address or not.");

        meOSCTrackingModuleUpdateInterval = _mcOsc.CreateEntry("TrackingModuleUpdateInterval", 0f,
            description:
            "Minimum of seconds between sending each tracking data update. Default: 0 (will update every frame) " +
            "Eg: 0.050 will update every 50 milliseconds.");


        // Chat Box
        meOSCChatBoxModule = _mcOsc.CreateEntry("ChatBoxModule", true,
            description: "Whether the mod will listen on the /chatbox/ address or not.");


        // Misc
        meOSCDebug = _mcOsc.CreateEntry("Debug", false,
            description: "Whether should spam the console with debug messages or not.");
        meOSCDebugConfigWarnings = _mcOsc.CreateEntry("DebugConfigWarnings", true,
            description: "Whether it should warn everytime you send an osc msg  and it gets ignored because a config " +
                         "setting. (for example sending a trigger parameter when triggers are disabled).");
        meOSCPerformanceMode = _mcOsc.CreateEntry("PerformanceMode", false,
            description: "If performance mode is activated the mod will stop listening to most game events. " +
                         "This will result on a lot of the osc messages going out from our mod to not work. If you" +
                         "only want the mod to listen to osc messages going into CVR you should have this option on! " +
                         "As there's a lot of processing/msg sending when listening the all the game events.");

        OSCServer.Modules[OSCTrackingModule.ModulePrefix] = new OSCTrackingModule();
        OSCServer.Modules[OSCSpawnableModule.ModulePrefix] = new OSCSpawnableModule();
        OSCServer.Modules[OSCConfigModule.ModulePrefix] = new OSCConfigModule();
    }
}
