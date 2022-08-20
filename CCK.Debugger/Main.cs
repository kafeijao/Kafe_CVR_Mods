using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.MovementSystem;
using CCK.Debugger.Components;
using CCK.Debugger.Components.MenuHandlers;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace CCK.Debugger;

public class CCKDebugger : MelonMod {
    
    private const string _assetPath = "Assets/Prefabs/CCKDebuggerMenu.prefab";

    [HarmonyPatch]
    private class HarmonyPatches
    {
        // Spawnables
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Spawnable_t), nameof(Spawnable_t.Recycle))]
        static void BeforeSpawnableDetailsRecycle(Spawnable_t __instance) {
            Events.Spawnable.OnSpawnableDetailsRecycled(__instance);
        }
        
        // Avatar
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AvatarDetails_t), nameof(AvatarDetails_t.Recycle))]
        static void BeforeAvatarDetailsRecycle(AvatarDetails_t __instance) {
            Events.Avatar.OnAvatarDetailsRecycled(__instance);
        }
        
        // Player
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MovementSystem), nameof(MovementSystem.UpdateAnimatorManager))]
        static void AfterUpdateAnimatorManager(CVRAnimatorManager manager) {
            Events.Avatar.OnAnimatorManagerUpdate(manager);
        }
        
        //Quick Menu
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.ToggleQuickMenu))]
        private static void AfterMenuToggle(bool show) {
            Events.QuickMenu.OnQuickMenuIsShownChanged(show);
        }
        
        // Players
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void BeforeCreatePlayer(ref CVRPlayerManager __instance, out int __state) {
            __state = __instance.NetworkPlayers.Count;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVRPlayerManager), nameof(CVRPlayerManager.TryCreatePlayer))]
        private static void AfterCreatePlayer(ref CVRPlayerManager __instance, int __state) {
            if(__state < __instance.NetworkPlayers.Count) {
                var player = __instance.NetworkPlayers[__state];
                Events.Player.OnPlayerLoaded(player.Uuid, player.Username);
            }
        }
        
        // Scene
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), "Start")]
        private static void AfterMenuCreated(ref CVR_MenuManager __instance) {
            var quickMenuTransform = __instance.quickMenu.transform;
            
            // Load prefab from asset bundle
            AssetBundle assetBundle = AssetBundle.LoadFromMemory(Resources.Resources.cckdebugger);
            assetBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            var prefab = assetBundle.LoadAsset<GameObject>(_assetPath);
            
            // Instantiate and add the controller script
            var cckDebugger = UnityEngine.Object.Instantiate(prefab, quickMenuTransform);
            Events.DebuggerMenu.MenuInit += AvatarMenuHandler.Init;
            Events.DebuggerMenu.MenuUpdate += AvatarMenuHandler.Update;
            cckDebugger.AddComponent<Menu>();
            
            // Add ourselves to the player list (why not here xd)
            Events.Player.OnPlayerLoaded(MetaPort.Instance.ownerId, MetaPort.Instance.username);
        }
    }
}