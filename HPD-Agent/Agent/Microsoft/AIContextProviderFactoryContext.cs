using System.Text.Json;

namespace HPD.Agent.Microsoft;

/// <summary>
/// Context passed to AIContextProvider factory functions.
/// Microsoft.Agents.AI compatible - matches the pattern used in ChatClientAgentOptions.
/// </summary>
/// <remarks>
/// This class enables stateful AIContextProvider implementations by providing:
/// <list type="bullet">
/// <item>Serialized state for restoring providers across sessions</item>
/// <item>JSON serializer options for custom deserialization logic</item>
/// </list>
///
/// <para><b>Usage Pattern:</b></para>
/// <code>
/// var agent = new AgentBuilder()
///     .WithContextProviderFactory(ctx =>
///     {
///         // Check if we're restoring from saved state
///         if (ctx.SerializedState.ValueKind != JsonValueKind.Undefined &amp;&amp;
///             ctx.SerializedState.ValueKind != JsonValueKind.Null)
///         {
///             // Restore from state
///             return new MyMemoryProvider(ctx.SerializedState, ctx.JsonSerializerOptions);
///         }
///
///         // Create new instance
///         return new MyMemoryProvider();
///     })
///     .BuildMicrosoftAgent();
/// </code>
/// </remarks>
public class AIContextProviderFactoryContext
{
    /// <summary>
    /// Serialized provider state for restoration, or default for new instances.
    /// </summary>
    /// <remarks>
    /// Check <see cref="JsonElement.ValueKind"/> to determine if state exists:
    /// <code>
    /// if (ctx.SerializedState.ValueKind != JsonValueKind.Undefined &amp;&amp;
    ///     ctx.SerializedState.ValueKind != JsonValueKind.Null)
    /// {
    ///     // State exists - restore provider
    /// }
    /// else
    /// {
    ///     // No state - create new provider
    /// }
    /// </code>
    ///
    /// <b>Note:</b> This is not nullable to match Microsoft's pattern. Use ValueKind to check.
    /// </remarks>
    public JsonElement SerializedState { get; set; }

    /// <summary>
    /// Optional JSON serializer options for deserialization.
    /// Allows custom converters, naming policies, etc.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }
}
