using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


/// <summary>
/// Clean audio capability using Extensions.AI
/// Integrates STT, LLM, and TTS with sophisticated error handling and filtering
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class AudioCapability : IAsyncDisposable
{
    private readonly Agent _agent;
    private readonly AudioCapabilityOptions _options;
    private readonly ISpeechToTextClient? _sttClient;
    private readonly ITextToSpeechClient? _ttsClient;
    private readonly ScopedFilterManager? _filterManager;
    private readonly ILogger<AudioCapability>? _logger;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioCapability"/> class
    /// </summary>
    /// <param name="agent">The agent this capability belongs to</param>
    /// <param name="options">Audio capability options</param>
    /// <param name="sttClient">Speech-to-text client (optional)</param>
    /// <param name="ttsClient">Text-to-speech client (optional)</param>
    /// <param name="filterManager">Filter manager for applying scoped filters</param>
    /// <param name="logger">Logger instance</param>
    public AudioCapability(
        Agent agent,
        AudioCapabilityOptions options,
        ISpeechToTextClient? sttClient = null,
        ITextToSpeechClient? ttsClient = null,
        ScopedFilterManager? filterManager = null,
        ILogger<AudioCapability>? logger = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sttClient = sttClient;
        _ttsClient = ttsClient;
        _filterManager = filterManager;
        _logger = logger;
    }

    /// <summary>Complete STT → LLM → TTS pipeline</summary>
    /// <param name="audioInput">The input audio stream</param>
    /// <param name="options">Processing options for this request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete audio processing response</returns>
    public async Task<AudioResponse> ProcessAudioAsync(
        Stream audioInput,
        AudioProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(audioInput);
        
        options ??= new AudioProcessingOptions();
        
        await _processingLock.WaitAsync(cancellationToken);
        try
        {
            using var timeoutCts = new CancellationTokenSource(_options.OperationTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var stopwatch = Stopwatch.StartNew();
            var metrics = new AudioProcessingMetrics();
            
            // STT Phase
            var transcriptionStopwatch = Stopwatch.StartNew();
            var transcribedText = await TranscribeAudioAsync(audioInput, options, linkedCts.Token);
            transcriptionStopwatch.Stop();
            metrics.TranscriptionDuration = transcriptionStopwatch.Elapsed;
            
            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                _logger?.LogWarning("Audio transcription produced empty result");
                return new AudioResponse
                {
                    Metrics = metrics,
                    Warnings = { "No speech detected in audio input" }
                };
            }
            
            // LLM Phase
            var llmStopwatch = Stopwatch.StartNew();
            var responseText = await ProcessWithLlmAsync(transcribedText, options, linkedCts.Token);
            llmStopwatch.Stop();
            metrics.LlmDuration = llmStopwatch.Elapsed;
            
            // TTS Phase
            Stream? audioResponse = null;
            if (_ttsClient != null && !string.IsNullOrWhiteSpace(responseText))
            {
                var ttsStopwatch = Stopwatch.StartNew();
                audioResponse = await SynthesizeAudioAsync(responseText, options, linkedCts.Token);
                ttsStopwatch.Stop();
                metrics.SynthesisDuration = ttsStopwatch.Elapsed;
            }
            
            stopwatch.Stop();
            metrics.TotalDuration = stopwatch.Elapsed;
            metrics.InputAudioBytes = GetStreamLength(audioInput);
            metrics.OutputAudioBytes = GetStreamLength(audioResponse);
            
            return new AudioResponse
            {
                TranscribedText = options.IncludeTranscription ? transcribedText : null,
                ResponseText = responseText,
                AudioStream = audioResponse,
                Metrics = metrics
            };
        }
        finally
        {
            _processingLock.Release();
        }
    }

    /// <summary>Speech-to-text only</summary>
    /// <param name="audioInput">The input audio stream</param>
    /// <param name="options">Speech-to-text options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcribed text</returns>
    public async Task<string> TranscribeAsync(
        Stream audioInput,
        SpeechToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureSttAvailable();
        
        return await TranscribeAudioAsync(audioInput, 
            new AudioProcessingOptions { SttModel = options?.ModelId, Language = options?.SpeechLanguage }, 
            cancellationToken);
    }

    /// <summary>Text-to-speech only</summary>
    /// <param name="text">The text to synthesize</param>
    /// <param name="options">Text-to-speech options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Synthesized audio stream</returns>
    public async Task<Stream> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureTtsAvailable();
        
        return await SynthesizeAudioAsync(text, 
            new AudioProcessingOptions { TtsModel = options?.ModelId, Voice = options?.Voice }, 
            cancellationToken);
    }

    /// <summary>Streaming STT → LLM → TTS</summary>
    /// <param name="audioInput">The input audio stream</param>
    /// <param name="options">Processing options for this request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of audio response updates</returns>
    public async IAsyncEnumerable<AudioResponseUpdate> ProcessAudioStreamingAsync(
        Stream audioInput,
        AudioProcessingOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        options ??= new AudioProcessingOptions();
        
        // STT Streaming
        await foreach (var sttUpdate in _sttClient!.GetStreamingTextAsync(audioInput, 
            new SpeechToTextOptions { ModelId = options.SttModel, SpeechLanguage = options.Language }, 
            cancellationToken))
        {
            yield return new AudioResponseUpdate
            {
                Kind = AudioUpdateKind.TranscriptionUpdated,
                Text = sttUpdate.Text
            };
            
            if (sttUpdate.Kind == SpeechToTextResponseUpdateKind.TextUpdated)
            {
                // Process complete transcription with LLM
                yield return new AudioResponseUpdate { Kind = AudioUpdateKind.LlmProcessing };
                
                var responseText = await ProcessWithLlmAsync(sttUpdate.Text, options, cancellationToken);
                
                yield return new AudioResponseUpdate
                {
                    Kind = AudioUpdateKind.LlmCompleted,
                    Text = responseText
                };
                
                // TTS if available
                if (_ttsClient != null)
                {
                    yield return new AudioResponseUpdate { Kind = AudioUpdateKind.SynthesisStarted };
                    
                    var audioStream = await SynthesizeAudioAsync(responseText, options, cancellationToken);
                    
                    yield return new AudioResponseUpdate
                    {
                        Kind = AudioUpdateKind.SynthesisCompleted,
                        AudioChunk = audioStream
                    };
                }
            }
        }
    }

    /// <summary>Check if STT is available</summary>
    public bool HasSpeechToText => _sttClient != null;
    
    /// <summary>Check if TTS is available</summary>
    public bool HasTextToSpeech => _ttsClient != null;

    #region Private Implementation Methods

    private async Task<string> TranscribeAudioAsync(Stream audioInput, AudioProcessingOptions options, CancellationToken cancellationToken)
    {
        EnsureSttAvailable();
        
        var sttOptions = new SpeechToTextOptions
        {
            ModelId = options.SttModel ?? _options.DefaultSttModel,
            SpeechLanguage = options.Language ?? _options.DefaultLanguage
        };
        
        // Apply STT filters if available
        if (_filterManager != null)
        {
            var filters = _filterManager.GetApplicableFilters("Audio.SpeechToText");
            // TODO: Apply filters (implementation depends on filter architecture)
        }
        
        var response = await _sttClient!.GetTextAsync(audioInput, sttOptions, cancellationToken);
        return response.Text;
    }
    
    private async Task<string> ProcessWithLlmAsync(string text, AudioProcessingOptions options, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();
        
        if (!string.IsNullOrWhiteSpace(options.CustomInstructions))
        {
            messages.Add(new ChatMessage(ChatRole.System, options.CustomInstructions));
        }
        
        messages.Add(new ChatMessage(ChatRole.User, text));
        
        var response = await _agent.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Messages.LastOrDefault()?.Text ?? string.Empty;
    }
    
    private async Task<Stream> SynthesizeAudioAsync(string text, AudioProcessingOptions options, CancellationToken cancellationToken)
    {
        EnsureTtsAvailable();
        
        var ttsOptions = new TextToSpeechOptions
        {
            ModelId = options.TtsModel ?? _options.DefaultTtsModel,
            Voice = options.Voice ?? _options.DefaultVoice,
            Language = options.Language ?? _options.DefaultLanguage
        };
        
        var response = await _ttsClient!.GetAudioAsync(text, ttsOptions, cancellationToken);
        return response.AudioStream;
    }
    
    private void EnsureSttAvailable()
    {
        if (_sttClient == null)
            throw new InvalidOperationException("Speech-to-text client not configured");
    }
    
    private void EnsureTtsAvailable()
    {
        if (_ttsClient == null)
            throw new InvalidOperationException("Text-to-speech client not configured");
    }
    
    private static long GetStreamLength(Stream? stream) => 
        stream?.CanSeek == true ? stream.Length : 0;
    
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioCapability));
    }

    #endregion

    /// <summary>Disposes the audio capability and its resources</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_sttClient != null)
                _sttClient.Dispose();
            if (_ttsClient != null)
                _ttsClient.Dispose();
            
            _processingLock?.Dispose();
            _disposed = true;
        }
        
        await Task.CompletedTask; // Satisfy async requirement
    }
}
