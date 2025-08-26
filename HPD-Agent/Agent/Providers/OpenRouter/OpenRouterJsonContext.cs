using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using static HPDAIFunctionFactory;

/// <summary>
/// AOT-compatible JSON source generation context for OpenRouter API types
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
[JsonSerializable(typeof(OpenRouterRequest))]
[JsonSerializable(typeof(OpenRouterResponse))]
[JsonSerializable(typeof(OpenRouterMessage))]
[JsonSerializable(typeof(OpenRouterToolCall))]
[JsonSerializable(typeof(OpenRouterFunction))]
[JsonSerializable(typeof(OpenRouterTool))]
[JsonSerializable(typeof(OpenRouterToolFunction))]
[JsonSerializable(typeof(OpenRouterChoice))]
[JsonSerializable(typeof(OpenRouterUsage))]
[JsonSerializable(typeof(OpenRouterError))]
[JsonSerializable(typeof(OpenRouterReasoning))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(List<OpenRouterMessage>))]
[JsonSerializable(typeof(List<OpenRouterToolCall>))]
[JsonSerializable(typeof(List<OpenRouterTool>))]
[JsonSerializable(typeof(List<OpenRouterChoice>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
internal partial class OpenRouterJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Combined type info resolver that includes both OpenRouter and HPD types
    /// </summary>
    public static IJsonTypeInfoResolver Combined { get; } = 
        JsonTypeInfoResolver.Combine(Default, HPDJsonContext.Default);
}