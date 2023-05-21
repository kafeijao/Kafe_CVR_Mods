using ABI_RC.Core.Base;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
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

                // Don't allow to grab this grabby bone if is parent/child of the hand grabbing
                if (sourceTransform.IsChildOf(root.RootTransform) || root.RootTransform.IsChildOf(sourceTransform)) break;

                foreach (var childTransform in root.ChildTransforms) {
                    var radius = grabbyBone.GetRadius(childTransform);
                    var currentDistance = Vector3.Distance(sourceTransform.position, childTransform.position);
                    // MelonLogger.Msg($"\tPicked closest! radius: {radius}, currentDistance: {currentDistance}, maxDistance: {radius + avatarHeight * AvatarSizeToHandProportions}");

                    // Ignore if we're grabbing outside of the bone radius
                    if (currentDistance > radius + avatarHeight * AvatarSizeToHandProportions) continue;

                    // Don't allow to grab this root if is parent/child of the hand grabbing
                    if (sourceTransform.IsChildOf(childTransform) || childTransform.IsChildOf(sourceTransform)) break;

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
        closestGrabbyBoneInfo.DisablePhysics();
        closestGrabbyBoneRoot.Grab(closestChildTransform, sourceTransformOffset);
    }

    private static void ResetGrab(GrabbedInfo grabbed) {
        grabbed.Root.Release();
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

        private static void CreateMagicaRoot(HashSet<Tuple<Transform, HashSet<Transform>>> results, Transform root, List<Transform> childNodes, Transform transformToPivot) {
            var actualChildren = childNodes.Where(ct => ct != null && ct != root && ct.IsChildOf(transformToPivot));
            results.Add(new Tuple<Transform, HashSet<Transform>>(root, actualChildren.ToHashSet()));

            // var rotationLimitAngles = new HashSet<RotationLimitAngle>();
            // foreach (var actualChild in actualChildren) {
            //     if (!canFixedRotate &&
            //         TryGetSelectionData(childNodes, selectData, actualChild, out var selectionDataType) &&
            //         selectionDataType == SelectionData.Fixed) {
            //         if (!ikBones.Contains(actualChild)) {
            //             // Todo: investigate a better solution for not moving child bones
            //             var rotLimitAngle = actualChild.gameObject.AddComponentIfMissing<RotationLimitAngle>();
            //             rotLimitAngle.limit = 0f;
            //             rotLimitAngle.twistLimit = 0f;
            //             rotLimitAngle.Disable();
            //             rotationLimitAngles.Add(rotLimitAngle);
            //         }
            //     }
            // }
        }

        private static void PopulateMagicaRoots(
            ref HashSet<Tuple<Transform, HashSet<Transform>>> results,
            Transform root,
            List<Transform> useTransformList,
            List<int> selectData,
            bool canFixedRotate,
            bool hasFixedParent,
            int it) {

            var rootTransformIdx = useTransformList.IndexOf(root);
            if (rootTransformIdx == -1) {
                MelonLogger.Warning($"[GetMagicaRoots] Root {root.name} not found in childNodes...");
                return;
            }
            var rootType = selectData[rootTransformIdx];

            // Found a green dot with a fixed parent
            if (rootType == SelectionData.Move && hasFixedParent) {
                #if DEBUG
                MelonLogger.Msg($"[PopulateMagicaRoots]{new string('\t', it)} Creating Root {root.name} pivoting on itself");
                #endif
                CreateMagicaRoot(results, root, useTransformList, root);
                return;
            }

            // Found a red that can rotate -> look for green children
            if (rootType == SelectionData.Fixed && canFixedRotate) {
                foreach (var directChild in useTransformList.Where(c => c.parent == root)) {
                    var directChildIdx = useTransformList.IndexOf(directChild);
                    if (directChildIdx == -1) {
                        MelonLogger.Warning($"[GetMagicaRoots] DirectChild {directChild.name} not found in childNodes...");
                        return;
                    }
                    var directChildType = selectData[directChildIdx];

                    // Found a green children! Let's create a root on this red pointing pivoting on the green child
                    // We're pivoting because other direct children can't be included in the chain
                    if (directChildType == SelectionData.Move) {
                        #if DEBUG
                        MelonLogger.Msg($"[PopulateMagicaRoots]{new string('\t', it)} Creating Root {root.name} pivoting on {directChild.name}");
                        #endif
                        CreateMagicaRoot(results, root, useTransformList, directChild);
                    }
                    // Otherwise let's go down the chain
                    else {
                        #if DEBUG
                        MelonLogger.Msg($"[PopulateMagicaRoots]{new string('\t', it)} Can't use {root.name} as root... " +
                                        $"[type=Fixed] [canFixedRotate=true] [hasFixedParent={hasFixedParent}]");
                        #endif
                        PopulateMagicaRoots(ref results, directChild, useTransformList, selectData, true, true, it+1);
                    }
                }
                return;
            }

            // Everything else, let's just go down the chain... If already had a fixed parent let's keep it, otherwise just check if it's fixed
            foreach (var directChild in useTransformList.Where(c => c.parent == root)) {
                #if DEBUG
                var typeName = rootType switch {
                    SelectionData.Fixed => nameof(SelectionData.Fixed),
                    SelectionData.Move => nameof(SelectionData.Move),
                    SelectionData.Extend => nameof(SelectionData.Extend),
                    _ => nameof(SelectionData.Invalid),
                };
                MelonLogger.Msg($"[PopulateMagicaRoots]{new string('\t', it)} Can't use {root.name} as root... " +
                                $"[type={typeName}] [canFixedRotate={canFixedRotate}] [hasFixedParent={hasFixedParent}]");
                #endif
                PopulateMagicaRoots(ref results, directChild, useTransformList, selectData, canFixedRotate, hasFixedParent || rootType == SelectionData.Fixed, it+1);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicaBoneCloth), nameof(MagicaBoneCloth.ClothInit))]
        public static void After_MagicaBoneCloth_ClothInit(MagicaBoneCloth __instance) {
            // Initialize FABRIK for magica bone cloth roots
            try {
                var puppetMaster = __instance.GetComponentInParent<PuppetMaster>();
                // var animator = puppetMaster == null ? PlayerSetup.Instance._animator : puppetMaster._animator;

                var selectionList = __instance.GetSelectionList();

                if (selectionList == null) {
                    #if DEBUG
                    MelonLogger.Warning($"[After_MagicaBoneCloth_ClothInit] [{GetPlayerName(puppetMaster)}] {__instance.name} had a null selectionList. This needs investigation... Skipping!");
                    #endif
                    return;
                }

                if (selectionList.Count != __instance.useTransformList.Count) {
                    #if DEBUG
                    MelonLogger.Warning($"[After_MagicaBoneCloth_ClothInit] [{GetPlayerName(puppetMaster)}] {__instance.name} had a selectionList count ({selectionList.Count}) " +
                                        $"different than __instance.useTransformList count ({__instance.useTransformList.Count}). This needs investigation... Skipping!");
                    #endif
                    return;
                }

                GrabbyBoneInfo grabbyBoneInfo = new GrabbyMagicaBoneInfo(__instance, __instance.Params.GravityDirection);

                for (var i = 0; i < __instance.ClothTarget.RootCount; i++) {

                    var canFixedRotate = !__instance.Params.useFixedNonRotation;

                    var root = __instance.ClothTarget.GetRoot(i);
                    if (root == null) continue;

                    var innerRoots = new HashSet<Tuple<Transform, HashSet<Transform>>>();

                    // var ikBones = new HashSet<Transform>();
                    // if (animator != null && animator.isHuman) {
                    //     foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones))) {
                    //         if (bone == HumanBodyBones.LastBone) continue;
                    //         var boneTransform = animator.GetBoneTransform(bone);
                    //         if (boneTransform != null) {
                    //             ikBones.Add(boneTransform);
                    //         }
                    //     }
                    // }

                    // Create our own roots, because magica is funny and roots are not really roots ;_;
                    PopulateMagicaRoots(ref innerRoots, root, __instance.useTransformList, selectionList, canFixedRotate, false, 0);

                    // Iterate our generated roots
                    foreach (var innerRoot in innerRoots) {
                        if (innerRoot.Item2.Count == 0) continue;

                        var fabrik = innerRoot.Item1.gameObject.AddComponent<FABRIK>();
                        fabrik.solver.useRotationLimits = true;
                        fabrik.enabled = false;

                        grabbyBoneInfo.AddRoot(fabrik, innerRoot.Item1, innerRoot.Item2, new HashSet<RotationLimitAngle>());
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


        private static Dictionary<Transform, HashSet<Transform>> GetDynBoneRoots(Animator animator, DynamicBone dynamicBone, Transform root, IEnumerable<Transform> childNodes, int it = 0) {

            var result = new Dictionary<Transform, HashSet<Transform>>();
            var currentChildren = childNodes.Where(ct => ct != null && ct != root && !dynamicBone.m_Exclusions.Contains(ct) && ct.IsChildOf(root)).ToHashSet();

            #if DEBUG
            MelonLogger.Msg($"{(it == 0 ? "[DynamicBone] Creating Root" : $"{new string('\t', it)}Creating Sub-Root")}: {root.name}... Children: ({currentChildren.Count}) {currentChildren.Join(t => t.name)}");
            #endif

            // People use stiffness to prevent roots from moving
            var stiffness = GrabbyDynamicBoneInfo.GetStiffness(dynamicBone, root);

            // People have their dyn bone components deeper inside of the root they reference
            var isComponentInside = dynamicBone.transform != root && dynamicBone.transform.IsChildOf(root);

            // Root bone with multiple children won't be rotated
            var cantRotateDueChildCount = it == 0 && currentChildren.Count(c => c.parent == root) > 1;

            // Let's not move the humanoid bones (if configured to do so)
            var isHumanoidBone = ModConfig.MePreventGrabIKBones.Value && animator.isHuman && ((HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones))).Any(b => b != HumanBodyBones.LastBone && animator.GetBoneTransform(b) == root);

            if (Mathf.Approximately(stiffness, 1f) || stiffness >= 1f || isComponentInside || isHumanoidBone || cantRotateDueChildCount) {

                #if DEBUG
                MelonLogger.Msg($"\tSkipping {root.name} root! Stiffness: {stiffness}, IsComponentInside: {isComponentInside}, " +
                                $"isHumanoidBone: {isHumanoidBone}, cantRotateDueChildCount: {cantRotateDueChildCount}. Creating Sub-Roots...");
                #endif

                foreach (var directChild in currentChildren.Where(c => c.parent == root)) {
                    foreach (var directChildResult in GetDynBoneRoots(animator, dynamicBone, directChild, currentChildren, it+1)) {
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
                var puppetMaster = __instance.GetComponentInParent<PuppetMaster>();
                var animator = puppetMaster == null ? PlayerSetup.Instance._animator : puppetMaster._animator;

                GrabbyBoneInfo grabbyBoneInfo = new GrabbyDynamicBoneInfo(__instance, __instance.m_Gravity, __instance.m_Force);

                // Ignore null or excluded roots
                var root = __instance.m_Root;
                if (root == null || __instance.m_Exclusions.Contains(root)) return;

                var innerRoots = GetDynBoneRoots(animator, __instance, root, __instance.GetTransformList);

                foreach (var innerRoot in innerRoots) {

                    // Remove transforms that are children of (or themselves) exclusions
                    innerRoot.Value.RemoveWhere(t => __instance.m_Exclusions.Exists(t.IsChildOf));

                    if (innerRoot.Value.Count == 0) continue;

                    var fabrik = innerRoot.Key.gameObject.AddComponent<FABRIK>();
                    fabrik.solver.useRotationLimits = true;
                    fabrik.enabled = false;

                    grabbyBoneInfo.AddRoot(fabrik, innerRoot.Key, innerRoot.Value, new HashSet<RotationLimitAngle>());
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
