using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.Camera;
using HarmonyLib;
using Kafe.QRCode.ResultHandlers;
using MelonLoader;
using TMPro;

namespace Kafe.QRCode;

public class QRCode : MelonMod {

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        // Register Handlers

        // CVR Entities
        ResultHandler.RegisterHandler(new AvatarResultHandler());
        ResultHandler.RegisterHandler(new PropResultHandler());
        ResultHandler.RegisterHandler(new UserResultHandler());
        ResultHandler.RegisterHandler(new WorldResultHandler());
        ResultHandler.RegisterHandler(new InstanceResultHandler());

        // URL optional handler
        ResultHandler.RegisterHandler(new URLResultHandler());

        // Fallback default to copying to clipboard
        ResultHandler.RegisterHandler(new DefaultResultHandler());

        // Add Text Mesh Pro to the props whitelist
        SharedFilter._spawnableWhitelist.Add(typeof(TextMeshPro));
        MelonLogger.Msg($"Adding {nameof(TextMeshPro)} type to the props whitelist...");
    }


    [HarmonyPatch]
    internal class HarmonyPatches {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PortableCamera), nameof(PortableCamera.Start))]
        public static void After_PortableCamera_Start(PortableCamera __instance) {
            try {
                MelonLogger.Msg("Initializing the camera QR Menu...");
                var targetTransform = PortableCamera.Instance.cameraCanvasGroup.transform.parent;
                var qrMenu = UnityEngine.Object.Instantiate(ModConfig.QRCodePrefab, targetTransform, false);
                qrMenu.AddComponent<QRCodeBehavior>();
            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patch: {nameof(After_PortableCamera_Start)}");
                MelonLogger.Error(e);
            }
        }
    }
}
