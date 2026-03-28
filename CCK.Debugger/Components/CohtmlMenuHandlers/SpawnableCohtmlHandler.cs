using System.Globalization;
using ABI_RC.Core.Util;
using ABI.CCK.Components;
using Kafe.CCK.Debugger.Components.PointerVisualizers;
using Kafe.CCK.Debugger.Components.TriggerVisualizers;
using Kafe.CCK.Debugger.Entities;
using Kafe.CCK.Debugger.Utils;
using NAK.Contacts;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;

public class SpawnableCohtmlHandler : ICohtmlHandler
{
    static SpawnableCohtmlHandler()
    {
        PropsData = new LooseList<CVRSyncHelper.PropData>(CVRSyncHelper.Props, propData => propData != null && propData.Spawnable != null);

        // Triggers
        TrackedTriggers = new HashSet<TriggerToContact>();
        TriggerTaskLastTriggered = new Dictionary<TriggerToContact.ContactTriggerTask, float>();
        TriggerTasksLastExecuted = new Dictionary<TriggerToContact.ContactTriggerTask, float>();
        TriggerStayTasksLastTriggered = new Dictionary<TriggerToContact.ContactTriggerStayTask, float>();
        TriggerStayTasksLastTriggeredValue = new Dictionary<TriggerToContact.ContactTriggerStayTask, float>();

        // Triggers last time triggered/executed save
        Events.Spawnable.SpawnableTriggerCollided += (trigger, triggerTask) =>
        {
            if (TrackedTriggers.Contains(trigger))
                TriggerTaskLastTriggered[triggerTask] = Time.time;
        };
        Events.Spawnable.SpawnableTriggerExecuted += (trigger, triggerTask) =>
        {
            if (TrackedTriggers.Contains(trigger))
                TriggerTasksLastExecuted[triggerTask] = Time.time;
        };
        Events.Spawnable.SpawnableStayTriggerExecuted += (trigger, triggerTask) =>
        {
            if (!TrackedTriggers.Contains(trigger)) return;
            TriggerStayTasksLastTriggered[triggerTask] = Time.time;
            TriggerStayTasksLastTriggeredValue[triggerTask] = triggerTask.spawnable.GetValue(triggerTask.parameterIndex);
        };

        SyncTypeDict = new Dictionary<int, string>
        {
            { -1, "None" },
            { 0, "Physics" },
            { 1, "Grabbed" },
            { 2, "Attached" },
            { 3, "Tele-Grabbed" },
            { 4, "Seat" },
            { 5, "Newton?" },
            { 6, "Animator Driver" },
        };

        Events.DebuggerMenu.SpawnableLoaded += (spawnable, isLoaded) =>
        {
            // If the inspected element is destroyed, lets reset
            var isActuallyLoaded = isLoaded || PropsData.CurrentObject == null || PropsData.CurrentObject.Spawnable != spawnable;
            ResetProp(isActuallyLoaded);
        };

        Events.DebuggerMenu.EntityChanged += () =>
        {
            if (!PropsData.ListenPageChangeEvents) return;
            ResetProp();
        };
    }

    // Internals
    private Core _core;

    private static bool _propChanged;
    private static bool _hasLoaded;

    // Triggers
    private static readonly Dictionary<int, string> SyncTypeDict;
    private static readonly LooseList<CVRSyncHelper.PropData> PropsData;
    private static readonly HashSet<TriggerToContact> TrackedTriggers;
    private static readonly Dictionary<TriggerToContact.ContactTriggerTask, float> TriggerTaskLastTriggered;
    private static readonly Dictionary<TriggerToContact.ContactTriggerTask, float> TriggerTasksLastExecuted;

    private static readonly Dictionary<TriggerToContact.ContactTriggerStayTask, float>
        TriggerStayTasksLastTriggered;

    private static readonly Dictionary<TriggerToContact.ContactTriggerStayTask, float>
        TriggerStayTasksLastTriggeredValue;

    protected override void Load()
    {
        PropsData.ListenPageChangeEvents = true;
        ResetProp();
    }

    protected override void Unload()
    {
        PropsData.ListenPageChangeEvents = false;
    }

    protected override void Reset()
    {
        PropsData.Reset();
    }

    private static void ResetProp()
    {
        var prop = PropsData.CurrentObject?.Spawnable;
        ResetProp(prop != null && Events.DebuggerMenu.IsSpawnableLoaded(prop));
    }

    private static void ResetProp(bool isLoaded)
    {
        _propChanged = true;
        _hasLoaded = isLoaded;
    }

    public override void Update()
    {
        UpdateHeader(out var currentSpawnable, out var currentSpawnablePropData);

        // Handle prop changes
        if (_propChanged)
        {
            // If the prop changed, let's reset our entities
            ResetCurrentEntities();

            // Recreate the core menu
            _core = new Core("Props");

            // If has a prop selected
            if (currentSpawnable)
            {
                // Set up the base part of the menu
                SetupPropBase(currentSpawnable, currentSpawnablePropData, _hasLoaded);

                // Set up the body part of the menu
                if (_hasLoaded) SetupPropBody(currentSpawnable);
            }

            // Consume the prop change and send core create update
            _propChanged = false;
            Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(_core);
        }

        // Update button's states
        Core.UpdateButtonsState();

        // Update all sections from getters
        Core.UpdateSectionsFromGetters();
    }


    private void UpdateHeader(out CVRSpawnable currentSpawnable, out CVRSyncHelper.PropData currentSpawnablePropData)
    {
        PropsData.UpdateViaSource();

        var propCount = PropsData.Count;

        var propCurrentIndex = propCount > 0 ? (PropsData.CurrentObjectIndex + 1) : 0;
        _core?.UpdateCore(propCount > 1, $"{propCurrentIndex}/{propCount}", propCount > 0);

        currentSpawnablePropData = PropsData.CurrentObject;
        currentSpawnable = currentSpawnablePropData?.Spawnable;
    }

    private void SetupPropBase(CVRSpawnable currentSpawnable, CVRSyncHelper.PropData currentSpawnablePropData,
        bool isLoaded)
    {
        // Attributes sections
        var attributesSection = _core.AddSection("Attributes", false);
        attributesSection.AddSection("Name/ID")
            .AddValueGetter(() => GetSpawnableName(currentSpawnablePropData.ObjectId));
        attributesSection.AddSection("Is Loading").AddValueGetter(() => ToString(!isLoaded));
        attributesSection.AddSection("Spawned By")
            .AddValueGetter(() => GetUsername(currentSpawnablePropData.SpawnedBy));
        attributesSection.AddSection("Synced By").AddValueGetter(() => GetUsername(currentSpawnablePropData.syncedBy));
        attributesSection.AddSection("Sync Type").AddValueGetter(() =>
        {
            // Todo: Update the prop data!!!
            var syncType = currentSpawnable.SyncType;
            if (syncType == 0 && !currentSpawnable.isPhysicsSynced) syncType = -1;
            var syncTypeString = SyncTypeDict.TryGetValue(syncType, out var value) ? value : "Unknown";
            return $"[{currentSpawnable.SyncType.ToString()}] {syncTypeString}";
        });
    }

    private void SetupPropBody(CVRSpawnable currentSpawnable)
    {
        // Place the highlighter on the first collider found (if present)
        var firstCollider = currentSpawnable.transform.GetComponentInChildren<Collider>();
        if (firstCollider != null) Highlighter.SetTargetHighlight(firstCollider.gameObject);

        // Setup buttons
        var pointerButton = _core.AddButton(new Button(Button.ButtonType.Pointer, false, false));
        var triggerButton = _core.AddButton(new Button(Button.ButtonType.Trigger, false, false));

        // Setup button Handlers
        pointerButton.StateUpdater = button =>
            button.IsOn = CurrentEntityPointerList.Count > 0 && CurrentEntityPointerList.All(vis => vis.enabled);
        pointerButton.ClickHandler = button =>
        {
            button.IsOn = !button.IsOn;
            CurrentEntityPointerList.ForEach(vis => { vis.enabled = button.IsOn; });
        };
        triggerButton.StateUpdater = button =>
            button.IsOn = CurrentEntityTriggerList.Count > 0 && CurrentEntityTriggerList.All(vis => vis.enabled);
        triggerButton.ClickHandler = button =>
        {
            button.IsOn = !button.IsOn;
            CurrentEntityTriggerList.ForEach(vis => { vis.enabled = button.IsOn; });
        };

        // Dynamic sections
        var categorySyncedParameters = _core.AddSection("Animator Synced Parameters", true);
        var categoryAnimatorsParameters = _core.AddSection("Animators Parameters", true);
        var categoryAnimatorsLayers = _core.AddSection("Animators Layers", true);
        var categoryPickups = _core.AddSection("Pickups", true);
        var categoryAttachments = _core.AddSection("Attachments", true);
        var categoryPointers = _core.AddSection("CVR Spawnable Pointers", true);
        var categoryTriggers = _core.AddSection("CVR Spawnable Triggers", true);

        // Restore synced parameters
        var syncedParametersPerAnimator = new Dictionary<Animator, Section>();
        Section nullAnimatorSection = null;
        foreach (var syncValue in currentSpawnable.syncValues)
        {
            if (syncValue.animator == null)
            {
                nullAnimatorSection ??= categorySyncedParameters.AddSection("N/A - Animator not selected", "", true);
                nullAnimatorSection.AddSection(syncValue.name).AddValueGetter(() =>
                    syncValue.currentValue.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                if (!syncedParametersPerAnimator.TryGetValue(syncValue.animator, out var section))
                {
                    section = categorySyncedParameters.AddSection(syncValue.animator.name, "", true);
                    syncedParametersPerAnimator[syncValue.animator] = section;
                }

                section.AddSection(syncValue.name)
                    .AddValueGetter(() => syncValue.currentValue.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Restore Animator's Parameters/Layers
        foreach (var animator in currentSpawnable.gameObject.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null) continue;

            var categoryAnimatorParameters = categoryAnimatorsParameters.AddSection(animator.name, "", true);
            var sectionAnimatorLayers = categoryAnimatorsLayers.AddSection(animator.name, "", true);

            // Setup parameters
            foreach (var parameter in animator.parameters)
            {
                var parameterEntry = ParameterEntrySection.Get(animator, parameter);
                string GetParamValue() => parameterEntry.GetValue();
                categoryAnimatorParameters.AddSection(parameter.name).AddValueGetter(GetParamValue);
            }

            // Set up the animator layers
            const string noClipsText = "Playing no Clips";
            for (var i = 0; i < animator.layerCount; i++)
            {
                var layerIndex = i;
                var layerSection = sectionAnimatorLayers.AddSection(animator.GetLayerName(layerIndex), "", true);
                layerSection.AddSection("Layer Weight")
                    .AddValueGetter(() => animator.GetLayerWeight(layerIndex).ToString("F"));
                var playingClipsSection =
                    layerSection.AddSection("Playing Clips [Weight:Name]", noClipsText, false, true);
                playingClipsSection.AddValueGetter(() =>
                {
                    var clipInfos = animator.GetCurrentAnimatorClipInfo(layerIndex);
                    var newSections = new List<Section>();
                    if (clipInfos.Length <= 0)
                    {
                        playingClipsSection.QueueDynamicSectionsUpdate(newSections);
                        return noClipsText;
                    }

                    foreach (var animatorClipInfo in clipInfos.OrderByDescending(info => info.weight))
                    {
                        newSections.Add(new Section(_core)
                        {
                            Title = animatorClipInfo.weight.ToString("F"), Value = animatorClipInfo.clip.name,
                            Collapsable = false, DynamicSubsections = false
                        });
                    }

                    playingClipsSection.QueueDynamicSectionsUpdate(newSections);
                    return $"Playing {clipInfos.Length} Clips";
                });
            }
        }

        // Restore Pickups
        foreach (var pickup in currentSpawnable.pickups)
        {
            var pickupSection = categoryPickups.AddSection(pickup.name);
            pickupSection.AddSection("GrabbedBy").AddValueGetter(() => GetUsername(pickup.GrabbedBy));
        }

        // Restore Attachments
        foreach (var attachment in currentSpawnable._attachments)
        {
            var attachmentSection = categoryAttachments.AddSection(attachment.name);

            attachmentSection.AddSection("Is Attached").AddValueGetter(() => ToString(attachment.IsAttached()));

            attachmentSection.AddSection("GameObject Name").AddValueGetter(() =>
                attachment.IsAttached() && attachment._attachedTransform != null
                    ? attachment._attachedTransform.gameObject.name
                    : Na);
        }

        // Set up CVR Pointers
        var spawnablePointers = currentSpawnable.GetComponentsInChildren<CVRPointer>(true);
        CurrentEntityPointerList.Clear();
        foreach (var pointer in spawnablePointers)
        {
            var pointerGo = pointer.gameObject;

            // Create all pointer sections and sub-sections
            var pointerSubSection = categoryPointers.AddSection(pointerGo.name, "", true);
            pointerSubSection.AddSection("Is Active")
                .AddValueGetter(() => ToString(pointer.gameObject.activeInHierarchy));
            pointerSubSection.AddSection("Class", pointer.GetType().Name);
            pointerSubSection.AddSection("Layer", pointerGo.layer.ToString());
            pointerSubSection.AddSection("Type", pointer.type);

            // Add the visualizer
            CurrentEntityPointerList.Add(PointerVisualizer.CreateVisualizer(pointer));
        }

        pointerButton.IsVisible = CurrentEntityPointerList.Count > 0;

        // Set up CVR Triggers
        CurrentEntityTriggerList.Clear();
        TrackedTriggers.Clear();
        TriggerTaskLastTriggered.Clear();
        TriggerTasksLastExecuted.Clear();
        TriggerStayTasksLastTriggered.Clear();
        TriggerStayTasksLastTriggeredValue.Clear();
        var spawnableTriggers = currentSpawnable.GetComponentsInChildren<TriggerToContact>(true);
        foreach (var trigger in spawnableTriggers)
        {
            var triggerGameObject = trigger.gameObject;
            TrackedTriggers.Add(trigger);

            var receiver = trigger.receiver;

            // Create all spawnable sections and sub-sections
            var spawnableSection = categoryTriggers.AddSection(triggerGameObject.name, "", true);

            spawnableSection.AddSection("Is Active").AddValueGetter(() => ToString(triggerGameObject.activeInHierarchy));
            spawnableSection.AddSection("Owner ID", receiver.OwnerId.ToString());
            spawnableSection.AddSection("Contact ID", receiver.ContactId.ToString());
            spawnableSection.AddSection("Receiver Type", receiver.receiverType.ToString());
            spawnableSection.AddSection("Fire Constant Updates", ToString(receiver.FireConstantUpdates));
            spawnableSection.AddSection("Layer", triggerGameObject.layer.ToString());
            spawnableSection.AddSection("Allow Self", ToString(receiver.allowSelf));
            spawnableSection.AddSection("Allow Others", ToString(receiver.allowOthers));

            var collisionTagsSection = spawnableSection.AddSection("Collision Tags", receiver.collisionTags.Length == 0 ? Na : "");
            foreach (var collisionTags in receiver.collisionTags)
                collisionTagsSection.AddSection(collisionTags);

            // OnEnter, OnExit, and OnStay Tasks
            var tasksOnEnterSection = spawnableSection.AddSection("Tasks [OnEnter]", trigger.onEnterTasksCount == 0 ? Na : "");
            for (var index = 0; index < trigger.onEnterTasksCount; index++)
                GetContactReceiverTemplate(tasksOnEnterSection, trigger.onEnterTasks[index], index);

            var tasksOnExitSection = spawnableSection.AddSection("Tasks [OnExit]", trigger.onExitTasksCount == 0 ? Na : "");
            for (var index = 0; index < trigger.onExitTasksCount; index++)
                GetContactReceiverTemplate(tasksOnExitSection, trigger.onExitTasks[index], index);

            var tasksOnStaySection = spawnableSection.AddSection("Tasks [OnStay]", trigger.onStayTasksCount == 0 ? Na : "");
            for (var index = 0; index < trigger.onStayTasksCount; index++)
            {
                var stayTask = trigger.onStayTask[index];

                var specificTaskSection = tasksOnStaySection.AddSection($"#{index}");
                specificTaskSection.AddSection("Name", stayTask.parameterName);
                specificTaskSection.AddSection("Update Method", stayTask.updateMethod.ToString());

                switch (stayTask.updateMethod)
                {
                    case TriggerToContact.ContactTriggerStayTask.UpdateMethod.SetFromPosition:
                        specificTaskSection.AddSection("Sample direction", stayTask.sampleDirection.ToString());
                        specificTaskSection.AddSection("Min Value", stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                        specificTaskSection.AddSection("Max Value", stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case TriggerToContact.ContactTriggerStayTask.UpdateMethod.Add:
                        specificTaskSection.AddSection("Increment/second", stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case TriggerToContact.ContactTriggerStayTask.UpdateMethod.Subtract:
                        specificTaskSection.AddSection("Decrement/second", stayTask.minValue.ToString(CultureInfo.InvariantCulture));
                        break;
                }

                specificTaskSection.AddSection("Last Executed").AddValueGetter(() =>
                    TriggerStayTasksLastTriggered.TryGetValue(stayTask, out float stayTaskValue)
                        ? GetTimeDifference(stayTaskValue)
                        : "?" + " secs ago");
                specificTaskSection.AddSection("Last Value Set").AddValueGetter(() =>
                    TriggerStayTasksLastTriggeredValue.TryGetValue(stayTask, out float stayTaskValue)
                        ? stayTaskValue.ToString(CultureInfo.InvariantCulture)
                        : "?");
                specificTaskSection.AddSection("Last Sender ID").AddValueGetter(() => stayTask.lastSender?.ContactId.ToString(CultureInfo.InvariantCulture) ?? "?");
            }

            // Add the visualizer
            CurrentEntityTriggerList.Add(TriggerToContactVisualizer.CreateVisualizer(trigger));
        }

        triggerButton.IsVisible = CurrentEntityTriggerList.Count > 0;
    }

    private static void GetContactReceiverTemplate(Section parentSection, TriggerToContact.ContactTriggerTask task, int idx)
    {
        var specificTaskSection = parentSection.AddSection($"#{idx}");
        specificTaskSection.AddSection("Name", task.parameterName);
        specificTaskSection.AddSection("Value").AddValueGetter(() => task.parameterValue.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Delay", task.delay.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Hold Time", task.holdTime.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Update Method", task.updateMethod.ToString());
        specificTaskSection.AddSection("Active").AddValueGetter(() => ToString(task.active));
        specificTaskSection.AddSection("Hold Remaining").AddValueGetter(() => task.holdRemaining.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Hold Passed").AddValueGetter(() => task.holdPassed.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Delay Remaining").AddValueGetter(() => task.delayRemaining.ToString(CultureInfo.InvariantCulture));
        specificTaskSection.AddSection("Last Triggered").AddValueGetter(() =>
            TriggerTaskLastTriggered.TryGetValue(task, out float lastTriggeredTime)
                ? GetTimeDifference(lastTriggeredTime)
                : "?" + " secs ago");
        specificTaskSection.AddSection("Last Executed").AddValueGetter(() =>
            TriggerTasksLastExecuted.TryGetValue(task, out float lastExecutedTime)
                ? GetTimeDifference(lastExecutedTime)
                : "?" + " secs ago");
        specificTaskSection.AddSection("Last Sender ID").AddValueGetter(() => task.lastSender?.ContactId.ToString(CultureInfo.InvariantCulture) ?? "?");
    }
}
