using ABI_RC.Core.Util;
using ABI.CCK.Components;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public class SpawnableMenuHandler : MenuHandler {

    static SpawnableMenuHandler() {
        PropsData = new LooseList<CVRSyncHelper.PropData>(CVRSyncHelper.Props, propData => propData != null && propData.Spawnable != null);

        SyncedParametersValues = new Dictionary<CVRSpawnableValue, TextMeshProUGUI>();
        PickupsValues = new Dictionary<CVRPickupObject, TextMeshProUGUI>();
        AttachmentsValues = new Dictionary<CVRAttachment, TextMeshProUGUI>();

        // Pointers
        PointerValues = new Dictionary<TextMeshProUGUI, (CVRPointer, string)>();

        // Triggers
        TriggerValues = new Dictionary<TextMeshProUGUI, (CVRSpawnableTrigger, string, List<Func<string>>)>();
        TriggerSpawnableTaskLastTriggered = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableTasksLastExecuted = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableStayTasksLastTriggered = new Dictionary<CVRSpawnableTriggerTaskStay, float>();
        TriggerSpawnableStayTasksLastTriggeredValue = new Dictionary<CVRSpawnableTriggerTaskStay, float>();

        // Triggers last time triggered/executed save
        Events.Spawnable.SpawnableTriggerTriggered += task => {
            if (TriggerValues.Values.Any(t => t.Item1.enterTasks.Contains(task)) ||
                TriggerValues.Values.Any(t => t.Item1.exitTasks.Contains(task))) {
                TriggerSpawnableTaskLastTriggered[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableTriggerExecuted += task => {
            if (TriggerValues.Values.Any(t => t.Item1.enterTasks.Contains(task)) ||
                TriggerValues.Values.Any(t => t.Item1.exitTasks.Contains(task))) {
                TriggerSpawnableTasksLastExecuted[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableStayTriggerTriggered += task => {
            if (TriggerValues.Values.Any(t => t.Item1.stayTasks.Contains(task)) ||
                TriggerValues.Values.Any(t => t.Item1.stayTasks.Contains(task))) {
                TriggerSpawnableStayTasksLastTriggered[task] = Time.time;
                TriggerSpawnableStayTasksLastTriggeredValue[task] = task.spawnable.GetValue(task.settingIndex);
            }
        };

        SyncTypeDict = new() {
            { 1, "GrabbedByMe" },
            { 3, "TeleGrabbed" },
            { 2, "Attached" },
        };
    }

    private static readonly Dictionary<int, string> SyncTypeDict;

    private static readonly LooseList<CVRSyncHelper.PropData> PropsData;

    // Attributes
    private static TextMeshProUGUI _attributeId;
    private static TextMeshProUGUI _attributeSpawnedByValue;
    private static TextMeshProUGUI _attributeSyncedByValue;
    private static TextMeshProUGUI _attributeSyncTypeValue;

    // Parameters
    private static GameObject _categorySyncedParameters;
    private static readonly Dictionary<CVRSpawnableValue, TextMeshProUGUI> SyncedParametersValues;

    // Main animator Parameters
    private static GameObject _categoryMainAnimatorParameters;
    private static Animator _mainAnimator;

    // Pickups
    private static GameObject _categoryPickups;
    private static readonly Dictionary<CVRPickupObject, TextMeshProUGUI> PickupsValues;

    // Attachments
    private static GameObject _categoryAttachments;
    private static readonly Dictionary<CVRAttachment, TextMeshProUGUI> AttachmentsValues;

    // Pointers
    private static GameObject _categoryPointers;
    private static readonly Dictionary<TextMeshProUGUI, (CVRPointer, string)> PointerValues;

    // Triggers
    private static GameObject _categoryTriggers;
    private static readonly Dictionary<TextMeshProUGUI, (CVRSpawnableTrigger, string, List<Func<string>>)> TriggerValues;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTaskLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTasksLastExecuted;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggeredValue;

    public override void Load(Menu menu) {

        menu.AddNewDebugger("Props");

        var categoryAttributes = menu.AddCategory("Attributes");
        _attributeId = menu.AddCategoryEntry(categoryAttributes, "Name/ID:");
        _attributeSpawnedByValue = menu.AddCategoryEntry(categoryAttributes, "Spawned By:");
        _attributeSyncedByValue = menu.AddCategoryEntry(categoryAttributes, "Synced By:");
        _attributeSyncTypeValue = menu.AddCategoryEntry(categoryAttributes, "Sync Type:");

        _categorySyncedParameters = menu.AddCategory("Synced Parameters");

        _categoryMainAnimatorParameters = menu.AddCategory("Main Animator Parameters");

        _categoryPickups = menu.AddCategory("Pickups");

        _categoryAttachments = menu.AddCategory("Attachments");

        _categoryPointers = menu.AddCategory("CVR Spawnable Pointers");

        _categoryTriggers = menu.AddCategory("CVR Spawnable Triggers");

        PropsData.ListenPageChangeEvents = true;
        PropsData.HasChanged = true;
    }

    public override void Unload() {
        PropsData.ListenPageChangeEvents = false;
    }

    public override void Update(Menu menu) {

        PropsData.UpdateViaSource();

        var propCount = PropsData.Count;

        menu.SetControlsExtra($"({PropsData.CurrentObjectIndex+1}/{propCount})");

        menu.ToggleCategories(propCount > 0);

        menu.ShowControls(propCount > 1);

        if (propCount < 1) return;

        var currentSpawnablePropData = PropsData.CurrentObject;
        var currentSpawnable = currentSpawnablePropData.Spawnable;

        // Prop Data Info
        if (Menu.HasValueChanged(_attributeId, currentSpawnablePropData?.ObjectId)) _attributeId.SetText(menu.GetSpawnableName(currentSpawnablePropData?.ObjectId));
        if (Menu.HasValueChanged(_attributeSpawnedByValue, currentSpawnablePropData?.SpawnedBy)) _attributeSpawnedByValue.SetText(menu.GetUsername(currentSpawnablePropData?.SpawnedBy));
        if (Menu.HasValueChanged(_attributeSyncedByValue, currentSpawnablePropData?.syncedBy)) _attributeSyncedByValue.SetText(menu.GetUsername(currentSpawnablePropData?.syncedBy));
        var syncType = currentSpawnablePropData?.syncType;
        string syncTypeString = "N/A";
        string syncTypeValue = "N/A";
        if (syncType.HasValue) {
            syncTypeValue = syncType.Value.ToString();
            if (SyncTypeDict.ContainsKey(syncType.Value)) {
                syncTypeString = SyncTypeDict[syncType.Value];
            }
            else {
                syncTypeString = currentSpawnable.isPhysicsSynced ? "Physics" : "None";
            }
        }
        var syncTypeValueFull = $"{syncTypeValue} [{syncTypeString}?]";
        if (Menu.HasValueChanged(_attributeSyncTypeValue, syncTypeValueFull)) _attributeSyncTypeValue.SetText(syncTypeValueFull);

        // Update the menus if the spawnable changed
        if (PropsData.HasChanged) {

            // Place the highlighter on the first collider found (if present)
            var firstCollider = currentSpawnable.transform.GetComponentInChildren<Collider>();
            if (firstCollider != null) Highlighter.SetTargetHighlight(firstCollider.gameObject);

            // Restore parameters
            menu.ClearCategory(_categorySyncedParameters);
            SyncedParametersValues.Clear();
            foreach (var syncValue in currentSpawnable.syncValues) {
                var tmpParamValue = menu.AddCategoryEntry(_categorySyncedParameters, syncValue.name);
                SyncedParametersValues[syncValue] = tmpParamValue;
            }

            // Restore Main Animator Parameters
            menu.ClearCategory(_categoryMainAnimatorParameters);
            ParameterEntry.Entries.Clear();
            _mainAnimator = currentSpawnable.gameObject.GetComponent<Animator>();
            if (_mainAnimator != null) {
                foreach (var parameter in _mainAnimator.parameters) {
                    var tmpPickupValue = menu.AddCategoryEntry(_categoryMainAnimatorParameters, parameter.name);
                    ParameterEntry.Add(_mainAnimator, parameter, tmpPickupValue);
                }
            }

            // Restore Pickups
            menu.ClearCategory(_categoryPickups);
            PickupsValues.Clear();
            var pickups = Traverse.Create(currentSpawnable).Field("pickups").GetValue<CVRPickupObject[]>();
            foreach (var pickup in pickups) {
                var tmpPickupValue = menu.AddCategoryEntry(_categoryPickups, pickup.name);
                PickupsValues[pickup] = tmpPickupValue;
            }

            // Restore Attachments
            menu.ClearCategory(_categoryAttachments);
            AttachmentsValues.Clear();
            var attachments = Traverse.Create(currentSpawnable).Field("_attachments").GetValue<List<CVRAttachment>>();
            foreach (var attachment in attachments) {
                var tmpPickupValue = menu.AddCategoryEntry(_categoryAttachments, attachment.name);
                AttachmentsValues[attachment] = tmpPickupValue;
            }

            // Set up CVR Pointers
            menu.ClearCategory(_categoryPointers);
            PointerValues.Clear();
            var spawnablePointers = currentSpawnable.GetComponentsInChildren<CVRPointer>(true);
            foreach (var pointer in spawnablePointers) {

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

                // Add the visualizer
                if (PointerVisualizer.CreateVisualizer(pointer, out var pointerVisualizer)) {
                    menu.CurrentEntityPointerList.Add(pointerVisualizer);
                }
            }

            // Set up CVR Triggers
            menu.ClearCategory(_categoryTriggers);
            TriggerValues.Clear();
            TriggerSpawnableTaskLastTriggered.Clear();
            TriggerSpawnableTasksLastExecuted.Clear();
            TriggerSpawnableStayTasksLastTriggered.Clear();
            TriggerSpawnableStayTasksLastTriggeredValue.Clear();
            var spawnableTriggers = currentSpawnable.GetComponentsInChildren<CVRSpawnableTrigger>(true);
            foreach (var trigger in spawnableTriggers) {

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
                    $"\n\t{White}Particle Interactions: {Blue}{(trigger.allowParticleInteraction ? "yes" : "no")}" +
                    $"\n\t{White}Layer: {Blue}{triggerGo.layer}";

                var allowedTypes = trigger.allowedTypes.Length != 0
                    ? $"\n\t\t{Blue}{string.Join("\n\t\t", trigger.allowedTypes)}"
                    : "[None]";
                template += $"\n\t{White}Allowed Types: {allowedTypes}";

                string GetTriggerTaskTemplate(CVRSpawnableTriggerTask task, string taskType) {
                    var name = "-none-";
                    if (task.spawnable != null) {
                        name = task.spawnable.syncValues.ElementAtOrDefault(task.settingIndex) != null
                            ? task.spawnable.syncValues[task.settingIndex].name
                            : "-none-";
                    }
                    string LastTriggered() => TriggerSpawnableTaskLastTriggered.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerSpawnableTaskLastTriggered[task])
                        : "?";
                    string LastExecuted() => TriggerSpawnableTasksLastExecuted.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerSpawnableTasksLastExecuted[task])
                        : "?";

                    templateArgs.Add(() => task.settingValue.ToString());
                    templateArgs.Add(LastTriggered);
                    templateArgs.Add(LastExecuted);
                    return $"\n\t\t{Purple}[{taskType}]" +
                           $"\n\t\t\t{White}Name: {Blue}{name}" +
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
                    var name = "-none-";
                    if (stayTask.spawnable != null) {
                        name = stayTask.spawnable.syncValues.ElementAtOrDefault(stayTask.settingIndex) != null
                            ? stayTask.spawnable.syncValues[stayTask.settingIndex].name
                            : "-none-";
                    }
                    string LastTriggered() => TriggerSpawnableStayTasksLastTriggered.ContainsKey(stayTask)
                        ? Menu.GetTimeDifference(TriggerSpawnableStayTasksLastTriggered[stayTask])
                        : "?";
                    string LastTriggeredValue() => TriggerSpawnableStayTasksLastTriggeredValue.ContainsKey(stayTask)
                        ? TriggerSpawnableStayTasksLastTriggeredValue[stayTask].ToString()
                        : "?";

                    template += $"\n\t\t{Purple}[OnStay]" +
                                $"\n\t\t\t{White}Name: {Blue}{name}" +
                                $"\n\t\t\t{White}Update Method: {Blue}{stayTask.updateMethod.ToString()}";
                    if (stayTask.updateMethod == CVRSpawnableTriggerTaskStay.UpdateMethod.SetFromPosition) {
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

                }

                // Associate the trigger template and values to the TMP Text
                TriggerValues[menu.AddCategoryEntry(_categoryTriggers).Item1] = (trigger, template, templateArgs);

                // Add the visualizer
                if (TriggerVisualizer.CreateVisualizer(trigger, out var triggerVisualizer)) {
                    menu.CurrentEntityTriggerList.Add(triggerVisualizer);
                }
            }

            // Consume the spawnable changed
            PropsData.HasChanged = false;
        }

        // Update sync parameter values
        foreach (var syncedParametersValue in SyncedParametersValues) {
            // Ignore if the value didn't change
            if (!Menu.HasValueChanged(syncedParametersValue.Value, syncedParametersValue.Key.currentValue)) continue;
            syncedParametersValue.Value.SetText(syncedParametersValue.Key.currentValue.ToString());
        }

        // Update main animator parameter values
        if (_mainAnimator != null) {
            foreach (var entry in ParameterEntry.Entries) entry.Update();
        }

        // Update pickup values
        foreach (var pickupValue in PickupsValues) {
            // Ignore if the value didn't change
            if (!Menu.HasValueChanged(pickupValue.Value, pickupValue.Key.grabbedBy)) continue;
            pickupValue.Value.SetText($" GrabbedBy: {menu.GetUsername(pickupValue.Key.grabbedBy)}");
        }

        // Update attachment values
        foreach (var attachmentsValue in AttachmentsValues) {
            var attachedTransformName = "";
            var isAttached = attachmentsValue.Key.IsAttached();
            if (isAttached) {
                var attTrns = Traverse.Create(attachmentsValue.Key).Field("_attachedTransform").GetValue<Transform>();
                if (attTrns != null) {
                    attachedTransformName = $" [{attTrns.gameObject.name}]";
                }
            }

            var text = attachmentsValue.Value;
            // If no values have changed -> ignore
            if (!Menu.HasValueChanged(text, attachedTransformName) &&
                !Menu.HasValueChanged(text, isAttached)) continue;
            text.SetText($"Attached: {(isAttached ? "yes" : "no")}{attachedTransformName}");
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
