using System.Runtime.InteropServices;

namespace Kafe.BetterDECtalk;

public sealed class DECtalkEngine : IDisposable
{
    public static DECtalkEngine Instance { private set; get; }

    private bool _enabledPhonemes;

    private IntPtr _handle;
    private bool _disposed;
    private readonly object _lock = new object();

    // Callback must be stored as a field to prevent GC collection
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly DECtalkNative.DtCallbackRoutine _callback;

    // Buffer message ID from Windows
    private static readonly uint UiBufferMsg = DECtalkNative.RegisterWindowMessage("DECtalkBufferMessage");

    // In-memory render state
    private MemoryStream _bufferStream;
    private byte[] _dataArray;
    private GCHandle _dataPin;
    private DECtalkNative.TTS_BUFFER_T _ttsBuffer;

    private const int BufferSize = 64 * 1024; // 64 KB

    public DECtalkEngine()
    {
        _callback = TtsCallback;

        uint result = DECtalkNative.TextToSpeechStartupEx(
            out _handle,
            DECtalkNative.WAVE_MAPPER,
            DECtalkNative.DO_NOT_USE_AUDIO_DEVICE,
            _callback,
            ref _handle);

        if (result != DECtalkNative.MMSYSERR_NOERROR)
            throw new InvalidOperationException($"DECtalk TextToSpeechStartupEx failed with code {result}.");

        Instance = this;
    }

    private void TtsCallback(int lParam1, int lParam2, uint drCallbackParameter, uint uiMsg)
    {
        if (uiMsg == UiBufferMsg && _bufferStream != null)
        {
            // Drain filled buffer data
            int bytesWritten = (int)_ttsBuffer.dwBufferLength;
            if (bytesWritten > 0)
            {
                _bufferStream.Write(_dataArray, 0, bytesWritten);
            }

            // Reset and resubmit
            _ttsBuffer.dwBufferLength = 0;
            Check(DECtalkNative.TextToSpeechAddBuffer(_handle, ref _ttsBuffer));
        }
    }

    public DECtalkVoice Voice
    {
        get { ThrowIfDisposed(); DECtalkNative.TextToSpeechGetSpeaker(_handle, out uint s); return (DECtalkVoice)s; }
        set { ThrowIfDisposed(); Check(DECtalkNative.TextToSpeechSetSpeaker(_handle, (uint)value)); }
    }

    public uint Rate
    {
        get { ThrowIfDisposed(); DECtalkNative.TextToSpeechGetRate(_handle, out uint r); return r; }
        set { ThrowIfDisposed(); Check(DECtalkNative.TextToSpeechSetRate(_handle, value)); }
    }

    public void SetPhonemes(bool enablePhonemes)
    {
        _enabledPhonemes = enablePhonemes;
    }

    public byte[] SpeakToMemory(string text)
    {
        // Decide whether to do phonemes or not
        text = $"[:phone {(_enabledPhonemes ? "on" : "off")}]" + text;

        ThrowIfDisposed();
        lock (_lock)
        {
            _dataArray = new byte[BufferSize];
            _dataPin = GCHandle.Alloc(_dataArray, GCHandleType.Pinned);
            _bufferStream = new MemoryStream();

            try
            {
                _ttsBuffer = new DECtalkNative.TTS_BUFFER_T
                {
                    lpData = _dataPin.AddrOfPinnedObject(),
                    lpPhonemeArray = IntPtr.Zero,
                    lpIndexArray = IntPtr.Zero,
                    dwMaximumBufferLength = BufferSize,
                    dwMaximumNumberOfPhonemeChanges = 0,
                    dwMaximumNumberOfIndexMarks = 0,
                    dwBufferLength = 0,
                    dwNumberOfPhonemeChanges = 0,
                    dwNumberOfIndexMarks = 0,
                    dwReserved = 0,
                };

                Check(DECtalkNative.TextToSpeechOpenInMemory(_handle, DECtalkNative.WAVE_FORMAT_1M16));
                Check(DECtalkNative.TextToSpeechAddBuffer(_handle, ref _ttsBuffer));
                Check(DECtalkNative.TextToSpeechSpeak(_handle, text, DECtalkNative.TTS_FORCE));
                Check(DECtalkNative.TextToSpeechSync(_handle));

                // After Sync, the callback has already drained all full buffers.
                // But there may be a partial final buffer that didn't trigger a callback.
                int remaining = (int)_ttsBuffer.dwBufferLength;
                if (remaining > 0)
                    _bufferStream.Write(_dataArray, 0, remaining);

                Check(DECtalkNative.TextToSpeechCloseInMemory(_handle));
                return _bufferStream.ToArray();
            }
            catch
            {
                try { DECtalkNative.TextToSpeechReset(_handle, true); } catch { }
                try { DECtalkNative.TextToSpeechCloseInMemory(_handle); } catch { }
                return Array.Empty<byte>();
            }
            finally
            {
                _bufferStream = null;
                _dataPin.Free();
            }
        }
    }

    public short[] SpeakToSamples(string text)
    {
        byte[] pcm = SpeakToMemory(text);
        var samples = new short[pcm.Length / 2];
        Buffer.BlockCopy(pcm, 0, samples, 0, pcm.Length);
        return samples;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
        {
            DECtalkNative.TextToSpeechShutdown(_handle);
            _handle = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    ~DECtalkEngine() => Dispose();

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DECtalkEngine));
    }

    private static void Check(uint mmResult)
    {
        if (mmResult != DECtalkNative.MMSYSERR_NOERROR)
            throw new InvalidOperationException($"DECtalk API call failed with MMRESULT code {mmResult}");
    }
}
