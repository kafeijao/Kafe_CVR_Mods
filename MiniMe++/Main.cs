using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.GameServer;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.ContentClones;
using ABI.CCK.Components;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.MiniMePlusPlus;

public class MiniMePlusPlus : MelonMod
{
    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();

        Networking.Initialize();
        RemoteMiniMe.Initialize();

        Networking.Show += RemoteMiniMe.ShowFromNetwork;
        Networking.Hide += RemoteMiniMe.Hide;
    }

    [HarmonyPatch]
    public class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.Initialize))]
        private static void After_MiniMe_Initialize()
        {
            try
            {
                // ModConfig.InitializeUILibMenu(); Init after self portrait
                Networking.MiniMeInitialize();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_Initialize)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.UILibInit))]
        private static void After_CVR_MenuManager_UILibInit()
        {
            try
            {
                ModConfig.InitializeUILibMenu();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVR_MenuManager_UILibInit)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.UpdateAnchorMode))]
        private static void After_MiniMe_UpdateAnchorMode()
        {
            try
            {
                if (!MiniMe._cloneHolderObject || MiniMe._localPlayerClone == null || MiniMe._localPlayerClone.IsDestroyed)
                    return;

                Transform transform = MiniMe._cloneHolderObject.transform;
                switch (MiniMe._currentAnchorMode)
                {
                    case MiniMe.AnchorMode.QuickMenu:
                        break;
                    case MiniMe.AnchorMode.PlaySpacePickup:
                        var localScalePlaySpace = Vector3.one * ModConfig.MeMiniMeScaleMultiplier.Value;
                        if (localScalePlaySpace != transform.localScale)
                        {
                            // MelonLogger.Msg($"Changed PlaySpace MiniMe local scale to: {localScalePlaySpace:F2}");
                            transform.localScale = localScalePlaySpace;
                        }
                        break;
                    case MiniMe.AnchorMode.WorldSpacePickup:
                        var localScaleWorldSpace = Vector3.one * ModConfig.MeMiniMeScaleMultiplier.Value;
                        if (localScaleWorldSpace != transform.localScale)
                        {
                            // MelonLogger.Msg($"Changed WorldSpace MiniMe local scale to: {localScaleWorldSpace:F2}");
                            transform.localScale = localScaleWorldSpace;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_UpdateAnchorMode)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.TrySetupClone))]
        private static void After_MiniMe_TrySetupClone()
        {
            try
            {
                if (!MiniMe._cloneHolderObject || MiniMe._localPlayerClone == null || MiniMe._localPlayerClone.IsDestroyed)
                    return;

                if (!MiniMe._cloneHolderObject.TryGetComponent<CVRPickupObject>(out var cvrPickupObject))
                    return;

                // Add event handles for grabbing and dropping
                cvrPickupObject.onGrab.AddListener(_ => Networking.SendUpdate());
                cvrPickupObject.onDrop.AddListener(_ => Networking.SendUpdate());
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_TrySetupClone)} Patch", e);
            }
        }

        /// <summary>
        /// Make the MiniMe sync data updates respect the server tick rate updates
        /// </summary>
        /// <param name="update"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MetaPort), nameof(MetaPort.OnGSInfoUpdate))]
        private static void After_MetaPort_OnGSInfoUpdate(GSInfoUpdate update)
        {
            try
            {
                var newInvokeRate = 1f / update.TickRate;
                RemoteMiniMe.NetworkInterval = newInvokeRate;
                Networking.UpdateTickRate(newInvokeRate);
                MelonLogger.Msg($"Updated the MiniMe sending data rate to {newInvokeRate}Hz");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MetaPort_OnGSInfoUpdate)} Patch", e);
            }
        }
    }
}
