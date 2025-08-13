// Models for ElevenLabs TTS request

using System.Text.Json.Serialization;

namespace HPD_Agent.Audio.Providers.TTS
{
    public class VoiceSettings
{
    [JsonPropertyName("stability")]
    public float Stability { get; set; }
    [JsonPropertyName("similarity_boost")]
    public float SimilarityBoost { get; set; }
    [JsonPropertyName("style")]
    public float Style { get; set; }
    [JsonPropertyName("use_speaker_boost")]
    public bool UseSpeakerBoost { get; set; }
    }

    public class ElevenLabsTtsRequest
    {
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = null!;
    [JsonPropertyName("voice_settings")]
    public VoiceSettings VoiceSettings { get; set; } = null!;
    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = null!;
    }
}
