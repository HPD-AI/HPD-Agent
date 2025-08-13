using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using HPD_Agent.Audio.Providers.TTS;
using Microsoft.Extensions.Logging;


/// <summary>
/// ElevenLabs Text-to-Speech client for Extensions.AI
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class ElevenLabsTextToSpeechClient : ITextToSpeechClient
{
    private readonly ElevenLabsConfig _config;
    private readonly string? _voiceId;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElevenLabsTextToSpeechClient>? _logger;
    private readonly bool _disposeHttpClient;

    public ElevenLabsTextToSpeechClient(
        ElevenLabsConfig config, 
        string? voiceId = null,
        HttpClient? httpClient = null, 
        ILogger<ElevenLabsTextToSpeechClient>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _voiceId = voiceId ?? config.DefaultVoiceId;
        _logger = logger;
        
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _disposeHttpClient = true;
        }
        
        _httpClient.DefaultRequestHeaders.Add("xi-api-key", _config.ApiKey);
    }

    public async Task<TextToSpeechResponse> GetAudioAsync(
        string text, 
        TextToSpeechOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        try
        {
            // Build typed request model for source-generated serialization
            var requestBody = new ElevenLabsTtsRequest
            {
                Text = text,
                ModelId = options?.ModelId ?? _config.ModelId ?? "eleven_multilingual_v2",
                VoiceSettings = new VoiceSettings
                {
                    Stability = _config.Stability ?? 0.5f,
                    SimilarityBoost = _config.SimilarityBoost ?? 0.75f,
                    Style = _config.Style ?? 0.0f,
                    UseSpeakerBoost = _config.UseSpeakerBoost ?? true
                },
                OutputFormat = _config.OutputFormat ?? "mp3_44100_128"
            };

            // Use source-generated context for native AOT compatibility
            var json = JsonSerializer.Serialize(requestBody, ElevenLabsJsonContext.Default.ElevenLabsTtsRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var voiceId = options?.Voice ?? _voiceId;
            var url = $"{_config.BaseUrl}/text-to-speech/{voiceId}";

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"ElevenLabs TTS failed: {response.StatusCode} - {errorContent}");
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var audioStream = new MemoryStream(audioBytes);
            
            return new TextToSpeechResponse(audioStream)
            {
                ModelId = options?.ModelId ?? _config.ModelId,
                Voice = voiceId
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ElevenLabs TTS conversion failed");
            throw;
        }
    }

    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, 
        TextToSpeechOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ElevenLabs supports streaming TTS but for simplicity, we'll implement basic version
        var response = await GetAudioAsync(text, options, cancellationToken);
        
        yield return new TextToSpeechResponseUpdate(response.AudioStream)
        {
            Kind = TextToSpeechResponseUpdateKind.AudioCompleted,
            ModelId = response.ModelId,
            Voice = response.Voice
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(ElevenLabsConfig) ? _config : null;
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
