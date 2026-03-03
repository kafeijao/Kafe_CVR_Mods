#nullable disable

using System.Collections.Concurrent;
using ABI_RC.Core;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.IO.Social;
using ABI_RC.Systems.ChatBox;
using ABI_RC.Systems.Communications;
using ABI_RC.Systems.Communications.Audio;
using ABI_RC.Systems.Communications.Audio.Components;
using ABI_RC.Systems.GameEventSystem;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

#if WHISPER_UNITY
using System.Collections;
using Whisper;
#endif

#if WHISPER_NET
using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;
#endif

namespace Kafe.Captions;

public class Captions : MelonPlugin
{
    public static Action ReloadModel;

    private const int Channels = 1;
    private const int Frequency = 48_000;
    // private const int Frequency = 16_000;

    private const float MaxSpeechSeconds = 10f;

    private static readonly char[] UnwantedPrefixes = new[] { ':', '.', '!', '-' };

    private class PlayerSpeechState
    {
        public string PlayerId;
        public string PlayerName;
        public bool IsFriend;

        public int PlayerChannels;
        public int PlayerFrequency;

        public float[] ActiveBuffer;

        public int ActiveWritePos;

        public bool IsRecording;
        public float SpeechStartTime;
        public float LastVoiceTime;
    }

    private struct TranscriptionJob
    {
        public PlayerSpeechState Player;
        public float[] Buffer;
    }

    private static Comms_CapturePipeline _lastLocalCapturePipeline;

    private static readonly ConcurrentDictionary<Comms_AudioTap, PlayerSpeechState> Players =
        new ConcurrentDictionary<Comms_AudioTap, PlayerSpeechState>();

    private static readonly ConcurrentQueue<TranscriptionJob> Queue = new ConcurrentQueue<TranscriptionJob>();

    public override void OnPreInitialization()
    {
        // The native dlls must be loaded here, I don't know what melon loader is doing, but if we load them later
        // it will result in native crashes when used. Thanks Bono for suggesting using a Plugin~

        ModConfig.LoadMelonPrefs();
        ModConfig.LoadEmbedResources(MelonAssembly.Assembly);

        MelonLogger.Msg("Loading the native binaries for whisper...");

        #if WHISPER_UNITY

        MelonLogger.Msg("Using: whisper.unity library");

        ModConfig.LoadWhisperUnityNativeBinaries(MelonAssembly.Assembly);

        #endif

        #if WHISPER_NET

        MelonLogger.Msg("Using: whisper.net library");

        ModConfig.ExtractWhisperNetNativeBinaries(MelonAssembly.Assembly);
        RuntimeOptions.LibraryPath = ModConfig.NativeBinariesFolderFullPath;

        LogProvider.AddLogger((level, log) =>
        {
            // Remove trailing newlines
            log = log.TrimEnd(WhisperNetEndings);
            switch (level)
            {
                case WhisperLogLevel.Error:
                    MelonLogger.Error($"[whisper.net] {log}");
                    break;
                case WhisperLogLevel.Warning:
                    MelonLogger.Warning($"[whisper.net] {log}");
                    break;
                case WhisperLogLevel.None:
                    if (!ModConfig.VerboseWhisperNet) return;
                    MelonLogger.Msg($"[whisper.net] {log}");
                    break;
                case WhisperLogLevel.Cont:
                    if (!ModConfig.VerboseWhisperNet) return;
                    MelonLogger.Msg($"[whisper.net][cont] {log}");
                    break;
                case WhisperLogLevel.Debug:
                    if (!ModConfig.VerboseWhisperNet) return;
                    MelonLogger.Msg($"[whisper.net][debug] {log}");
                    break;
                case WhisperLogLevel.Info:
                    if (!ModConfig.VerboseWhisperNet) return;
                    MelonLogger.Msg($"[whisper.net][info] {log}");
                    break;
            }
        });

        // These trigger the native dll load for whisper.net
        var runtimeInfo = WhisperFactory.GetRuntimeInfo();
        IEnumerable<string> supportedLanguages = WhisperFactory.GetSupportedLanguages();

        // Might as well log them
        MelonLogger.Msg($"Runtime Info:{runtimeInfo}");
        MelonLogger.Msg($"Supported languages: {string.Join(", ", supportedLanguages)}");

        #endif
    }

    public override void OnLateInitializeMelon()
    {
        // Run the patches manually (needed because it's a plugin)
        HarmonyInstance.PatchAll(MelonAssembly.Assembly);

        InitWhisper();

        // On auth reload the model, so the username and user id are set
        CVRGameEventSystem.Authentication.OnLogin.AddListener(_ =>
        {
            TryRegisterLocalPlayer(null);
        });

        ReloadModel += InitWhisper;

        ModConfig.LoadUILib(MelonAssembly.Assembly);
    }

    public static void InitWhisper()
    {
        #if WHISPER_UNITY
        InitWhisperUnity();
        #endif

        #if WHISPER_NET
        StartWhisperNet(ModConfig.SelectedModelPath, ModConfig.UseGpu, ModConfig.ThreadsCount);
        #endif
    }

    public static void DeInitWhisper()
    {
        #if WHISPER_UNITY
        DeInitWhisperUnity();
        #endif

        #if WHISPER_NET
        Task.Run(StopGracefullyAsync);
        #endif
    }

    private static void Enqueue(TranscriptionJob job)
    {
        Queue.Enqueue(job);

        #if WHISPER_NET
        _queueSignal.Release();
        #endif
    }
    
    #region whisper.net

    #if WHISPER_NET

    private static readonly char[] WhisperNetEndings = new[] { '\r', '\n' };

    private static SemaphoreSlim _queueSignal = new SemaphoreSlim(0);
    private static CancellationTokenSource _whisperNetCts = new CancellationTokenSource();

    private static Task _backgroundTask;

    private static void StartWhisperNet(string modelPath, bool useGpu, int threads)
    {
        var previousTask = _backgroundTask;
        _backgroundTask = Task.Run(() => RunWhisperNet(modelPath, useGpu, threads, previousTask));
    }

    private static async Task StopGracefullyAsync()
    {
        await StopGracefullyAsync(_backgroundTask);
    }

    private static async Task StopGracefullyAsync(Task previousTask)
    {
        if (ModConfig.Verbose)
            MelonLogger.Msg("Stopping the previous whisper.net processor...");

        try
        {
            _whisperNetCts.Cancel();
            _queueSignal.Release(); // unblock if waiting

            if (previousTask != null)
                await previousTask;

            _queueSignal.Dispose();
            _whisperNetCts.Dispose();

            // Recreate for the next time Initializing
            _whisperNetCts = new CancellationTokenSource();
            _queueSignal = new SemaphoreSlim(0);
        }
        catch (Exception e)
        {
            MelonLogger.Error("Failed to stop the previous whisper.net processor", e);
        }
    }

    private static float[] Downsample48KTo16K(float[] input)
    {
        int newLength = input.Length / 3;
        float[] output = new float[newLength];

        int j = 0;
        for (int i = 0; i < newLength; i++)
        {
            output[i] = (input[j] + input[j + 1] + input[j + 2]) / 3f;
            j += 3;
        }

        return output;
    }

    private static async Task RunWhisperNet(string modelPath, bool useGpu, int threads, Task previousTask)
    {
        // Kill the previous instance
        await StopGracefullyAsync(previousTask);

        MelonLogger.Msg("Starting a new whisper.net processor...");

        var token = _whisperNetCts.Token;

        try
        {
            MelonLogger.Msg($"Loading whisper model, useGpu: {useGpu}, threads: {threads}, modelPath: {modelPath}");

            using var whisperFactory = WhisperFactory.FromPath(
                modelPath,
                new WhisperFactoryOptions
                {
                    UseGpu = useGpu,
                    UseFlashAttention = false,
                    GpuDevice = 0,
                });

            await using var processor = whisperFactory.CreateBuilder()
                // .WithLanguageDetection() // Will transcribe non-foreign languages instead of translating
                .WithLanguage("en") // Will attempt to always be english
                .WithTranslate()
                .WithNoContext()
                .WithSingleSegment()
                .WithThreads(threads)
                .Build();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await _queueSignal.WaitAsync(token);

                    if (token.IsCancellationRequested) break;

                    // Drain all entries
                    while (!token.IsCancellationRequested && Queue.TryDequeue(out var entry))
                    {
                        // Todo: Don't allocate lol
                        var downSampled = Downsample48KTo16K(entry.Buffer);

                        var playerName = entry.Player.PlayerName;
                        var playerId = entry.Player.PlayerId;

                        await foreach (SegmentData result in processor.ProcessAsync(downSampled, token))
                        {
                            if (ModConfig.Verbose)
                                MelonLogger.Msg($"[{playerName}] Language: {result.Language}, NoSpeech Probability: {result.NoSpeechProbability}, Text: {result.Text}");

                            // AI sometimes gives this garbo, remove the garbo prefixes
                            var finalTextTrimmed = result.Text.TrimStart(UnwantedPrefixes);

                            if (!string.IsNullOrWhiteSpace(finalTextTrimmed))
                            {
                                _ = RootLogic.RunInMainThread(() =>
                                {
                                    ChatBoxBubbleBehavior.OnMessageReceived(new ChatBoxAPI.ChatBoxMessage(
                                        ChatBoxAPI.MessageSource.Mod,
                                        playerId,
                                        $"🎙️{nameof(Captions)}🎙️\n\n{finalTextTrimmed}",
                                        false,
                                        true,
                                        true,
                                        nameof(Captions)));
                                });
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Unexpected exception during {nameof(RunWhisperNet)}", e);
            }

        }
        catch (Exception e)
        {
            MelonLogger.Error("Failed to start a new whisper.net instance", e);
        }

        MelonLogger.Msg($"[{nameof(RunWhisperNet)}] Ended the execution of this whisper.net task, IsCancellationRequested: {token.IsCancellationRequested}");
    }

    #endif

    #endregion whisper.net

    #region whiper.unity

    #if WHISPER_UNITY

    private static GameObject _managerWrapper;
    private static WhisperManager _manager;

    private static object _processCoroutine;
    private static bool _isProcessing;

    private static void DeInitWhisperUnity()
    {
        if (_managerWrapper != null)
        {
            // Stop process coroutine, hopefully this will not explode violently
            if (_processCoroutine != null)
                MelonCoroutines.Stop(_processCoroutine);
            UnityEngine.Object.Destroy(_managerWrapper);
            MelonLogger.Msg("Destroyed the current Whisper Manager");
        }
    }

    private static void InitWhisperUnity()
    {
        // Destroy previous whisper manager and wrapper
        DeInitWhisperUnity();

        string modelPath = ModConfig.ModelsPath;
        bool useGpu = ModConfig.UseGpu;

        if (!File.Exists(modelPath))
        {
            MelonLogger.Warning($"Can't start Whisper because the model provided doesn't exist on the path: {modelPath}");
            return;
        }

        MelonLogger.Msg($"Setting up the Whisper Manager. useGpu: {useGpu}, modelPath: {modelPath}");

        var managerWrapper = _managerWrapper = new GameObject($"[{nameof(Captions)}Mod]");
        UnityEngine.Object.DontDestroyOnLoad(managerWrapper);

        // Start on a disabled wrapper to prevent triggering the Awake before setup
        managerWrapper.SetActive(false);

        // Create the manager GameObject (needs to be a child of the inactive wrapper to prevent events)
        var managerGo = new GameObject("WhisperManager");
        managerGo.transform.SetParent(managerWrapper.transform);

        // Add and initialize the WhisperManager
        var whisperManager = _manager = managerGo.AddComponent<WhisperManager>();

        // Configure the whisper manager
        whisperManager.IsModelPathInStreamingAssets = false;
        whisperManager.ModelPath = modelPath;

        whisperManager.language = "en";
        whisperManager.translateToEnglish = true;

        whisperManager.SetUseGpu(useGpu); // Using reflection because it's private :eye_roll:

        whisperManager.noContext = true; // No context since the manager is shared by all players

        // Todo: is this useful?
        // whisperManager.singleSegment = true; // Test this, there's no point in multiple segments if we're doing it sequentially

        // Todo: is this useful?
        // whisperManager.initialPrompt = GetInitialPrompt("always translate other spoken languages to english text never put the text in double quotes");

        // Set active to true to trigger the Awake and all the remaining initialization
        managerWrapper.SetActive(true);

        // Start Process queue
        _processCoroutine = MelonCoroutines.Start(ProcessWhisperUnityQueue());
    }

    private static IEnumerator ProcessWhisperUnityQueue()
    {
        while (true)
        {
            // Handle Queue
            if (!_isProcessing && Queue.TryDequeue(out var state))
            {
                _isProcessing = true;
                yield return ProcessWhisperUnityPlayerBuffer(state);
                _isProcessing = false;
            }

            yield return null;

            // Game is closing
            if (CommonTools.IsQuitting)
                yield break;
        }
    }

    private static IEnumerator ProcessWhisperUnityPlayerBuffer(TranscriptionJob state)
    {
        MelonLogger.Msg($"[{state.Player.PlayerName}] Starting to process the buffer");

        string playerName = state.Player.PlayerName;
        string playerId = state.Player.PlayerId;

        Task<WhisperResult> inferenceTask = _manager.GetTextAsync(state.Buffer, state.Player.PlayerFrequency, state.Player.PlayerChannels);

        yield return new WaitUntil(() => inferenceTask.IsCompleted);
        if (inferenceTask.IsFaulted)
        {
            MelonLogger.Error("CreateStream has failed!", inferenceTask.Exception);
            yield break;
        }

        var inferenceResult = inferenceTask.Result;
        var finalText = inferenceResult.Result;

        MelonLogger.Msg($"[{playerName}] Result[{inferenceResult.Language}:{inferenceResult.LanguageId}]: {finalText}");

        // AI sometimes gives this garbo, remove the garbo prefixes
        var finalTextTrimmed= finalText.Trim();

        if (finalTextTrimmed.StartsWith(":"))
        {
            finalTextTrimmed = finalTextTrimmed[1..];
            finalTextTrimmed = finalTextTrimmed.Trim();
        }

        if (finalTextTrimmed.StartsWith("."))
        {
            finalTextTrimmed = finalTextTrimmed[1..];
            finalTextTrimmed = finalTextTrimmed.Trim();
        }

        if (finalTextTrimmed.StartsWith("!"))
        {
            finalTextTrimmed = finalTextTrimmed[1..];
            finalTextTrimmed = finalTextTrimmed.Trim();
        }

        if (finalTextTrimmed.StartsWith("-"))
        {
            finalTextTrimmed = finalTextTrimmed[1..];
            finalTextTrimmed = finalTextTrimmed.Trim();
        }

        if (!string.IsNullOrWhiteSpace(finalTextTrimmed))
        {
            RootLogic.RunInMainThread(() =>
            {
                ChatBoxBubbleBehavior.OnMessageReceived(new ChatBoxAPI.ChatBoxMessage(
                    ChatBoxAPI.MessageSource.Mod,
                    playerId,
                    $"🎙️{nameof(Captions)}🎙️\n\n{finalTextTrimmed}",
                    false,
                    true,
                    true,
                    nameof(Captions)));
            });
        }
    }

    #endif

    #endregion whiper.unity

    /// <summary>
    /// Use this cursed magic, apparently wrapping the prompt in this makes it better
    /// https://github.com/ggml-org/whisper.cpp/discussions/348#discussioncomment-4559682
    /// </summary>
    private static string GetInitialPrompt(string initialPrompt)
    {
        return $"hello how is it going {initialPrompt} goodbye one two three start stop i you me they";
    }

    private static void TryRegisterLocalPlayer(Comms_CapturePipeline tap)
    {
        if (tap == null)
            tap = _lastLocalCapturePipeline;

        if (tap == null)
        {
            if (ModConfig.Verbose)
                MelonLogger.Msg($"[{nameof(TryRegisterLocalPlayer)}] Waiting for the audio capture pipeline to init...");
            return;
        }

        if (!AuthManager.IsAuthenticated)
        {

            if (ModConfig.Verbose)
                MelonLogger.Msg($"[{nameof(TryRegisterLocalPlayer)}] Waiting for the user to authenticate...");
            return;
        }

        RegisterPlayer(tap, AuthManager.UserId, AuthManager.Username);
    }

    private static void RegisterPlayer(Comms_AudioTap tap, string playerId, string playerName)
    {
        const int channels = Channels;
        const int frequency = Frequency;

        int maxSamples = (int)(frequency! * channels! * MaxSpeechSeconds);

        var state = new PlayerSpeechState
        {
            PlayerId = playerId,
            PlayerName = playerName,
            ActiveBuffer = new float[maxSamples],
            ActiveWritePos = 0,
            PlayerChannels = channels,
            PlayerFrequency = frequency,
            IsFriend = Friends.FriendsWith(playerId),
        };

        Players[tap] = state;

        if (ModConfig.Verbose)
            MelonLogger.Msg($"{playerName} comms have been registered with a buffer of {maxSamples} samples.");
    }

    private static void UnRegisterPlayer(Comms_AudioTap tap)
    {
        Players.TryRemove(tap, out var removed);
        if (ModConfig.Verbose)
            MelonLogger.Msg($"{removed.PlayerName} comms have been unregistered");
    }

    private static void ProcessAudio(Comms_AudioTap tap, float[] data, bool isMuted)
    {
        if (!Players.TryGetValue(tap, out var state))
            return;

        float amplitude = GetAmplitude(data);

        bool startSpeaking = amplitude > ModConfig.StartThreshold;
        bool continueSpeaking = amplitude > ModConfig.ContinueThreshold;

        float now = Time.time;

        if (startSpeaking && !state.IsRecording && !isMuted)
        {
            state.IsRecording = true;
            state.SpeechStartTime = now;
            state.ActiveWritePos = 0;

            // Todo: We're cleaning the buffer here, but this should probably not be needed, was me trying to find ghosts
            Array.Clear(state.ActiveBuffer, 0, state.ActiveBuffer.Length);

            if (ModConfig.Verbose)
                MelonLogger.Msg($"[{state.PlayerName}] Started speaking... amplitude: {amplitude:F5}");
        }

        if (state.IsRecording)
        {
            if (!isMuted)
            {
                int remaining = state.ActiveBuffer.Length - state.ActiveWritePos;
                int copyCount = Mathf.Min(remaining, data.Length);
                Array.Copy(data, 0, state.ActiveBuffer, state.ActiveWritePos, copyCount);
                state.ActiveWritePos += copyCount;

                if (continueSpeaking)
                    state.LastVoiceTime = now;
            }

            bool silenceTimeout = now - state.LastVoiceTime > ModConfig.SilenceStopSeconds;
            bool maxDuration = now - state.SpeechStartTime > MaxSpeechSeconds;
            bool overflow = state.ActiveWritePos >= state.ActiveBuffer.Length;

            if (isMuted || silenceTimeout || maxDuration || overflow)
            {
                if (ModConfig.Verbose)
                    MelonLogger.Msg($"[{state.PlayerName}] Finished speaking... amplitude: {amplitude:F5}, isMuted: {isMuted}, silenceTimeout: {silenceTimeout}, maxDuration: {maxDuration}, overflow: {overflow}");
                FinishSpeech(state);
            }
        }
    }

    private static float GetAmplitude(float[] data)
    {
        float sum = 0f;

        for (int i = 0; i < data.Length; i++)
            sum += data[i] * data[i];

        return Mathf.Sqrt(sum / data.Length);
    }

    private static void FinishSpeech(PlayerSpeechState state)
    {
        if (state.ActiveWritePos <= 0)
        {
            if (ModConfig.Verbose)
                MelonLogger.Msg($"[{state.PlayerName}] Ignored speech, the write position was: {state.ActiveWritePos}");
            state.ActiveWritePos = 0;
            state.IsRecording = false;
            return;
        }

        int frozenLength = state.ActiveWritePos;

        // Todo: Stop this from allocating, think of a better way, I was having issues so I hacked this in
        float[] trimmed = new float[frozenLength];
        Array.Copy(state.ActiveBuffer, trimmed, frozenLength);

        Enqueue(new TranscriptionJob
        {
            Player = state,
            Buffer = trimmed,
        });

        state.ActiveWritePos = 0;
        state.IsRecording = false;

        float audioSeconds = (float)frozenLength / (state.PlayerFrequency * state.PlayerChannels);

        if (ModConfig.Verbose)
            MelonLogger.Msg($"[{state.PlayerName}] Queued {frozenLength}/{state.ActiveBuffer.Length} samples, which is {audioSeconds} seconds of audio");
    }

    public override void OnUpdate()
    {
        if (!ModConfig.Enabled) return;

        float now = Time.time;

        // Handle silent and max duration detection
        foreach (PlayerSpeechState playerState in Players.Values)
        {
            if (!playerState.IsRecording)
                continue;

            bool silenceTimeout = now - playerState.LastVoiceTime > ModConfig.SilenceStopSeconds;
            bool maxDuration = now - playerState.SpeechStartTime > MaxSpeechSeconds;

            if (silenceTimeout || maxDuration)
            {
                if (ModConfig.Verbose)
                    MelonLogger.Msg($"[{playerState.PlayerName}] Finished speaking... silenceTimeout: {silenceTimeout}, maxDuration: {maxDuration}");
                FinishSpeech(playerState);
            }
        }
    }

    [HarmonyPatch]
    private class HarmonyPatches
    {
        #region Comms_CapturePipeline

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Comms_CapturePipeline), nameof(Comms_CapturePipeline.Init))]
        private static void After_Comms_CapturePipeline_Init(Comms_CapturePipeline __instance)
        {
            try
            {
                _lastLocalCapturePipeline = __instance;
                TryRegisterLocalPlayer(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_Comms_CapturePipeline_Init)}", e);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Comms_CapturePipeline), nameof(Comms_CapturePipeline.End))]
        private static void After_Comms_CapturePipeline_End(Comms_CapturePipeline __instance)
        {
            try
            {
                _lastLocalCapturePipeline = null;
                UnRegisterPlayer(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_Comms_CapturePipeline_End)}", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Comms_AudioCapture), nameof(Comms_AudioCapture.TransmitPacket))]
        private static void Before_Comms_AudioCapture_TransmitPacket(float[] data, ushort sequenceIdx)
        {
            try
            {
                if (!ModConfig.Enabled)
                    return;

                if (!ModConfig.ProcessLocalPlayer)
                    return;

                if (_lastLocalCapturePipeline == null)
                    return;

                ProcessAudio(_lastLocalCapturePipeline, data, Comms_Manager.IsMicMuted);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_Comms_AudioCapture_TransmitPacket)}", e);
            }
        }

        #endregion

        #region Comms_ParticipantPipeline

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Comms_ParticipantPipeline), nameof(Comms_ParticipantPipeline.Init))]
        private static void After_Comms_ParticipantPipeline_Init(Comms_ParticipantPipeline __instance) {
            try
            {
                RegisterPlayer(__instance, __instance._puppetMaster.CVRPlayerEntity.Uuid, __instance._puppetMaster.CVRPlayerEntity.Username);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_Comms_ParticipantPipeline_Init)}", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Comms_ParticipantPipeline), nameof(Comms_ParticipantPipeline.End))]
        private static void After_Comms_ParticipantPipeline_End(Comms_ParticipantPipeline __instance)
        {
            try
            {
                UnRegisterPlayer(__instance);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(After_Comms_ParticipantPipeline_End)}", e);
            }
        }

        #endregion Comms_ParticipantPipeline

        private static readonly Dictionary<Comms_AudioProcessor, Comms_AudioTap> AudioProcessorsMap =
            new Dictionary<Comms_AudioProcessor, Comms_AudioTap>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Comms_AudioProcessor), nameof(Comms_AudioProcessor.QueueSampleForPlayback))]
        private static void Before_Comms_AudioProcessor_SetData(Comms_AudioProcessor __instance, float[] sample)
        {
            try
            {
                if (!ModConfig.Enabled)
                    return;

                if (sample == null || sample.Length <= 0)
                    return;

                if (!AudioProcessorsMap.TryGetValue(__instance, out Comms_AudioTap audioTap) || audioTap == null)
                    return;

                if (!Players.TryGetValue(audioTap, out var player))
                    return;

                if (!ModConfig.ProcessFriends && player.IsFriend)
                    return;

                if (!ModConfig.ProcessOthers && !player.IsFriend)
                    return;

                ProcessAudio(audioTap, sample, false);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_Comms_AudioProcessor_SetData)}", e);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Comms_AudioTap), nameof(Comms_AudioTap.ResetProcessor))]
        private static void After_Comms_AudioTap_ResetProcessor(Comms_AudioTap __instance)
        {
            try
            {
                var processor = __instance.GetProcessor();
                if (processor != null)
                    AudioProcessorsMap[processor] = __instance;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_Comms_AudioProcessor_SetData)}", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Comms_AudioTap), nameof(Comms_AudioTap.OnDestroy))]
        private static void Before_Comms_AudioTap_OnDestroy(Comms_AudioTap __instance)
        {
            try
            {
                var processor = __instance.GetProcessor();
                if (processor != null)
                    AudioProcessorsMap.Remove(processor);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error during the patched function {nameof(Before_Comms_AudioProcessor_SetData)}", e);
            }
        }
    }
}
