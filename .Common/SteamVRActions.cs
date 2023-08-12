using MelonLoader;
using Newtonsoft.Json;
using Valve.VR;

namespace Kafe.Common;

public static class SteamVRActions {

    public static void LoadAction(string actionName, string readableName) {

        var actionPath = $"/actions/AlphaBlendInteractive/in/{actionName}";

        if (!SteamVR_Input.initialized || SteamVR_Input.HasAction(actionPath)) {
            return;
        }

        var actionsFilePath = SteamVR_Input.GetActionsFilePath();
        var json = File.ReadAllText(actionsFilePath);
        var actionsFile = JsonConvert.DeserializeObject<ActionsFile>(json);

        if (actionsFile.actions.Exists(a => a.name == actionPath)) {
            MelonLogger.Msg($"{actionPath} is already present in SteamVR bindings.");
            return;
        }

        MelonLogger.Msg($"Didn't find {actionPath} in SteamVR bindings, adding it with the name {readableName}...");

        // Add action
        var newAction = new Action {
            name = actionPath,
            type = "boolean",
            requirement = "optional"
        };
        actionsFile.actions.Add(newAction);

        // Add action localization - find English localization, if it doesn't exist create one
        var englishLocalization = actionsFile.localization.Find(l => l.ContainsKey("language_tag") && l["language_tag"] == "en_US");
        if (englishLocalization == null) {
            englishLocalization = new Dictionary<string, string> {{"language_tag", "en_US"}};
            actionsFile.localization.Add(englishLocalization);
        }
        englishLocalization[actionPath] = readableName;


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
