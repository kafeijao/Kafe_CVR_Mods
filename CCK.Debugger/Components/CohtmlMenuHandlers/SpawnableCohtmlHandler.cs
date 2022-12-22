using System.Globalization;
using ABI_RC.Core.Util;
using ABI.CCK.Components;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using UnityEngine;

namespace CCK.Debugger.Components.CohtmlMenuHandlers;

public class SpawnableCohtmlHandler : ICohtmlHandler {

    static SpawnableCohtmlHandler() {
        PropsData = new LooseList<CVRSyncHelper.PropData>(CVRSyncHelper.Props, propData => propData != null && propData.Spawnable != null);

        // Triggers
        TrackedTriggers = new List<CVRSpawnableTrigger>();
        TriggerSpawnableTaskLastTriggered = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableTasksLastExecuted = new Dictionary<CVRSpawnableTriggerTask, float>();
        TriggerSpawnableStayTasksLastTriggered = new Dictionary<CVRSpawnableTriggerTaskStay, float>();
        TriggerSpawnableStayTasksLastTriggeredValue = new Dictionary<CVRSpawnableTriggerTaskStay, float>();

        // Triggers last time triggered/executed save
        Events.Spawnable.SpawnableTriggerTriggered += task => {
            if (TrackedTriggers.Any(t => t.enterTasks.Contains(task)) ||
                TrackedTriggers.Any(t => t.exitTasks.Contains(task))) {
                TriggerSpawnableTaskLastTriggered[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableTriggerExecuted += task => {
            if (TrackedTriggers.Any(t => t.enterTasks.Contains(task)) ||
                TrackedTriggers.Any(t => t.exitTasks.Contains(task))) {
                TriggerSpawnableTasksLastExecuted[task] = Time.time;
            }
        };
        Events.Spawnable.SpawnableStayTriggerTriggered += task => {
            if (TrackedTriggers.Any(t => t.stayTasks.Contains(task))) {
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

    private Core _core;

    private static readonly Dictionary<int, string> SyncTypeDict;

    private static readonly LooseList<CVRSyncHelper.PropData> PropsData;

    // Attributes
    private static Section _attributeId;
    private static Section _attributeSpawnedByValue;
    private static Section _attributeSyncedByValue;
    private static Section _attributeSyncTypeValue;

    // Parameters
    private static Section _categorySyncedParameters;

    // Main animator Parameters
    private static Section _categoryMainAnimatorParameters;
    private static Animator _mainAnimator;

    // Pickups
    private static Section _categoryPickups;

    // Attachments
    private static Section _categoryAttachments;

    // Pointers
    private static Section _categoryPointers;

    // Triggers
    private static Section _categoryTriggers;
    private static readonly List<CVRSpawnableTrigger> TrackedTriggers;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTaskLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTask, float> TriggerSpawnableTasksLastExecuted;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggered;
    private static readonly Dictionary<CVRSpawnableTriggerTaskStay, float> TriggerSpawnableStayTasksLastTriggeredValue;

    public override void Load(CohtmlMenuController menu) {
        PropsData.ListenPageChangeEvents = true;
        PropsData.HasChanged = true;
    }

    public override void Unload() {
        PropsData.ListenPageChangeEvents = false;
    }

    public override void Update(CohtmlMenuController menu) {

        PropsData.UpdateViaSource();

        var propCount = PropsData.Count;

        var propCurrentIndex = propCount > 0 ? (PropsData.CurrentObjectIndex + 1) : 0;
        _core?.UpdateCore(propCount > 1, $"({propCurrentIndex}/{propCount})", propCount > 0);

        if (propCount < 1) {
            // Let's create a basic core with just the headers
            if (PropsData.HasChanged) {
                // Consume the spawnable changed
                _core = new Core("Props");
                PropsData.HasChanged = false;
                menu.SetCore(_core);
            }

            return;
        }

        var currentSpawnablePropData = PropsData.CurrentObject;
        var currentSpawnable = currentSpawnablePropData.Spawnable;

        // Update the menus if the spawnable changed
        if (PropsData.HasChanged) {

            // Place the highlighter on the first collider found (if present)
            var firstCollider = currentSpawnable.transform.GetComponentInChildren<Collider>();
            if (firstCollider != null) Highlighter.SetTargetHighlight(firstCollider.gameObject);

            // Recreate the core menu
            _core = new Core("Props");

            // Static sections
            var attributesSection = _core.AddSection("Attributes");
            _attributeId = attributesSection.AddSection("Name/ID");
            _attributeSpawnedByValue = attributesSection.AddSection("Spawned By");
            _attributeSyncedByValue = attributesSection.AddSection("Synced By");
            _attributeSyncTypeValue = attributesSection.AddSection("Sync By");

            // Dynamic sections
            _categorySyncedParameters = _core.AddSection("Synced Parameters");
            _categoryMainAnimatorParameters = _core.AddSection("Main Animator Parameters");
            _categoryPickups = _core.AddSection("Pickups");
            _categoryAttachments = _core.AddSection("Attachments");
            _categoryPointers = _core.AddSection("CVR Spawnable Pointers");
            _categoryTriggers = _core.AddSection("CVR Spawnable Triggers");

            // Restore parameters
            foreach (var syncValue in currentSpawnable.syncValues) {
                _categorySyncedParameters.AddSection(syncValue.name).AddValueGetter(() => syncValue.currentValue.ToString(CultureInfo.InvariantCulture));
            }

            // Restore Main Animator Parameters
            _mainAnimator = currentSpawnable.gameObject.GetComponent<Animator>();
            if (_mainAnimator != null) {
                foreach (var parameter in _mainAnimator.parameters) {
                    var parameterEntry = ParameterEntrySection.Get(_mainAnimator, parameter);
                    _categoryMainAnimatorParameters.AddSection(parameter.name).AddValueGetter(() => parameterEntry.GetValue());
                }
            }

            // Restore Pickups
            var pickups = Traverse.Create(currentSpawnable).Field("pickups").GetValue<CVRPickupObject[]>();
            foreach (var pickup in pickups) {
                var pickupSection = _categoryPickups.AddSection(pickup.name);
                pickupSection.AddSection("GrabbedBy").AddValueGetter(() => GetUsername(pickup.grabbedBy));
            }

            // Restore Attachments
            var attachments = Traverse.Create(currentSpawnable).Field("_attachments").GetValue<List<CVRAttachment>>();
            foreach (var attachment in attachments) {
                var attachmentSection = _categoryAttachments.AddSection(attachment.name);

                attachmentSection.AddSection("Is Attached").AddValueGetter(() => ToString(attachment.IsAttached()));

                var attachmentTransform = Traverse.Create(attachment).Field("_attachedTransform").GetValue<Transform>();
                attachmentSection.AddSection("GameObject Name").AddValueGetter(() => attachment.IsAttached() && attachmentTransform != null
                    ? attachmentTransform.gameObject.name
                    : Na);
            }

            // Set up CVR Pointers
            var spawnablePointers = currentSpawnable.GetComponentsInChildren<CVRPointer>(true);
            foreach (var pointer in spawnablePointers) {

                var pointerGo = pointer.gameObject;

                // Create all pointer sections and sub-sections
                var pointerSubSection = _categoryPointers.AddSection(pointerGo.name);
                pointerSubSection.AddSection("Is Active").AddValueGetter(() => ToString(pointer.gameObject.activeInHierarchy));
                pointerSubSection.AddSection("Class", pointer.GetType().Name);
                pointerSubSection.AddSection("Is Internal", ToString(pointer.isInternalPointer));
                pointerSubSection.AddSection("Is Local", ToString(pointer.isLocalPointer));
                pointerSubSection.AddSection("Limit To Filtered Triggers", ToString(pointer.limitToFilteredTriggers));
                pointerSubSection.AddSection("Layer", pointerGo.layer.ToString());
                pointerSubSection.AddSection("Type", pointer.type);

                // Add the visualizer
                if (PointerVisualizer.CreateVisualizer(pointer, out var pointerVisualizer)) {
                    CurrentEntityPointerList.Add(pointerVisualizer);
                }
            }

            // Set up CVR Triggers
            TrackedTriggers.Clear();
            TriggerSpawnableTaskLastTriggered.Clear();
            TriggerSpawnableTasksLastExecuted.Clear();
            TriggerSpawnableStayTasksLastTriggered.Clear();
            TriggerSpawnableStayTasksLastTriggeredValue.Clear();
            var spawnableTriggers = currentSpawnable.GetComponentsInChildren<CVRSpawnableTrigger>(true);
            foreach (var trigger in spawnableTriggers) {

                var triggerGo = trigger.gameObject;
                TrackedTriggers.Add(trigger);

                // Create all spawnable sections and sub-sections
                var spawnableSection = _categoryTriggers.AddSection(triggerGo.name);

                spawnableSection.AddSection("Is Active").AddValueGetter(() => ToString(triggerGo.gameObject.activeInHierarchy));
                spawnableSection.AddSection("Class", trigger.GetType().Name);
                spawnableSection.AddSection("Advanced Trigger", ToString(trigger.useAdvancedTrigger));
                spawnableSection.AddSection("Particle Interactions", ToString(trigger.allowParticleInteraction));
                spawnableSection.AddSection("Layer", triggerGo.layer.ToString());

                var allowedTypesSection = spawnableSection.AddSection("Allowed Types", trigger.allowedTypes.Length == 0 ? Na : "");
                foreach (var triggerAllowedType in trigger.allowedTypes) {
                    allowedTypesSection.AddSection(triggerAllowedType);
                }

                void GetTriggerTaskTemplate(Section parentSection, CVRSpawnableTriggerTask task, int idx) {
                    var name = Na;
                    if (task.spawnable != null) {
                        name = task.spawnable.syncValues.ElementAtOrDefault(task.settingIndex) != null
                            ? task.spawnable.syncValues[task.settingIndex].name
                            : Na;
                    }
                    string LastTriggered() => TriggerSpawnableTaskLastTriggered.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerSpawnableTaskLastTriggered[task])
                        : "?" + " secs ago";
                    string LastExecuted() => TriggerSpawnableTasksLastExecuted.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerSpawnableTasksLastExecuted[task])
                        : "?" + " secs ago";

                    var specificTaskSection = parentSection.AddSection($"#{idx}");
                    specificTaskSection.AddSection($"GO Name", name);
                    specificTaskSection.AddSection($"Value").AddValueGetter(() => task.settingValue.ToString(CultureInfo.InvariantCulture));
                    specificTaskSection.AddSection($"Delay", task.delay.ToString(CultureInfo.InvariantCulture));
                    specificTaskSection.AddSection($"Hold Time", task.holdTime.ToString(CultureInfo.InvariantCulture));
                    specificTaskSection.AddSection($"Update Method", task.updateMethod.ToString());
                    specificTaskSection.AddSection($"Last Triggered").AddValueGetter(LastTriggered);
                    specificTaskSection.AddSection($"Last Executed").AddValueGetter(LastExecuted);
                }

                // OnEnter, OnExit, and OnStay Tasks
                var tasksOnEnterSection = spawnableSection.AddSection("Tasks [OnEnter]", trigger.enterTasks.Count == 0 ? Na : "");
                for (var index = 0; index < trigger.enterTasks.Count; index++) {
                    GetTriggerTaskTemplate(tasksOnEnterSection, trigger.enterTasks[index], index);
                }

                var tasksOnExitSection = spawnableSection.AddSection("Tasks [OnExit]", trigger.exitTasks.Count == 0 ? Na : "");
                for (var index = 0; index < trigger.exitTasks.Count; index++) {
                    GetTriggerTaskTemplate(tasksOnExitSection, trigger.exitTasks[index], index);
                }

                var tasksOnStaySection = spawnableSection.AddSection("Tasks [OnStay]", trigger.stayTasks.Count == 0 ? Na : "");
                for (var index = 0; index < trigger.stayTasks.Count; index++) {
                    var stayTask = trigger.stayTasks[index];
                    var name = Na;
                    if (stayTask.spawnable != null) {
                        name = stayTask.spawnable.syncValues.ElementAtOrDefault(stayTask.settingIndex) != null
                            ? stayTask.spawnable.syncValues[stayTask.settingIndex].name
                            : Na;
                    }
                    string LastTriggered() => TriggerSpawnableStayTasksLastTriggered.ContainsKey(stayTask)
                        ? Menu.GetTimeDifference(TriggerSpawnableStayTasksLastTriggered[stayTask])
                        : "?" + " secs ago";
                    string LastTriggeredValue() => TriggerSpawnableStayTasksLastTriggeredValue.ContainsKey(stayTask)
                        ? TriggerSpawnableStayTasksLastTriggeredValue[stayTask].ToString(CultureInfo.InvariantCulture)
                        : "?";

                    var specificTaskSection = tasksOnStaySection.AddSection($"#{index}");
                    specificTaskSection.AddSection($"GO Name", name);
                    specificTaskSection.AddSection($"Update Method", stayTask.updateMethod.ToString());

                    if (stayTask.updateMethod == CVRSpawnableTriggerTaskStay.UpdateMethod.SetFromPosition) {
                        specificTaskSection.AddSection($"Min Value").AddValueGetter(() => stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                        specificTaskSection.AddSection($"Max Value").AddValueGetter(() => stayTask.maxValue.ToString(CultureInfo.InvariantCulture));
                    }
                    else {
                        specificTaskSection.AddSection($"Change per sec").AddValueGetter(() => stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                    }

                    specificTaskSection.AddSection($"Sample direction", trigger.sampleDirection.ToString());
                    specificTaskSection.AddSection($"Last Triggered").AddValueGetter(LastTriggered);
                    specificTaskSection.AddSection($"Last Triggered Value").AddValueGetter(LastTriggeredValue);
                }

                // Add the visualizer
                if (TriggerVisualizer.CreateVisualizer(trigger, out var triggerVisualizer)) {
                    CurrentEntityTriggerList.Add(triggerVisualizer);
                }
            }

            // Consume the spawnable changed
            PropsData.HasChanged = false;
            menu.SetCore(_core);
        }

        // Update Prop Data Info
        _attributeId.Update(GetSpawnableName(currentSpawnablePropData?.ObjectId));
        _attributeSpawnedByValue.Update(GetUsername(currentSpawnablePropData?.SpawnedBy));
        _attributeSyncedByValue.Update(GetUsername(currentSpawnablePropData?.syncedBy));
        var syncType = currentSpawnablePropData?.syncType;
        var syncTypeString = "N/A";
        var syncTypeValue = "N/A";
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
        _attributeSyncTypeValue.Update(syncTypeValueFull);

        // Update sync parameter values
        _categorySyncedParameters.UpdateFromGetter(true);

        // Update main animator parameter values
        if (_mainAnimator != null) _categoryMainAnimatorParameters.UpdateFromGetter(true);

        // Update pickup values
        _categoryPickups.UpdateFromGetter(true);

        // Update attachment values
        _categoryAttachments.UpdateFromGetter(true);

        // Update cvr spawnable pointer values
        _categoryPointers.UpdateFromGetter(true);

        // Update cvr trigger values
        _categoryTriggers.UpdateFromGetter(true);
    }
}
