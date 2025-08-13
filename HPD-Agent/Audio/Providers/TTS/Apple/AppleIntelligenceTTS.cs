using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
/// <summary>
/// Apple Intelligence Text-to-Speech client implementing custom TTS interface
/// Uses AVSpeechSynthesizer (iOS/tvOS/watchOS) and NSSpeechSynthesizer (macOS)
/// </summary>
[Experimental("HPDAUDIO001")]
public sealed class AppleIntelligenceTextToSpeechClient : ITextToSpeechClient
{
    private readonly AppleIntelligenceConfig _config;
    private readonly ILogger<AppleIntelligenceTextToSpeechClient>? _logger;
    private readonly SpeechSynthesizerHandle _synthesizer;
    private bool _disposed;

    /// <summary>
    /// Gets whether Apple Speech Synthesis is available on this system
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                return AVSpeechSynthesisInterop.IsAvailable();
            }
            catch
            {
                return false;
            }
        }
    }

    public AppleIntelligenceTextToSpeechClient(
        AppleIntelligenceConfig config,
        ILogger<AppleIntelligenceTextToSpeechClient>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        if (!IsAvailable)
        {
            throw new NotSupportedException(
                "Apple Speech Synthesis not available. Requires iOS 7.0+/macOS 10.7+ with speech synthesis enabled.");
        }

        try
        {
            _synthesizer = AVSpeechSynthesisInterop.CreateSynthesizer();
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("Failed to initialize Apple Speech Synthesizer.", ex);
        }
    }

    public async Task<TextToSpeechResponse> GetAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger?.LogDebug("Starting Apple Intelligence text-to-speech conversion");

            var voiceId = options?.Voice ?? _config.DefaultVoice ?? "com.apple.voice.compact.en-US.Samantha";
            var language = options?.Language ?? _config.DefaultLanguage ?? "en-US";
            var speed = options?.Speed ?? 0.5f;
            var pitch = options?.Pitch ?? 1.0f;

            // Create utterance with Apple's TTS
            var utteranceHandle = AVSpeechSynthesisInterop.CreateUtterance(text);
            
            // Configure utterance properties
            AVSpeechSynthesisInterop.SetVoice(utteranceHandle, voiceId);
            AVSpeechSynthesisInterop.SetRate(utteranceHandle, speed);
            AVSpeechSynthesisInterop.SetPitchMultiplier(utteranceHandle, pitch);
            AVSpeechSynthesisInterop.SetVolume(utteranceHandle, 1.0f);

            // Synthesize audio
            var audioData = await NativeSpeechSynthesisAsync(_synthesizer, utteranceHandle, cancellationToken);
            var audioStream = ConvertAppleAudioDataToStream(audioData);

            _logger?.LogDebug("Successfully synthesized audio using Apple Intelligence TTS");

            return new TextToSpeechResponse(audioStream)
            {
                ResponseId = Guid.NewGuid().ToString("N"),
                ModelId = options?.ModelId ?? "apple-avspeechsynthesizer",
                Voice = voiceId,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["provider"] = "Apple Intelligence",
                    ["on_device"] = true,
                    ["language"] = language,
                    ["voice_id"] = voiceId,
                    ["api_version"] = "avspeechsynthesizer_ios7"
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Apple Intelligence text-to-speech conversion failed");
            throw;
        }
        finally
        {
            // Clean up utterance handle if needed
        }
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text,
        TextToSpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger?.LogDebug("Starting Apple Intelligence streaming text-to-speech");

        var responseId = Guid.NewGuid().ToString("N");
        var voiceId = options?.Voice ?? _config.DefaultVoice ?? "com.apple.voice.compact.en-US.Samantha";

        // Use callback-based streaming with Channel
        var channel = Channel.CreateUnbounded<TextToSpeechResponseUpdate>();
        var writer = channel.Writer;

        var handler = new StreamingSpeechSynthesisHandler((audioChunk, chunkSize, isComplete, error) =>
        {
            if (error != IntPtr.Zero)
            {
                writer.TryComplete(new InvalidOperationException("Streaming speech synthesis failed"));
                return;
            }

            if (chunkSize > 0)
            {
                var audioBytes = new byte[chunkSize];
                Marshal.Copy(audioChunk, audioBytes, 0, chunkSize);
                var audioStream = new MemoryStream(audioBytes);

                var update = new TextToSpeechResponseUpdate(audioStream)
                {
                    Kind = isComplete ? TextToSpeechResponseUpdateKind.AudioCompleted : TextToSpeechResponseUpdateKind.AudioUpdated,
                    ModelId = options?.ModelId ?? "apple-avspeechsynthesizer",
                    Voice = voiceId,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["provider"] = "Apple Intelligence",
                        ["chunk_size"] = chunkSize,
                        ["is_complete"] = isComplete
                    }
                };

                if (!writer.TryWrite(update))
                {
                    writer.TryComplete();
                    return;
                }
            }

            if (isComplete)
            {
                writer.TryComplete();
            }
        });

        // Create utterance and start streaming synthesis
        var utteranceHandle = AVSpeechSynthesisInterop.CreateUtterance(text);
        AVSpeechSynthesisInterop.SetVoice(utteranceHandle, voiceId);
        AVSpeechSynthesisInterop.SetRate(utteranceHandle, options?.Speed ?? 0.5f);

        try
        {
            AVSpeechSynthesisInterop.StartStreamingSynthesis(_synthesizer, utteranceHandle, handler);

            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            AVSpeechSynthesisInterop.StopStreamingSynthesis(_synthesizer);
            AVSpeechSynthesisInterop.DisposeUtterance(utteranceHandle);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));

        return serviceKey is not null ? null :
            serviceType == typeof(AppleIntelligenceTextToSpeechClient) ? this :
            serviceType == typeof(AppleIntelligenceConfig) ? _config :
            null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            AVSpeechSynthesisInterop.DisposeSynthesizer(_synthesizer);
            _disposed = true;
        }
    }

    #region Private Implementation

    private static async Task<ManagedAudioData> NativeSpeechSynthesisAsync(
        SpeechSynthesizerHandle synthesizer,
        SpeechUtteranceHandle utterance,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ManagedAudioData>();
        
        var handler = new SpeechSynthesisHandler((audioData, audioSize, errorPtr) =>
        {
            if (errorPtr != IntPtr.Zero)
            {
                tcs.TrySetException(new InvalidOperationException("Speech synthesis failed"));
            }
            else
            {
                var managedAudioData = new ManagedAudioData(audioData, audioSize);
                tcs.TrySetResult(managedAudioData);
            }
        });

        AVSpeechSynthesisInterop.SynthesizeUtterance(synthesizer, utterance, handler);
        cancellationToken.Register(() => tcs.TrySetCanceled());
        
        return await tcs.Task;
    }

    private static Stream ConvertAppleAudioDataToStream(ManagedAudioData audioData)
    {
        var audioBytes = new byte[audioData.Size];
        Marshal.Copy(audioData.Handle, audioBytes, 0, audioData.Size);
        return new MemoryStream(audioBytes);
    }

    private sealed class ManagedAudioData : IDisposable
    {
        public IntPtr Handle { get; }
        public int Size { get; }
        private bool _disposed;

        public ManagedAudioData(IntPtr handle, int size)
        {
            Handle = handle;
            Size = size;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                AVSpeechSynthesisInterop.DisposeAudioData(Handle);
                _disposed = true;
            }
        }
    }

    #endregion
}