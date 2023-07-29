using ABI_RC.Core.Player;
using ABI_RC.Core.Player.AvatarTracking.Local;
using ABI_RC.Core.Player.AvatarTracking.Remote;
using ABI.CCK.Components;
using EyeMovementFix.CCK;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if DEBUG
using ABI_RC.Core.Savior;
using Kafe.CCK.Debugger.Components;
using CCKDebugger = Kafe.CCK.Debugger;
using Kafe.CCK.Debugger.Components.GameObjectVisualizers;
#endif

namespace Kafe.EyeMovementFix;

[DefaultExecutionOrder(999999)]
public class BetterEyeController : MonoBehaviour {

    private static bool _errored;
    public static readonly Dictionary<CVREyeController, BetterEyeController> BetterControllers = new();
    public static readonly Dictionary<CVRAvatar, Quaternion> OriginalLeftEyeLocalRotation = new();
    public static readonly Dictionary<CVRAvatar, Quaternion> OriginalRightEyeLocalRotation = new();

    private EyeRotationLimits _eyeRotationLimits;

    private CVRAvatar _avatar;

    public CVREyeController cvrEyeController;

    public Transform viewpoint;

    private bool _hasLeftEye;
    private BetterEye _leftEye;

    private bool _hasRightEye;
    private BetterEye _rightEye;

    // Internal
    public bool initialized;
    private float _getNextTargetAt;
    private TargetCandidate _lastTarget;
    private bool _wasViewpointNull = false;

    private class BetterEye {
        public bool IsLeft;
        public Transform RealEye;
        public Transform FakeEye;
        public Transform FakeEyeWrapper;
        public Transform FakeEyeViewpointOffset;
    }

    // Debugging
    #if DEBUG

    public string lastTargetDebugNameFirst;
    public string lastTargetDebugName;
    public readonly List<string> LastTargetCandidates = new();

    private Vector3 _viewpointPositionFixedUpdate;
    private Quaternion _viewpointRotationFixedUpdate;

    private Vector3 _viewpointPositionUpdate;
    private Quaternion _viewpointRotationUpdate;

    private Vector3 _viewpointPositionLateUpdate;
    private Quaternion _viewpointRotationLateUpdate;

    private Vector3 _viewpointPositionPreRender;
    private Quaternion _viewpointRotationPreRender;


    private Quaternion _rightEyeAttempted;
    private Quaternion _rightEyeFixedUpdate;
    private Quaternion _rightEyeUpdate;
    private Quaternion _rightEyeLateUpdate;
    private Quaternion _rightEyeOnRender;

    private Quaternion _leftEyeAttempted;
    private Quaternion _leftEyeFixedUpdate;
    private Quaternion _leftEyeUpdate;
    private Quaternion _leftEyeLateUpdate;
    private Quaternion _leftEyeOnRender;

    private string _debug1;
    private string _debug2;
    private string _debug3;
    private string _debug4;
    #endif

    private static void CreateFake(BetterEye eye, Transform viewpoint, Transform head, CVRAvatar avatar) {

        // Create the viewpoint offset parent, this is needed when the parent of the real eye is not aligned with the viewpoint
        // This way we keep the rotation offset, allowing the looking forward of the eye wrapper to be local rotation = Quaternion.identity
        var fakeEyeBallViewpointOffset = new GameObject($"[EyeMovementFix] Fake{(eye.IsLeft ? "Left" : "Right")}EyeViewpointOffset");
        var viewpointOffsetEye = fakeEyeBallViewpointOffset.transform;

        viewpointOffsetEye.SetParent(eye.RealEye.parent, true);
        viewpointOffsetEye.localScale = Vector3.one;
        viewpointOffsetEye.position = eye.RealEye.position;
        viewpointOffsetEye.rotation = viewpoint.rotation;

        // Create the in-between fake eye ball wrapper
        var fakeEyeBallWrapper = new GameObject($"[EyeMovementFix] Fake{(eye.IsLeft ? "Left" : "Right")}EyeWrapper");
        var wrapperEye = fakeEyeBallWrapper.transform;

        wrapperEye.SetParent(viewpointOffsetEye, true);
        wrapperEye.localScale = Vector3.one;
        wrapperEye.localPosition = Vector3.zero;
        wrapperEye.localRotation = Quaternion.identity;

        // Create the in-between fake eye ball, copying the eye initial local rotation
        var fakeEyeBall = new GameObject($"[EyeMovementFix] Fake{(eye.IsLeft ? "Left" : "Right")}Eye");
        var fakeEye = fakeEyeBall.transform;

        fakeEye.SetParent(wrapperEye, true);
        fakeEye.localScale = Vector3.one;
        fakeEye.localPosition = Vector3.zero;
        //fakeEye.rotation = eye.RealEye.rotation;

        if (eye.IsLeft && OriginalLeftEyeLocalRotation.ContainsKey(avatar)) {
            // Use the rotation offset grabbed right after initializing the avatar, before ik/network do the funny
            fakeEye.rotation = avatar.transform.rotation * OriginalLeftEyeLocalRotation[avatar];
        }
        else if (!eye.IsLeft && OriginalRightEyeLocalRotation.ContainsKey(avatar)) {
            // Use the rotation offset grabbed right after initializing the avatar, before ik/network do the funny
            fakeEye.rotation = avatar.transform.rotation * OriginalRightEyeLocalRotation[avatar];
        }
        else {
            // Default to the current real eye rotation
            fakeEye.rotation = eye.RealEye.rotation;
            #if DEBUG
            MelonLogger.Msg($"[{(eye.IsLeft ? "Left" : "Right")} Eye][{avatar.GetInstanceID()}] Had no initial rotation, falling back to current rotation.");
            #endif
        }

        eye.FakeEyeViewpointOffset = viewpointOffsetEye;
        eye.FakeEyeWrapper = wrapperEye;
        eye.FakeEye = fakeEye;

        #if DEBUG
        MelonLogger.Msg($"{(eye.IsLeft ? "Left" : "Right")} Eye:");
        MelonLogger.Msg($"Avat: {avatar.transform.position.ToString("F2")} - {avatar.transform.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"Head: {head.position.ToString("F2")} - {head.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"VPVP: {viewpoint.position.ToString("F2")} - {viewpoint.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"Real: {eye.RealEye.position.ToString("F2")} - {eye.RealEye.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"WrVP: {eye.FakeEyeViewpointOffset.position.ToString("F2")} - {eye.FakeEyeViewpointOffset.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"FkWr: {eye.FakeEyeWrapper.position.ToString("F2")} - {eye.FakeEyeWrapper.rotation.eulerAngles.ToString("F2")}");
        MelonLogger.Msg($"FaEy: {eye.FakeEye.position.ToString("F2")} - {eye.FakeEye.rotation.eulerAngles.ToString("F2")}");
        #endif
    }

    private static void Initialize(CVRAvatar avatar, Animator animator, CVREyeController eyeController, Transform head, Transform leftRealEye, Transform rightRealEye) {

        // Initialize our better eye controller
        var betterEyeController = eyeController.gameObject.AddComponent<BetterEyeController>();
        BetterControllers[eyeController] = betterEyeController;
        betterEyeController.cvrEyeController = eyeController;
        betterEyeController._avatar = avatar;

        // Fetch and load the eye rotation limits
        LoadRotationLimits(avatar, animator, betterEyeController);

        // Todo: Improve this crap
        if (eyeController.isLocal) {
            var localHeadPoint = PlayerSetup.Instance._viewPoint;
            if (localHeadPoint == null) {
                MelonLogger.Warning($"Failed to get our avatar's viewpoint... Eye Movement will break for me ;_;");
                betterEyeController.enabled = false;
                return;
            }
            betterEyeController.viewpoint = localHeadPoint.GetTransform();

        }
        else {
            var playerDescriptor = avatar.puppetMaster._playerDescriptor;
            var remoteHeadPoint = avatar.puppetMaster._viewPoint;
            if (remoteHeadPoint == null) {
                MelonLogger.Warning($"Failed to get {(playerDescriptor == null ? "???" : playerDescriptor.userName)} avatar's viewpoint... Eye Movement will break for them;_;");
                betterEyeController.enabled = false;
                return;
            }
            betterEyeController.viewpoint = remoteHeadPoint.GetTransform();
        }

        // Create the fake left eye
        if (leftRealEye != null) {
            var betterLeftEye = new BetterEye { IsLeft = true, RealEye = leftRealEye };
            CreateFake(betterLeftEye, betterEyeController.viewpoint, head, avatar);
            betterEyeController._leftEye = betterLeftEye;
            betterEyeController._hasLeftEye = true;
        }

        // Create the fake right eye
        if (rightRealEye != null) {
            var betterRightEye = new BetterEye { IsLeft = false, RealEye = rightRealEye };
            CreateFake(betterRightEye, betterEyeController.viewpoint, head, avatar);
            betterEyeController._rightEye = betterRightEye;
            betterEyeController._hasRightEye = true;
        }

        // Exclude our fake eyeballs from dyn bones
        foreach (var dynamicBone in avatar.GetComponentsInChildren<DynamicBone>(true)) {
            if (betterEyeController._hasLeftEye) {
                dynamicBone.m_Exclusions.Add(betterEyeController._leftEye.RealEye);
                dynamicBone.m_Exclusions.Add(betterEyeController._leftEye.FakeEyeViewpointOffset);
            }
            if (betterEyeController._hasRightEye) {
                dynamicBone.m_Exclusions.Add(betterEyeController._rightEye.RealEye);
                dynamicBone.m_Exclusions.Add(betterEyeController._rightEye.FakeEyeViewpointOffset);
            }
        }
    }

    private static void LoadRotationLimits(CVRAvatar avatar, Animator animator, BetterEyeController betterEyeController) {

        // Look for the EyeRotationLimit Script
        betterEyeController._eyeRotationLimits = avatar.GetComponent<EyeRotationLimits>();

        #if DEBUG
        if (betterEyeController._eyeRotationLimits != null) MelonLogger.Msg($"Found avatar EyeRotationLimits in the avatar!");
        #endif

        if (betterEyeController._eyeRotationLimits != null) return;

        // If there is no script, created with defaults (25)
        betterEyeController._eyeRotationLimits = avatar.gameObject.AddComponent<EyeRotationLimits>();

        // If there are bone muscle limits, get them and apply over the defaults!
        var humanBones = animator.avatar.humanDescription.human;
        foreach (var humanBone in humanBones) {

            // Ignore default muscle values values
            if (humanBone.limit.useDefaultValues) continue;

            if (humanBone.humanName == HumanBodyBones.LeftEye.ToString()) {
                betterEyeController._eyeRotationLimits.LeftEyeMinY = humanBone.limit.min.z;
                betterEyeController._eyeRotationLimits.LeftEyeMaxY = humanBone.limit.max.z;
                betterEyeController._eyeRotationLimits.LeftEyeMinX = -humanBone.limit.max.y;
                betterEyeController._eyeRotationLimits.LeftEyeMaxX = -humanBone.limit.min.y;

                #if DEBUG
                MelonLogger.Msg($"Found avatar Left Eye Muscle Limits on the avatar!");
                #endif
            }
            else if (humanBone.humanName == HumanBodyBones.RightEye.ToString()) {
                betterEyeController._eyeRotationLimits.RightEyeMinY = humanBone.limit.min.z;
                betterEyeController._eyeRotationLimits.RightEyeMaxY = humanBone.limit.max.z;
                betterEyeController._eyeRotationLimits.RightEyeMinX = humanBone.limit.min.y;
                betterEyeController._eyeRotationLimits.RightEyeMaxX = humanBone.limit.max.y;

                #if DEBUG
                MelonLogger.Msg($"Found avatar Right Eye Muscle Limits on the avatar!");
                #endif
            }
        }
    }

    private void Start() {

#if DEBUG
        for (var i = 0; i < _debugLineRenderers.Length; i++) {
            var a = new GameObject("[EyeMovementFix] Line Visualizer", typeof(LineRenderer));
            a.transform.SetParent(viewpoint);
            a.transform.localScale = Vector3.one;
            a.transform.localPosition = Vector3.zero;
            a.transform.localRotation = Quaternion.identity;
            var l = a.GetComponent<LineRenderer>();
            l.material = new Material(Shader.Find("Sprites/Default")) {
                color = _debugColors[i]
            };
            l.startWidth = 0.002f;
            l.endWidth = 0.002f;
            _debugLineRenderers[i] = l;
        }


        CCKDebugger.Components.CohtmlMenuHandlers.AvatarCohtmlHandler.AvatarChangeEvent += OnCCKDebuggerAvatarChanged;
#endif

        if (enabled) initialized = true;
    }

    private void FindAndSetNewTarget(CVREyeController controller) {

        // The target candidates needs to be fetched on LateUpdate, because for some reason the viewpoint positions are
        // all wonky on the Update loop. They seem fine in FixedUpdate and LateUpdate. By wonky I mean that the position
        // y (height) doesn't follow the head while in VR
        if (TargetCandidate.Initialized) TargetCandidate.UpdateTargetCandidates();

        // Grab a candidate
        _lastTarget = TargetCandidate.GetNewTarget(this, controller, viewpoint)?.GetCopy();

        #if DEBUG
        lastTargetDebugName = $"{_lastTarget?.GetName()} [Picked or Random] -> {_lastTarget?.Weight:F2}";
        lastTargetDebugNameFirst = $"{_lastTarget?.Position:F2}";
        #endif
    }

    private void TargetHandler(CVREyeController controller) {

#if DEBUG
        if (isDebugging) {
            if (Input.GetKeyDown(KeyCode.T)) FindAndSetNewTarget(controller);
            return;
        }
#endif

        if (Time.time > _getNextTargetAt) {
            // Pick a random time to get another target from 2 to 8 seconds
            _getNextTargetAt = Time.time + UnityEngine.Random.Range(2f, 8f);
            FindAndSetNewTarget(controller);
        }
    }

    private static float NormalizeAngleToPercent(float angle, float minAngle, float maxAngle) {
        if (angle >= 0) {
            return Mathf.Lerp(0, 1f, Mathf.InverseLerp(0, maxAngle, angle));
        }
        else {
            return Mathf.Lerp(-1f, 0, Mathf.InverseLerp(minAngle, 0, angle));
        }
    }

    private void UpdateEyeRotation(BetterEye eye, Quaternion lookRotation) {

        // Limit the rotation on the X and Y axes on the left eye
        eye.FakeEyeWrapper.rotation = lookRotation;
        var wrapperLocalRotation = eye.FakeEyeWrapper.localRotation.eulerAngles;
        if (wrapperLocalRotation.x > 180f) wrapperLocalRotation.x -= 360f;
        if (wrapperLocalRotation.y > 180f) wrapperLocalRotation.y -= 360f;

        if (eye.IsLeft) {
            wrapperLocalRotation.x = Mathf.Clamp(wrapperLocalRotation.x, _eyeRotationLimits.LeftEyeMinY, _eyeRotationLimits.LeftEyeMaxY);
            wrapperLocalRotation.y = Mathf.Clamp(wrapperLocalRotation.y, _eyeRotationLimits.LeftEyeMinX, _eyeRotationLimits.LeftEyeMaxX);
        }
        else {
            wrapperLocalRotation.x = Mathf.Clamp(wrapperLocalRotation.x, _eyeRotationLimits.RightEyeMinY, _eyeRotationLimits.RightEyeMaxY);
            wrapperLocalRotation.y = Mathf.Clamp(wrapperLocalRotation.y, _eyeRotationLimits.RightEyeMinX, _eyeRotationLimits.RightEyeMaxX);
        }

        #if DEBUG
        var previousLocal = eye.FakeEyeWrapper.localRotation;
        #endif

        // Set the rotation of the wrapper, this way we can query the fake eyes to the proper position of the real eyes
        eye.FakeEyeWrapper.localRotation = Quaternion.Euler(wrapperLocalRotation);

        #if DEBUG
        _debug1 = $"prev: {previousLocal.eulerAngles:F2}, Frame: {Time.frameCount}";
        _debug2 = $"{wrapperLocalRotation:F2} -> {eye.FakeEyeWrapper.localRotation.eulerAngles:F2}";
        _debug3 = $"Angle: {Quaternion.Angle(previousLocal, eye.FakeEyeWrapper.localRotation):F2}";
        #endif

        // Set the eye angle (we're setting twice if we have 2 eyes, but the values should be the same anyway)
        // This will give values different than cvr. I've opted to have the looking forward angle to be 0
        // And then goes between [-1;0] and [0;+1], instead of [335-360] and [0-25] (cvr default)
        cvrEyeController.eyeAngle.Set(
            NormalizeAngleToPercent(wrapperLocalRotation.y, _eyeRotationLimits.LeftEyeMinX, _eyeRotationLimits.LeftEyeMaxX),
            NormalizeAngleToPercent(wrapperLocalRotation.x, _eyeRotationLimits.LeftEyeMinY, _eyeRotationLimits.LeftEyeMaxY));
    }

    private void UpdateEyeRotations() {

        // If setting the eyes is disabled, prevent updates
        if (!_avatar.useEyeMovement) return;

        // Log an issue with the viewpoint being null
        if (viewpoint == null) {
            if (!_wasViewpointNull) {
                var avatarId = "N/A";
                if (_avatar != null && _avatar.transform != null && _avatar.transform.parent != null && _avatar.transform.parent.parent != null) {
                    avatarId = _avatar.transform.parent.parent.name;
                }
                MelonLogger.Warning($"[UpdateEyeRotations] The avatar with the id: {avatarId} had viewpoint set to null... Disabling Eye Tracking...");
                _wasViewpointNull = true;
            }
            return;
        }
        if (_wasViewpointNull) {
            var avatarId = "N/A";
            if (_avatar != null && _avatar.transform != null && _avatar.transform.parent != null && _avatar.transform.parent.parent != null) {
                avatarId = _avatar.transform.parent.parent.name;
            }
            MelonLogger.Warning($"[UpdateEyeRotations] The avatar with the id: {avatarId} had viewpoint set to null, BUT NOT ITS FINE... Resuming Eye tracking...");
            _wasViewpointNull = false;
        }

        var target = _lastTarget?.Position ?? Vector3.zero;

        // Calculate the look direction
        var forwardViewpoint = target - viewpoint.position;

        var lookRotationLeft = Quaternion.identity;
        var lookRotationRight = Quaternion.identity;

        if (forwardViewpoint == Vector3.zero) {
            // If we're already aligned, just grab the rotation
            if (_hasLeftEye) lookRotationLeft = _leftEye.FakeEyeWrapper.rotation;
            if (_hasRightEye) lookRotationRight = _rightEye.FakeEyeWrapper.rotation;

            #if DEBUG
            _debug4 = $"Grabbed rotation because already aligned... {Time.frameCount}";
            #endif
        }
        else {
            // Otherwise let's calculate the direction
            //lookRotation = Quaternion.LookRotation(forward, viewpoint.up);
            if (_hasLeftEye) lookRotationLeft = Quaternion.LookRotation(target - _leftEye.FakeEye.position, _leftEye.FakeEyeViewpointOffset.up);
            if (_hasRightEye) lookRotationRight = Quaternion.LookRotation(target - _rightEye.FakeEye.position, _leftEye.FakeEyeViewpointOffset.up);

            #if DEBUG
            _debug4 = $"Calculated look at like a chad...{Time.frameCount}";
            #endif
        }

        //_debug1 = $"trg: {target:F2} view: {viewpoint.position:F2}, forward: {forward:F2} {Time.frameCount}";


        var isBehind = viewpoint.InverseTransformDirection(forwardViewpoint).z < 0;

        var isLooking = target != Vector3.zero;

        // Reset the wrapper rotation when not looking
        // We also reset looking when the target goes behind the viewpoint, this prevents gimbal lock
        if (!isLooking || isBehind) {
            if (_hasLeftEye) _leftEye.FakeEyeWrapper.localRotation = Quaternion.identity;
            if (_hasRightEye) _rightEye.FakeEyeWrapper.localRotation = Quaternion.identity;
            cvrEyeController.eyeAngle.Set(0f, 0f);

            // Let's clear our target
            _lastTarget = null;

            #if DEBUG
            lastTargetDebugName = $"[None] Looked away: {!isLooking}, gotBehind:{isBehind}... {Time.frameCount}";
            #endif
        }

        // Otherwise we update the wrapper rotations to match looking at the target
        else {
            if (_hasLeftEye) UpdateEyeRotation(_leftEye, lookRotationLeft);
            if (_hasRightEye) UpdateEyeRotation(_rightEye, lookRotationRight);

            #if DEBUG
            _rightEyeAttempted = lookRotationLeft;
            _leftEyeAttempted = lookRotationRight;
            #endif
        }

        // Finally we update the real eyes by querying the fake eyes inside of the wrapper

        if (_hasLeftEye) _leftEye.RealEye.rotation = _leftEye.FakeEye.rotation;
        if (_hasRightEye) _rightEye.RealEye.rotation = _rightEye.FakeEye.rotation;
        //_debug3 = $"wra: {_leftEye.FakeEyeWrapper.rotation.eulerAngles:F2} fake: {_leftEye.FakeEye.rotation.eulerAngles:F2}, real: {_leftEye.RealEye.rotation.eulerAngles:F2} {Time.frameCount}";
    }

    private void OnDestroy() {
        initialized = false;
        var eyeControllers = BetterControllers.Where(kvp => kvp.Value == this).Select(kvp => kvp.Key).ToList();
        foreach(var eyeController in eyeControllers) {
            BetterControllers.Remove(eyeController);
        }

        #if DEBUG
        CCKDebugger.Components.CohtmlMenuHandlers.AvatarCohtmlHandler.AvatarChangeEvent -= OnCCKDebuggerAvatarChanged;
        #endif
    }

#if DEBUG

    private void FixedUpdate() {
        if (!initialized) return;
        _viewpointPositionFixedUpdate = viewpoint.position;
        _viewpointRotationFixedUpdate = viewpoint.rotation;

        _rightEyeFixedUpdate = _leftEye.FakeEyeWrapper.rotation;
        _leftEyeFixedUpdate = _rightEye.FakeEyeWrapper.rotation;
    }

    private void Update() {
        if (!initialized) return;
        _viewpointPositionUpdate = viewpoint.position;
        _viewpointRotationUpdate = viewpoint.rotation;

        _rightEyeUpdate = _leftEye.FakeEyeWrapper.rotation;
        _leftEyeUpdate = _rightEye.FakeEyeWrapper.rotation;
    }

    private bool _clickedGetTarget;

    private void LateUpdate() {

        // I want to execute this in late update, because it's when I do it, and also it's when it's gonna be right
        if (_clickedGetTarget) {
            _clickedGetTarget = false;

            TargetCandidate.UpdateTargetCandidates();
            foreach (Transform trx in MetaPort.Instance.transform) {
                if (trx.gameObject.name == "[EyeMovementFixDebugTargets]") {
                    Destroy(trx);
                }
            }
            var go = new GameObject("[EyeMovementFixDebugTargets]");
            go.transform.SetParent(MetaPort.Instance.transform);
            foreach (var candidate in TargetCandidate.TargetCandidates) {
                var goo = new GameObject($"[EyeMovementFixDebugTargets] {candidate.GetName()}");
                goo.transform.SetParent(go.transform);
                goo.transform.position = candidate.Position;
                var bv = BoneVisualizer.Create(goo, 1);
                bv.enabled = true;
            }

            FindAndSetNewTarget(cvrEyeController);
        }

        if (!initialized) return;

        _viewpointPositionLateUpdate = viewpoint.position;
        _viewpointRotationLateUpdate = viewpoint.rotation;

        _rightEyeLateUpdate = _leftEye.FakeEyeWrapper.rotation;
        _leftEyeLateUpdate = _rightEye.FakeEyeWrapper.rotation;



        if (!_isDebuggingLines) return;

        void DrawRay(LineRenderer lineRenderer, Vector3 position, Vector3 forward, float distance = 2) {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, position);
            lineRenderer.SetPosition(1, forward * distance + position);
        }

        var targetPos = _lastTarget?.Position ?? Vector3.zero;

        // Calculate the look direction
        var forward = targetPos - viewpoint.position;

        // Green
        DrawRay(_debugLineRenderers[0], viewpoint.position, viewpoint.forward);

        // Red
        DrawRay(_debugLineRenderers[1], viewpoint.position, forward);

        // Blue
        DrawRay(_debugLineRenderers[2], _leftEye.FakeEyeWrapper.position, _leftEye.FakeEyeWrapper.forward);
        DrawRay(_debugLineRenderers[3], _rightEye.FakeEyeWrapper.position, _rightEye.FakeEyeWrapper.forward);

        // Cyan
        DrawRay(_debugLineRenderers[4], _leftEye.FakeEye.position, _leftEye.FakeEye.forward);
        DrawRay(_debugLineRenderers[5], _rightEye.FakeEye.position, _rightEye.FakeEye.forward);
    }

    private void OnCCKDebuggerAvatarChanged(Core core, bool isLocal, CVRPlayerEntity remotePlayer, GameObject avatarGameObject, Animator avatarAnimator) {

        if (_avatar.gameObject != avatarGameObject) return;

        var debuggingButton = core.AddButton(new Button(Button.ButtonType.Mod1, false, true));
        debuggingButton.StateUpdater = button => button.IsOn = isDebugging;
        debuggingButton.ClickHandler = button => ToggleDebugging();

        var linesButton = core.AddButton(new Button(Button.ButtonType.Mod2, false, true));
        linesButton.StateUpdater = button => button.IsOn = _isDebuggingLines;
        linesButton.ClickHandler = button => ToggleDebuggingVisualizers();

        var getTarget = core.AddButton(new Button(Button.ButtonType.Mod3, false, false));
        getTarget.StateUpdater = button => {
            button.IsOn = debuggingButton.IsOn;
            button.IsVisible = debuggingButton.IsOn;
        };
        getTarget.ClickHandler = button => {
            _clickedGetTarget = true;
        };

        var eyeSection = core.AddSection("[EyeMovementFix] View Point positions", true);

        eyeSection.AddSection("VP Fiixed").AddValueGetter(() => $"{_viewpointPositionFixedUpdate:F2} - {_viewpointRotationFixedUpdate.eulerAngles:F2}");
        eyeSection.AddSection("VP Update").AddValueGetter(() => $"{_viewpointPositionUpdate:F2} - {_viewpointRotationUpdate.eulerAngles:F2}");
        eyeSection.AddSection("VP LateOR").AddValueGetter(() => $"{_viewpointPositionPreRender:F2} - {_viewpointRotationPreRender.eulerAngles:F2}");
        eyeSection.AddSection("VP Laaate").AddValueGetter(() => $"{_viewpointPositionLateUpdate:F2} - {_viewpointRotationLateUpdate.eulerAngles:F2}");

        var targetingSection = core.AddSection("[EyeMovementFix] Target", true);
        targetingSection.AddSection("Target").AddValueGetter(() => lastTargetDebugName);
        targetingSection.AddSection("Target Pos First").AddValueGetter(() => lastTargetDebugNameFirst);
        targetingSection.AddSection("Target Pos").AddValueGetter(() => (_lastTarget?.Position ?? Vector3.zero).ToString("F2"));

        var targetCandidatesSection = targetingSection.AddSection("Target Candidates", "", false, true);
        targetCandidatesSection.AddValueGetter(() => {
            var newUncachedSections = new List<Section>();
            foreach (var lastTargetCandidate in LastTargetCandidates) {
                newUncachedSections.Add(new Section(core) {
                    Title = lastTargetCandidate,
                    Value = "",
                    Collapsable = false,
                    DynamicSubsections = false,
                });
            }
            targetCandidatesSection.QueueDynamicSectionsUpdate(newUncachedSections);
            return $"[{newUncachedSections.Count}]";
        });

        var eyeWrapper = core.AddSection("[EyeMovementFix] Eye Wrapper", true);

        eyeWrapper.AddSection("debug1").AddValueGetter(() => $"{_debug1}");
        eyeWrapper.AddSection("debug2").AddValueGetter(() => $"{_debug2}");
        eyeWrapper.AddSection("debug3").AddValueGetter(() => $"{_debug3}");
        eyeWrapper.AddSection("debug4").AddValueGetter(() => $"{_debug4}");

        eyeWrapper.AddSection("Eye Attemp").AddValueGetter(() => $"{_rightEyeAttempted.eulerAngles:F2} - {_leftEyeAttempted.eulerAngles:F2}");
        eyeWrapper.AddSection("Eye Fiixed").AddValueGetter(() => $"{_rightEyeFixedUpdate.eulerAngles:F2} - {_leftEyeFixedUpdate.eulerAngles:F2}");
        eyeWrapper.AddSection("Eye Update").AddValueGetter(() => $"{_rightEyeUpdate.eulerAngles:F2} - {_leftEyeUpdate.eulerAngles:F2}");
        eyeWrapper.AddSection("Eye LateOR").AddValueGetter(() => $"{_rightEyeLateUpdate.eulerAngles:F2} - {_leftEyeLateUpdate.eulerAngles:F2}");
        eyeWrapper.AddSection("Eye Render").AddValueGetter(() => $"{_rightEyeOnRender.eulerAngles:F2} - {_leftEyeOnRender.eulerAngles:F2}");
    }

    private void ToggleDebugging() {
        isDebugging = !isDebugging;
    }

    private void ToggleDebuggingVisualizers() {
        _isDebuggingLines = !_isDebuggingLines;
    }

    public bool isDebugging;
    private bool _isDebuggingLines;
    private readonly LineRenderer[] _debugLineRenderers = new LineRenderer[6];
    private readonly Color[] _debugColors = { Color.green, Color.red, Color.blue, Color.blue, Color.cyan, Color.cyan };

    private void OnRenderObject() {
        _rightEyeOnRender = _leftEye.FakeEyeWrapper.rotation;
        _leftEyeOnRender = _rightEye.FakeEyeWrapper.rotation;
    }
#endif

    [HarmonyPatch]
    private static class HarmonyPatches {


        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeController), nameof(CVREyeController.LateUpdate))]
        private static void Before_CVREyeController_LateUpdate(CVREyeController __instance, out Vector2 __state) {
            // Save eye angle before the update from CVR
            __state = __instance.eyeAngle;
        }

        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(CVREyeController), nameof(CVREyeController.Update))]
        // private static void After_CVREyeController_Update(CVREyeController __instance, ref Vector2 __state) {
        //     // Restore the eye angle after the update from CVR
        //     // We do this because we're managing that value in LateUpdate
        //     __instance.eyeAngle = __state;
        // }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), nameof(CVREyeController.LateUpdate))]
        private static void After_CVREyeController_LateUpdate(CVREyeController __instance, ref Vector2 __state) {
            if (_errored) return;

            try {
                // Restore the eye angle after the update from CVR
                // We do this because we're managing that value in LateUpdate
                __instance.eyeAngle = __state;

                // Check if we're ready to run our stuff
                if (!BetterControllers.ContainsKey(__instance)) return;
                var betterEyeController = BetterControllers[__instance];
                if (!betterEyeController.initialized) return;

                #if DEBUG
                betterEyeController._viewpointPositionPreRender = betterEyeController.viewpoint.position;
                betterEyeController._viewpointRotationPreRender = betterEyeController.viewpoint.rotation;
                #endif

                // We're picking the target in LateUpdate because the viewpoint in FBT does not follow the head up and
                // down in the Update loop. In FixedUpdate and LateUpdate the value seems to be fine.
                if (!__instance.viewNetworkControlled) betterEyeController.TargetHandler(__instance);

                // Do the eye ball orientation properly
                betterEyeController.UpdateEyeRotations();
            }
            catch (Exception e) {
                MelonLogger.Error(e);
                MelonLogger.Error("We've encountered an error, in order to not spam or lag we're going to stop the " +
                                  "the execution. Contact the mod creator with the error above.");
                _errored = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), nameof(CVREyeController.Start))]
        private static void After_CVREyeControllerManager_Start(ref CVREyeController __instance, ref CVRAvatar ___avatar) {
            if (_errored) return;

            try {
                var animator = __instance.animator;

                // Let's only worry about human rigs
                if (___avatar == null || animator == null || !animator.isHuman) return;

                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                var rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

                // If the avatar has no no eyes ignore! Also we need the head for the local player ;_;
                if ((__instance.isLocal && head == null) || leftEye == null && rightEye == null) return;

                // Initialize the controller
                Initialize(___avatar, animator, __instance, head, leftEye, rightEye);

            }
            catch (Exception e) {
                MelonLogger.Error(e);
                MelonLogger.Error("We've encountered an error, in order to not spam or lag we're going to stop the " +
                                  "the execution. Contact the mod creator with the error above.");
                _errored = true;
            }
        }
    }
}
