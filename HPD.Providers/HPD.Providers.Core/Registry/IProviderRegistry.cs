// HPD.Providers.Core/Registry/IProviderRegistry.cs
using System.Collections.Generic;

namespace HPD.Providers.Core;

/// <summary>
/// Registry for provider features across HPD ecosystem (Agent + Memory + Future products).
/// Providers self-register via ModuleInitializers on assembly load.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>
    /// Register a provider's features.
    /// Called by provider ModuleInitializers during assembly load.
    /// </summary>
    /// <param name="features">Provider features implementation</param>
    void Register(IProviderFeatures features);

    /// <summary>
    /// Get provider features by key (case-insensitive).
    /// </summary>
    /// <param name="providerKey">Provider identifier (e.g., "openai", "anthropic", "qdrant")</param>
    /// <returns>Provider features, or null if not registered</returns>
    IProviderFeatures? GetProvider(string providerKey);

    /// <summary>
    /// Check if a provider is registered.
    /// </summary>
    /// <param name="providerKey">Provider identifier</param>
    /// <returns>True if provider is registered</returns>
    bool IsRegistered(string providerKey);

    /// <summary>
    /// Get all registered provider keys.
    /// </summary>
    /// <returns>Collection of registered provider keys</returns>
    IReadOnlyCollection<string> GetRegisteredProviders();

    /// <summary>
    /// Clear all registrations (for testing only).
    /// </summary>
    void Clear();
}
