// HPD.Providers.Core/Registry/ProviderConfigRegistry.cs
using System;
using System.Collections.Generic;

namespace HPD.Providers.Core;

/// <summary>
/// Global registry for provider-specific configuration types.
/// Enables type-safe serialization/deserialization for FFI scenarios without core knowing concrete types.
/// Provider packages register their config types via ModuleInitializer.
/// </summary>
public static class ProviderConfigRegistry
{
    private static readonly Dictionary<string, ProviderConfigRegistration> _configTypes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    /// <summary>
    /// Registers a provider-specific configuration type for FFI serialization.
    /// Called by provider package ModuleInitializers.
    /// </summary>
    /// <typeparam name="TConfig">The provider-specific config type (e.g., AnthropicProviderConfig)</typeparam>
    /// <param name="providerKey">Provider key (e.g., "anthropic")</param>
    /// <param name="deserializer">Function to deserialize JSON to the config type</param>
    /// <param name="serializer">Function to serialize the config type to JSON</param>
    /// <example>
    /// <code>
    /// // In AnthropicProviderModule.Initialize():
    /// ProviderConfigRegistry.Register&lt;AnthropicProviderConfig&gt;(
    ///     "anthropic",
    ///     json => JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.AnthropicProviderConfig),
    ///     config => JsonSerializer.Serialize(config, AnthropicJsonContext.Default.AnthropicProviderConfig));
    /// </code>
    /// </example>
    public static void Register<TConfig>(
        string providerKey,
        Func<string, TConfig?> deserializer,
        Func<TConfig, string> serializer) where TConfig : class
    {
        lock (_lock)
        {
            _configTypes[providerKey] = new ProviderConfigRegistration(
                typeof(TConfig),
                json => deserializer(json),
                obj => serializer((TConfig)obj));
        }
    }

    /// <summary>
    /// Gets the registered config type for a provider.
    /// </summary>
    /// <param name="providerKey">Provider key (e.g., "anthropic")</param>
    /// <returns>Registration info, or null if not registered</returns>
    public static ProviderConfigRegistration? GetRegistration(string providerKey)
    {
        lock (_lock)
        {
            return _configTypes.TryGetValue(providerKey, out var registration) ? registration : null;
        }
    }

    /// <summary>
    /// Deserializes provider-specific config from JSON using the registered deserializer.
    /// </summary>
    /// <param name="providerKey">Provider key (e.g., "anthropic")</param>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Deserialized config object, or null if provider not registered or JSON is empty</returns>
    public static object? Deserialize(string providerKey, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var registration = GetRegistration(providerKey);
        return registration?.Deserialize(json);
    }

    /// <summary>
    /// Serializes provider-specific config to JSON using the registered serializer.
    /// </summary>
    /// <param name="providerKey">Provider key (e.g., "anthropic")</param>
    /// <param name="config">Config object to serialize</param>
    /// <returns>JSON string, or null if provider not registered or config is null</returns>
    public static string? Serialize(string providerKey, object? config)
    {
        if (config == null)
            return null;

        var registration = GetRegistration(providerKey);
        return registration?.Serialize(config);
    }

    /// <summary>
    /// Gets all registered provider config types.
    /// Used by FFI layer for schema discovery.
    /// </summary>
    public static IReadOnlyDictionary<string, ProviderConfigRegistration> GetAll()
    {
        lock (_lock)
        {
            return new Dictionary<string, ProviderConfigRegistration>(_configTypes);
        }
    }

    /// <summary>
    /// For testing: clear registry.
    /// </summary>
    internal static void ClearForTesting()
    {
        lock (_lock)
        {
            _configTypes.Clear();
        }
    }
}

/// <summary>
/// Registration info for a provider-specific configuration type.
/// Enables type-safe serialization/deserialization without core knowing the concrete type.
/// </summary>
public class ProviderConfigRegistration
{
    /// <summary>
    /// The CLR type of the provider config (e.g., typeof(AnthropicProviderConfig)).
    /// </summary>
    public Type ConfigType { get; }

    private readonly Func<string, object?> _deserializer;
    private readonly Func<object, string> _serializer;

    public ProviderConfigRegistration(
        Type configType,
        Func<string, object?> deserializer,
        Func<object, string> serializer)
    {
        ConfigType = configType;
        _deserializer = deserializer;
        _serializer = serializer;
    }

    /// <summary>
    /// Deserializes JSON to the provider config type.
    /// </summary>
    public object? Deserialize(string json) => _deserializer(json);

    /// <summary>
    /// Serializes the provider config to JSON.
    /// </summary>
    public string Serialize(object config) => _serializer(config);
}
