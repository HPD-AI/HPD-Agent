using Microsoft.Extensions.AI;


/// <summary>
/// ElevenLabs configuration with Extensions.AI metadata support
/// </summary>
/// [Experimental("HPDAUDIO001")]
public sealed class ElevenLabsConfig
{
    /// <summary>Gets or sets the ElevenLabs API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the default voice ID to use.</summary>
    public string? DefaultVoiceId { get; set; }

    /// <summary>Gets or sets the model ID for text-to-speech.</summary>
    public string? ModelId { get; set; } = "eleven_multilingual_v2";

    /// <summary>Gets or sets the base URL for the ElevenLabs API.</summary>
    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/v1";

    /// <summary>Gets or sets the output format for generated audio.</summary>
    public string? OutputFormat { get; set; } = "mp3_44100_128";

    // Voice settings
    /// <summary>Gets or sets the stability setting for voice generation (0.0 - 1.0).</summary>
    public float? Stability { get; set; } = 0.5f;

    /// <summary>Gets or sets the similarity boost setting for voice generation (0.0 - 1.0).</summary>
    public float? SimilarityBoost { get; set; } = 0.75f;

    /// <summary>Gets or sets the style setting for voice generation (0.0 - 1.0).</summary>
    public float? Style { get; set; } = 0.0f;

    /// <summary>Gets or sets whether to use speaker boost.</summary>
    public bool? UseSpeakerBoost { get; set; } = true;

    /// <summary>Creates Extensions.AI metadata for speech-to-text.</summary>
    /// <returns>A <see cref="SpeechToTextClientMetadata"/> instance.</returns>
    public SpeechToTextClientMetadata ToSttMetadata() => new(
        providerName: "ElevenLabs",
        providerUri: new Uri(BaseUrl),
        defaultModelId: "scribe_v1");

    /// <summary>Validates the configuration.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("ElevenLabs API key is required");
        
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("ElevenLabs base URL is required");
    }
}
