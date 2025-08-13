using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


/// <summary>
/// ElevenLabs Speech-to-Text client for Extensions.AI
/// Ports existing Semantic Kernel implementation to cleaner interface
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class ElevenLabsSpeechToTextClient : ISpeechToTextClient
{
    private readonly ElevenLabsConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElevenLabsSpeechToTextClient>? _logger;
    private readonly bool _disposeHttpClient;

    public ElevenLabsSpeechToTextClient(
        ElevenLabsConfig config, 
        HttpClient? httpClient = null, 
        ILogger<ElevenLabsSpeechToTextClient>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
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

    public async Task<SpeechToTextResponse> GetTextAsync(
        Stream audioSpeechStream, 
        SpeechToTextOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Starting ElevenLabs speech-to-text conversion");

            using var httpContent = new MultipartFormDataContent();
            
            // Convert stream to byte array
            using var memoryStream = new MemoryStream();
            await audioSpeechStream.CopyToAsync(memoryStream, cancellationToken);
            var audioData = memoryStream.ToArray();
            
            var audioHttpContent = new ByteArrayContent(audioData);
            // Support multiple audio formats - try webm first since that's what browsers send
            audioHttpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
            
            httpContent.Add(audioHttpContent, "file", "audio.webm");
            httpContent.Add(new StringContent("scribe_v1"), "model_id");
            httpContent.Add(new StringContent("json"), "response_format");

            var response = await _httpClient.PostAsync(_config.BaseUrl + "/speech-to-text", httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("ElevenLabs STT API request failed: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"ElevenLabs STT failed: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // For now, use a simple manual parsing approach to avoid AOT issues
            // TODO: Replace with proper JSON source generation
            var text = ExtractTextFromJson(jsonResponse);
            
            _logger?.LogDebug("Successfully transcribed audio using ElevenLabs STT");
            
            return new SpeechToTextResponse(text)
            {
                ModelId = options?.ModelId ?? "scribe_v1"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during ElevenLabs speech-to-text conversion");
            throw;
        }
    }

    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(
        Stream audioSpeechStream, 
        SpeechToTextOptions? options = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ElevenLabs doesn't support streaming STT, so fall back to single response
        var response = await GetTextAsync(audioSpeechStream, options, cancellationToken);
        
        yield return new SpeechToTextResponseUpdate(response.Text)
        {
            Kind = SpeechToTextResponseUpdateKind.TextUpdated,
            ResponseId = response.ResponseId,
            ModelId = response.ModelId
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
    
    /// <summary>
    /// Simple JSON text extraction to avoid AOT issues with reflection-based deserialization
    /// </summary>
    private static string ExtractTextFromJson(string json)
    {
        // Simple extraction for {"text": "value"} pattern
        var textStart = json.IndexOf("\"text\"");
        if (textStart == -1) return string.Empty;
        
        var valueStart = json.IndexOf(':', textStart);
        if (valueStart == -1) return string.Empty;
        
        var quoteStart = json.IndexOf('"', valueStart + 1);
        if (quoteStart == -1) return string.Empty;
        
        var quoteEnd = json.IndexOf('"', quoteStart + 1);
        if (quoteEnd == -1) return string.Empty;
        
        return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }
}

/// <summary>
/// ElevenLabs transcription response model
/// </summary>
[Experimental("HPDAUDIO001")]
public sealed class ElevenLabsTranscriptionResponse
{
    public string? Text { get; set; }
}
