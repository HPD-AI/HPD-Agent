using Microsoft.Extensions.AI;

/// A data-centric class that holds all the serializable configuration
/// for creating a new agent.
/// </summary>
public class AgentConfig
{
    public string Name { get; set; } = "HPD-Agent";
    public string SystemInstructions { get; set; } = "You are a helpful assistant.";
    
    /// <summary>
    /// Maximum number of turns the agent can take to call functions before requiring continuation permission.
    /// Each turn allows the LLM to analyze previous results and decide whether to call more functions or provide a final response.
    /// </summary>
    public int MaxFunctionCallTurns { get; set; } = 10;
    public int MaxConversationHistory { get; set; } = 20;
    
    /// <summary>
    /// How many additional turns to allow when user chooses to continue beyond the limit.
    /// This includes extra iterations for the LLM to complete its task and generate a final response.
    /// </summary>
    public int ContinuationExtensionAmount { get; set; } = 3;

    /// <summary>
    /// Configuration for the AI provider (e.g., OpenAI, Ollama).
    /// </summary>
    public ProviderConfig? Provider { get; set; }

    /// <summary>
    /// Configuration for the agent's injected memory (Full Text Injection).
    /// </summary>
    public InjectedMemoryConfig? InjectedMemory { get; set; }

    /// <summary>
    /// Configuration for the Model Context Protocol (MCP).
    /// </summary>
    public McpConfig? Mcp { get; set; }

    /// <summary>
    /// Configuration for web search capabilities.
    /// </summary>
    public WebSearchConfig? WebSearch { get; set; }

    /// <summary>
    /// Configuration for error handling behavior.
    /// </summary>
    public ErrorHandlingConfig? ErrorHandling { get; set; }
}

#region Supporting Configuration Classes

/// <summary>
/// Configuration for the agent's dynamic, editable working memory.
/// Mirrors properties from AgentInjectedMemoryOptions.
/// </summary>
public class InjectedMemoryConfig
{
    /// <summary>
    /// The root directory where agent memories will be stored.
    /// </summary>
    public string StorageDirectory { get; set; } = "./agent-injected-memory-storage";

    /// <summary>
    /// The maximum number of tokens to include from the injected memory.
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Automatically evict old memories when approaching token limit.
    /// </summary>
    public bool EnableAutoEviction { get; set; } = true;

    /// <summary>
    /// Token threshold for triggering auto-eviction (percentage).
    /// </summary>
    public int AutoEvictionThreshold { get; set; } = 85;
}

/// <summary>
/// Configuration for the Model Context Protocol (MCP).
/// </summary>
public class McpConfig
{
    public string ManifestPath { get; set; } = string.Empty;
    public MCPOptions? Options { get; set; }
}

/// <summary>
/// Configuration for AI provider settings.
/// Based on existing patterns in AgentBuilder.
/// </summary>
public class ProviderConfig
{
    public ChatProvider Provider { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }
}

/// <summary>
/// Holds all web search related configurations.
/// </summary>
public class WebSearchConfig
{
    /// <summary>
    /// The name of the default search provider to use if multiple are configured.
    /// Should match one of the keys in the provider configs (e.g., "Tavily").
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// Configuration for Tavily web search provider.
    /// </summary>
    public TavilyConfig? Tavily { get; set; }

    /// <summary>
    /// Configuration for Brave web search provider.
    /// </summary>
    public BraveConfig? Brave { get; set; }

    /// <summary>
    /// Configuration for Bing web search provider.
    /// </summary>
    public BingConfig? Bing { get; set; }
}

/// <summary>
/// Configuration for error handling behavior.
/// </summary>
public class ErrorHandlingConfig
{
    /// <summary>
    /// Whether to normalize provider-specific errors into standard formats
    /// </summary>
    public bool NormalizeErrors { get; set; } = true;

    /// <summary>
    /// Whether to include provider-specific details in error messages
    /// </summary>
    public bool IncludeProviderDetails { get; set; } = false;

    /// <summary>
    /// Maximum number of retries for transient errors
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Provider-specific configuration extensions for enhanced provider support.
/// These can be accessed via ChatOptions.AdditionalProperties or RawRepresentationFactory.
/// </summary>
public class ProviderSpecificConfig
{
    /// <summary>
    /// OpenAI-specific configuration
    /// </summary>
    public OpenAISettings? OpenAI { get; set; }

    /// <summary>
    /// Azure OpenAI-specific configuration
    /// </summary>
    public AzureOpenAISettings? AzureOpenAI { get; set; }

    /// <summary>
    /// Ollama-specific configuration
    /// </summary>
    public OllamaSettings? Ollama { get; set; }

    /// <summary>
    /// OpenRouter-specific configuration
    /// </summary>
    public OpenRouterSettings? OpenRouter { get; set; }
}

/// <summary>
/// OpenAI-specific settings that can be applied via AdditionalProperties
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// Organization ID for OpenAI API requests
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Whether to use strict JSON schema validation
    /// </summary>
    public bool? StrictJsonSchema { get; set; }

    /// <summary>
    /// Image detail level for vision models (low, high, auto)
    /// </summary>
    public string? ImageDetail { get; set; }

    /// <summary>
    /// Voice selection for audio models
    /// </summary>
    public string? AudioVoice { get; set; }

    /// <summary>
    /// Audio output format
    /// </summary>
    public string? AudioFormat { get; set; }

    /// <summary>
    /// Whether to enable reasoning tokens display
    /// </summary>
    public bool? IncludeReasoningTokens { get; set; }
}

/// <summary>
/// Azure OpenAI-specific settings
/// </summary>
public class AzureOpenAISettings
{
    /// <summary>
    /// Azure resource name
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// Deployment name for the model
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Azure OpenAI API version
    /// </summary>
    public string ApiVersion { get; set; } = "2024-08-01-preview";

    /// <summary>
    /// Whether to use Entra ID authentication instead of API key
    /// </summary>
    public bool UseEntraId { get; set; } = false;

    /// <summary>
    /// Azure region for data residency requirements
    /// </summary>
    public string? Region { get; set; }
}

/// <summary>
/// Ollama-specific settings that can be applied to requests
/// </summary>
public class OllamaSettings
{
    /// <summary>
    /// Context window size (num_ctx parameter)
    /// </summary>
    public int? NumCtx { get; set; }

    /// <summary>
    /// How long to keep the model loaded in memory
    /// </summary>
    public string? KeepAlive { get; set; }

    /// <summary>
    /// Use memory locking to prevent swapping
    /// </summary>
    public bool? UseMlock { get; set; }

    /// <summary>
    /// Number of threads to use for inference
    /// </summary>
    public int? NumThread { get; set; }

    /// <summary>
    /// Temperature override for Ollama models
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Top-p override for Ollama models
    /// </summary>
    public float? TopP { get; set; }
}

/// <summary>
/// OpenRouter-specific settings and features
/// </summary>
public class OpenRouterSettings
{
    /// <summary>
    /// HTTP Referer header for OpenRouter requests
    /// </summary>
    public string? HttpReferer { get; set; }

    /// <summary>
    /// Application name for OpenRouter analytics
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Reasoning configuration for models that support it
    /// </summary>
    public OpenRouterReasoningConfig? Reasoning { get; set; }

    /// <summary>
    /// Preferred provider for models available on multiple providers
    /// </summary>
    public string? PreferredProvider { get; set; }

    /// <summary>
    /// Whether to allow fallback to other providers if preferred is unavailable
    /// </summary>
    public bool AllowFallback { get; set; } = true;
}

/// <summary>
/// OpenRouter reasoning configuration for models like DeepSeek-R1
/// </summary>
public class OpenRouterReasoningConfig
{
    /// <summary>
    /// Whether to enable reasoning mode
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum reasoning tokens to generate
    /// </summary>
    public int? MaxReasoningTokens { get; set; }

    /// <summary>
    /// Whether to include reasoning in the response
    /// </summary>
    public bool IncludeReasoning { get; set; } = true;
}

#endregion
