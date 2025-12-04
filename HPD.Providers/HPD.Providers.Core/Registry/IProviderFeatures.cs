// HPD.Providers.Core/Registry/IProviderFeatures.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HPD.Providers.Core;

/// <summary>
/// Represents all capabilities provided by a specific provider.
/// A single provider can serve multiple HPD products (Agent, Memory, etc.)
/// by implementing different capability methods.
///
/// Implementations are contributed by provider packages via ModuleInitializer.
/// </summary>
public interface IProviderFeatures
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "openai", "anthropic", "qdrant").
    /// Must be lowercase and URL-safe (used in JSON config).
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Display name for UI purposes (e.g., "OpenAI", "Anthropic Claude", "Qdrant").
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Capability flags indicating what this provider supports.
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    // ===== AGENT CAPABILITIES (HPD-Agent) =====

    /// <summary>
    /// Create a chat client for this provider from configuration.
    /// Returns null if provider doesn't support chat.
    /// </summary>
    /// <param name="config">Provider-specific configuration</param>
    /// <param name="services">Optional service provider for DI</param>
    /// <returns>Configured IChatClient instance, or null if not supported</returns>
    IChatClient? CreateChatClient(ProviderConfig config, IServiceProvider? services = null) => null;

    /// <summary>
    /// Create an error handler for this provider.
    /// Returns null if provider doesn't provide custom error handling.
    /// </summary>
    /// <returns>Provider-specific error handler instance, or null</returns>
    IProviderErrorHandler? CreateErrorHandler() => null;

    /// <summary>
    /// Get metadata about this provider's Agent-specific capabilities.
    /// Returns null if provider doesn't support Agent features.
    /// </summary>
    /// <returns>Provider metadata, or null if not supported</returns>
    ProviderMetadata? GetMetadata() => null;

    /// <summary>
    /// Validate provider-specific configuration (synchronous).
    /// Returns null if provider doesn't support validation.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation result, or null if not supported</returns>
    ProviderValidationResult? ValidateConfiguration(ProviderConfig config) => null;

    /// <summary>
    /// Validate provider-specific configuration asynchronously with live API testing.
    /// This method can perform network requests to validate API keys, check credit balances,
    /// test model availability, etc.
    /// Returns null if provider doesn't support async validation.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result, or null if not supported</returns>
    Task<ProviderValidationResult>? ValidateConfigurationAsync(
        ProviderConfig config,
        CancellationToken cancellationToken = default) => null;

    // ===== MEMORY CAPABILITIES (HPD-Agent.Memory) =====

    /// <summary>
    /// Create an embedding provider instance.
    /// Returns null if provider doesn't support embeddings.
    /// </summary>
    /// <param name="services">Service provider for DI</param>
    /// <param name="config">Provider-specific configuration</param>
    /// <returns>Embedding provider instance, or null if not supported</returns>
    IEmbeddingProvider? CreateEmbeddingProvider(
        IServiceProvider services,
        IConfiguration config) => null;

    /// <summary>
    /// Create a vector store instance.
    /// Returns null if provider doesn't support vector storage.
    /// </summary>
    /// <param name="services">Service provider for DI</param>
    /// <param name="config">Provider-specific configuration</param>
    /// <returns>Vector store instance, or null if not supported</returns>
    IVectorStore? CreateVectorStore(
        IServiceProvider services,
        IConfiguration config) => null;

    /// <summary>
    /// Create a document store instance.
    /// Returns null if provider doesn't support document storage.
    /// </summary>
    /// <param name="services">Service provider for DI</param>
    /// <param name="config">Provider-specific configuration</param>
    /// <returns>Document store instance, or null if not supported</returns>
    IDocumentStore? CreateDocumentStore(
        IServiceProvider services,
        IConfiguration config) => null;

    /// <summary>
    /// Create a graph store instance.
    /// Returns null if provider doesn't support graph storage.
    /// </summary>
    /// <param name="services">Service provider for DI</param>
    /// <param name="config">Provider-specific configuration</param>
    /// <returns>Graph store instance, or null if not supported</returns>
    IGraphStore? CreateGraphStore(
        IServiceProvider services,
        IConfiguration config) => null;
}

// ===== AGENT SUPPORTING TYPES =====

/// <summary>
/// Provider-specific configuration container.
/// </summary>
public class ProviderConfig
{
    /// <summary>
    /// Provider identifier (lowercase, e.g., "openai", "anthropic", "ollama").
    /// This is the primary key for provider resolution.
    /// </summary>
    public string ProviderKey { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public ChatOptions? DefaultChatOptions { get; set; }

    /// <summary>
    /// Provider-specific configuration as raw JSON string.
    /// This is the preferred way for FFI/JSON configuration.
    /// The JSON is deserialized using the provider's registered deserializer.
    /// </summary>
    public string? ProviderOptionsJson { get; set; }

    /// <summary>
    /// Provider-specific configuration as key-value pairs.
    /// Legacy approach - prefer ProviderOptionsJson for FFI compatibility.
    /// See provider documentation for available options.
    /// </summary>
    public Dictionary<string, object>? AdditionalProperties { get; set; }
}

/// <summary>
/// Metadata about a provider's Agent-specific capabilities.
/// </summary>
public class ProviderMetadata
{
    public string ProviderKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool SupportsStreaming { get; init; } = true;
    public bool SupportsFunctionCalling { get; init; } = true;
    public bool SupportsVision { get; init; } = false;
    public bool SupportsAudio { get; init; } = false;
    public int? DefaultContextWindow { get; init; }
    public string? DocumentationUrl { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
}

/// <summary>
/// Result of provider configuration validation.
/// </summary>
public class ProviderValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public static ProviderValidationResult Success() => new() { IsValid = true };

    public static ProviderValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = new List<string>(errors) };
}

/// <summary>
/// Optional interface for providers that support advanced features like credit management,
/// usage tracking, or other provider-specific capabilities beyond the core IProviderFeatures.
/// </summary>
public interface IProviderExtendedFeatures : IProviderFeatures
{
    /// <summary>
    /// Check if the provider supports credit/usage management.
    /// </summary>
    bool SupportsCreditManagement => false;

    /// <summary>
    /// Check if the provider supports attribution headers (e.g., HTTP-Referer, X-Title).
    /// </summary>
    bool SupportsAttribution => false;

    /// <summary>
    /// Check if the provider supports model routing/fallbacks.
    /// </summary>
    bool SupportsModelRouting => false;
}

// ===== PLACEHOLDER INTERFACES FOR MEMORY =====
// These will be replaced by actual implementations when HPD-Agent.Memory is built

/// <summary>
/// Placeholder interface for embedding providers.
/// Will be replaced by actual implementation in HPD-Agent.Memory.
/// </summary>
public interface IEmbeddingProvider
{
    // Placeholder - actual interface will be defined in Memory project
}

/// <summary>
/// Placeholder interface for vector stores.
/// Will be replaced by actual implementation in HPD-Agent.Memory.
/// </summary>
public interface IVectorStore
{
    // Placeholder - actual interface will be defined in Memory project
}

/// <summary>
/// Placeholder interface for document stores.
/// Will be replaced by actual implementation in HPD-Agent.Memory.
/// </summary>
public interface IDocumentStore
{
    // Placeholder - actual interface will be defined in Memory project
}

/// <summary>
/// Placeholder interface for graph stores.
/// Will be replaced by actual implementation in HPD-Agent.Memory.
/// </summary>
public interface IGraphStore
{
    // Placeholder - actual interface will be defined in Memory project
}
