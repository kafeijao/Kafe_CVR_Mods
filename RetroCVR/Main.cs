using System.Collections;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util.AssetFiltering;
using ABI_RC.Systems.GameEventSystem;
using ABI.CCK.Components;
using HarmonyLib;
using Kafe.RetroCVR.CCK;
using MelonLoader;
using SK.Libretro.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kafe.RetroCVR;

public class RetroCVR : MelonMod {

    internal static LibretroInstanceVariable globalInstanceVariable;

    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();
        ModConfig.InitializeBTKUI();
        ModConfig.InitializeFolders();
        ModConfig.LoadAssemblyResources(MelonAssembly.Assembly);

        SharedFilter._spawnableWhitelist.Add(typeof(RetroCVRCore));

        // CVRGameEventSystem.Instance.OnConnected.AddListener(instanceID => {
        //     if (!CVRWorld.Instance.allowSpawnables || AuthManager.Username != "Kafeijao" || MetaPort.Instance.isUsingVr) return;
        //     MelonLogger.Msg($"Connected to instance: {instanceID} Spawning in one seconds...");
        //     IEnumerator DelaySpawnProp() {
        //         yield return new WaitForSeconds(3f);
        //         PlayerSetup.Instance.DropProp("8de53302-ccd6-4a2a-869a-0e6e13cf7bdc");
        //     }
        //     MelonCoroutines.Start(DelaySpawnProp());
        // });

        // // Extract the native binary to the plugins folder
        // const string dll1Name = "NAudio.Core.dll";
        // const string dll2Name = "NAudio.WinMM.dll";
        // const string dst1Path = "ChilloutVR_Data/Plugins/x86_64/" + dll1Name;
        // const string dst2Path = "ChilloutVR_Data/Plugins/x86_64/" + dll2Name;
        //
        // try {
        //     MelonLogger.Msg($"Copying the {dll1Name} to {dst1Path}");
        //     using var resourceStream1 = MelonAssembly.Assembly.GetManifestResourceStream(dll1Name);
        //     using var fileStream1 = File.Open(dst1Path, FileMode.Create, FileAccess.Write);
        //     resourceStream1!.CopyTo(fileStream1);
        //
        //     MelonLogger.Msg($"Copying the {dll2Name} to {dst2Path}");
        //     using var resourceStream2 = MelonAssembly.Assembly.GetManifestResourceStream(dll2Name);
        //     using var fileStream2 = File.Open(dst2Path, FileMode.Create, FileAccess.Write);
        //     resourceStream2!.CopyTo(fileStream2);
        // }
        // catch (IOException ex) {
        //     MelonLogger.Error("Failed to copy native library: " + ex.Message);
        // }

        // CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((spawnerGuid, spawnable) => {
        //     // Add and initialize the SK.Libretro components
        //     spawnable.gameObject.AddComponent<AudioProcessor>();
        //     spawnable.gameObject.AddComponent<LibretroInstance>();
        // });

    }

    [HarmonyPatch]
    private class HarmonyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerSetup), nameof(PlayerSetup.Start))]
        public static void After_PlayerSetup_Start(PlayerSetup __instance) {
            try {

                // Initialize the input processor
                GameObject processorGameObject = new("LibretroInputProcessor");
                UnityEngine.Object.DontDestroyOnLoad(processorGameObject);
                var playerInputManager = processorGameObject.AddComponent<PlayerInputManager>();
                playerInputManager.EnableJoining();
                playerInputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;

                // Hack - For some reason the prefab loses the PlayerInputProcessor. Probably asset bundle
                var playerInputProcessor = ModConfig.LibretroUserInputPrefab.AddComponent<PlayerInputProcessor>();

                globalInstanceVariable = ScriptableObject.CreateInstance<LibretroInstanceVariable>();

                playerInputProcessor._libretroInstanceVariable = globalInstanceVariable;

                playerInputManager.playerPrefab = ModConfig.LibretroUserInputPrefab;

            }
            catch (Exception e) {
                MelonLogger.Error($"Error during the patched function {nameof(After_PlayerSetup_Start)}.");
                MelonLogger.Error(e);
            }
        }
    }
}
