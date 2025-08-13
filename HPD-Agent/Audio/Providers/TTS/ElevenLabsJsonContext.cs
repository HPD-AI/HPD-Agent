using System.Text.Json;
using System.Text.Json.Serialization;

namespace HPD_Agent.Audio.Providers.TTS
{
    // Context for source-generated JSON serialization for native AOT
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ElevenLabsTtsRequest))]
    [JsonSerializable(typeof(VoiceSettings))]
    internal partial class ElevenLabsJsonContext : JsonSerializerContext
    {
    }
}
