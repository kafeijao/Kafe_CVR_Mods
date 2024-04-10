using ABI_RC.Systems.GameEventSystem;
using MelonLoader;
using SteamAudio;
using UnityEngine;

namespace Kafe.SteamAudioOverrides;

public class SteamAudioOverrides : MelonMod {


    public override void OnInitializeMelon() {

        ModConfig.InitializeMelonPrefs();

        CVRGameEventSystem.Spawnable.OnInstantiate.AddListener((spawnerGuid, spawnable) => {
            if (!ModConfig.MeForceAddSteamAudioSources.Value) return;
            foreach (var audioSource in spawnable.GetComponentsInChildren<AudioSource>(true)) {
                if (audioSource.GetComponent<SteamAudioSource>() != null) continue;
                audioSource.spatialize = true;
                var sas = audioSource.gameObject.AddComponent<SteamAudioSource>();
                sas.distanceAttenuation = true;
                sas.directivity = true;
                sas.occlusion = true;
                sas.pathing = true;
                sas.reflections = true;
                sas.transmission = true;
            }
        });
    }
}
