using System;
using System.Collections.Generic;
using System.IO;
using ABI_RC.Core.Savior;
using MelonLoader;
using OSC.Components;
using OSC.Handlers;
using OSC.Utils;

namespace OSC;

public class OSC : MelonMod {
    
    public static OSC Instance { get; private set; }
    
    private MelonPreferences_Category mcOSC;
    
    // Connection
    public MelonPreferences_Entry<int> meOSCInPort;
    public MelonPreferences_Entry<string> meOSCOutIp;
    public MelonPreferences_Entry<int> meOSCOutPort;
    
    // Preferences
    public MelonPreferences_Entry<bool> meOSCEnableTriggers;
    public MelonPreferences_Entry<bool> meOSCEnableSetAvatar;
    
    public MelonPreferences_Entry<bool> meOSCEnableInputs;
    public MelonPreferences_Entry<List<string>> meOSCInputBlacklist;
    
    public MelonPreferences_Entry<bool> meOSCEnableUuidPrefixes;
    public MelonPreferences_Entry<bool> meOSCAlwaysReplaceConfig;
    
    public MelonPreferences_Entry<bool> meOSCEnableOverridePath;
    public MelonPreferences_Entry<string> meOSCOverridePath;

    private HandlerOsc _handlerOsc;
    
    public override void OnApplicationStart() {
        
        Instance = this;

        // Melon Config
        mcOSC = MelonPreferences.CreateCategory(nameof(OSC));
        
        // OSC Server
        meOSCInPort = mcOSC.CreateEntry("inPort", 9000,
            description: "Port that external programs should use to send messages into CVR.");
        meOSCInPort.OnValueChanged += Events.Config.OnInPortChanged;
        
        meOSCOutIp = mcOSC.CreateEntry("outIp", "127.0.0.1",
            description: "Ip of the external program to listen to messages from CVR.");
        meOSCOutIp.OnValueChanged += Events.Config.OnOutIpChanged;
        
        meOSCOutPort = mcOSC.CreateEntry("outPort", 9001,
            description: "Port that external programs should use listen to messages from CVR.");
        meOSCOutPort.OnValueChanged += Events.Config.OnOutPortChanged;

        // Inputs
        meOSCEnableInputs = mcOSC.CreateEntry("enableInputs", true,
            description: "Whether the mod is listening for inputs on the /input/ address or not.");
        
        meOSCInputBlacklist = mcOSC.CreateEntry("inputBlacklist", new List<string> { "Reload", "Respawn", "QuitGame" },
            description: "List of blocked inputs. Some of them can be quite annoying and abuse-able.");
        
        // Avatar OSC Configs
        meOSCEnableUuidPrefixes = mcOSC.CreateEntry("addUuidPrefixes", true,
            description: "Whether the mod should prefix the OSC config file path/name with usr_ and avtr_");
        
        meOSCAlwaysReplaceConfig = mcOSC.CreateEntry("replaceConfigIfExists", true,
            description: "Whether the mod should replace (if exists) the avatar OSC config when loading into the avatar.");
        
        meOSCEnableOverridePath = mcOSC.CreateEntry("enableOverridePath", false,
            description: "Whether the mode should use the override path to store/read OSC configs.");
        
        var defaultOverrideFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalLow", "VRChat", "VRChat");
        meOSCOverridePath = mcOSC.CreateEntry("overridePath", defaultOverrideFolder,
            description: "The override path to store/read configs. Note: Only used if enableOverridePath = true");

        // Misc
        meOSCEnableTriggers = mcOSC.CreateEntry("enableTriggerParameter", false,
            description: "Whether the mod listens and sends events for trigger parameters (by sending/receiving osc " +
                         "messages with just the address and no value) or not.");
        
        meOSCEnableSetAvatar = mcOSC.CreateEntry("enableSetAvatar", true,
            description: "Whether the mod accepts avatar change requests, by sending the avatar guid to the address: " +
                         "/avatar/change");

        // Load env variables (may change the melon config)
        EnvVariables.Load();
        
        mcOSC.SaveToFile(false);
        
        Events.Scene.InputManagerCreated += () => {
            var inputModuleOsc = CVRInputManager.Instance.gameObject.AddComponent<InputModuleOSC>();
            inputModuleOsc.enabled = meOSCEnableInputs.Value;
            MelonLogger.Msg("[Input] OSC Input Module Initialized.");
            meOSCEnableInputs.OnValueChanged += (_, newValue) => inputModuleOsc.enabled = newValue;
        };
        
        // Start OSC server
        _handlerOsc = new HandlerOsc();
    }
}