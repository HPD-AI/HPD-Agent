public class OpenRouterConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    // Default to OpenRouter's chat completions endpoint if not set
    public string Endpoint { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string HttpReferer { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 1.0;
    public int DefaultMaxTokenTotal { get; set; } = 4096;
}
