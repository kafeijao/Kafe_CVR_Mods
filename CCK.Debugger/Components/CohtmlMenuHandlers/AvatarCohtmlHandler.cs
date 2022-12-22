using System.Globalization;
using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using CCK.Debugger.Components.GameObjectVisualizers;
using CCK.Debugger.Components.PointerVisualizers;
using CCK.Debugger.Components.TriggerVisualizers;
using CCK.Debugger.Entities;
using CCK.Debugger.Utils;
using HarmonyLib;
using UnityEngine;

namespace CCK.Debugger.Components.CohtmlMenuHandlers;

public class AvatarCohtmlHandler : ICohtmlHandler {

    static AvatarCohtmlHandler() {

        // Todo: Allow to inspect other people's avatars if they give permission, waiting for bios...
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

        // Triggers
        TrackedTriggers = new List<CVRAdvancedAvatarSettingsTrigger>();
        TriggerAasTaskLastTriggered = new Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float>();
        TriggerAasTasksLastExecuted = new Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float>();
        TriggerAasStayTasksLastTriggered = new Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float>();
        TriggerAasStayTasksLastTriggeredValue = new Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float>();

        // Triggers last time triggered/executed save
        Events.Avatar.AasTriggerTriggered += task => {
            if (TrackedTriggers.Any(t => t.enterTasks.Contains(task)) ||
                TrackedTriggers.Any(t => t.exitTasks.Contains(task))) {
                TriggerAasTaskLastTriggered[task] = Time.time;
            }
        };
        Events.Avatar.AasTriggerExecuted += task => {
            if (TrackedTriggers.Any(t => t.enterTasks.Contains(task)) ||
                TrackedTriggers.Any(t => t.exitTasks.Contains(task))) {
                TriggerAasTasksLastExecuted[task] = Time.time;
            }
        };
        Events.Avatar.AasStayTriggerTriggered += task => {
            if (TrackedTriggers.Any(t => t.stayTasks.Contains(task))) {
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

    private Core _core;

    // Attributes
    private static Section _attributeUsername;
    private static Section _attributeAvatar;

    private static Animator _mainAnimator;

    // Animator Synced Parameters
    private static Section _sectionSyncedParameters;

    // Animator Local Parameters
    private static Section _sectionLocalParameters;

    // Core Parameters
    private static Section _sectionCoreParameters;
    private static readonly HashSet<string> CoreParameterNames;

    // Pointers
    private static Section _sectionPointers;

    // Triggers
    private static Section _sectionTriggers;
    private static readonly List<CVRAdvancedAvatarSettingsTrigger> TrackedTriggers;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTaskLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTasksLastExecuted;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggeredValue;

    public override void Load(CohtmlMenuController menu) {
        PlayerEntities.ListenPageChangeEvents = true;
        PlayerEntities.HasChanged = true;
    }

    public override void Unload() {
        PlayerEntities.ListenPageChangeEvents = false;
    }

    public override void Update(CohtmlMenuController menu) {

        PlayerEntities.UpdateViaSource();

        var playerCount = PlayerEntities.Count;

        var isLocal = PlayerEntities.CurrentObject == null;
        var currentPlayer = PlayerEntities.CurrentObject;

        _core?.UpdateCore(playerCount > 1, $"({PlayerEntities.CurrentObjectIndex+1}/{playerCount})", true);

        // Update the menus if the spawnable changed
        if (PlayerEntities.HasChanged) {

            _mainAnimator = isLocal
                ? Events.Avatar.LocalPlayerAnimatorManager?.animator
                : Traverse.Create(currentPlayer.PuppetMaster).Field("_animatorManager").GetValue<CVRAnimatorManager>().animator;

            // Wait for the animator to start
            if (_mainAnimator == null || !_mainAnimator.isInitialized || _mainAnimator.parameters == null) return;

            // Highlight on local player makes us lag for some reason
            if (isLocal) Highlighter.ClearTargetHighlight();
            else Highlighter.SetTargetHighlight(_mainAnimator.gameObject);

            // Recreate the core menu
            _core = new Core("Avatars");

            // Static sections
            var attributesSection = _core.AddSection("Attributes");
            _attributeUsername = attributesSection.AddSection("User Name");
            _attributeAvatar = attributesSection.AddSection("Avatar Name/ID");

            _sectionSyncedParameters = _core.AddSection("Avatar Synced Parameters");
            _sectionLocalParameters = _core.AddSection("Avatar Local Parameters");
            _sectionCoreParameters = _core.AddSection("Avatar Default Parameters");

            _sectionPointers = _core.AddSection("CVR Pointers");
            _sectionTriggers = _core.AddSection("CVR AAS Triggers");


            // Restore Main Animator Parameters
            foreach (var parameter in _mainAnimator.parameters) {
                var parameterEntry = ParameterEntrySection.Get(_mainAnimator, parameter);

                // Add the parameter to the proper category
                if (parameter.name.StartsWith("#")) _sectionLocalParameters.AddSection(parameter.name).AddValueGetter(() => parameterEntry.GetValue());
                else if (CoreParameterNames.Contains(parameter.name)) _sectionCoreParameters.AddSection(parameter.name).AddValueGetter(() => parameterEntry.GetValue());
                else _sectionSyncedParameters.AddSection(parameter.name).AddValueGetter(() => parameterEntry.GetValue());
            }

            var avatarGo = isLocal ? PlayerSetup.Instance._avatar : currentPlayer.PuppetMaster.avatarObject;

            // Set up CVR Pointers
            var avatarPointers = avatarGo.GetComponentsInChildren<CVRPointer>(true);
            foreach (var pointer in avatarPointers) {

                var pointerGo = pointer.gameObject;

                // Create all pointer sections and sub-sections
                var pointerSubSection = _sectionPointers.AddSection(pointerGo.name);
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
            TriggerAasTaskLastTriggered.Clear();
            TriggerAasTasksLastExecuted.Clear();
            TriggerAasStayTasksLastTriggered.Clear();
            TriggerAasStayTasksLastTriggeredValue.Clear();
            var avatarTriggers = avatarGo.GetComponentsInChildren<CVRAdvancedAvatarSettingsTrigger>(true);
            foreach (var trigger in avatarTriggers) {

                var triggerGo = trigger.gameObject;
                TrackedTriggers.Add(trigger);

                // Create all spawnable sections and sub-sections
                var spawnableSection = _sectionTriggers.AddSection(triggerGo.name);

                spawnableSection.AddSection("Is Active").AddValueGetter(() => ToString(triggerGo.gameObject.activeInHierarchy));
                spawnableSection.AddSection("Class", trigger.GetType().Name);
                spawnableSection.AddSection("Advanced Trigger", ToString(trigger.useAdvancedTrigger));
                spawnableSection.AddSection("Particle Interactions", ToString(trigger.allowParticleInteraction));
                spawnableSection.AddSection("Layer", triggerGo.layer.ToString());

                var allowedTypesSection = spawnableSection.AddSection("Allowed Types", trigger.allowedTypes.Length == 0 ? Na : "");
                foreach (var triggerAllowedType in trigger.allowedTypes) {
                    allowedTypesSection.AddSection(triggerAllowedType);
                }

                void GetTriggerTaskTemplate(Section parentSection, CVRAdvancedAvatarSettingsTriggerTask task, int idx) {
                    string LastTriggered() => TriggerAasTaskLastTriggered.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerAasTaskLastTriggered[task])
                        : "?" + " secs ago";
                    string LastExecuted() => TriggerAasTasksLastExecuted.ContainsKey(task)
                        ? Menu.GetTimeDifference(TriggerAasTasksLastExecuted[task])
                        : "?" + " secs ago";

                    var specificTaskSection = parentSection.AddSection($"#{idx}");
                    specificTaskSection.AddSection($"Name", task.settingName);
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
                    string LastTriggered() => TriggerAasStayTasksLastTriggered.ContainsKey(stayTask)
                        ? Menu.GetTimeDifference(TriggerAasStayTasksLastTriggered[stayTask])
                        : "?" + " secs ago";
                    string LastTriggeredValue() => TriggerAasStayTasksLastTriggeredValue.ContainsKey(stayTask)
                        ? TriggerAasStayTasksLastTriggeredValue[stayTask].ToString(CultureInfo.InvariantCulture)
                        : "?";

                    var specificTaskSection = tasksOnStaySection.AddSection($"#{index}");
                    specificTaskSection.AddSection($"Name", stayTask.settingName);
                    specificTaskSection.AddSection($"Update Method", stayTask.updateMethod.ToString());

                    if (stayTask.updateMethod == CVRAdvancedAvatarSettingsTriggerTaskStay.UpdateMethod.SetFromPosition) {
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

            var avatarHeight = Traverse.Create(isLocal ? PlayerSetup.Instance : currentPlayer.PuppetMaster).Field("_avatarHeight").GetValue<float>();

            // Set up the Humanoid Bones
            CurrentEntityBoneList.Clear();
            if (_mainAnimator.isHuman) {
                foreach (var target in BoneVisualizer.GetAvailableBones(_mainAnimator)) {
                    if (BoneVisualizer.Create(target, out var boneVisualizer, avatarHeight)) {
                        CurrentEntityBoneList.Add(boneVisualizer);
                    }
                }
            }

            // Consume the avatar changed
            PlayerEntities.HasChanged = false;
            menu.SetCore(_core);
        }

        var playerUserName = isLocal ? MetaPort.Instance.username : currentPlayer.Username;
        var playerAvatarName = GetAvatarName(isLocal ? MetaPort.Instance.currentAvatarGuid : currentPlayer.AvatarId);

        // Update Avatar Data Info
        _attributeUsername.Update(playerUserName);
        _attributeAvatar.Update(playerAvatarName);

        // Update animator parameters
        _sectionLocalParameters.UpdateFromGetter(true);
        _sectionCoreParameters.UpdateFromGetter(true);
        _sectionSyncedParameters.UpdateFromGetter(true);

        // Update cvr spawnable pointer values
        _sectionPointers.UpdateFromGetter(true);

        // Update cvr trigger values
        _sectionTriggers.UpdateFromGetter(true);
    }
}
