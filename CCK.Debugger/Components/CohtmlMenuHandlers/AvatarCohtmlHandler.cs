using System.Globalization;
using ABI_RC.Core;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
using Kafe.CCK.Debugger.Components.PointerVisualizers;
using Kafe.CCK.Debugger.Components.TriggerVisualizers;
using Kafe.CCK.Debugger.Entities;
using Kafe.CCK.Debugger.Utils;
using MelonLoader;
using UnityEngine;

namespace Kafe.CCK.Debugger.Components.CohtmlMenuHandlers;

public class AvatarCohtmlHandler : ICohtmlHandler {

    static AvatarCohtmlHandler() {

        // Todo: Allow to inspect other people's avatars if they give permission, waiting for bios...
        var players = CCKDebugger.TestMode ? CVRPlayerManager.Instance.NetworkPlayers : new List<CVRPlayerEntity>();

        bool IsValid(CVRPlayerEntity entity) {
            if (entity?.PuppetMaster == null) return false;
            return entity.PuppetMaster._animator != null;
        }

        PlayerEntities = new LooseList<CVRPlayerEntity>(players, IsValid, true);
        CoreParameterNames = CVRAnimatorManager.coreParameters;

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

        bool IsCurrentInspectedAvatar(CVRAvatar avatar) {
            // Local player avatar
            if (PlayerEntities.CurrentObject == null) {
                if (PlayerSetup.Instance._avatar == avatar.gameObject) {
                    return true;
                }
            }
            // Prevent crashing with PuppetMaster null bug
            else if (PlayerEntities.CurrentObject.PuppetMaster == null) {
                #if DEBUG
                MelonLogger.Warning("Tried to inspect a remote player with a null PuppetMaster (this should never happen).");
                #endif
                return false;
            }
            // Remote player avatar
            else if (PlayerEntities.CurrentObject.PuppetMaster.avatarObject == avatar.gameObject) {
                return true;
            }
            return false;
        }

        Events.DebuggerMenu.AvatarLoaded += (avatar, isLoaded) => {
            if (isLoaded) {
                if (!IsCurrentInspectedAvatar(avatar)) return;
                // The current inspected player loaded a new avatar
                _currentAvatar = avatar;
                ResetAvatar(true);
            }
            // The current inspected player unloaded the avatar
            else if (_currentAvatar == avatar) {
                ResetAvatar(false);
            }
        };

        Events.DebuggerMenu.EntityChanged += () => {
            if (!PlayerEntities.ListenPageChangeEvents) return;
            ResetAvatar();
        };
    }

    // Internals
    private Core _core;

    private static bool _avatarChanged;
    private static bool _wasDisabled;
    private static bool _wasInitialized;
    private static bool _isLoaded;
    private static CVRAvatar _currentAvatar;

    // Event to allow other mods to tag in
    public static event Action<Core, bool, CVRPlayerEntity, GameObject, Animator> AvatarChangeEvent;

    private static readonly LooseList<CVRPlayerEntity> PlayerEntities;
    private static readonly HashSet<string> CoreParameterNames;
    private static readonly List<CVRAdvancedAvatarSettingsTrigger> TrackedTriggers;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTaskLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTask, float> TriggerAasTasksLastExecuted;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggered;
    private static readonly Dictionary<CVRAdvancedAvatarSettingsTriggerTaskStay, float> TriggerAasStayTasksLastTriggeredValue;

    protected override void Load() {
        PlayerEntities.ListenPageChangeEvents = true;
        ResetAvatar();
    }

    protected override void Unload() {
        PlayerEntities.ListenPageChangeEvents = false;
    }

    protected override void Reset() {
        PlayerEntities.Reset();
    }

    private static void ResetAvatar() => ResetAvatar(Events.DebuggerMenu.IsAvatarLoaded(_currentAvatar));

    private static void ResetAvatar(bool isLoaded) {
        _avatarChanged = true;
        _isLoaded = isLoaded;
    }

    public override void Update() {

        UpdateHeader(out var isLocal, out var isDisabled, out var isInitialized, out var currentPlayer);

        // Handle visibility and animator initialized changes
        if (isDisabled != _wasDisabled || isInitialized != _wasInitialized) {
            _avatarChanged = true;
        }

        // Handle avatar and visibility changes (setup of the avatar)
        if (_avatarChanged) {

            // If the avatar changed, let's reset our entities
            ResetCurrentEntities();

            // Update the visibility and initialization when the avatar changes
            _wasDisabled = isDisabled;
            _wasInitialized = isInitialized;

            // Set up the base part of the menu
            SetupAvatarBase(_isLoaded, isLocal, isDisabled, isInitialized, currentPlayer);

            // Set up the body part of the menu
            if (!isDisabled && isInitialized && _isLoaded) {
                SetupAvatarBody(isLocal, currentPlayer, out var avatarGameObject, out var avatarAnimator);
                AvatarChangeEvent?.Invoke(_core, isLocal, currentPlayer, avatarGameObject, avatarAnimator);
            }

            // Consume the avatar change and send core create update
            _avatarChanged = false;
            Events.DebuggerMenuCohtml.OnCohtmlMenuCoreCreate(_core);
        }

        // Update button's states
        Core.UpdateButtonsState();

        // Update all sections from getters
        Core.UpdateSectionsFromGetters();
    }

    private void UpdateHeader(out bool isLocal, out bool isAvatarDisabled, out bool isAnimatorInitialized, out CVRPlayerEntity currentPlayer) {

        PlayerEntities.UpdateViaSource();

        var playerCount = PlayerEntities.Count;

        isLocal = PlayerEntities.CurrentObject == null;
        currentPlayer = PlayerEntities.CurrentObject;
        if (isLocal) {
            isAvatarDisabled = PlayerSetup.Instance._isBlocked || PlayerSetup.Instance._isBlockedAlt;
            isAnimatorInitialized = true;
        }
        else {
            var puppetMaster = currentPlayer.PuppetMaster;
            isAvatarDisabled = puppetMaster._isHidden || puppetMaster._isBlocked || puppetMaster._isBlockedAlt;
            isAnimatorInitialized = puppetMaster._animator != null;
        }

        _core?.UpdateCore(playerCount > 1, $"{PlayerEntities.CurrentObjectIndex+1}/{playerCount}", true);
    }

    private void SetupAvatarBase(bool isLoaded, bool isLocal, bool isAvatarDisabled, bool isInitialized, CVRPlayerEntity currentPlayer) {

        // Recreate the core menu
        _core = new Core("Avatars");

        // Attributes section
        var attributesSection = _core.AddSection("Attributes");
        attributesSection.AddSection("User Name").AddValueGetter(() => isLocal ? AuthManager.username : currentPlayer.Username);
        attributesSection.AddSection("User ID").AddValueGetter(() => isLocal ? MetaPort.Instance.ownerId : currentPlayer.Uuid);
        attributesSection.AddSection("Avatar Name/ID").AddValueGetter(() => GetAvatarName(isLocal ? MetaPort.Instance.currentAvatarGuid : currentPlayer.AvatarId));
        attributesSection.AddSection("Loading").Value = ToString(!isLoaded || !isInitialized);
        attributesSection.AddSection("Avatar Hidden").Value = ToString(isAvatarDisabled);
    }

    private void SetupAvatarBody(bool isLocal, CVRPlayerEntity currentPlayer, out GameObject avatarGo, out Animator avatarAnimator) {

        // Setup buttons
        var trackerButton = _core.AddButton(new Button(Button.ButtonType.Tracker, false, false));
        var boneButton = _core.AddButton(new Button(Button.ButtonType.Bone, false, false));
        var pointerButton = _core.AddButton(new Button(Button.ButtonType.Pointer, false, false));
        var triggerButton = _core.AddButton(new Button(Button.ButtonType.Trigger, false, false));
        var eyeButton = _core.AddButton(new Button(Button.ButtonType.Eye, false, true));

        // Setup button Handlers
        trackerButton.StateUpdater = button => {
            var hasTrackersActive = TrackerVisualizer.HasTrackersActive();
            button.IsOn = hasTrackersActive;
            button.IsVisible = MetaPort.Instance.isUsingVr && isLocal;
        };
        trackerButton.ClickHandler = button => TrackerVisualizer.ToggleTrackers(!button.IsOn);
        boneButton.StateUpdater = button => button.IsOn = CurrentEntityBoneList.Count > 0 && CurrentEntityBoneList.All(vis => vis.enabled);
        boneButton.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            CurrentEntityBoneList.ForEach(vis => {
                if (vis != null) vis.enabled = button.IsOn;
            });
        };
        pointerButton.StateUpdater = button => button.IsOn = CurrentEntityPointerList.Count > 0 && CurrentEntityPointerList.All(vis => vis.enabled);

        pointerButton.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            CurrentEntityPointerList.ForEach(vis => {
                vis.enabled = button.IsOn;
            });
        };
        triggerButton.StateUpdater = button => button.IsOn = CurrentEntityTriggerList.Count > 0 && CurrentEntityTriggerList.All(vis => vis.enabled);
        triggerButton.ClickHandler = button => {
            button.IsOn = !button.IsOn;
            CurrentEntityTriggerList.ForEach(vis => {
                vis.enabled = button.IsOn;
            });
        };
        eyeButton.ClickHandler = button => button.IsOn = !button.IsOn;

        var mainAnimator = avatarAnimator = isLocal
            ? Events.Avatar.LocalPlayerAnimatorManager?.animator
            : currentPlayer.PuppetMaster._animator;

        // Check if something borked and there is no animator ;_;
        if (mainAnimator == null || !mainAnimator.isInitialized || mainAnimator.parameters == null) {
            throw new Exception("The main animator of the avatar was null, not initialized, or the parameters " +
                                "were null. This is a bug, send a bug report to the mod creator!");
        }

        // Highlight on local player makes us lag for some reason
        if (isLocal) Highlighter.ClearTargetHighlight();
        else Highlighter.SetTargetHighlight(mainAnimator.gameObject);

        var sectionSyncedParameters = _core.AddSection("Avatar Synced Parameters", true);
        var sectionLocalParameters = _core.AddSection("Avatar Local Parameters", true);
        var sectionCoreParameters = _core.AddSection("Avatar Default Parameters", true);

        var sectionAnimatorLayers = _core.AddSection("Animator Layers", true);

        var sectionPointers = _core.AddSection("CVR Pointers", true);
        var sectionTriggers = _core.AddSection("CVR AAS Triggers", true);

        // Restore Main Animator Parameters
        foreach (var parameter in mainAnimator.parameters) {
            var parameterEntry = ParameterEntrySection.Get(mainAnimator, parameter);

            // Add the parameter to the proper category
            string GetParamValue() => parameterEntry.GetValue();
            if (parameter.name.StartsWith("#")) sectionLocalParameters.AddSection(parameter.name).AddValueGetter(GetParamValue);
            else if (CoreParameterNames.Contains(parameter.name)) sectionCoreParameters.AddSection(parameter.name).AddValueGetter(GetParamValue);
            else sectionSyncedParameters.AddSection(parameter.name).AddValueGetter(GetParamValue);
        }

        // Set up the animator layers
        const string noClipsText = "Playing no Clips";
        for (var i = 0; i < mainAnimator.layerCount; i++) {
            var layerIndex = i;
            var layerSection = sectionAnimatorLayers.AddSection(mainAnimator.GetLayerName(layerIndex), "", true);
            layerSection.AddSection("Layer Weight").AddValueGetter(() => mainAnimator.GetLayerWeight(layerIndex).ToString("F"));
            var playingClipsSection = layerSection.AddSection("Playing Clips [Weight:Name]", noClipsText, false, true);
            playingClipsSection.AddValueGetter(() => {
                var clipInfos = mainAnimator.GetCurrentAnimatorClipInfo(layerIndex);
                var newSections = new List<Section>();
                if (clipInfos.Length <= 0) {
                    playingClipsSection.QueueDynamicSectionsUpdate(newSections);
                    return noClipsText;
                }
                foreach (var animatorClipInfo in clipInfos.OrderByDescending(info => info.weight)) {
                    newSections.Add(new Section(_core) { Title = animatorClipInfo.weight.ToString("F"), Value = animatorClipInfo.clip.name, Collapsable = false, DynamicSubsections = false});
                }
                playingClipsSection.QueueDynamicSectionsUpdate(newSections);
                return $"Playing {clipInfos.Length} Clips";
            });
        }

        avatarGo = isLocal ? PlayerSetup.Instance._avatar : currentPlayer.PuppetMaster.avatarObject;

        // Set up CVR Pointers
        var avatarPointers = avatarGo.GetComponentsInChildren<CVRPointer>(true);
        CurrentEntityPointerList.Clear();
        foreach (var pointer in avatarPointers) {

            var pointerGo = pointer.gameObject;

            // Create all pointer sections and sub-sections
            var pointerSubSection = sectionPointers.AddSection(pointerGo.name, "", true);
            pointerSubSection.AddSection("Is Active").AddValueGetter(() => ToString(pointer.gameObject.activeInHierarchy));
            pointerSubSection.AddSection("Class", pointer.GetType().Name);
            pointerSubSection.AddSection("Is Internal", ToString(pointer.isInternalPointer));
            pointerSubSection.AddSection("Is Local", ToString(pointer.isLocalPointer));
            pointerSubSection.AddSection("Layer", pointerGo.layer.ToString());
            pointerSubSection.AddSection("Type", pointer.type);

            // Add the visualizer
            CurrentEntityPointerList.Add(PointerVisualizer.CreateVisualizer(pointer));
        }
        // Update button visibility
        pointerButton.IsVisible = CurrentEntityPointerList.Count > 0;

        // Set up CVR Triggers
        CurrentEntityTriggerList.Clear();
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
            var spawnableSection = sectionTriggers.AddSection(triggerGo.name, "", true);

            spawnableSection.AddSection("Is Active").AddValueGetter(() => ToString(triggerGo.gameObject.activeInHierarchy));
            spawnableSection.AddSection("Class", trigger.GetType().Name);
            spawnableSection.AddSection("Advanced Trigger", ToString(trigger.useAdvancedTrigger));
            spawnableSection.AddSection("Particle Interactions", ToString(trigger.allowParticleInteraction));
            spawnableSection.AddSection("Is Local Interactable", ToString(trigger.isLocalInteractable));
            spawnableSection.AddSection("Layer", triggerGo.layer.ToString());

            var allowedTypesSection = spawnableSection.AddSection("Allowed Types", trigger.allowedTypes.Length == 0 ? Na : "");
            foreach (var triggerAllowedType in trigger.allowedTypes) {
                allowedTypesSection.AddSection(triggerAllowedType);
            }

            var allowedPointersSection = spawnableSection.AddSection("Allowed Pointers", trigger.allowedPointer.Count == 0 ? Na : "");
            foreach (var triggerAllowedPointer in trigger.allowedPointer) {
                allowedPointersSection.AddSection(triggerAllowedPointer != null ? triggerAllowedPointer.name : "-none-");
            }

            void GetTriggerTaskTemplate(Section parentSection, CVRAdvancedAvatarSettingsTriggerTask task, int idx) {
                string LastTriggered() => TriggerAasTaskLastTriggered.ContainsKey(task)
                    ? GetTimeDifference(TriggerAasTaskLastTriggered[task])
                    : "?" + " secs ago";
                string LastExecuted() => TriggerAasTasksLastExecuted.ContainsKey(task)
                    ? GetTimeDifference(TriggerAasTasksLastExecuted[task])
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
                    ? GetTimeDifference(TriggerAasStayTasksLastTriggered[stayTask])
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
            CurrentEntityTriggerList.Add(TriggerVisualizer.CreateVisualizer(trigger));
        }
        // Update button visibility
        triggerButton.IsVisible = CurrentEntityTriggerList.Count > 0;

        var avatarHeight = isLocal ? PlayerSetup.Instance._avatarHeight : currentPlayer.PuppetMaster._avatarHeight;

        // Set up the Humanoid Bones
        CurrentEntityBoneList.Clear();
        if (mainAnimator.isHuman) {
            foreach (var target in BoneVisualizer.GetAvailableBones(mainAnimator)) {
                CurrentEntityBoneList.Add(BoneVisualizer.Create(target, avatarHeight));
            }
        }
        // Update button visibility
        boneButton.IsVisible = CurrentEntityBoneList.Count > 0;


        // Eye movement target
        EyeTargetVisualizer.UpdateActive(false, new List<CVREyeControllerCandidate>(), "");
        var eyeMovementSection = _core.AddSection("Eye Movement", true);
        var eyeManager = CVREyeControllerManager.Instance;
        var localController = eyeManager.controllerList.FirstOrDefault(controller => controller.animator == mainAnimator);

        var hasEyeController = localController != null;

        // Update button visibility
        eyeButton.IsVisible = hasEyeController;

        // Prevent initializing if there is no Eye Movement controller
        if (hasEyeController) {

            var targetGuid = localController.targetGuid;

            // Eye movement visualizers updater
            eyeButton.StateUpdater = button => {

                if (button.IsOn && eyeButton.IsVisible) {

                    foreach (var candidate in eyeManager.targetCandidates) {
                        // Create visualizer (if doesn't exist yet)
                        EyeTargetVisualizer.Create(eyeManager.gameObject, candidate.Key, candidate.Value) ;
                    }

                    // Update the visualizer states
                    EyeTargetVisualizer.UpdateActive(button.IsOn, eyeManager.targetCandidates.Values, targetGuid);
                }
            };

            eyeMovementSection.AddSection("Target Guid").AddValueGetter(() => localController.targetGuid);
            eyeMovementSection.AddSection("Eye Angle").AddValueGetter(() => localController.eyeAngle.ToString("F3"));
            eyeMovementSection.AddSection("Left Eye Rotation").AddValueGetter(() => localController.EyeLeft == null ? Na : localController.EyeLeft.localRotation.ToString("F3"));
            eyeMovementSection.AddSection("Left Eye Rotation Base").AddValueGetter(() => localController.EyeLeftBaseRot == null ? Na : localController.EyeLeftBaseRot.ToString("F3"));
            eyeMovementSection.AddSection("Right Eye Rotation").AddValueGetter(() => localController.EyeRight == null ? Na : localController.EyeRight.localRotation.ToString("F3"));
            eyeMovementSection.AddSection("Right Eye Rotation Base").AddValueGetter(() => localController.EyeRightBaseRot == null ? Na : localController.EyeRightBaseRot.ToString("F3"));
            eyeMovementSection.AddSection("Candidates").AddValueGetter(() => eyeManager.targetCandidates.Values.Join(candidate => candidate.Guid));
        }
    }
}
