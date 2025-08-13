using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;


/// <summary>
/// Azure Speech configuration with Extensions.AI metadata support
/// </summary>
[Experimental("HPDAUDIO001")]
public sealed class AzureSpeechConfig
{
    /// <summary>Gets or sets the Azure Speech API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the Azure Speech region.</summary>
    public string? Region { get; set; }

    /// <summary>Gets or sets the Azure Speech endpoint (alternative to region).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the default language for speech recognition.</summary>
    public string? DefaultLanguage { get; set; } = "en-US";

    /// <summary>Gets or sets the default voice for speech synthesis.</summary>
    public string? DefaultVoice { get; set; } = "en-US-AriaNeural";

    /// <summary>Gets or sets the output format for synthesized audio.</summary>
    public string? OutputFormat { get; set; } = "audio-24khz-96kbitrate-mono-mp3";

    /// <summary>Creates Extensions.AI metadata for speech-to-text.</summary>
    /// <returns>A <see cref="SpeechToTextClientMetadata"/> instance.</returns>
    public SpeechToTextClientMetadata ToSttMetadata() => new(
        providerName: "Azure Speech",
        providerUri: string.IsNullOrEmpty(Endpoint) ? null : new Uri(Endpoint),
        defaultModelId: "azure-speech");

    /// <summary>Validates the configuration.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("Azure Speech API key is required");
        
        if (string.IsNullOrWhiteSpace(Region) && string.IsNullOrWhiteSpace(Endpoint))
            throw new InvalidOperationException("Azure Speech region or endpoint is required");
    }
}
