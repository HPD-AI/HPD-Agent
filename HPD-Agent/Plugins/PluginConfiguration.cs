using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Configuration for context-aware plugin behavior.
/// Enables runtime plugin function filtering based on dynamic context properties.
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// The name of the plugin type (e.g., "WebSearchPlugin").
    /// Should match the plugin class name.
    /// </summary>
    [JsonPropertyName("pluginName")]
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// The context type name for validation (e.g., "WebSearchPluginMetadataContext").
    /// Should match the TContext generic parameter in AIFunction&lt;TContext&gt;.
    /// </summary>
    [JsonPropertyName("contextType")]
    public string ContextType { get; set; } = string.Empty;

    /// <summary>
    /// Dynamic properties to be injected into the plugin context at runtime.
    /// Keys should match property names on the context type.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Optional list of specific functions to make available.
    /// If null or empty, all functions (subject to conditional filtering) will be available.
    /// </summary>
    [JsonPropertyName("availableFunctions")]
    public List<string>? AvailableFunctions { get; set; }
}

/// <summary>
/// Metadata about a plugin function that has been dynamically resolved.
/// Returned by FFI functions for Rust consumption.
/// </summary>
public class DynamicFunctionMetadata
{
    /// <summary>
    /// The name of the function.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The resolved description after template processing.
    /// </summary>
    [JsonPropertyName("resolvedDescription")]
    public string ResolvedDescription { get; set; } = string.Empty;

    /// <summary>
    /// The JSON schema for the function parameters.
    /// </summary>
    [JsonPropertyName("schema")]
    public Dictionary<string, object?> Schema { get; set; } = new();

    /// <summary>
    /// Whether this function is available given the current context.
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Whether this function requires special permissions.
    /// </summary>
    [JsonPropertyName("requiresPermission")]
    public bool RequiresPermission { get; set; } = false;
}

/// <summary>
/// Result container for plugin operations that may fail.
/// Provides structured error handling across the FFI boundary.
/// </summary>
/// <typeparam name="T">The type of data being returned on success</typeparam>
public class PluginOperationResult<T>
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// The result data if successful.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// Error information if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    public PluginError? Error { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PluginOperationResult<T> CreateSuccess(T data) =>
        new() { Success = true, Data = data };

    /// <summary>
    /// Creates a failed result with error information.
    /// </summary>
    public static PluginOperationResult<T> CreateFailure(PluginError error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Structured error information for plugin operations.
/// </summary>
public class PluginError
{
    /// <summary>
    /// The type of error that occurred.
    /// </summary>
    [JsonPropertyName("errorType")]
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The name of the property that caused the error (if applicable).
    /// </summary>
    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; set; }

    /// <summary>
    /// The expression that caused the error (if applicable).
    /// </summary>
    [JsonPropertyName("expression")]
    public string? Expression { get; set; }

    /// <summary>
    /// Suggested fixes or alternatives.
    /// </summary>
    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// A dynamic implementation of IPluginMetadataContext that can be configured at runtime.
/// This enables context-aware plugin behavior based on configuration passed from Rust.
/// </summary>
public class DynamicPluginMetadataContext : IPluginMetadataContext
{
    private readonly Dictionary<string, object?> _properties;

    /// <summary>
    /// Creates a new dynamic plugin context with the specified properties.
    /// </summary>
    /// <param name="properties">Dictionary of property name to value mappings</param>
    public DynamicPluginMetadataContext(Dictionary<string, object?> properties)
    {
        _properties = properties ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Gets a property value with type conversion and default value support.
    /// </summary>
    /// <typeparam name="T">The expected type of the property</typeparam>
    /// <param name="propertyName">The name of the property</param>
    /// <param name="defaultValue">The default value if the property is not found or conversion fails</param>
    /// <returns>The property value or the default value</returns>
    public T GetProperty<T>(string propertyName, T defaultValue = default)
    {
        if (!_properties.TryGetValue(propertyName, out var value) || value == null)
        {
            return defaultValue;
        }

        try
        {
            // Handle common type conversions
            if (value is T directMatch)
            {
                return directMatch;
            }

            // Handle JsonElement from deserialization
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElement<T>(jsonElement, defaultValue);
            }

            // Attempt standard conversion
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Checks if a property exists in the context.
    /// </summary>
    /// <param name="propertyName">The name of the property to check</param>
    /// <returns>True if the property exists, false otherwise</returns>
    public bool HasProperty(string propertyName)
    {
        return _properties.ContainsKey(propertyName);
    }

    /// <summary>
    /// Gets all property names available in this context.
    /// </summary>
    /// <returns>Enumerable of property names</returns>
    public IEnumerable<string> GetPropertyNames()
    {
        return _properties.Keys;
    }

    /// <summary>
    /// Converts a JsonElement to the specified type.
    /// </summary>
    private static T ConvertJsonElement<T>(JsonElement element, T defaultValue)
    {
        var targetType = typeof(T);

        try
        {
            if (targetType == typeof(string))
            {
                return (T)(object)(element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.GetRawText());
            }
            
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return element.ValueKind == JsonValueKind.True ? (T)(object)true :
                       element.ValueKind == JsonValueKind.False ? (T)(object)false :
                       defaultValue;
            }
            
            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return element.TryGetInt32(out var intValue) ? (T)(object)intValue : defaultValue;
            }
            
            if (targetType == typeof(long) || targetType == typeof(long?))
            {
                return element.TryGetInt64(out var longValue) ? (T)(object)longValue : defaultValue;
            }
            
            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return element.TryGetDouble(out var doubleValue) ? (T)(object)doubleValue : defaultValue;
            }
            
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                return element.TryGetDecimal(out var decimalValue) ? (T)(object)decimalValue : defaultValue;
            }

            // For complex types, try JSON deserialization
            var json = element.GetRawText();
            var result = JsonSerializer.Deserialize<T>(json, HPDJsonContext.Default.Options);
            return result ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}