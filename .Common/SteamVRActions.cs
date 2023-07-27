using MelonLoader;
using Newtonsoft.Json;
using Valve.VR;

namespace Kafe.Common;

public static class SteamVRActions {

    public static void LoadAction(string actionName) {

        var actionPath = $"/actions/AlphaBlendInteractive/in/{actionName}";

        if (!SteamVR_Input.initialized || SteamVR_Input.HasAction(actionPath)) return;

        var actionsFilePath = SteamVR_Input.GetActionsFilePath();
        var json = File.ReadAllText(actionsFilePath);
        var actionsFile = JsonConvert.DeserializeObject<ActionsFile>(json);

        if (actionsFile.actions.Exists(a => a.name == actionPath)) {
            return;
        }

        MelonLogger.Msg($"Didn't find {actionPath} in SteamVR bindings, adding it...");

        var newAction = new Action {
            name = actionPath,
            type = "boolean",
            requirement = "optional"
        };
        actionsFile.actions.Add(newAction);
        json = JsonConvert.SerializeObject(actionsFile, Formatting.Indented);
        File.WriteAllText(actionsFilePath, json);
        SteamVR_Input.InitializeFile(true, true);
        SteamVR_Input.Initialize(true);
    }

    [Serializable]
    public class ActionsFile {
        public List<Action> actions;
        public List<ActionSet> action_sets;
        public List<DefaultBinding> default_bindings;
        public List<Dictionary<string, string>> localization;
    }

    [Serializable]
    public class Action {
        public string name;
        public string type;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string skeleton;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string requirement;
    }

    [Serializable]
    public class ActionSet {
        public string name;
        public string usage;
    }

    [Serializable]
    public class DefaultBinding {
        public string controller_type;
        public string binding_url;
    }
}
