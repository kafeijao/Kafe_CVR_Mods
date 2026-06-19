using System.Runtime.InteropServices;

namespace Kafe.BetterDECtalk;

public static class DECtalkNative
{
    private const string DLL = "DECtalk.dll";

    #region Constants

    public const uint WAVE_MAPPER = 0xFFFFFFFF;

    // Startup device flags (from ttsapi.h)
    public const uint OWN_AUDIO_DEVICE        = 0x00000001;
    public const uint REPORT_OPEN_ERROR       = 0x00000002;
    public const uint DO_NOT_USE_AUDIO_DEVICE = 0x80000000;

    // TextToSpeechSpeak flags
    public const uint TTS_NORMAL = 0;
    public const uint TTS_FORCE  = 1;

    // 11025 Hz, Mono, 16-bit PCM
    public const uint WAVE_FORMAT_1M16 = 0x00000004;

    // Speaker IDs
    public const uint PAUL   = 0;
    public const uint BETTY  = 1;
    public const uint HARRY  = 2;
    public const uint FRANK  = 3;
    public const uint DENNIS = 4;
    public const uint KIT    = 5;
    public const uint URSULA = 6;
    public const uint RITA   = 7;
    public const uint WENDY  = 8;

    public const uint MMSYSERR_NOERROR = 0;

    #endregion Constants

    #region Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct TTS_BUFFER_T
    {
        public IntPtr lpData;
        public IntPtr lpPhonemeArray;
        public IntPtr lpIndexArray;
        public uint dwMaximumBufferLength;
        public uint dwMaximumNumberOfPhonemeChanges;
        public uint dwMaximumNumberOfIndexMarks;
        public uint dwBufferLength;
        public uint dwNumberOfPhonemeChanges;
        public uint dwNumberOfIndexMarks;
        public uint dwReserved;
    }

    #endregion Structures


    #region Delegates

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DtCallbackRoutine(
        int lParam1,
        int lParam2,
        uint drCallbackParameter,
        uint uiMsg);

    #endregion Delegates

    #region Functions

    [DllImport("user32.dll")]
    public static extern uint RegisterWindowMessage([MarshalAs(UnmanagedType.LPStr)] string lpString);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechStartupEx(
        out IntPtr handle,
        uint devNo,
        uint devOptions,
        DtCallbackRoutine callback,
        ref IntPtr dwCallbackParameter);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechShutdown(IntPtr handle);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechSpeak(
        IntPtr handle,
        [MarshalAs(UnmanagedType.LPStr)] string text,
        uint flags);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechSync(IntPtr handle);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechSetSpeaker(IntPtr handle, uint speaker);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechGetSpeaker(IntPtr handle, out uint speaker);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechSetRate(IntPtr handle, uint rate);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechGetRate(IntPtr handle, out uint rate);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechReset(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool bReset);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechOpenInMemory(IntPtr handle, uint format);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechCloseInMemory(IntPtr handle);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern uint TextToSpeechAddBuffer(IntPtr handle, ref TTS_BUFFER_T buffer);

    #endregion Functions
}
