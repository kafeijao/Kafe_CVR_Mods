using ABI_RC.Systems.Communications.Audio.TTS;
using MelonLoader;

namespace Kafe.BetterDECtalk;

public class BetterDECtalk : MelonMod
{
    private const string DECtalkName = "DECtalk";

    public override void OnLateInitializeMelon()
    {
        ModConfig.LoadMelonPrefs();
        ModConfig.ExtractFilesAndBinaries(MelonAssembly.Assembly);
        Comms_TTSHandler.AddModule<DECtalkTTSModule>(DECtalkName, DECtalkName);
    }
}
