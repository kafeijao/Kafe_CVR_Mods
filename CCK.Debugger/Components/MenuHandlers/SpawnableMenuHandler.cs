using ABI_RC.Core.Util;
using ABI.CCK.Components;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CCK.Debugger.Components.MenuHandlers;

public class SpawnableMenuHandler : IMenuHandler {

    static SpawnableMenuHandler() {
        PropsData = new LooseList<CVRSyncHelper.PropData>(CVRSyncHelper.Props, propData => propData != null && propData.Spawnable != null);

        SyncedParametersValues = new Dictionary<CVRSpawnableValue, TextMeshProUGUI>();
        PickupsValues = new Dictionary<CVRPickupObject, TextMeshProUGUI>();
        AttachmentsValues = new Dictionary<CVRAttachment, TextMeshProUGUI>();

        // Pointers
        PointerValues = new Dictionary<CVRPointer, TextMeshProUGUI>();

        // Triggers
        TriggerValues = new Dictionary<CVRSpawnableTrigger, TextMeshProUGUI>();
        TriggerSpawnableTaskLastTriggered = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableTasksLastExecuted = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableStayTasksLastTriggered = new Dictionary<CVRSpawnableTriggerTaskStay, float>();
        TriggerSpawnableStayTasksLastTriggeredValue = new Dictionary<CVRSpawnableTriggerTaskStay, float>();

        // Triggers last time triggered/executed save
        Events.Spawnable.SpawnableTriggerTriggered += task => {
            if (TriggerValues.Keys.Any(t => t.enterTasks.Contains(task)) ||
                TriggerValues.Keys.Any(t => t.exitTasks.Contains(task))) {
                TriggerSpawnableTaskLastTriggered[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableTriggerExecuted += task => {
            if (TriggerValues.Keys.Any(t => t.enterTasks.Contains(task)) ||
                TriggerValues.Keys.Any(t => t.exitTasks.Contains(task))) {
                TriggerSpawnableTasksLastExecuted[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableStayTriggerTriggered += task => {
            if (TriggerValues.Keys.Any(t => t.stayTasks.Contains(task)) ||
                TriggerValues.Keys.Any(t => t.stayTasks.Contains(task))) {
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

    // Colors
    private const string White = "<color=white>";
    private const string Blue = "<#00AFFF>";
    private const string Purple = "<#A000C8>";

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
    private static readonly Dictionary<CVRPointer, TextMeshProUGUI> PointerValues;

    // Triggers
    private static GameObject _categoryTriggers;
    private static readonly Dictionary<CVRSpawnableTrigger, TextMeshProUGUI> TriggerValues;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTaskLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTasksLastExecuted;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggeredValue;

    public void Load(Menu menu) {

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

    public void Unload() {
        PropsData.ListenPageChangeEvents = false;
    }

    public void Update(Menu menu) {

        PropsData.UpdateViaSource();

        var propCount = PropsData.Count;

        menu.SetControlsExtra($"({PropsData.CurrentObjectIndex+1}/{propCount})");

        menu.ToggleCategories(propCount > 0);

        menu.ShowControls(propCount > 1);

        if (propCount < 1) return;

        var currentSpawnablePropData = PropsData.CurrentObject;
        var currentSpawnable = currentSpawnablePropData.Spawnable;

        // Prop Data Info
        _attributeId.SetText(menu.GetSpawnableName(currentSpawnablePropData?.ObjectId));
        _attributeSpawnedByValue.SetText(menu.GetUsername(currentSpawnablePropData?.SpawnedBy));
        _attributeSyncedByValue.SetText(menu.GetUsername(currentSpawnablePropData?.syncedBy));
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
        _attributeSyncTypeValue.SetText($"{syncTypeValue} [{syncTypeString}?]");

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
                PointerValues[pointer] = menu.AddCategoryEntry(_categoryPointers).Item1;
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
                TriggerValues[trigger] = menu.AddCategoryEntry(_categoryTriggers).Item1;
            }

            // Consume the spawnable changed
            PropsData.HasChanged = false;
        }

        // Update sync parameter values
        foreach (var syncedParametersValue in SyncedParametersValues) {
            syncedParametersValue.Value.SetText(syncedParametersValue.Key.currentValue.ToString());
        }

        // Update main animator parameter values
        if (_mainAnimator != null) {
            foreach (var entry in ParameterEntry.Entries) entry.Update();
        }

        // Update pickup values
        foreach (var pickupValue in PickupsValues) {
            pickupValue.Value.SetText($" GrabbedBy: {menu.GetUsername(pickupValue.Key.grabbedBy)}");
        }

        // Update attachment values
        foreach (var attachmentsValue in AttachmentsValues) {
            var attachedTransformName = "";
            if (attachmentsValue.Key.IsAttached()) {
                var attTrns = Traverse.Create(attachmentsValue.Key).Field("_attachedTransform").GetValue<Transform>();
                if (attTrns != null) {
                    attachedTransformName = $" [{attTrns.gameObject.name}]";
                }
            }
            var attachedStr = attachmentsValue.Key.IsAttached() ? "yes" : "no";
            attachmentsValue.Value.SetText($"Attached: {attachedStr}{attachedTransformName}");
        }

        // Update cvr spawnable pointer values
        foreach (var pointerValue in PointerValues) {
            var pointer = pointerValue.Key;
            var pointerGo = pointer.gameObject;
            pointerValue.Value.SetText(
                $"{White}<b>{pointerGo.name}:</b>" +
                $"\n\t{White}Is Active: {Blue}{(pointerGo.activeInHierarchy ? "yes" : "no")}" +
                $"\n\t{White}Is Internal: {Blue}{(pointer.isInternalPointer ? "yes" : "no")}" +
                $"\n\t{White}Is Local: {Blue}{(pointer.isLocalPointer ? "yes" : "no")}" +
                $"\n\t{White}Limit To Filtered Triggers: {Blue}{(pointer.limitToFilteredTriggers ? "yes" : "no")}" +
                $"\n\t{White}Layer: {Blue}{pointerGo.layer}" +
                $"\n\t{White}Type: {Blue}{pointer.type}");
        }

        // Update cvr trigger values
        foreach (var triggerValue in TriggerValues) {
            var trigger = triggerValue.Key;
            var triggerGo = trigger.gameObject;
            var triggerInfo =
                $"{White}<b>{triggerGo.name}:</b>" +
                $"\n\t{White}Active: {Blue}{(triggerGo.activeInHierarchy ? "yes" : "no")}" +
                $"\n\t{White}Advanced Trigger: {Blue}{(trigger.useAdvancedTrigger ? "yes" : "no")}" +
                $"\n\t{White}Particle Interactions: {Blue}{(trigger.allowParticleInteraction ? "yes" : "no")}" +
                $"\n\t{White}Layer: {Blue}{triggerGo.layer}";
            var allowedTypes = trigger.allowedTypes.Length != 0
                ? $"\n\t\t{Blue}{string.Join("\n\t\t", trigger.allowedTypes)}"
                : "[None]";
            triggerInfo += $"\n\t{White}Allowed Types: {allowedTypes}";

            // OnEnter, OnExit, and OnStay Tasks
            triggerInfo += $"\n\t{White}Trigger Tasks:";
            foreach (var enterTask in trigger.enterTasks) {
                var name = enterTask.spawnable.syncValues.ElementAtOrDefault(enterTask.settingIndex) != null
                    ? enterTask.spawnable.syncValues[enterTask.settingIndex].name
                    : "-none-";
                var lastTriggered = TriggerSpawnableTaskLastTriggered.ContainsKey(enterTask)
                    ? (Time.time - TriggerSpawnableTaskLastTriggered[enterTask]).ToString("0.00")
                    : "?";
                var lastExecuted = TriggerSpawnableTasksLastExecuted.ContainsKey(enterTask)
                    ? (Time.time - TriggerSpawnableTasksLastExecuted[enterTask]).ToString("0.00")
                    : "?";
                    triggerInfo += $"\n\t\t{Purple}[OnEnter]" +
                                   $"\n\t\t\t{White}Name: {Blue}{name}" +
                                   $"\n\t\t\t{White}Value: {Blue}{enterTask.settingValue}" +
                                   $"\n\t\t\t{White}Delay: {Blue}{enterTask.delay}" +
                                   $"\n\t\t\t{White}Hold Time: {Blue}{enterTask.holdTime}" +
                                   $"\n\t\t\t{White}Update Method: {Blue}{enterTask.updateMethod.ToString()}" +
                                   $"\n\t\t\t{White}Last Triggered: {Blue}{lastTriggered} secs ago" +
                                   $"\n\t\t\t{White}Last Executed: {Blue}{lastExecuted} secs ago";
            }
            foreach (var exitTask in trigger.exitTasks) {
                var name = exitTask.spawnable.syncValues.ElementAtOrDefault(exitTask.settingIndex) != null
                    ? exitTask.spawnable.syncValues[exitTask.settingIndex].name
                    : "-none-";
                var lastTriggered = TriggerSpawnableTaskLastTriggered.ContainsKey(exitTask)
                    ? (Time.time - TriggerSpawnableTaskLastTriggered[exitTask]).ToString("0.00")
                    : "?";
                var lastExecuted = TriggerSpawnableTasksLastExecuted.ContainsKey(exitTask)
                    ? (Time.time - TriggerSpawnableTasksLastExecuted[exitTask]).ToString("0.00")
                    : "?";
                triggerInfo += $"\n\t\t{Purple}[OnExit]" +
                               $"\n\t\t\t{White}Name: {Blue}{name}" +
                               $"\n\t\t\t{White}Value: {Blue}{exitTask.settingValue}" +
                               $"\n\t\t\t{White}Delay: {Blue}{exitTask.delay}" +
                               $"\n\t\t\t{White}Update Method: {Blue}{exitTask.updateMethod.ToString()}" +
                               $"\n\t\t\t{White}Last Triggered: {Blue}{lastTriggered} secs ago" +
                               $"\n\t\t\t{White}Last Executed: {Blue}{lastExecuted} secs ago";
            }
            foreach (var stayTask in trigger.stayTasks) {
                var name = stayTask.spawnable.syncValues.ElementAtOrDefault(stayTask.settingIndex) != null
                    ? stayTask.spawnable.syncValues[stayTask.settingIndex].name
                    : "-none-";
                var lastTriggered = TriggerSpawnableStayTasksLastTriggered.ContainsKey(stayTask)
                    ? (Time.time - TriggerSpawnableStayTasksLastTriggered[stayTask]).ToString("0.00")
                    : "?";
                var lastTriggeredValue = TriggerSpawnableStayTasksLastTriggeredValue.ContainsKey(stayTask)
                    ? TriggerSpawnableStayTasksLastTriggeredValue[stayTask].ToString()
                    : "?";
                triggerInfo += $"\n\t\t{Purple}[OnStay]" +
                               $"\n\t\t\t{White}Name: {Blue}{name}" +
                               $"\n\t\t\t{White}Update Method: {Blue}{stayTask.updateMethod.ToString()}";
                if (stayTask.updateMethod == CVRSpawnableTriggerTaskStay.UpdateMethod.SetFromPosition) {
                    triggerInfo += $"\n\t\t\t{White}Min Value: {Blue}{stayTask.minValue}" +
                                   $"\n\t\t\t{White}Max Value: {Blue}{stayTask.maxValue}";
                }
                else {
                    triggerInfo += $"\n\t\t\t{White}Change per sec: {Blue}{stayTask.minValue}";
                }
                triggerInfo += $"\n\t\t\t{White}Sample direction: {Blue}{trigger.sampleDirection}";
                triggerInfo += $"\n\t\t\t{White}Last Triggered: {Blue}{lastTriggered} secs ago";
                triggerInfo += $"\n\t\t\t{White}Last Triggered Value: {Blue}{lastTriggeredValue}";
            }

            triggerValue.Value.SetText(triggerInfo);
        }
    }
}
