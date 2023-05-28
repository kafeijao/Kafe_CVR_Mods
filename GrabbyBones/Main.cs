// #define DEBUG_ROOT_GEN

using ABI_RC.Core.Player;
using ABI.CCK.Components;
using HarmonyLib;
using MagicaCloth;
using MelonLoader;
using RootMotion.FinalIK;
using UnityEngine;
using static Kafe.GrabbyBones.Data;

namespace Kafe.GrabbyBones;


public class GrabbyBones : MelonMod {

    private static bool _initialize;

    private const float AvatarSizeToHandProportions = 0.1f;

    private const string IgnoreGrabbyBonesTag = "[NGB]";

    public override void OnInitializeMelon() {
        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();

        #if DEBUG
        MelonLogger.Warning("This mod was compiled with the DEBUG mode on. There might be an excess of logging and performance overhead...");
        #endif
    }

    private static readonly HashSet<GrabbyBoneInfo> GrabbyBonesCache = new();

    internal static string GetPlayerName(PuppetMaster puppetMaster) {
        if (puppetMaster == null) return "Me";
        if (puppetMaster._playerDescriptor != null) return puppetMaster._playerDescriptor.userName;
        return "N/A";
    }


    private static void CheckState(AvatarHandInfo handInfo) {
        var currentGrabState = handInfo.GetGrabState();
        if (currentGrabState == GrabState.Grab && handInfo.PreviousGrabState == GrabState.None) {
            Grab(handInfo);
        }
        else if (currentGrabState == GrabState.None && handInfo.PreviousGrabState != GrabState.None) {
            Release(handInfo);
        }
        handInfo.PreviousGrabState = currentGrabState;
    }

    private static void OnVeryLateUpdate() {
        if (!_initialize) return;

        foreach (var handInfo in AvatarHandInfo.Hands) {
            if (!handInfo.IsAllowed()) continue;
            CheckState(handInfo);
        }

        AvatarHandInfo.CheckGrabbedBones();
    }

    private static void Grab(AvatarHandInfo handInfo) {

        var avatarHeight = handInfo.GetAvatarHeight();

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
                if (AvatarHandInfo.IsRootGrabbed(root)) continue;

                // Don't allow to grab this grabby bone if is parent/child of the hand grabbing
                if (handInfo.GrabbingPoint.IsChildOf(root.RootTransform) || root.RootTransform.IsChildOf(handInfo.GrabbingPoint)) break;

                foreach (var childTransform in root.ChildTransforms) {
                    var radius = grabbyBone.GetRadius(childTransform);
                    var currentDistance = Vector3.Distance(handInfo.GrabbingPoint.position, childTransform.position);
                    // MelonLogger.Msg($"\tPicked closest! radius: {radius}, currentDistance: {currentDistance}, maxDistance: {radius + avatarHeight * AvatarSizeToHandProportions}");

                    // Ignore if we're grabbing outside of the bone radius
                    if (currentDistance > radius + avatarHeight * AvatarSizeToHandProportions) continue;

                    // Don't allow to grab this root if is parent/child of the hand grabbing
                    if (handInfo.GrabbingPoint.IsChildOf(childTransform) || childTransform.IsChildOf(handInfo.GrabbingPoint)) break;

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
        MelonLogger.Msg($"[{GetPlayerName(handInfo.PuppetMaster)}] Grabbed: distance: {closestDistance}, bone: {(closestChildTransform == null ? "N/A" : closestChildTransform.name)}");
        #endif

        // Set the offset position
        handInfo.GrabbingOffset.position = closestChildTransform.position;

        handInfo.Grab(new GrabbedInfo(handInfo, closestGrabbyBoneInfo, closestGrabbyBoneRoot, closestChildTransform));
    }

    private static void Release(AvatarHandInfo handInfo) {
        if (!handInfo.IsGrabbing) return;
        #if DEBUG
        MelonLogger.Msg($"[{GetPlayerName(handInfo.PuppetMaster)}] Released {handInfo.GrabbedBoneInfo.Root.RootTransform.name}");
        #endif
        handInfo.Release();
    }

    [DefaultExecutionOrder(9999999)]
    internal class TheVeryLast : MonoBehaviour {

        private void FixedUpdate() {
            if (!ModConfig.MeEnabled.Value) return;

            // Update the angle parameters
            AvatarHandInfo.UpdateAngleParameters();

            // Prevent the IK Solver from running at the usual time. We're going to run manually later.
            AvatarHandInfo.SetSkipIKSolver();
        }

        private void LateUpdate() {
            if (!ModConfig.MeEnabled.Value) return;

            try {
                // We need the very last positions accurately, so it is compatible with (VRIK, and LeapMotion mod)
                OnVeryLateUpdate();

                // We need to run the skipped FABRIK solver after VRIK, otherwise vr will be funny
                AvatarHandInfo.ExecuteIKSolver();
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
                    AvatarHandInfo.ReleaseAll();
                });

                _initialize = true;
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}.");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.Start))]
        public static void After_CVRAvatar_Start(CVRAvatar __instance) {
            // Initialize the avatar's hand info
            try {

                // Need to get it like this because if the avatars are hidden via distance seem to not call the whole enumerator
                var puppetMaster = __instance.transform.parent.GetComponentInParent<PuppetMaster>();
                AvatarHandInfo.Create(__instance, puppetMaster);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_CVRAvatar_Start)}.");
                MelonLogger.Error(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRAvatar), nameof(CVRAvatar.OnDestroy))]
        public static void After_PlayerSetup_OnDestroy(CVRAvatar __instance) {
            // Cleanup the avatar's hand info
            try {

                AvatarHandInfo.Delete(__instance);
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_OnDestroy)}.");
                MelonLogger.Error(e);
            }
        }

        private static void CreateMagicaRoot(HashSet<Tuple<Transform, HashSet<Transform>>> results, Transform root, List<Transform> childNodes, Transform transformToPivot) {
            var actualChildren = childNodes.Where(ct => ct != null && ct != root && ct.IsChildOf(transformToPivot));
            results.Add(new Tuple<Transform, HashSet<Transform>>(root, actualChildren.ToHashSet()));
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
                #if DEBUG_ROOT_GEN
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
                        #if DEBUG_ROOT_GEN
                        MelonLogger.Msg($"[PopulateMagicaRoots]{new string('\t', it)} Creating Root {root.name} pivoting on {directChild.name}");
                        #endif
                        CreateMagicaRoot(results, root, useTransformList, directChild);
                    }
                    // Otherwise let's go down the chain
                    else {
                        #if DEBUG_ROOT_GEN
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
                #if DEBUG_ROOT_GEN
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

                // We only want to mess with the avatar's magica cloth bones, for now
                if (__instance.GetComponentInParent<CVRAvatar>() == null) return;

                if (__instance.gameObject.name.Contains(IgnoreGrabbyBonesTag)) return;

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

                GrabbyBoneInfo grabbyBoneInfo = new GrabbyMagicaBoneInfo(puppetMaster, __instance, __instance.Params.GravityDirection);

                for (var i = 0; i < __instance.ClothTarget.RootCount; i++) {

                    var canFixedRotate = !__instance.Params.useFixedNonRotation;

                    var root = __instance.ClothTarget.GetRoot(i);
                    if (root == null) continue;

                    var innerRoots = new HashSet<Tuple<Transform, HashSet<Transform>>>();

                    // Create our own roots, because magica is funny and roots are not really roots ;_;
                    PopulateMagicaRoots(ref innerRoots, root, __instance.useTransformList, selectionList, canFixedRotate, false, 0);

                    // Iterate our generated roots
                    foreach (var innerRoot in innerRoots) {
                        if (innerRoot.Item2.Count == 0) continue;

                        var fabrik = innerRoot.Item1.gameObject.AddComponent<FABRIK>();
                        fabrik.solver.useRotationLimits = true;
                        fabrik.fixTransforms = true;
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
            try {

                foreach (var grabbyBoneInfo in GrabbyBonesCache) {
                    if (grabbyBoneInfo.HasInstance(__instance)) {
                        foreach (var root in grabbyBoneInfo.Roots.Where(root => root.IK != null)) {
                            UnityEngine.Object.Destroy(root.IK);
                        }
                    }
                }
                AvatarHandInfo.ReleaseAllWithBehavior(__instance);
                GrabbyBonesCache.RemoveWhere(gb => gb.HasInstance(__instance));
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_MagicaBoneCloth_ClothDispose)}.");
                MelonLogger.Error(e);
            }
        }


        private static Dictionary<Transform, HashSet<Transform>> GetDynBoneRoots(Animator animator, DynamicBone dynamicBone, Transform root, IEnumerable<Transform> childNodes, int it = 0) {

            var result = new Dictionary<Transform, HashSet<Transform>>();
            var currentChildren = childNodes.Where(ct => ct != null && ct != root && !dynamicBone.m_Exclusions.Contains(ct) && ct.IsChildOf(root)).ToHashSet();

            #if DEBUG_ROOT_GEN
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

                #if DEBUG_ROOT_GEN
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
                #if DEBUG_ROOT_GEN
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

                // We only want to mess with the dynamic bones from avatars, for now
                if (__instance.GetComponentInParent<CVRAvatar>() == null) return;

                if (__instance.gameObject.name.Contains(IgnoreGrabbyBonesTag)) return;

                var puppetMaster = __instance.GetComponentInParent<PuppetMaster>();
                var animator = puppetMaster == null ? PlayerSetup.Instance._animator : puppetMaster._animator;

                GrabbyBoneInfo grabbyBoneInfo = new GrabbyDynamicBoneInfo(puppetMaster, __instance, __instance.m_Gravity, __instance.m_Force);

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
                    fabrik.fixTransforms = true;
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
            try {

                AvatarHandInfo.ReleaseAllWithBehavior(__instance);
                GrabbyBonesCache.RemoveWhere(gb => gb.HasInstance(__instance));
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_DynamicBone_OnDestroy)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
