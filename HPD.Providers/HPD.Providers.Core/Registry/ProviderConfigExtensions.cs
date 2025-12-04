// HPD.Providers.Core/Registry/ProviderConfigExtensions.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace HPD.Providers.Core;

/// <summary>
/// Extension methods for ProviderConfig to support typed configuration.
/// </summary>
public static class ProviderConfigExtensions
{
    private static readonly ConditionalWeakTable<ProviderConfig, StrongBox<object?>> _cache = new();

    /// <summary>
    /// Gets the provider-specific configuration using the registered deserializer.
    /// Prefers ProviderOptionsJson (FFI-friendly), falls back to AdditionalProperties.
    /// Uses the provider's registered deserializer from ProviderConfigRegistry for AOT compatibility.
    ///
    /// Usage in providers:
    /// <code>
    /// var myConfig = config.GetTypedProviderConfig&lt;AnthropicProviderConfig&gt;();
    /// </code>
    /// </summary>
    /// <typeparam name="T">The strongly-typed configuration class</typeparam>
    /// <param name="config">The provider config instance</param>
    /// <returns>Parsed configuration object, or null if no config is present</returns>
    public static T? GetTypedProviderConfig<T>(this ProviderConfig config) where T : class
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Return cached value if available and correct type
        if (_cache.TryGetValue(config, out var box) && box.Value is T cached)
            return cached;

        // Priority 1: Use ProviderOptionsJson with registered deserializer
        if (!string.IsNullOrWhiteSpace(config.ProviderOptionsJson))
        {
            var registration = ProviderConfigRegistry.GetRegistration(config.ProviderKey);
            if (registration != null && registration.ConfigType == typeof(T))
            {
                var result = registration.Deserialize(config.ProviderOptionsJson) as T;
                _cache.GetValue(config, _ => new StrongBox<object?>()).Value = result;
                return result;
            }
        }

        // Priority 2: Fall back to AdditionalProperties (legacy)
        var legacyConfig = GetProviderConfigFromDictionary<T>(config.AdditionalProperties);
        _cache.GetValue(config, _ => new StrongBox<object?>()).Value = legacyConfig;
        return legacyConfig;
    }

    /// <summary>
    /// Sets the provider-specific configuration and updates ProviderOptionsJson.
    /// Uses the provider's registered serializer from ProviderConfigRegistry for AOT compatibility.
    /// </summary>
    /// <typeparam name="T">The strongly-typed configuration class</typeparam>
    /// <param name="config">The provider config instance</param>
    /// <param name="providerConfig">The configuration object to set</param>
    public static void SetTypedProviderConfig<T>(this ProviderConfig config, T providerConfig) where T : class
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _cache.GetValue(config, _ => new StrongBox<object?>()).Value = providerConfig;

        // Serialize using registered serializer
        var registration = ProviderConfigRegistry.GetRegistration(config.ProviderKey);
        if (registration != null && registration.ConfigType == typeof(T))
        {
            config.ProviderOptionsJson = registration.Serialize(providerConfig);
        }
    }

    /// <summary>
    /// Deserializes AdditionalProperties to a strongly-typed configuration class.
    /// Legacy method - prefer GetTypedProviderConfig for FFI/AOT compatibility.
    /// </summary>
    /// <typeparam name="T">The strongly-typed configuration class</typeparam>
    /// <param name="additionalProperties">Dictionary of additional properties</param>
    /// <returns>Parsed configuration object, or null if dictionary is empty</returns>
    [RequiresUnreferencedCode("Generic deserialization requires runtime type information. Use typed provider config methods for AOT.")]
    private static T? GetProviderConfigFromDictionary<T>(Dictionary<string, object>? additionalProperties) where T : class
    {
        if (additionalProperties == null || additionalProperties.Count == 0)
            return null;

        try
        {
            // Convert dictionary to JSON
            var json = JsonSerializer.Serialize(additionalProperties);

            // Deserialize to target type
            var result = JsonSerializer.Deserialize<T>(json);
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse provider configuration for {typeof(T).Name}. " +
                $"Please check that your AdditionalProperties match the expected structure. " +
                $"Error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unexpected error parsing provider configuration for {typeof(T).Name}: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Helper class for weak reference caching.
/// </summary>
internal class StrongBox<T>
{
    public T? Value { get; set; }
}
