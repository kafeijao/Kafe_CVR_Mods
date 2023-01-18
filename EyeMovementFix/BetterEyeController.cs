using ABI_RC.Core;
using ABI_RC.Core.Player;
using ABI_RC.Core.Player.AvatarTracking.Local;
using ABI_RC.Core.Player.AvatarTracking.Remote;
using ABI_RC.Core.Savior;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace EyeMovementFix;

[DefaultExecutionOrder(999999)]
public class BetterEyeController : MonoBehaviour {

    private static bool _errored;
    private static readonly Dictionary<CVREyeController, BetterEyeController> BetterControllers = new();

    private static readonly int DefaultLayer = LayerMask.NameToLayer("Default");
    private static readonly int PlayerLocalLayer = LayerMask.NameToLayer("PlayerLocal");
    private static readonly int PlayerNetworkLayer = LayerMask.NameToLayer("PlayerNetwork");
    private static readonly int DefaultLayerMask = 1 << DefaultLayer;
    private static readonly int PlayerLocalMask = 1 << PlayerLocalLayer;
    private static readonly int PlayerNetworkMask = 1 << PlayerNetworkLayer;
    private static readonly int DefaultOrRemotePlayersMask = DefaultLayerMask | PlayerNetworkMask;
    private static readonly int DefaultOrAllPlayersMask = DefaultLayerMask | PlayerLocalMask | PlayerNetworkMask;


    private const float MaxVerticalAngle = 25f;
    private const float MaxHorizontalAngle = 25f;

    private const float MaxTargetAngle = 45f;

    private const string CameraGuid = "CVRCamera";
    private const string MirrorSuffix = " [mirror]";

    private CVRAvatar _avatar;
    private Traverse<Vector3> _targetTraverse;
    private Traverse<Vector2> _eyeAngleTraverse;
    private Traverse<string> _targetGuidTraverse;

    private bool _isLocal;

    private Transform _viewpoint;

    private bool _hasLeftEye;
    private BetterEye _leftEye;

    private bool _hasRightEye;
    private BetterEye _rightEye;

    // Internal
    private bool _initialized;
    private float _getNextTargetAt;


    private class BetterEye {
        public bool IsLeft;
        public Transform RealEye;
        public Transform FakeEye;
        public Transform FakeEyeWrapper;
    }

    private static void CreateFake(BetterEye eye, Transform viewpoint) {

        // Create the in-between fake eye ball wrapper
        var fakeEyeBallWrapper = new GameObject($"[EyeMovementFix] Fake{(eye.IsLeft ? "Left" : "Right")}EyeWrapper");
        var wrapperEye = fakeEyeBallWrapper.transform;

        wrapperEye.SetParent(eye.RealEye.parent, true);
        wrapperEye.localScale = Vector3.one;
        wrapperEye.position = eye.RealEye.position;
        wrapperEye.rotation = viewpoint.rotation;

        // Create the in-between fake eye ball, copying the og eye initial local rotation
        var fakeEyeBall = new GameObject($"[EyeMovementFix] Fake{(eye.IsLeft ? "Left" : "Right")}Eye");
        var fakeEye = fakeEyeBall.transform;

        fakeEye.SetParent(wrapperEye, true);
        fakeEye.localScale = Vector3.one;
        fakeEye.localPosition = Vector3.zero;
        fakeEye.rotation = eye.RealEye.rotation;

        eye.FakeEyeWrapper = wrapperEye;
        eye.FakeEye = fakeEye;
    }

    private static void Initialize(CVRAvatar avatar, CVREyeController eyeController, Transform head, Transform leftRealEye, Transform rightRealEye) {

        // Initialize our better eye controller
        var betterEyeController = eyeController.gameObject.AddComponent<BetterEyeController>();
        BetterControllers[eyeController] = betterEyeController;
        betterEyeController._avatar = avatar;
        betterEyeController._targetTraverse = Traverse.Create(eyeController).Field<Vector3>("targetViewPosition");
        betterEyeController._eyeAngleTraverse = Traverse.Create(eyeController).Field<Vector2>("eyeAngle");
        betterEyeController._targetGuidTraverse = Traverse.Create(eyeController).Field<string>("targetGuid");
        betterEyeController._isLocal = eyeController.isLocal;

        // Create the viewpoint
        // var viewpointGo = new GameObject($"[EyeMovementFix] Viewpoint");
        // var viewpoint = viewpointGo.transform;
        //
        // // Setup the viewpoint
        // viewpoint.SetParent(head, true);
        // viewpoint.localScale = Vector3.one;
        // viewpoint.localRotation = Quaternion.identity;
        // if (eyeController.isLocal) {
        //     // Because the _avatar.GetViewWorldPosition() of the local player is completely broken
        //     viewpoint.position = head.position + head.TransformVector(Traverse.Create(PlayerSetup.Instance).Field<Vector3>("initialCameraPos").Value);
        // }
        // else {
        //     // This only works because I patched the CVREverController to fix the CVRAvatar not having a puppet master
        //     viewpoint.position = avatar.GetViewWorldPosition();
        // }
        // betterEyeController._viewpoint = viewpoint;


        // Todo: Improve this crap
        if (eyeController.isLocal) {
            var localHeadPoint = Traverse.Create(PlayerSetup.Instance).Field<LocalHeadPoint>("_viewPoint").Value;
            if (localHeadPoint == null) {
                MelonLogger.Warning($"Failed to get our avatar's viewpoint... Eye Movement will break for me ;_;");
                betterEyeController.enabled = false;
                return;
            }
            betterEyeController._viewpoint = localHeadPoint.GetTransform();


            // // Create the viewpoint
            // var viewpointGo = new GameObject($"[EyeMovementFix] Viewpoint");
            // var viewpoint = viewpointGo.transform;
            //
            // var localHeadPoint = Traverse.Create(PlayerSetup.Instance).Field<LocalHeadPoint>("_viewPoint").Value;
            // if (localHeadPoint == null) {
            //     MelonLogger.Warning($"Failed to get our avatar's viewpoint... Eye Movement will break for me ;_;");
            //     betterEyeController.enabled = false;
            //     return;
            // }
            //
            // var localHeadTransform = localHeadPoint.transform;
            //
            // // Setup the viewpoint
            // viewpoint.SetParent(head, true);
            // viewpoint.localScale = Vector3.one;
            // viewpoint.rotation = localHeadTransform.rotation;
            // viewpoint.position = localHeadTransform.position;
            //
            // betterEyeController._viewpoint = localHeadPoint.GetTransform();
        }
        else {
            var puppetMaster = Traverse.Create(avatar).Field<PuppetMaster>("puppetMaster").Value;
            var playerDescriptor = Traverse.Create(puppetMaster).Field<PlayerDescriptor>("_playerDescriptor").Value;
            var remoteHeadPoint = Traverse.Create(puppetMaster).Field<RemoteHeadPoint>("_viewPoint").Value;
            if (remoteHeadPoint == null) {
                MelonLogger.Warning($"Failed to get {(playerDescriptor == null ? "???" : playerDescriptor.userName)} avatar's viewpoint... Eye Movement will break for them;_;");
                betterEyeController.enabled = false;
                return;
            }
            betterEyeController._viewpoint = remoteHeadPoint.GetTransform();
        }



        // Create the fake left eye
        if (leftRealEye != null) {
            var betterLeftEye = new BetterEye { IsLeft = true, RealEye = leftRealEye };
            CreateFake(betterLeftEye, betterEyeController._viewpoint);
            betterEyeController._leftEye = betterLeftEye;
            betterEyeController._hasLeftEye = true;
        }

        // Create the fake right eye
        if (rightRealEye != null) {
            var betterRightEye = new BetterEye { IsLeft = false, RealEye = rightRealEye };
            CreateFake(betterRightEye, betterEyeController._viewpoint);
            betterEyeController._rightEye = betterRightEye;
            betterEyeController._hasRightEye = true;
        }
    }

    private void Start() {

#if DEBUG
        for (var i = 0; i < _debugLineRenderers.Length; i++) {
            var a = new GameObject("[EyeMovementFix] Line Visualizer", typeof(LineRenderer));
            a.transform.SetParent(_viewpoint);
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
#endif

        if (enabled) _initialized = true;
    }

    public void GetNewTarget(CVREyeController controller) {

        float GetAlignmentScore(CVREyeControllerCandidate targetCandidate) {
            // Get how aligned (angle wise) the target is to our viewpoint center
            // 100% align returns 1, angles equal or higher than 45 degrees will return 0
            var targetDirection = (targetCandidate.Position - controller.viewPosition).normalized;
            var angle = Mathf.Abs(CVRTools.AngleToSigned(Vector3.Angle(targetDirection, _viewpoint.forward)));
            return 1 - Mathf.InverseLerp(0, MaxTargetAngle, angle);
        }

        float GetDistanceScore(float targetDistance) {
            // Get how close the target is
            // Right on top of us is 1, 15 meters and further is 0
            return 1 - Mathf.InverseLerp(0, 15, targetDistance);
        }

        bool HasLineOfSight(CVREyeControllerCandidate targetCandidate, float targetDistance, out bool inMirror) {
            // Shoot a raycast that stops at target distance
            // If collides with a player capsule, assume LOS
            // If doesnt collide with anything, assume LOS because there was nothing blocking reaching the target

            // Ignore mirrors and camera (because the raycast won't reach a player lol)
            // And can't look if the ray hit has the mirror script, because not all mirrors have colliders
            if (targetCandidate.Guid == CameraGuid || targetCandidate.Guid.Contains(MirrorSuffix)) {
                inMirror = true;
                return true;
            }
            inMirror = false;

            var targetDirection = (targetCandidate.Position - controller.viewPosition).normalized;

            // If we're shooting the raycast from the local player we don't want to hit ourselves
            var mask = _isLocal ? DefaultOrRemotePlayersMask : DefaultOrAllPlayersMask;

            if (Physics.Raycast(_viewpoint.position, targetDirection, out var hitInfo, targetDistance, mask)) {
#if DEBUG
                if (_isDebugging) MelonLogger.Msg($"\t\t[Raycast] Layer: {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)}, Name: {hitInfo.collider.gameObject.name}");
#endif
                // If we hit something other than the default layer we can consider we didn't get blocked
                if (hitInfo.collider.gameObject.layer != DefaultLayer) return true;

                // Otherwise lets check if the thing we hit got a player descriptor (some player stuff is in the Default Layer)
                return hitInfo.collider.GetComponent<PlayerDescriptor>() != null;
            }

#if DEBUG
            if (_isDebugging) MelonLogger.Msg($"\t\t[Raycast] Did not reach anything (means target wasn't blocked)");
#endif
            // Since we limited our raycast to the target's distance, here means the raycast wasn't blocked!
            return true;
        }

        float GetTalkingScore(CVREyeControllerCandidate targetCandidate, out string playerName, out bool isLocal) {
            // Attempt to get the talking score of the target
            // It seems like the talker amplitude changes from 0 to 0.1 (max), so this seems like a good approximation
            // Returns 1 when really loud, and 0 when not talking

            // See if it's the local player
            if (PlayerSetup.Instance.PlayerAvatarParent.transform.childCount > 0) {
                var avatarInstanceId = PlayerSetup.Instance.PlayerAvatarParent.transform.GetChild(0).gameObject.GetInstanceID();
                if (targetCandidate.Guid.StartsWith(avatarInstanceId.ToString())) {
                    playerName = MetaPort.Instance.username;
                    isLocal = true;
                    return Mathf.InverseLerp(0f, 0.1f, RootLogic.Instance.comms.Players[0].Amplitude);
                }
            }

            // See if it's any other player
            isLocal = false;
            foreach (var entity in CVRPlayerManager.Instance.NetworkPlayers) {
                // When avatars are initializing they might not have anything in here yet
                if (entity.AvatarHolder.transform.childCount == 0) continue;
                var avatarInstanceId = entity.AvatarHolder.transform.GetChild(0).gameObject.GetInstanceID();
                if (!targetCandidate.Guid.StartsWith(avatarInstanceId.ToString())) continue;
                playerName = entity.Username;
                return Mathf.InverseLerp(0f, 0.1f, entity.TalkerAmplitude);
            }

            playerName = "N/A";
            return 0f;
        }

        var totalWeight = 0;
        var targetCandidates = CVREyeControllerManager.Instance.targetCandidates;

#if DEBUG
        if (_isDebugging) MelonLogger.Msg($"\nPicking new target...");
#endif

        var userNameMap = new Dictionary<CVREyeControllerCandidate, string>();

        foreach (var (guid, targetCandidate) in targetCandidates) {

            // Exclude ourselves from the targets
            if (guid == controller.gameObject.GetInstanceID().ToString()) {
                targetCandidate.Weight = 0;
#if DEBUG
                if (_isDebugging) MelonLogger.Msg($"\t[__SELF__] [{targetCandidate.Guid}] Ignored ourselves c:");
#endif
                continue;
            }

            var talkingScore = GetTalkingScore(targetCandidate, out var username, out var isLocal);
            userNameMap[targetCandidate] = username;

            var targetDistance = Vector3.Distance(controller.viewPosition, targetCandidate.Position);

            // If the player is being hidden by a wall or some collider in the default layer
            if (!HasLineOfSight(targetCandidate, targetDistance, out var inMirror)) {
                targetCandidate.Weight = 0;
#if DEBUG
                if (_isDebugging) MelonLogger.Msg($"\t[{username}] [{targetCandidate.Guid}] Ignored a player because was behind a wall!");
#endif
                continue;
            }

            var distanceScore = GetDistanceScore(targetDistance);

            // Exclude targets with a distance further than 15 meters from our viewpoint
            if (Mathf.Approximately(distanceScore, 0)) {
                targetCandidate.Weight = 0;
#if DEBUG
                if (_isDebugging) MelonLogger.Msg($"\t[{username}] [{targetCandidate.Guid}] Further than 15 meters!");
#endif
                continue;
            }

            var alignmentScore = GetAlignmentScore(targetCandidate);

            // Exclude targets with a direction further than 45 degrees from out viewpoint
            if (Mathf.Approximately(alignmentScore, 0)) {
                targetCandidate.Weight = 0;
#if DEBUG
                if (_isDebugging) MelonLogger.Msg($"\t[{username}] [{targetCandidate.Guid}] Angle bigger than {MaxTargetAngle}!");
#endif
                continue;
            }

            // Please notice me more than my mirror reflection
            var realMeScore = inMirror ? 0 : 1;

            // Lets not lie to ourselves :)
            var narcissistScore = isLocal ? 1 : 0;

            // Create a weighted score, here we can decide the weight of each category
            targetCandidate.Weight = (int) Math.Round(alignmentScore * 100f + distanceScore * 100f + talkingScore * 200f + narcissistScore * 50f + realMeScore * 50f, 0);

#if DEBUG
            if (_isDebugging) MelonLogger.Msg($"\t[{username}] [{targetCandidate.Guid}] alignmentScore: {alignmentScore}, distanceScore{distanceScore}, talkingScore: {talkingScore}, narcissistScore: {narcissistScore}, realMeScore: {realMeScore}, Weight: {targetCandidate.Weight}");
#endif

            totalWeight += targetCandidate.Weight;
        }

        // Roll 50% from just picking the highest weight
        if (UnityEngine.Random.Range(0f, 1f) > 0.5) {
            var targetCandidate = targetCandidates.Values.OrderByDescending(item => item.Weight).First();
            _targetGuidTraverse.Value = targetCandidate.Guid;
#if DEBUG
            if (_isDebugging) {
                var usr = "N/A";
                if (userNameMap.ContainsKey(targetCandidate)) {
                    usr = userNameMap[targetCandidate];
                }
                MelonLogger.Msg($"[{usr}] [{targetCandidate.Guid}] [Picked] was selected with {targetCandidate.Weight}!!!\n");
            }
#endif
            return;
        }


        // Random weighted picker
        float randomValue = UnityEngine.Random.Range(0, totalWeight);
        foreach (var (_, targetCandidate) in targetCandidates) {
            if (targetCandidate.Weight == 0) continue;
            randomValue -= targetCandidate.Weight;
            if (randomValue > 0) continue;
            _targetGuidTraverse.Value = targetCandidate.Guid;
#if DEBUG
            if (_isDebugging) {
                var usr = "N/A";
                if (userNameMap.ContainsKey(targetCandidate)) {
                    usr = userNameMap[targetCandidate];
                }
                MelonLogger.Msg($"[{usr}] [{targetCandidate.Guid}] [Random] HAS WON with {targetCandidate.Weight}!!!\n");
            }
#endif
            return;
        }

        _targetGuidTraverse.Value = "";
    }

    private void TargetHandler(CVREyeController controller) {

#if DEBUG
        if (_isDebugging) {
            if (Input.GetKeyDown(KeyCode.T) ) {
                GetNewTarget(controller);
                controller.targetViewPosition = CVREyeControllerManager.Instance.GetPositionFromGuid(_targetGuidTraverse.Value);
            }
            return;
        }
#endif

        if (Time.time > _getNextTargetAt) {
            // Pick a random time to get another target from 2 to 8 seconds
            _getNextTargetAt = Time.time + UnityEngine.Random.Range(2f, 8f);

            GetNewTarget(controller);

            // Update the target
            controller.targetViewPosition = CVREyeControllerManager.Instance.GetPositionFromGuid(_targetGuidTraverse.Value);
        }

    }

    private void UpdateEyeRotation(BetterEye eye, Quaternion lookRotation) {

        // Limit the rotation on the X and Y axes on the left eye
        eye.FakeEyeWrapper.rotation = lookRotation;
        var wrapperLocalRotation = eye.FakeEyeWrapper.localRotation.eulerAngles;
        if (wrapperLocalRotation.x > 180f) wrapperLocalRotation.x -= 360f;
        if (wrapperLocalRotation.y > 180f) wrapperLocalRotation.y -= 360f;
        wrapperLocalRotation.x = Mathf.Clamp(wrapperLocalRotation.x, -MaxVerticalAngle, MaxVerticalAngle);
        wrapperLocalRotation.y = Mathf.Clamp(wrapperLocalRotation.y, -MaxHorizontalAngle, MaxHorizontalAngle);

        // Set the rotation of the wrapper, this way we can query the fake eyes to the proper position of the real eyes
        eye.FakeEyeWrapper.localRotation = Quaternion.Euler(wrapperLocalRotation);

        // Set the eye angle (we're setting twice if we have 2 eyes, but the values should be the same anyway)
        // This will give values different than cvr. I've opted to have the looking forward angle to be 0
        // And then goes between [-25;0] and [0;+25], instead of [335-360] and [0-25] (cvr default)
        _eyeAngleTraverse.Value.Set(wrapperLocalRotation.y, wrapperLocalRotation.x);
    }

    private void UpdateEyeRotations() {

        // If setting the eyes is disabled, prevent updates
        if (!_avatar.useEyeMovement) return;

        var target = _targetTraverse.Value;

        // Calculate the look direction
        var forward = target - _viewpoint.position;
        var lookRotation = Quaternion.LookRotation(forward, _viewpoint.up);

        var isBehind = _viewpoint.InverseTransformDirection(forward).z < 0;

        var isLooking = target != Vector3.zero;

        // Reset the wrapper rotation when not looking
        // We also reset looking when the target goes behind the viewpoint, this prevents gimbal lock
        if (!isLooking || isBehind) {
            if (_hasLeftEye) _leftEye.FakeEyeWrapper.localRotation = Quaternion.identity;
            if (_hasRightEye) _rightEye.FakeEyeWrapper.localRotation = Quaternion.identity;
            _eyeAngleTraverse.Value.Set(0f, 0f);

            // Let's clear our target
            _targetGuidTraverse.Value = "";
        }

        // Otherwise we update the wrapper rotations to match looking at the target
        else {
            if (_hasLeftEye) UpdateEyeRotation(_leftEye, lookRotation);
            if (_hasRightEye) UpdateEyeRotation(_rightEye, lookRotation);
        }

        // Finally we update the real eyes by querying the fake eyes inside of the wrapper
        if (_hasLeftEye) _leftEye.RealEye.rotation = _leftEye.FakeEye.rotation;
        if (_hasRightEye) _rightEye.RealEye.rotation = _rightEye.FakeEye.rotation;
    }

    private void FixViewpointPositionViewPoint(CVREyeController eyeController) {
        // Fix the local viewpoint, to match our pristine viewpoint c:
        // When in FBT it doesn't follow the head for some reason. This will ensure our view position will be accurate!

        // Heck let's just set them all to use my borked viewpoint
        eyeController.viewPosition = _viewpoint.position;
    }

    private void OnDestroy() {
        _initialized = false;
        foreach(var (eyeController, _) in BetterControllers.Where(kvp => kvp.Value == this).ToList()) {
            BetterControllers.Remove(eyeController);
        }
    }

#if DEBUG

    private void ToggleDebugging() {
        _isDebugging = !_isDebugging;
    }

    private bool _isDebugging;
    private readonly LineRenderer[] _debugLineRenderers = new LineRenderer[6];
    private readonly Color[] _debugColors = { Color.green, Color.red, Color.blue, Color.blue, Color.cyan, Color.cyan };

    private void OnRenderObject() {

        if (!_initialized || !_isDebugging) return;

        void DrawRay(LineRenderer lineRenderer, Vector3 position, Vector3 forward, float distance = 2) {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, position);
            lineRenderer.SetPosition(1, forward * distance + position);
        }

        var target = _targetTraverse.Value;

        // Calculate the look direction
        var forward = target - _viewpoint.position;
        var lookRotation = Quaternion.LookRotation(forward, _viewpoint.up);

        // Green
        DrawRay(_debugLineRenderers[0], _viewpoint.position, _viewpoint.forward);

        // Red
        DrawRay(_debugLineRenderers[1], _viewpoint.position, forward);

        // Blue
        DrawRay(_debugLineRenderers[2], _leftEye.FakeEyeWrapper.position, _leftEye.FakeEyeWrapper.forward);
        DrawRay(_debugLineRenderers[3], _rightEye.FakeEyeWrapper.position, _rightEye.FakeEyeWrapper.forward);

        // Cyan
        DrawRay(_debugLineRenderers[4], _leftEye.FakeEye.position, _leftEye.FakeEye.forward);
        DrawRay(_debugLineRenderers[5], _rightEye.FakeEye.position, _rightEye.FakeEye.forward);
    }
#endif

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), "Update")]
        private static void After_CVREyeController_Update(CVREyeController __instance) {
            if (_errored) return;

            try {
                if (!BetterControllers.ContainsKey(__instance)) return;
                var betterEyeController = BetterControllers[__instance];
                if (!betterEyeController._initialized) return;

                betterEyeController.FixViewpointPositionViewPoint(__instance);
                if (!__instance.viewNetworkControlled) betterEyeController.TargetHandler(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error(e);
                MelonLogger.Error("We've encountered an error, in order to not spam or lag we're going to stop the " +
                                  "the execution. Contact the mod creator with the error above.");
                _errored = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeController), "LateUpdate")]
        private static void After_CVREyeController_LateUpdate(CVREyeController __instance) {
            if (_errored) return;

            try {
                if (!BetterControllers.ContainsKey(__instance)) return;
                var betterEyeController = BetterControllers[__instance];
                if (!betterEyeController._initialized) return;
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
        [HarmonyPatch(typeof(CVREyeController), "Start")]
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
                Initialize(___avatar, __instance, head, leftEye, rightEye);

            }
            catch (Exception e) {
                MelonLogger.Error(e);
                MelonLogger.Error("We've encountered an error, in order to not spam or lag we're going to stop the " +
                                  "the execution. Contact the mod creator with the error above.");
                _errored = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeControllerManager), nameof(CVREyeControllerManager.Update))]
        private static void After_CVREyeControllerManager_Update(CVREyeControllerManager __instance) {
            // At this point I was lazy already, but basically this adds MirrorSuffix to mirror target GUIDs
            // This is used to tell if a target is a mirror during picking a target to bypass the player raycast...
            foreach (var mirror in __instance.mirrorList) {
                foreach (var (guid, candidate) in __instance.targetCandidates
                             .Where(pair => pair.Key.Contains(mirror.gameObject.GetInstanceID().ToString())).ToList()) {
                    __instance.targetCandidates.Remove(guid);
                    var newGuid = candidate.Guid + MirrorSuffix;
                    candidate.Guid = newGuid;
                    __instance.targetCandidates[newGuid] = candidate;
                }
            }
        }
    }
}
