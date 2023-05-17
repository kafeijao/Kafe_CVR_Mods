using ABI_RC.Core.Base;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using HarmonyLib;
using MagicaCloth;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using static Kafe.GrabbyBones.Data;

namespace Kafe.GrabbyBones;

public class GrabbyBones : MelonMod {

    private static bool _initialize;

    private const float AvatarSizeToBreakDistance = 1f;
    private const float AvatarSizeToHandProportions = 0.1f;

    private const string HandOffsetName = $"[{nameof(GrabbedBones)} Mod] HandOffset";

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    private static readonly HashSet<GrabbyBoneInfo> GrabbyBonesCache = new();

    private enum GrabState {
        None = 0,
        Grab,
        // Pose,
    }

    private static readonly Dictionary<Transform, GrabState> GrabbedStates = new();

    private static readonly HashSet<GrabbedInfo> GrabbedBones = new();

    private static string GetPlayerName(PuppetMaster puppetMaster) {
        if (puppetMaster == null) return "Me";
        if (puppetMaster._playerDescriptor != null) return puppetMaster._playerDescriptor.userName;
        return "N/A";
    }

    private static void CheckState(PuppetMaster puppetMaster, Transform handTransform, GrabState grabState) {
        GrabbedStates.TryGetValue(handTransform, out var previousGrabState);
        if (grabState == GrabState.Grab && previousGrabState == GrabState.None) {
            Grab(puppetMaster, handTransform);
        }
        else if (grabState == GrabState.None && previousGrabState != GrabState.None) {
            Release(puppetMaster, handTransform);
        }
        GrabbedStates[handTransform] = grabState;
    }

    private static GrabState GetGrabState(float gesture, float thumbCurl, float middleFingerCurl) {
        if (Mathf.Approximately(gesture, 1) || thumbCurl > 0.5f && middleFingerCurl > 0.5f) return GrabState.Grab;
        // if (Mathf.Approximately(gesture, 2) || thumbCurl < 0.4f && middleFingerCurl > 0.5f) return GrabState.Pose;
        return GrabState.None;
    }

    private static void CheckAvatarMovementData(PuppetMaster puppetMaster, Animator animator, PlayerAvatarMovementData data) {
        if (animator == null || !animator.isHuman) return;
        var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (leftHand != null) CheckState(puppetMaster, leftHand, GetGrabState(data.AnimatorGestureLeft, data.LeftThumbCurl, data.LeftMiddleCurl));
        var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand != null) CheckState(puppetMaster, rightHand, GetGrabState(data.AnimatorGestureRight, data.RightThumbCurl, data.RightMiddleCurl));
    }

    private static void OnVeryLateUpdate() {
        if (!_initialize) return;

        // The local player
        CheckAvatarMovementData(null, PlayerSetup.Instance._animator, PlayerSetup.Instance._playerAvatarMovementData);

        // Remote Players
        foreach (var player in CVRPlayerManager.Instance.NetworkPlayers) {
            if (ModConfig.MeOnlyFriends.Value && !Friends.FriendsWith(player.Uuid)) continue;
            if (player.PuppetMaster == null || player.PuppetMaster.PlayerAvatarMovementDataInput == null) continue;
            if (player.PuppetMaster._isHidden || player.PuppetMaster._isBlocked || player.PuppetMaster._isBlockedAlt) continue;
            if (ModConfig.MeMaxPlayerDistance.Value > 0 &&
                Vector3.Distance(player.PuppetMaster.transform.position, PlayerSetup.Instance.transform.position) >
                ModConfig.MeMaxPlayerDistance.Value) continue;

            CheckAvatarMovementData(player.PuppetMaster, player.PuppetMaster._animator, player.PuppetMaster.PlayerAvatarMovementDataInput);
        }

        // Check for currently grabbed bones breaks
        GrabbedBones.RemoveWhere(grabbedBone => {

            if (grabbedBone.PuppetMasterComponent != null) {
                var puppetMaster = grabbedBone.PuppetMasterComponent;

                // Handle player's avatar being hidden/blocked/blocked_alt
                if (puppetMaster._isHidden || puppetMaster._isBlocked || puppetMaster._isBlockedAlt) {
                    #if DEBUG
                    MelonLogger.Msg($"[{GetPlayerName(puppetMaster)}] Broken by being hidden/blocked/blocked_alt: {grabbedBone.Root.RootTransform.name}. " +
                                    $"_isHidden: {puppetMaster._isHidden}, _isBlocked: {puppetMaster._isBlocked}, _isBlockedAlt: {puppetMaster._isBlockedAlt}");
                    #endif
                    ResetGrab(grabbedBone);
                    return true;
                }

                // Handle player being too far
                if (ModConfig.MeMaxPlayerDistance.Value > 0 && Vector3.Distance(puppetMaster.transform.position, PlayerSetup.Instance.transform.position) > ModConfig.MeMaxPlayerDistance.Value) {
                    #if DEBUG
                    MelonLogger.Msg($"[{GetPlayerName(puppetMaster)}] Broken by grabbing player being too far: {grabbedBone.Root.RootTransform.name}.");
                    #endif
                    ResetGrab(grabbedBone);
                    return true;
                }
            }

            // Handle puppet master being null, but it wasn't the local player...
            else if (!grabbedBone.IsLocalPlayer) {
                #if DEBUG
                MelonLogger.Msg($"[N/A] Broken by remote player puppet master being gone: {grabbedBone.Root.RootTransform.name}.");
                #endif
                ResetGrab(grabbedBone);
                return true;
            }

            // Handle hand transform being gone (seems to happen when the player crashed?)
            if (grabbedBone.SourceHand == null || grabbedBone.SourceHandOffset == null) {
                #if DEBUG
                MelonLogger.Msg($"[{GetPlayerName(grabbedBone.PuppetMasterComponent)}] Broken by hand transform being gone: {grabbedBone.Root.RootTransform.name}");
                #endif
                ResetGrab(grabbedBone);
                return true;
            }

            // Handle source hand being too far
            var avatarHeight = grabbedBone.PuppetMasterComponent == null ? PlayerSetup.Instance._avatarHeight : grabbedBone.PuppetMasterComponent._avatarHeight;
            var breakDistance = avatarHeight * AvatarSizeToBreakDistance;
            var currentDistance = Vector3.Distance(grabbedBone.SourceHandOffset.position, grabbedBone.TargetChildBone.position);
            if (currentDistance > breakDistance) {
                #if DEBUG
                MelonLogger.Msg($"[{GetPlayerName(grabbedBone.PuppetMasterComponent)}] Broken by distance: {grabbedBone.Root.RootTransform.name}. " +
                                $"Current Distance: {currentDistance}, Breaking Distance: {breakDistance}");
                #endif
                ResetGrab(grabbedBone);
                return true;
            }

            return false;
        });
    }

    private static Transform[] GetIkBones(Transform root, Transform child) {
        var path = new Stack<Transform>();
        var current = child;
        while (current != null) {
            path.Push(current);
            if (current == root) {
                break;
            }
            current = current.parent;
        }
        return path.ToArray();
    }

    private static void Grab(PuppetMaster puppetMaster, Transform sourceTransform) {

        var avatarHeight = puppetMaster == null ? PlayerSetup.Instance._avatarHeight : puppetMaster._avatarHeight;

        GrabbyBoneInfo closestGrabbyBoneInfo = null;
        GrabbyBoneInfo.Root closestGrabbyBoneRoot = null;
        Transform closestChildTransform = null;
        var closestDistance = float.PositiveInfinity;

        // Find the closest child transform
        foreach (var grabbyBone in GrabbyBonesCache) {

            // Make sure the magica/dynamic bone bone is active
            if (!grabbyBone.IsEnabled()) continue;

            foreach (var root in grabbyBone.Roots) {

                // If the root is being grabbed already lets ignore
                var possibleGrabbed = GrabbedBones.FirstOrDefault(gb => gb.Root == root);
                if (possibleGrabbed != null) continue;

                foreach (var childTransform in root.ChildTransforms) {
                    var radius = grabbyBone.GetRadius(childTransform);
                    var currentDistance = Vector3.Distance(sourceTransform.position, childTransform.position);
                    // MelonLogger.Msg($"\tPicked closest! radius: {radius}, currentDistance: {currentDistance}, maxDistance: {radius + avatarHeight * AvatarSizeToHandProportions}");

                    // Ignore if we're grabbing outside of the bone radius
                    if (currentDistance > radius + avatarHeight * AvatarSizeToHandProportions) continue;

                    // Pick the bone closest to our hand
                    if (currentDistance < closestDistance) {
                        // MelonLogger.Msg($"\t\tPicked closest! radius: {radius}, currentDistance: {currentDistance}");

                        closestDistance = currentDistance;
                        closestGrabbyBoneInfo = grabbyBone;
                        closestGrabbyBoneRoot = root;
                        closestChildTransform = childTransform;
                    }
                }
            }
        }

        // #if DEBUG
        // MelonLogger.Msg($"[{GetPlayerName(puppetMaster)}] Grab closest distance: {closestDistance} by {(closestChildTransform == null ? "N/A" : closestChildTransform.name)}");
        // #endif

        // Ignore when no targets, or targets far away
        if (closestChildTransform == null) return;

        #if DEBUG
        MelonLogger.Msg($"[{GetPlayerName(puppetMaster)}] Grabbed: distance: {closestDistance}, bone: {(closestChildTransform == null ? "N/A" : closestChildTransform.name)}");
        #endif

        // Todo: Add these on initialization and cache it
        var sourceTransformOffset = sourceTransform.Find(HandOffsetName);
        if (sourceTransformOffset == null) {
            sourceTransformOffset = new GameObject(HandOffsetName).transform;
            sourceTransformOffset.SetParent(sourceTransform);
        }

        // Set the offset position
        sourceTransformOffset.position = closestChildTransform.position;

        GrabbedBones.Add(new GrabbedInfo(puppetMaster, sourceTransform, sourceTransformOffset, closestGrabbyBoneInfo, closestGrabbyBoneRoot, closestChildTransform));
        closestGrabbyBoneRoot.IK.solver.SetChain(GetIkBones(closestGrabbyBoneRoot.RootTransform, closestChildTransform), closestGrabbyBoneRoot.RootTransform);
        closestGrabbyBoneRoot.IK.solver.target = sourceTransformOffset;
        closestGrabbyBoneInfo.DisablePhysics();
        closestGrabbyBoneRoot.IK.enabled = true;
    }

    private static void ResetGrab(GrabbedInfo grabbed) {
        grabbed.Root.IK.enabled = false;
        grabbed.Info.RestorePhysics();
    }

    private static void Release(PuppetMaster puppetMaster, Transform sourceTransform) {

        var possibleGrabbed = GrabbedBones.FirstOrDefault(gb => gb.SourceHand == sourceTransform);
        if (possibleGrabbed != null) {
            ResetGrab(possibleGrabbed);
            GrabbedBones.Remove(possibleGrabbed);

            #if DEBUG
            MelonLogger.Msg($"[{GetPlayerName(puppetMaster)}] Released {possibleGrabbed.Root.RootTransform.name}");
            #endif
        }
    }

    [DefaultExecutionOrder(9999999)]
    internal class TheVeryLast : MonoBehaviour {

        private void FixedUpdate() {
            if (!ModConfig.MeEnabled.Value) return;

            // Prevent the IK Solver from running in LateUpdate for the local player. It's going to run later.
            foreach (var grabbedBone in GrabbedBones.Where(gb => gb.IsLocalPlayer)) {
                grabbedBone.Root.IK.skipSolverUpdate = true;
            }
        }

        private void LateUpdate() {
            if (!ModConfig.MeEnabled.Value) return;

            try {
                // We need to run the local player skipped FABRIK solver after VRIK, otherwise vr will be funny
                foreach (var grabbedBone in GrabbedBones.Where(gb => gb.IsLocalPlayer)) {
                    grabbedBone.Root.IK.UpdateSolverExternal();
                }

                // We need the very last positions accurately, so it is compatible with (VRIK, and LeapMotion mod)
                OnVeryLateUpdate();
            }
            catch (Exception e) {
                MelonLogger.Error(e);
            }
        }
    }

    [HarmonyPatch]
    internal class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {

                // Create a mono behavior so we can hack the execution order
                var modGameObject = new GameObject($"[{nameof(GrabbyBones)} Mod]");
                UnityEngine.Object.DontDestroyOnLoad(modGameObject);
                modGameObject.AddComponent<TheVeryLast>();

                // Release all grabbed bones if disabled
                ModConfig.MeEnabled.OnEntryValueChanged.Subscribe((_, isEnabled) => {
                    if (isEnabled) return;
                    #if DEBUG
                    MelonLogger.Msg($"[Disabled] Releasing all grabbed bones...");
                    #endif
                    GrabbedBones.RemoveWhere(grabbedBone => {
                        ResetGrab(grabbedBone);
                        return true;
                    });
                });

                _initialize = true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}.");
                MelonLogger.Error(e);
            }
        }

        private static Dictionary<Transform, HashSet<Transform>> GetMagicaRoots(Transform root, List<Transform> childNodes, List<int> selectData, bool canFixedRotate, bool fixedParent) {

            var rootIdx = childNodes.IndexOf(root);
            var rootType = selectData[rootIdx];
            var directChildren = childNodes.Where(c => c.parent == root).ToList();
            var result = new Dictionary<Transform, HashSet<Transform>>();

            var allDirectChildrenFixed = directChildren.Count > 0 && directChildren.All(c => selectData[childNodes.IndexOf(c)] == SelectionData.Fixed);
            var allDirectChildrenHaveChildren = directChildren.Count > 0 && directChildren.All(c => childNodes.Any(coc => coc.parent == c));

            // If it's a grey sphere
            // Or is red and (all child are also red or reds can't rotate)
            // => use their children as roots instead
            if (rootType == SelectionData.Invalid || rootType == SelectionData.Fixed && (!canFixedRotate && allDirectChildrenHaveChildren || allDirectChildrenFixed)) {
                var newChildNodes = childNodes.Where((_, index) => index != rootIdx).ToList();
                var newSelectData = selectData.Where((_, index) => index != rootIdx).ToList();
                #if DEBUG
                MelonLogger.Msg($"Creating Sub-Root: [{rootType}] {root.name}... newChildren({newChildNodes.Count}): {newChildNodes.Join(t => t.name)}");
                #endif
                foreach (var directChild in directChildren) {
                    foreach (var directChildResult in GetMagicaRoots(directChild, newChildNodes, newSelectData, canFixedRotate, rootType == SelectionData.Fixed)) {
                        result[directChildResult.Key] = directChildResult.Value;
                    }
                }
                return result;
            }

            // If the root is red (we already checked that not all children can be roots)
            // Or we can allow greens as roots if has a non-rotating parent
            // => Make this a root
            if (rootType == SelectionData.Fixed || fixedParent && !canFixedRotate && rootType == SelectionData.Move) {

                var filteredChildren = childNodes.Where((ct, idx) => {
                    if (ct == null) return false;

                    // Limit reds rotation if they shouldn't move (including the root)
                    if (!canFixedRotate && selectData[idx] == SelectionData.Fixed) {
                        var rotLimitAngle = ct.gameObject.AddComponentIfMissing<RotationLimitAngle>();
                        rotLimitAngle.limit = 0f;
                        rotLimitAngle.twistLimit = 0f;
                    }

                    return ct != root && ct.IsChildOf(root);
                }).ToHashSet();

                #if DEBUG
                MelonLogger.Msg($"Found Root: [{rootType}] {root.name}! Transforms({filteredChildren.Count}): {filteredChildren.Join(t => t.name)}");
                #endif

                result[root] = filteredChildren;
                return result;
            }

            #if DEBUG
            MelonLogger.Msg($"Giving up Returning empty... [{rootType}] {root.name}!");
            #endif

            // Return empty result, we found no valid roots
            return result;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicaBoneCloth), nameof(MagicaBoneCloth.ClothInit))]
        public static void After_MagicaBoneCloth_ClothInit(MagicaBoneCloth __instance) {
            // Initialize FABRIK for magica bone cloth roots
            try {
                GrabbyBoneInfo grabbyBoneInfo = new GrabbyMagicaBoneInfo(__instance, __instance.Params.GravityDirection);

                for (var i = 0; i < __instance.ClothTarget.RootCount; i++) {

                    var canFixedRotate = !__instance.Params.useFixedNonRotation;

                    var root = __instance.ClothTarget.GetRoot(i);
                    if (root == null) continue;

                    if (__instance.ClothSelection.selectionList.Count != 1) {
                        MelonLogger.Warning($"{__instance.name} had a selection list count different than 1. This needs investigation on how to handle these cases.");
                        continue;
                    }

                    // Create our own roots, because magica is funny and roots are not really roots ;_;
                    var innerRoots = GetMagicaRoots(root, __instance.useTransformList, __instance.ClothSelection.selectionList[0].selectData, canFixedRotate, false);

                    // Iterate our generated roots
                    foreach (var innerRoot in innerRoots) {
                        if (innerRoot.Value.Count == 0) continue;

                        var fabrik = innerRoot.Key.gameObject.AddComponent<FABRIK>();
                        fabrik.solver.useRotationLimits = true;
                        fabrik.enabled = false;

                        grabbyBoneInfo.AddRoot(fabrik, innerRoot.Key, innerRoot.Value);
                    }
                }

                GrabbyBonesCache.Add(grabbyBoneInfo);

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_MagicaBoneCloth_ClothInit)}. Probably some funny magica setup...");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicaBoneCloth), nameof(MagicaBoneCloth.ClothDispose))]
        public static void After_MagicaBoneCloth_ClothDispose(MagicaBoneCloth __instance) {
            // Clean cached info and currently grabbed info related to this magica bone cloth
            foreach (var grabbyBoneInfo in GrabbyBonesCache) {
                if (grabbyBoneInfo.HasInstance(__instance)) {
                    foreach (var root in grabbyBoneInfo.Roots.Where(root => root.IK != null)) {
                        UnityEngine.Object.Destroy(root.IK);
                    }
                }
            }
            GrabbedBones.RemoveWhere(gb => gb.Info.HasInstance(__instance));
            GrabbyBonesCache.RemoveWhere(gb => gb.HasInstance(__instance));
        }


        private static Dictionary<Transform, HashSet<Transform>> GetDynBoneRoots(DynamicBone dynamicBone, Transform root, IEnumerable<Transform> childNodes, int it = 0) {

            var result = new Dictionary<Transform, HashSet<Transform>>();
            var currentChildren = childNodes.Where(ct => ct != null && ct != root && !dynamicBone.m_Exclusions.Contains(ct) && ct.IsChildOf(root)).ToHashSet();

            #if DEBUG
            MelonLogger.Msg($"{(it == 0 ? "[DynamicBone] Creating Root" : $"{new string('\t', it)}Creating Sub-Root")}: {root.name}... Children: ({currentChildren.Count}) {currentChildren.Join(t => t.name)}");
            #endif

            // People use stiffness to prevent roots from moving
            var stiffness = GrabbyDynamicBoneInfo.GetStiffness(dynamicBone, root);

            // People have their dyn bone components deeper inside of the root they reference
            var isComponentInside = dynamicBone.transform != root && dynamicBone.transform.IsChildOf(root);

            if (stiffness >= 0.7f || isComponentInside) {

                #if DEBUG
                MelonLogger.Msg($"\tSkipping {root.name} root! Stiffness: {stiffness}, IsComponentInside: {isComponentInside}. Creating Sub-Roots...");
                #endif

                foreach (var directChild in currentChildren.Where(c => c.parent == root)) {
                    foreach (var directChildResult in GetDynBoneRoots(dynamicBone, directChild, currentChildren, it+1)) {
                        result[directChildResult.Key] = directChildResult.Value;
                    }
                }
                return result;
            }

            // Found a valid root! Add to the results
            if (currentChildren.Count > 0) {
                result.Add(root, currentChildren);
            }

            // Roots needs children...
            else {
                #if DEBUG
                MelonLogger.Msg($"Giving up Returning empty... Root: {root.name}!");
                #endif

            }

            return result;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.Start))]
        public static void After_DynamicBone_Start(DynamicBone __instance) {
            // Initialize FABRIK for dynamic bone roots
            try {
                GrabbyBoneInfo grabbyBoneInfo = new GrabbyDynamicBoneInfo(__instance, __instance.m_Gravity, __instance.m_Force);

                // Ignore null or excluded roots
                var root = __instance.m_Root;
                if (root == null || __instance.m_Exclusions.Contains(root)) return;

                var innerRoots = GetDynBoneRoots(__instance, root, __instance.GetTransformList);

                foreach (var innerRoot in innerRoots) {
                    if (innerRoot.Value.Count == 0) continue;

                    var fabrik = innerRoot.Key.gameObject.AddComponent<FABRIK>();
                    fabrik.solver.useRotationLimits = true;
                    fabrik.enabled = false;

                    grabbyBoneInfo.AddRoot(fabrik, innerRoot.Key, innerRoot.Value);
                }

                GrabbyBonesCache.Add(grabbyBoneInfo);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_DynamicBone_Start)}.");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.OnDestroy))]
        public static void After_DynamicBone_OnDestroy(DynamicBone __instance) {
            // Clean cached info and currently grabbed info related to this dyn bone
            GrabbedBones.RemoveWhere(gb => gb.Info.HasInstance(__instance));
            GrabbyBonesCache.RemoveWhere(gb => gb.HasInstance(__instance));
        }
    }
}
