using ABI_RC.Systems.Communications.TTS;
using MelonLoader;

namespace Kafe.BetterDECtalk;

public class DECtalkTTSModule : Comms_TTSModule
{
    private DECtalkEngine _engine;

    public override void Initialize()
    {
        Channels = 1;
        SampleRate = 11_025;

        foreach (DECtalkVoice voice in Enum.GetValues(typeof(DECtalkVoice)))
        {
            string name = voice.ToString(); // e.g. "Paul"
            Voices[name] = name;
        }

        var speakingRate = (uint)ModConfig.SpeakingRate.Value;
        var enablePhonemes = ModConfig.EnablePhonemes.Value;

        try
        {
            _engine = new DECtalkEngine();
            _engine.Rate = speakingRate;
            _engine.SetPhonemes(enablePhonemes);
            MelonLogger.Msg($"DECtalk engine initialized successfully with a Speaking Rate of {speakingRate} " +
                            $"words per minute and phonemes enabled: {enablePhonemes}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error("Failed to initialize DECtalk", ex);
        }
    }

    public override short[] Process(string msg)
    {
        try
        {
            _engine.Voice = (DECtalkVoice)Enum.Parse(typeof(DECtalkVoice), CurrentVoice);
            return _engine.SpeakToSamples(msg);
        }
        catch (Exception e)
        {
            MelonLogger.Error("Failed to process DECtalk", e);
            return null;
        }
    }
}
