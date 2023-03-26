using ABI_RC.Core;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.Camera;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using NicoKuroKusagi.MemoryManagement;
using UnityEngine;

namespace Kafe.EyeMovementFix;

// Todo: Implement different target candidates using classes
public sealed class TargetCandidate : IReusable {

    private static readonly int DefaultLayer = LayerMask.NameToLayer("Default");
    private static readonly int PlayerLocalLayer = LayerMask.NameToLayer("PlayerLocal");
    private static readonly int PlayerNetworkLayer = LayerMask.NameToLayer("PlayerNetwork");
    private static readonly int DefaultLayerMask = 1 << DefaultLayer;
    private static readonly int PlayerLocalMask = 1 << PlayerLocalLayer;
    private static readonly int PlayerNetworkMask = 1 << PlayerNetworkLayer;
    private static readonly int DefaultOrRemotePlayersMask = DefaultLayerMask | PlayerNetworkMask;
    private static readonly int DefaultOrAllPlayersMask = DefaultLayerMask | PlayerLocalMask | PlayerNetworkMask;

    private const float MaxTargetAngle = 45f;
    private const float MaxTargetDistance = 15f;

    private const string LookAtCameraSettingName = "PortableCamera_LookAtTarget";

    private static readonly ObjectPool<TargetCandidate> Pool = new(() => new TargetCandidate());
    public static readonly List<TargetCandidate> TargetCandidates = new();
    private static CVRPickupObject _cameraPickup;
    private static bool _cameraIsLookAtTarget;
    public static bool Initialized;
    private static int _lastTargetCandidatesUpdate;


    public Vector3 Position {
        get {
            if (_isCamera) return PortableCamera.Instance.cameraComponent.transform.position;
            if (_isPlayer) {
                if (_betterController == null) return Vector3.zero;
                if (_isMirrorReflection) {
                    if (_mirror == null) return Vector3.zero;
                    return _mirror.GetMirrorReflectionPosition(_betterController.viewpoint.position);
                }
                return _betterController.viewpoint.position;
            }

            const string err = "Something went very wrong... We have a target candidate that's not a camera nor a player...";
            MelonLogger.Error(err);
            throw new Exception(err);
        }
    }

    public int Weight;

    // Player related
    private bool _isPlayer;
    private CVREyeController _controller;
    private BetterEyeController _betterController;

    private bool _isMirrorReflection;
    private CVRMirror _mirror;

    // Misc
    private bool _isCamera;
    private string _username;

    private static void Clear() {
        foreach (var targetCandidate in TargetCandidates) {
            targetCandidate.Recycle();
        }
        TargetCandidates.Clear();
    }

    public void Recycle() {

        Weight = default;

        _isCamera = default;
        _isMirrorReflection = default;
        _mirror = default;
        _isPlayer = default;
        _controller = default;
        _betterController = default;

        _username = default;

        Pool.PutObject(this);
    }

    public TargetCandidate GetCopy() {
        return new TargetCandidate {
            Weight = Weight,
            _isCamera = _isCamera,
            _isMirrorReflection = _isMirrorReflection,
            _mirror = _mirror,
            _isPlayer = _isPlayer,
            _controller = _controller,
            _username = _username,
            _betterController = _betterController,
        };
    }

    private static void AddPlayer(CVREyeController controller, BetterEyeController betterController, CVRMirror mirror = null) {
        var candidate = Pool.GetObject();
        candidate._isPlayer = true;
        candidate._controller = controller;
        candidate._betterController = betterController;
        candidate._isMirrorReflection = mirror != null;
        candidate._mirror = mirror;
        TargetCandidates.Add(candidate);
    }

    private static void AddCamera() {
        var candidate = Pool.GetObject();
        candidate._isCamera = true;
        TargetCandidates.Add(candidate);
    }

    public string GetName() {
        return GetName(_username);
    }

    private string GetName(string username) {
        if (_isPlayer) {
            return $"[{username}] {(_isMirrorReflection ? " [Mirror]" : "")}";
        }
        if (_isCamera) {
            return "[CVRCamera]";
        }
        return "N/A";
    }

    public static void UpdateTargetCandidates() {

        // Check if we already updated the target candidates this frame, if we did there is no need to re-do it
        if (_lastTargetCandidatesUpdate == Time.frameCount) return;
        _lastTargetCandidatesUpdate = Time.frameCount;

        Clear();

        // Add player's view points as target candidates
        foreach (var betterController in BetterEyeController.BetterControllers.Values) {
            if (!betterController.initialized) continue;

            var cvrEyeController = betterController.cvrEyeController;

            AddPlayer(cvrEyeController, betterController);

            // Add the player's mirror reflections of viewpoints as target candidates
            foreach (var mirror in CVREyeControllerManager.Instance.mirrorList) {
                AddPlayer(cvrEyeController, betterController, mirror);
            }
        }

        // Add camera's position as target candidate if is active and the option is enabled
        if (CVRCamController.Instance.cvrCamera.activeSelf && _cameraIsLookAtTarget) {
            AddCamera();
        }
    }

    public static TargetCandidate GetNewTarget(BetterEyeController betterController, CVREyeController cvrController, Transform viewpoint) {

        float GetAlignmentScore(TargetCandidate targetCandidate) {
            // Get how aligned (angle wise) the target is to our viewpoint center
            // 100% align returns 1, angles equal or higher than 45 degrees will return 0
            var targetDirection = (targetCandidate.Position - cvrController.viewPosition).normalized;
            var angle = Mathf.Abs(CVRTools.AngleToSigned(Vector3.Angle(targetDirection, viewpoint.forward)));
            return 1 - Mathf.InverseLerp(0, MaxTargetAngle, angle);
        }

        float GetDistanceScore(float targetDistance) {
            // Get how close the target is
            // Right on top of us is 1, 15 meters and further is 0
            return 1 - Mathf.InverseLerp(0, MaxTargetDistance, targetDistance);
        }

        float GetAlignmentAndDistanceScoreCombined(float alignmentScore, float distanceScore) {

            // Use a weighting system to combine the scores
            // The closer the target is, the less the alignment score matters
            var alignmentWeight = (1 - distanceScore) * 0.35f;
            var distanceWeight = distanceScore * 0.65f;

            // Combine the scores
            return alignmentWeight * alignmentScore + distanceWeight * distanceScore;
        }

        // bool HasLineOfSight(TargetCandidate targetCandidate, float targetDistance, out bool inMirror) {
        //     // Shoot a raycast that stops at target distance
        //     // If collides with a player capsule, assume LOS
        //     // If doesnt collide with anything, assume LOS because there was nothing blocking reaching the target
        //
        //     // Ignore mirrors and camera (because the raycast won't reach a player lol)
        //     // And can't look if the ray hit has the mirror script, because not all mirrors have colliders
        //     // Todo: Think of a way to raycast the mirror reflections
        //     if (targetCandidate._isCamera || targetCandidate._isMirrorReflection) {
        //         inMirror = true;
        //         return true;
        //     }
        //     inMirror = false;
        //
        //     var targetDirection = (targetCandidate.Position - cvrController.viewPosition).normalized;
        //
        //     // If we're shooting the raycast from the local player we don't want to hit ourselves
        //     var mask = cvrController.isLocal ? DefaultOrRemotePlayersMask : DefaultOrAllPlayersMask;
        //
        //     if (Physics.Raycast(viewpoint.position, targetDirection, out var hitInfo, targetDistance, mask)) {
        //         #if DEBUG
        //         if (betterController.isDebugging) MelonLogger.Msg($"\t\t[Raycast] Layer: {LayerMask.LayerToName(hitInfo.collider.gameObject.layer)}, Name: {hitInfo.collider.gameObject.name}");
        //         #endif
        //
        //         // If we hit something other than the default layer we can consider we didn't get blocked
        //         if (hitInfo.collider.gameObject.layer != DefaultLayer) return true;
        //
        //         // Otherwise lets check if the thing we hit got a player descriptor (some player stuff is in the Default Layer)
        //         return hitInfo.collider.GetComponent<PlayerDescriptor>() != null;
        //     }
        //
        //     #if DEBUG
        //     if (betterController.isDebugging) MelonLogger.Msg($"\t\t[Raycast] Did not reach anything (means target wasn't blocked)");
        //     #endif
        //
        //     // Since we limited our raycast to the target's distance, here means the raycast wasn't blocked!
        //     return true;
        // }

        double GetTalkingScore(TargetCandidate targetCandidate) {
            // Attempt to get the talking score of the target
            // It seems like the talker amplitude changes from 0 to 0.1 (max), so this seems like a good approximation
            // Returns 1 when really loud, and 0 when not talking

            if (targetCandidate._isPlayer) {

                // See if it's the local player
                if (targetCandidate._controller.isLocal) {
                    targetCandidate._username = AuthManager.username;
                    var participant = PlayerCommsManager.Instance.GetParticipant(MetaPort.Instance.ownerId);
                    return participant is { SpeechDetected: true } ? participant.AudioEnergy : 0d;
                }

                // See if it's any other player, check if their avatar holder instances match
                foreach (var entity in CVRPlayerManager.Instance.NetworkPlayers) {
                    if (targetCandidate._controller.transform.parent != entity.AvatarHolder.transform) continue;
                    targetCandidate._username = entity.Username;
                    var participant = PlayerCommsManager.Instance.GetParticipant(entity.Uuid);
                    return participant is { SpeechDetected: true } ? participant.AudioEnergy : 0d;
                }
            }

            targetCandidate._username = "N/A";
            return 0f;
        }

        var totalWeight = 0;

        #if DEBUG
        if (betterController.isDebugging) {
            MelonLogger.Msg($"\nPicking new target...");
        }
        betterController.LastTargetCandidates.Clear();
        #endif

        foreach (var targetCandidate in TargetCandidates) {

            // Exclude ourselves from the targets
            if (targetCandidate._controller == cvrController && !targetCandidate._isMirrorReflection) {
                targetCandidate.Weight = 0;

                #if DEBUG
                if (betterController.isDebugging) {
                    MelonLogger.Msg($"\t{targetCandidate.GetName("__SELF__")} Ignored ourselves c:");
                }
                betterController.LastTargetCandidates.Add($"{targetCandidate.GetName("__SELF__")} Ignored ourselves c:");
                #endif

                continue;
            }

            var talkingScore = GetTalkingScore(targetCandidate);

            var targetDistance = Vector3.Distance(cvrController.viewPosition, targetCandidate.Position);

            // If the player is being hidden by a wall or some collider in the default layer
            // Todo: Find a better way to tackle this, because some worlds break this completely
            // if (!HasLineOfSight(targetCandidate, targetDistance, out var inMirror)) {
            //     targetCandidate.Weight = 0;
            //
            //     #if DEBUG
            //     if (betterController.isDebugging) {
            //         MelonLogger.Msg($"\t{targetCandidate.GetName()} Ignored a player because was behind a wall!");
            //     }
            //     betterController.LastTargetCandidates.Add($"{targetCandidate.GetName()} Ignored a player because was behind a wall!");
            //     #endif
            //
            //     continue;
            // }

            var distanceScore = GetDistanceScore(targetDistance);

            // Exclude targets with a distance further than 15 meters from our viewpoint
            if (Mathf.Approximately(distanceScore, 0)) {
                targetCandidate.Weight = 0;

                #if DEBUG
                if (betterController.isDebugging) {
                    MelonLogger.Msg($"\t{targetCandidate.GetName()} Further than {MaxTargetDistance} meters!");
                }
                betterController.LastTargetCandidates.Add($"{targetCandidate.GetName()} Further than {MaxTargetDistance} meters!");
                #endif
                continue;
            }

            var alignmentScore = GetAlignmentScore(targetCandidate);

            // Exclude targets with a direction further than 45 degrees from out viewpoint
            if (Mathf.Approximately(alignmentScore, 0)) {
                targetCandidate.Weight = 0;

                #if DEBUG
                if (betterController.isDebugging) {
                    MelonLogger.Msg($"\t{targetCandidate.GetName()} Angle bigger than {MaxTargetAngle}!");
                }
                betterController.LastTargetCandidates.Add($"{targetCandidate.GetName()} Angle bigger than {MaxTargetAngle}!");
                #endif

                continue;
            }

            var alignmentAndDistanceScore = GetAlignmentAndDistanceScoreCombined(alignmentScore, distanceScore);

            // Camera buff!
            var cameraBuff = 0;
            if (targetCandidate._isCamera && EyeMovementFix.MeForceLookAtCamera.Value && _cameraPickup.IsGrabbedByMe()) {
                cameraBuff = 2000;
            }

            // Please notice me more than my mirror reflection
            var realMeScore = targetCandidate._isCamera || targetCandidate._isMirrorReflection ? 0 : 1;

            // Lets not lie to ourselves :)
            var narcissistScore = targetCandidate._isPlayer && targetCandidate._controller.isLocal ? 1 : 0;

            // Create a weighted score, here we can decide the weight of each category
            targetCandidate.Weight = (int) Math.Round(alignmentAndDistanceScore * 200f + talkingScore * 200f + narcissistScore * 50f + realMeScore * 50f + cameraBuff, 0);

            #if DEBUG
            if (betterController.isDebugging) {
                MelonLogger.Msg($"\t{targetCandidate.GetName()} alignmentScore: {alignmentScore}/100, distanceScore{distanceScore}/100, alignmentAndDistanceScore: {alignmentAndDistanceScore}/200, talkingScore: {talkingScore}/200, narcissistScore: {narcissistScore}/50, realMeScore: {realMeScore}/50, Weight: {targetCandidate.Weight}");
            }
            betterController.LastTargetCandidates.Add($"{targetCandidate.GetName()} ali:{alignmentAndDistanceScore:F2}, tlk:{talkingScore:F2}, nar:{narcissistScore:F2}, real:{realMeScore:F2}, Cam:{cameraBuff}, W:{targetCandidate.Weight:F2}");
            #endif

            totalWeight += targetCandidate.Weight;
        }

        // Roll from just picking the highest weight (65% chance)
        if (UnityEngine.Random.Range(0f, 1f) < 0.75f) {
            var targetCandidate = TargetCandidates.OrderByDescending(item => item.Weight).First();

            #if DEBUG
            if (betterController.isDebugging) MelonLogger.Msg($"{targetCandidate.GetName()} [Picked] was selected with {targetCandidate.Weight}!!!\n");
            #endif

            // Return null if the weight of the highest weight is zero, because we don't have any selected duh
            return targetCandidate.Weight != 0 ? targetCandidate : null;
        }

        // Random weighted picker
        float randomValue = UnityEngine.Random.Range(0, totalWeight);
        foreach (var targetCandidate in TargetCandidates) {
            if (targetCandidate.Weight == 0) continue;
            randomValue -= targetCandidate.Weight;
            if (randomValue > 0) continue;

            #if DEBUG
            if (betterController.isDebugging) MelonLogger.Msg($"{targetCandidate.GetName()} [Random] HAS WON with {targetCandidate.Weight}!!!\n");
            #endif

            return targetCandidate;
        }

        return null;
    }

    [HarmonyPatch]
    private static class HarmonyPatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVREyeControllerManager), nameof(CVREyeControllerManager.Update))]
        private static bool Before_CVREyeControllerManager_Update(CVREyeControllerManager __instance) {
            // Skip the original method to get the target candidates, since we're managing targets on ours own
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVREyeControllerManager), nameof(CVREyeControllerManager.Start))]
        private static void After_CVREyeControllerManager_Start() {
            // Get the camera pickup instance so we can check if the camera is being held
            _cameraPickup = CVRCamController.Instance.cvrCamera.GetComponent<CVRPickupObject>();

            // Get value and listen to look at camera setting changes
            _cameraIsLookAtTarget = MetaPort.Instance.settings.GetCameraSettingsBool(LookAtCameraSettingName, true);
            MetaPort.Instance.settings.cameraSettingBoolChanged.AddListener((name, value) => {
                if (name != LookAtCameraSettingName) return;
                _cameraIsLookAtTarget = value;
            });

            Initialized = true;
        }
    }
}
