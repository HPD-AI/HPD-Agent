using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;


/// <summary>
/// Apple Intelligence Speech-to-Text client implementing Microsoft.Extensions.AI interface
/// Uses iOS 26+/macOS 26+ SpeechAnalyzer and SpeechTranscriber APIs
/// </summary>
[Experimental("MEAI001")]
public sealed class AppleIntelligenceSpeechToTextClient : ISpeechToTextClient
{
    private readonly AppleIntelligenceConfig _config;
    private readonly ILogger<AppleIntelligenceSpeechToTextClient>? _logger;
    private readonly SpeechToTextClientMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Gets whether Apple Intelligence Speech Recognition is available on this system
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                return SpeechAnalyzerInterop.IsAvailable();
            }
            catch
            {
                return false;
            }
        }
    }

    public AppleIntelligenceSpeechToTextClient(
        AppleIntelligenceConfig config, 
        ILogger<AppleIntelligenceSpeechToTextClient>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        
        if (!IsAvailable)
        {
            throw new NotSupportedException(
                "Apple Intelligence Speech Recognition not available. Requires iOS 26.0+/macOS 26.0+ with Apple Intelligence enabled.");
        }

        _metadata = new SpeechToTextClientMetadata(
            providerName: "Apple Intelligence",
            providerUri: null,
            defaultModelId: _config.DefaultSpeechModel ?? "apple-speechanalyzer-v1");
    }

    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (audioSpeechStream == null)
            throw new ArgumentNullException(nameof(audioSpeechStream));

        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger?.LogDebug("Starting Apple Intelligence speech-to-text conversion");

            var locale = options?.SpeechLanguage ?? _config.DefaultLanguage ?? "en-US";
            var modelId = options?.ModelId ?? _config.DefaultSpeechModel ?? "apple-speechanalyzer-v1";

            // Convert audio stream to Apple's required format
            using var audioData = await ConvertStreamToAppleAudioDataAsync(audioSpeechStream, cancellationToken);
            
            // Use SpeechAnalyzer + SpeechTranscriber for transcription
            var transcription = await NativeSpeechAnalysisAsync(
                audioData.Handle,
                locale,
                cancellationToken);

            _logger?.LogDebug("Successfully transcribed audio using Apple Intelligence");

            return new SpeechToTextResponse(transcription)
            {
                ResponseId = Guid.NewGuid().ToString("N"),
                ModelId = modelId,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["provider"] = "Apple Intelligence",
                    ["on_device"] = true,
                    ["locale"] = locale,
                    ["api_version"] = "speechanalyzer_ios26"
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Apple Intelligence speech recognition failed");
            throw;
        }
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (audioSpeechStream == null)
            throw new ArgumentNullException(nameof(audioSpeechStream));

        ObjectDisposedException.ThrowIf(_disposed, this);

        var locale = options?.SpeechLanguage ?? _config.DefaultLanguage ?? "en-US";
        var responseId = Guid.NewGuid().ToString("N");

        _logger?.LogDebug("Starting Apple Intelligence streaming speech-to-text");

        // Convert audio stream for Apple's format
        using var audioData = await ConvertStreamToAppleAudioDataAsync(audioSpeechStream, cancellationToken);

        // ===== FIXED: Use callback-based streaming =====
        var channel = Channel.CreateUnbounded<SpeechToTextResponseUpdate>();
        var writer = channel.Writer;

        var handler = new StreamingSpeechAnalysisHandler((transcription, isFinal, confidence, error) =>
        {
            if (error != IntPtr.Zero)
            {
                writer.TryComplete(new InvalidOperationException("Streaming speech analysis failed"));
                return;
            }

            var update = new SpeechToTextResponseUpdate(transcription)
            {
                Kind = isFinal ? SpeechToTextResponseUpdateKind.TextUpdated : SpeechToTextResponseUpdateKind.TextUpdated,
                ResponseId = responseId,
                ModelId = options?.ModelId ?? _config.DefaultSpeechModel ?? "apple-speechanalyzer-v1",
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["is_final"] = isFinal,
                    ["confidence"] = confidence,
                    ["provider"] = "Apple Intelligence"
                }
            };

            if (!writer.TryWrite(update))
            {
                writer.TryComplete();
            }

            if (isFinal)
            {
                writer.TryComplete();
            }
        });

        var transcriber = SpeechAnalyzerInterop.CreateTranscriber(locale, 0);
        var analyzer = SpeechAnalyzerInterop.CreateAnalyzer(new[] { transcriber });

        try
        {
            SpeechAnalyzerInterop.StartStreamingAnalysis(analyzer, audioData.Handle, handler);

            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            SpeechAnalyzerInterop.StopStreamingAnalysis(analyzer);
            SpeechAnalyzerInterop.DisposeAnalyzer(analyzer);
            SpeechAnalyzerInterop.DisposeTranscriber(transcriber);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == null)
            throw new ArgumentNullException(nameof(serviceType));

        return serviceKey is not null ? null :
            serviceType == typeof(SpeechToTextClientMetadata) ? _metadata :
            serviceType == typeof(AppleIntelligenceSpeechToTextClient) ? this :
            serviceType == typeof(AppleIntelligenceConfig) ? _config :
            null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Clean up any native resources
            _disposed = true;
        }
    }

    #region Private Implementation

    private static async Task<ManagedAudioData> ConvertStreamToAppleAudioDataAsync(
        Stream audioStream, 
        CancellationToken cancellationToken)
    {
        // Convert the audio stream to Apple's expected format
        using var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream, cancellationToken);
        var audioBytes = memoryStream.ToArray();
        
        var handle = SpeechAnalyzerInterop.CreateAudioData(audioBytes, audioBytes.Length);
        return new ManagedAudioData(handle);
    }

    private static async Task<string> NativeSpeechAnalysisAsync(
        AudioDataHandle audioData,
        string locale,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();
        
        var handler = new SpeechAnalysisHandler((transcription, errorPtr) =>
        {
            if (errorPtr != IntPtr.Zero)
            {
                tcs.TrySetException(new InvalidOperationException("Speech analysis failed"));
            }
            else
            {
                tcs.TrySetResult(transcription);
            }
        });

        // Create transcriber and analyzer
        var transcriber = SpeechAnalyzerInterop.CreateTranscriber(locale, 0); // 0 = default preset
        var analyzer = SpeechAnalyzerInterop.CreateAnalyzer(new[] { transcriber });
        
        try
        {
            SpeechAnalyzerInterop.AnalyzeAudio(analyzer, audioData, handler);
            cancellationToken.Register(() => tcs.TrySetCanceled());
            return await tcs.Task;
        }
        finally
        {
            SpeechAnalyzerInterop.DisposeAnalyzer(analyzer);
            SpeechAnalyzerInterop.DisposeTranscriber(transcriber);
        }
    }

    // Old NativeStreamingSpeechAnalysisAsync method removed; now using callback-based streaming in GetStreamingTextAsync

    private sealed class ManagedAudioData : IDisposable
    {
        public AudioDataHandle Handle { get; }
        private bool _disposed;

        public ManagedAudioData(AudioDataHandle handle)
        {
            Handle = handle;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                SpeechAnalyzerInterop.DisposeAudioData(Handle);
                _disposed = true;
            }
        }
    }

    private readonly struct SpeechResult
    {
        public string Text { get; init; }
        public bool IsFinal { get; init; }
        public float Confidence { get; init; }
    }

    #endregion
}