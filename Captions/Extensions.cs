#nullable disable

using System.Reflection;
using ABI_RC.Systems.Communications.Audio;
using ABI_RC.Systems.Communications.Audio.Components;
using UnityEngine;

#if WHISPER_UNITY
using Whisper;
#endif

namespace Kafe.Captions;

public static class Extensions
{
    public static AudioSource GetAudioSource(this Comms_AudioTap audioTap)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("AudioSource",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return propertyInfo.GetValue(audioTap) as AudioSource;
        }
        return null;
    }

    public static Comms_AudioProcessor GetProcessor(this Comms_AudioTap audioTap)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("Processor",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return propertyInfo.GetValue(audioTap) as Comms_AudioProcessor;
        }
        return null;
    }

    public static int? GetBufferSize(this Comms_AudioProcessor audioProcessor)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("BufferSize",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return (int) propertyInfo.GetValue(audioProcessor);
        }
        return null;
    }

    public static int? GetSampleRate(this Comms_AudioProcessor audioProcessor)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("SampleRate",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return (int) propertyInfo.GetValue(audioProcessor);
        }
        return null;
    }

    public static int? GetChannels(this Comms_AudioProcessor audioProcessor)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("Channels",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return (int) propertyInfo.GetValue(audioProcessor);
        }
        return null;
    }

    public static bool? GetStarted(this Comms_AudioTap audioTap)
    {
        PropertyInfo propertyInfo = typeof(Comms_AudioTap).GetProperty("Started",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty);
        if (propertyInfo != null)
        {
            return (bool) propertyInfo.GetValue(audioTap);
        }
        return null;
    }


    #if WHISPER_UNITY

    public static void SetUseGpu(this WhisperManager manager, bool value)
    {
        FieldInfo fieldInfo = typeof(WhisperManager).GetField("useGpu",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (fieldInfo != null)
        {
            fieldInfo.SetValue(manager, value);
        }
    }

    #endif
}
