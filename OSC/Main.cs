using ABI_RC.Core.Savior;
using MelonLoader;
using OSC.Components;
using OSC.Handlers;
using OSC.Utils;

namespace OSC;

public class OSC : MelonMod {

    public static OSC Instance { get; private set; }

    private MelonPreferences_Category _mcOsc;

    // Connection
    public MelonPreferences_Entry<int> meOSCServerInPort;
    public MelonPreferences_Entry<string> meOSCServerOutIp;
    public MelonPreferences_Entry<int> meOSCServerOutPort;

    // Avatar Module
    public MelonPreferences_Entry<bool> meOSCAvatarModule;
    public MelonPreferences_Entry<bool> meOSCAvatarModuleTriggers;
    public MelonPreferences_Entry<bool> meOSCAvatarModuleSetAvatar;
    public MelonPreferences_Entry<bool> meOSCAvatarModuleBypassJsonConfig;

    // Json config
    public MelonPreferences_Entry<bool> meOSCJsonConfigUuidPrefixes;
    public MelonPreferences_Entry<bool> meOSCJsonConfigAlwaysReplace;
    public MelonPreferences_Entry<bool> meOSCJsonConfigOverridePathEnabled;
    public MelonPreferences_Entry<string> meOSCJsonConfigOverridePath;

    // Input Module
    public MelonPreferences_Entry<bool> meOSCInputModule;
    public MelonPreferences_Entry<List<string>> meOSCInputModuleBlacklist;

    // Spawnable Module
    public MelonPreferences_Entry<bool> meOSCSpawnableModule;

    // Tracking Module
    public MelonPreferences_Entry<bool> meOSCTrackingModule;
    public MelonPreferences_Entry<float> meOSCTrackingModuleUpdateInterval;

    private HandlerOsc _handlerOsc;

    public override void OnApplicationStart() {

        Instance = this;

        // Melon Config
        _mcOsc = MelonPreferences.CreateCategory(nameof(OSC));


        // OSC Server
        meOSCServerInPort = _mcOsc.CreateEntry("ServerInPort", 9000,
            description: "Port that external programs should use to send messages into CVR.",
            oldIdentifier: "inPort");

        meOSCServerOutIp = _mcOsc.CreateEntry("ServerOutIp", "127.0.0.1",
            description: "Ip of the external program to listen to messages from CVR.",
            oldIdentifier: "outIp");

        meOSCServerOutPort = _mcOsc.CreateEntry("ServerOutPort", 9001,
            description: "Port that external programs should use listen to messages from CVR.",
            oldIdentifier: "outPort");


        // Avatar Module
        meOSCAvatarModule = _mcOsc.CreateEntry("AvatarModule", true,
            description: "Whether the mod is listening/writing for avatar parameters/changes on the /avatar/ address or not.");

        meOSCAvatarModuleTriggers = _mcOsc.CreateEntry("AvatarModuleTriggerParameters", false,
            description: "Whether the mod listens and sends events for trigger parameters (by sending/receiving osc " +
                         "messages with just the address and no value) or not.", oldIdentifier: "enableTriggerParameter");

        meOSCAvatarModuleSetAvatar = _mcOsc.CreateEntry("AvatarModuleSetAvatar", true,
            description: "Whether the mod accepts avatar change requests, by sending the avatar guid to the address: " +
                         "/avatar/change", oldIdentifier: "enableSetAvatar");

        meOSCAvatarModuleBypassJsonConfig = _mcOsc.CreateEntry("AvatarModuleBypassJsonConfig", true,
            description: "Whether the mod allows parameters to be set/read without being in the json config or not.");

        // Avatar OSC json Configs
        meOSCJsonConfigUuidPrefixes = _mcOsc.CreateEntry("JsonConfigUuidPrefixes", true,
            description: "Whether the mod should prefix the OSC config file path/name with usr_ and avtr_",
            oldIdentifier: "addUuidPrefixes");

        meOSCJsonConfigAlwaysReplace = _mcOsc.CreateEntry("JsonConfigReplaceEvenIfExists", true,
            description: "Whether the mod should replace the avatar OSC config (if already exists) when loading into the avatar.",
            oldIdentifier: "replaceConfigIfExists");

        meOSCJsonConfigOverridePathEnabled = _mcOsc.CreateEntry("JsonConfigOverridePathEnable", false,
            description: "Whether the mode should use the override path to store/read OSC configs.",
            oldIdentifier: "enableOverridePath");

        var defaultOverrideFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalLow", "VRChat", "VRChat");
        meOSCJsonConfigOverridePath = _mcOsc.CreateEntry("JsonConfigOverridePath", defaultOverrideFolder,
            description: "The override path to store/read configs. Note: Only used if JsonConfigOverridePathEnable = true",
            oldIdentifier: "overridePath");


        // Input Module
        meOSCInputModule = _mcOsc.CreateEntry("InputModule", true,
            description: "Whether the mod is listening for inputs on the /input/ address or not.",
            oldIdentifier: "enableInputs");

        meOSCInputModuleBlacklist = _mcOsc.CreateEntry("InputModuleBlacklist", new List<string> { "Reload", "Respawn", "QuitGame" },
            description: "List of blocked inputs. Some of them can be quite annoying and abuse-able.",
            oldIdentifier: "inputBlacklist");


        // Spawnable
        meOSCSpawnableModule = _mcOsc.CreateEntry("PropModule", true,
            description: "Whether the mod will listen/write data for props on the /prop/ address or not.");


        // Tracking
        meOSCTrackingModule = _mcOsc.CreateEntry("TrackingModule", true,
            description: "Whether the mod will send tracking data updates on the /tracking/ address or not.");

        meOSCTrackingModuleUpdateInterval = _mcOsc.CreateEntry("TrackingModuleUpdateInterval", 0f,
            description: "Minimum of seconds between each tracking data update. Default: 0 (will update every frame) " +
                         "Eg: 0.05 will update every 50 milliseconds.");


        // Load env variables (may change the melon config)
        EnvVariables.Load();

        _mcOsc.SaveToFile(false);

        // Attach OSC Input Module and handle their disabling/enabling
        Events.Scene.InputManagerCreated += () => {
            var inputModuleOsc = CVRInputManager.Instance.gameObject.AddComponent<InputModuleOSC>();
            inputModuleOsc.enabled = meOSCInputModule.Value;
            MelonLogger.Msg("[Input] OSC Input Module Initialized.");
            meOSCInputModule.OnValueChanged += (_, newValue) => inputModuleOsc.enabled = newValue;
        };

        // Start OSC server
        _handlerOsc = new HandlerOsc();
    }
}
