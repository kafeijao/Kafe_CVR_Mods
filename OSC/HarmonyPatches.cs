using ABI_RC.Core;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using HarmonyLib;
using MelonLoader;

namespace OSC; 

[HarmonyPatch]
internal class HarmonyPatches {

    // Avatar
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AvatarDetails_t), "Recycle")]
    internal static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance) {
        Events.Avatar.OnAvatarDetailsReceived(__instance.AvatarId, __instance.AvatarName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateAnimatorManager))]
    internal static void AfterUpdateAnimatorManager(CVRAnimatorManager manager) {
        Events.Avatar.OnAnimatorManagerUpdate(manager);
    }

    // Parameters
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterFloat))]
    internal static void AfterSetAnimatorParameterFloat(string name, float value, CVRAnimatorManager __instance) {
        Events.Avatar.OnParameterChangedFloat(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterInt))]
    internal static void AfterSetAnimatorParameterInt(string name, int value, CVRAnimatorManager __instance) {
        Events.Avatar.OnParameterChangedInt(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterBool))]
    internal static void AfterSetAnimatorParameterBool(string name, bool value, CVRAnimatorManager __instance) {
        Events.Avatar.OnParameterChangedBool(__instance, name, value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRAnimatorManager), nameof(CVRAnimatorManager.SetAnimatorParameterTrigger))]
    internal static void AfterSetAnimatorParameterTrigger(string name, CVRAnimatorManager __instance) {
        Events.Avatar.OnParameterChangedTrigger(__instance, name);
    }
    
    // Scene
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CVRInputManager), "Start")]
    private static void AfterInputManagerCreated() {
        Events.Scene.OnInputManagerCreated();
    }
}
