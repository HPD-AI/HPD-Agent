using System.Text.Json;

namespace HPD.Agent.Microsoft;

/// <summary>
/// Context provided to AIContextProvider factory functions.
/// Enables state restoration and configuration access during provider instantiation.
/// </summary>
public readonly record struct AIContextProviderFactoryContext
{
    /// <summary>
    /// Serialized state from a previous AIContextProvider instance (if restoring from checkpoint).
    /// Check ValueKind != Undefined && ValueKind != Null before using.
    /// </summary>
    public JsonElement SerializedState { get; init; }

    /// <summary>
    /// JSON serializer options to use for deserialization (optional).
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; init; }
}
