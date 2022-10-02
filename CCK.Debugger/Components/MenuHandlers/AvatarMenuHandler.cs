using System.Text;
using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public class AvatarMenuHandler : MenuHandler {

    static AvatarMenuHandler() {

        // Todo: Allow to inspect other people's avatars if they give permission
        // Todo: Waiting for bios
        var players = CCKDebugger.TestMode ? CVRPlayerManager.Instance.NetworkPlayers : new List<CVRPlayerEntity>();

        bool IsValid(CVRPlayerEntity entity) {
            if (entity?.PuppetMaster == null) return false;
            var animatorManager = Traverse.Create(entity.PuppetMaster)
                .Field("_animatorManager")
                .GetValue<CVRAnimatorManager>();
            return animatorManager != null;
        }

        PlayerEntities = new LooseList<CVRPlayerEntity>(players, IsValid, true);
        CoreParameterNames = Traverse.Create(typeof(CVRAnimatorManager)).Field("coreParameters").GetValue<HashSet<string>>();

        // Pointers
        PointerValues = new Dictionary<TextMeshProUGUI, (CVRPointer, string)>();

        // Triggers
        TriggerValues = new Dictionary<TextMeshProUGUI, (CVRAdvancedAvatarSettingsTrigger, string, List<Func<string>>)>();
        TriggerAasTaskLastTriggered = new Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float>();
        TriggerAasTasksLastExecuted = new Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float>();
        TriggerAasStayTasksLastTriggered = new Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float>();
        TriggerAasStayTasksLastTriggeredValue = new Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float>();

        // Triggers last time triggered/executed save
        Events.Avatar.AasTriggerTriggered += task => {
            if (TriggerValues.Values.Any(t => t.Item1.enterTasks.Contains(task)) ||
                TriggerValues.Values.Any(t => t.Item1.exitTasks.Contains(task))) {
                TriggerAasTaskLastTriggered[task] = Time.time;
            }
        };
        Events.Avatar.AasTriggerExecuted += task => {
            if (TriggerValues.Values.Any(t => t.Item1.enterTasks.Contains(task)) ||
                TriggerValues.Values.Any(t => t.Item1.exitTasks.Contains(task))) {
                TriggerAasTasksLastExecuted[task] = Time.time;
            }
        };
        Events.Avatar.AasStayTriggerTriggered += task => {
            if (TriggerValues.Values.Any(t => t.Item1.stayTasks.Contains(task))) {
                TriggerAasStayTasksLastTriggered[task] = Time.time;
                if (PlayerSetup.Instance == null) return;
                TriggerAasStayTasksLastTriggeredValue[task] = PlayerSetup.Instance.GetAnimatorParam(task.settingName);
            }
        };

        // Update local avatar if we changed avatar and local avatar is selected
        Events.Avatar.AnimatorManagerUpdated += () => {
            if (PlayerEntities.CurrentObject == null) PlayerEntities.HasChanged = true;
        };
    }

    private static readonly LooseList<CVRPlayerEntity> PlayerEntities;

    // Attributes
    private static TextMeshProUGUI _attributeUsername;
    private static TextMeshProUGUI _attributeAvatar;

    private static Animator _mainAnimator;

    // Animator Synced Parameters
    private static GameObject _categorySyncedParameters;

    // Animator Local Parameters
    private static GameObject _categoryLocalParameters;

    // Core Parameters
    private static readonly HashSet<string> CoreParameterNames;
    private static GameObject _coreParameters;

    // Pointers
    private static GameObject _categoryPointers;
    private static readonly Dictionary<TextMeshProUGUI, (CVRPointer, string)> PointerValues;

    // Triggers
    private static GameObject _categoryTriggers;
    private static readonly Dictionary<TextMeshProUGUI, (CVRAdvancedAvatarSettingsTrigger, string, List<Func<string>>)> TriggerValues;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTaskLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTasksLastExecuted;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggeredValue;

    public override void Load(Menu menu) {

        menu.AddNewDebugger("Avatars");

        var categoryAttributes = menu.AddCategory("Attributes");
        _attributeUsername = menu.AddCategoryEntry(categoryAttributes, "User Name:");
        _attributeAvatar = menu.AddCategoryEntry(categoryAttributes, "Avatar Name/ID:");

        _categorySyncedParameters = menu.AddCategory("Avatar Synced Parameters");
        _categoryLocalParameters = menu.AddCategory("Avatar Local Parameters");
        _coreParameters = menu.AddCategory("Avatar Default Parameters");

        _categoryPointers = menu.AddCategory("CVR Pointers");
        _categoryTriggers = menu.AddCategory("CVR AAS Triggers");

        menu.ToggleCategories(true);

        PlayerEntities.ListenPageChangeEvents = true;
        PlayerEntities.HasChanged = true;
    }
    public override void Unload() {
        PlayerEntities.ListenPageChangeEvents = false;
    }

    public override void Update(Menu menu) {

        PlayerEntities.UpdateViaSource();

        var playerCount = PlayerEntities.Count;

        var isLocal = PlayerEntities.CurrentObject == null;
        var currentPlayer = PlayerEntities.CurrentObject;

        menu.SetControlsExtra($"({PlayerEntities.CurrentObjectIndex+1}/{playerCount})");

        menu.ShowControls(playerCount > 1);

        var playerUserName = isLocal ? MetaPort.Instance.username : currentPlayer.Username;
        var playerAvatarName = menu.GetAvatarName(isLocal ? MetaPort.Instance.currentAvatarGuid : currentPlayer.AvatarId);

        // Avatar Data Info
        if (Menu.HasValueChanged(_attributeUsername, playerUserName)) _attributeUsername.SetText(playerUserName);
        if (Menu.HasValueChanged(_attributeAvatar, playerAvatarName)) _attributeAvatar.SetText(playerAvatarName);

        // Update the menus if the spawnable changed
        if (PlayerEntities.HasChanged) {

            _mainAnimator = isLocal
                ? Events.Avatar.LocalPlayerAnimatorManager?.animator
                : Traverse.Create(currentPlayer.PuppetMaster).Field("_animatorManager").GetValue<CVRAnimatorManager>().animator;

            if (_mainAnimator == null || !_mainAnimator.isInitialized || _mainAnimator.parameters == null || _mainAnimator.parameters.Length < 1) return;

            // Highlight on local player makes us lag for some reason
            if (isLocal) Highlighter.ClearTargetHighlight();
            else Highlighter.SetTargetHighlight(_mainAnimator.gameObject);

            // Restore parameters
            menu.ClearCategory(_categorySyncedParameters);
            menu.ClearCategory(_categoryLocalParameters);
            menu.ClearCategory(_coreParameters);
            ParameterEntry.Entries.Clear();
            foreach (var parameter in _mainAnimator.parameters) {

                // Generate the text mesh pro for the proper category
                TextMeshProUGUI tmpParamValue;
                if (parameter.name.StartsWith("#")) tmpParamValue = menu.AddCategoryEntry(_categoryLocalParameters, parameter.name);
                else if (CoreParameterNames.Contains(parameter.name)) tmpParamValue = menu.AddCategoryEntry(_coreParameters, parameter.name);
                else tmpParamValue = menu.AddCategoryEntry(_categorySyncedParameters, parameter.name);

                // Create a parameter entry linked to the TextMeshPro
                ParameterEntry.Add(_mainAnimator, parameter, tmpParamValue);
            }

            var avatarGo = isLocal ? PlayerSetup.Instance._avatar : currentPlayer.PuppetMaster.avatarObject;

            // Set up CVR Pointers
            menu.ClearCategory(_categoryPointers);
            PointerValues.Clear();
            var avatarPointers = avatarGo.GetComponentsInChildren<CVRPointer>(true);
            foreach (var pointer in avatarPointers) {

                // Template items:
                // {0} -> (pointerGo.activeInHierarchy ? "yes" : "no")
                var pointerGo = pointer.gameObject;
                var template =
                    $"{White}<b>{pointerGo.name}:</b>" +
                    $"\n\t{White}Is Active: {Blue}{{0}}" +
                    $"\n\t{White}Class: {Blue}{pointer.GetType().Name}" +
                    $"\n\t{White}Is Internal: {Blue}{(pointer.isInternalPointer ? "yes" : "no")}" +
                    $"\n\t{White}Is Local: {Blue}{(pointer.isLocalPointer ? "yes" : "no")}" +
                    $"\n\t{White}Limit To Filtered Triggers: {Blue}{(pointer.limitToFilteredTriggers ? "yes" : "no")}" +
                    $"\n\t{White}Layer: {Blue}{pointerGo.layer}" +
                    $"\n\t{White}Type: {Purple}{pointer.type}";

                PointerValues[menu.AddCategoryEntry(_categoryPointers).Item1] = (pointer, template);

                // Add visualizer
                if (PointerVisualizer.CreateVisualizer(pointer, out var pointerVisualizer)) {
                    menu.CurrentEntityPointerList.Add(pointerVisualizer);
                }
            }


            // Set up CVR Triggers
            menu.ClearCategory(_categoryTriggers);
            TriggerValues.Clear();
            TriggerAasTaskLastTriggered.Clear();
            TriggerAasTasksLastExecuted.Clear();
            TriggerAasStayTasksLastTriggered.Clear();
            TriggerAasStayTasksLastTriggeredValue.Clear();
            var avatarTriggers = avatarGo.GetComponentsInChildren<CVRAdvancedAvatarSettingsTrigger>(true);
            foreach (var trigger in avatarTriggers) {

                 var triggerGo = trigger.gameObject;

                // Generate the template with dynamic params
                var templateArgs = new List<Func<string>>();
                var argId = 0;

                templateArgs.Add(() => triggerGo.activeInHierarchy ? "yes" : "no");
                var template =
                    $"{White}<b>{triggerGo.name}:</b>" +
                    $"\n\t{White}Active: {Blue}{{{argId++}}}" +
                    $"\n\t{White}Class: {Blue}{trigger.GetType().Name}" +
                    $"\n\t{White}Advanced Trigger: {Blue}{(trigger.useAdvancedTrigger ? "yes" : "no")}" +
                    $"\n\t{White}Network Interactable: {Blue}{(trigger.isNetworkInteractable ? "yes" : "no")}" +
                    $"\n\t{White}Particle Interactions: {Blue}{(trigger.allowParticleInteraction ? "yes" : "no")}" +
                    $"\n\t{White}Layer: {Blue}{triggerGo.layer}";

                var allowedPointers = trigger.allowedPointer.Count != 0
                    ? $"\n\t\t{Blue}{string.Join("\n\t\t", trigger.allowedPointer.Select(o => o.gameObject.name))}"
                    : "[None]";
                template += $"\n\t{White}Allowed Pointers: {allowedPointers}";

                var allowedTypes = trigger.allowedTypes.Length != 0
                    ? $"\n\t\t{Blue}{string.Join("\n\t\t", trigger.allowedTypes)}"
                    : "[None]";
                template += $"\n\t{White}Allowed Types: {allowedTypes}";

                string GetTriggerTaskTemplate(CVRAdvancedAvatarSettingsTriggerTask task, string taskType) {
                    string LastTriggered() => TriggerAasTaskLastTriggered.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerAasTaskLastTriggered[task])
                        : "?";

                    string LastExecuted() => TriggerAasTasksLastExecuted.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerAasTasksLastExecuted[task])
                        : "?";

                    templateArgs.Add(() => task.settingValue.ToString());
                    templateArgs.Add(LastTriggered);
                    templateArgs.Add(LastExecuted);
                    return $"\n\t\t{Purple}[{taskType}]" +
                           $"\n\t\t\t{White}Name: {Blue}{task.settingName}" +
                           $"\n\t\t\t{White}Value: {Blue}{{{argId++}}}" +
                           $"\n\t\t\t{White}Delay: {Blue}{task.delay}" +
                           $"\n\t\t\t{White}Hold Time: {Blue}{task.holdTime}" +
                           $"\n\t\t\t{White}Update Method: {Blue}{task.updateMethod.ToString()}" +
                           $"\n\t\t\t{White}Last Triggered: {Blue}{{{argId++}}} secs ago" +
                           $"\n\t\t\t{White}Last Executed: {Blue}{{{argId++}}} secs ago";
                }

                // OnEnter, OnExit, and OnStay Tasks
                template += $"\n\t{White}Trigger Tasks:";
                foreach (var enterTask in trigger.enterTasks) {
                    template += GetTriggerTaskTemplate(enterTask, "OnEnter");
                }
                foreach (var exitTask in trigger.exitTasks) {
                    template += GetTriggerTaskTemplate(exitTask, "OnExit");
                }

                foreach (var stayTask in trigger.stayTasks) {
                    string LastTriggered() => TriggerAasStayTasksLastTriggered.ContainsKey(stayTask)
                        ? Menu.GetTimeDifference(TriggerAasStayTasksLastTriggered[stayTask])
                        : "?";

                    string LastTriggeredValue() => TriggerAasStayTasksLastTriggeredValue.ContainsKey(stayTask)
                        ? TriggerAasStayTasksLastTriggeredValue[stayTask].ToString()
                        : "?";

                    template += $"\n\t\t{Purple}[OnStay]" +
                                $"\n\t\t\t{White}Name: {Blue}{stayTask.settingName}" +
                                $"\n\t\t\t{White}Update Method: {Blue}{stayTask.updateMethod.ToString()}";
                    if (stayTask.updateMethod == CVRAdvancedAvatarSettingsTriggerTaskStay.UpdateMethod.SetFromPosition) {
                        templateArgs.Add(() => stayTask.minValue.ToString());
                        templateArgs.Add(() => stayTask.maxValue.ToString());
                        template += $"\n\t\t\t{White}Min Value: {Blue}{{{argId++}}}" +
                                       $"\n\t\t\t{White}Max Value: {Blue}{{{argId++}}}";
                    }
                    else {
                        templateArgs.Add(() => stayTask.minValue.ToString());
                        template += $"\n\t\t\t{White}Change per sec: {Blue}{{{argId++}}}";
                    }

                    templateArgs.Add(LastTriggered);
                    templateArgs.Add(LastTriggeredValue);
                    template += $"\n\t\t\t{White}Sample direction: {Blue}{trigger.sampleDirection}";
                    template += $"\n\t\t\t{White}Last Triggered: {Blue}{{{argId++}}} secs ago";
                    template += $"\n\t\t\t{White}Last Triggered Value: {Blue}{{{argId++}}}";

                    TriggerValues[menu.AddCategoryEntry(_categoryTriggers).Item1] = (trigger, template, templateArgs);
                }

                // Add the visualizer
                if (TriggerVisualizer.CreateVisualizer(trigger, out var triggerVisualizer)) {
                    menu.CurrentEntityTriggerList.Add(triggerVisualizer);
                }
            }

            // Consume the spawnable changed
            PlayerEntities.HasChanged = false;
        }

        // Iterate the parameter entries and update their values
        if (_mainAnimator != null && _mainAnimator.isInitialized) {
            foreach (var entry in ParameterEntry.Entries) entry.Update();
        }

        // Update cvr spawnable pointer values
        foreach (var pointerValue in PointerValues) {
            var value = pointerValue.Value.Item1.gameObject.activeInHierarchy;
            var text = pointerValue.Key;
            // Ignore if the value didn't change
            if (!Menu.HasValueChanged(text, value)) continue;
            text.SetText(string.Format(pointerValue.Value.Item2, value ? "yes" : "no"));
        }

        // Update cvr trigger values
        foreach (var triggerValue in TriggerValues) {

            var text = triggerValue.Key;

            // Execute to get the values
            var templateArgs = triggerValue.Value.Item3.Select(arg => arg()).ToArray();

            // Check if the param values are all the same as the previous frame
            if (!Menu.HasValueChanged(text, templateArgs)) continue;

            text.SetText(string.Format(triggerValue.Value.Item2, templateArgs));
        }
    }
}
