namespace HPD.Agent;

/// <summary>
/// Marker interface for middleware state records.
/// Uses C# 11 static abstract members to tie the key to the type itself.
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b></para>
/// <para>
/// Middleware state records implement this interface to enable type-safe,
/// thread-safe state management. Each state type defines its own unique key
/// and default factory, eliminating string-based lookups in user code.
/// </para>
///
/// <para><b>Thread Safety:</b></para>
/// <para>
/// This pattern ensures thread safety by keeping middleware instances stateless.
/// State flows through the context (not stored in middleware instance fields),
/// allowing multiple concurrent RunAsync() calls on the same agent instance.
/// </para>
///
/// <para><b>Usage:</b></para>
/// <code>
/// // Define a state record
/// public sealed record CircuitBreakerState : IMiddlewareState
/// {
///     public static string Key => "HPD.Agent.CircuitBreaker";
///     public static IMiddlewareState CreateDefault() => new CircuitBreakerState();
///
///     public ImmutableDictionary&lt;string, int&gt; ConsecutiveCountPerTool { get; init; }
///         = ImmutableDictionary&lt;string, int&gt;.Empty;
/// }
///
/// // Access in middleware (type-safe, no string keys!)
/// var state = context.State.GetState&lt;CircuitBreakerState&gt;();
/// context.UpdateState&lt;CircuitBreakerState&gt;(s => s with { ... });
/// </code>
///
/// <para><b>Serialization:</b></para>
/// <para>
/// State records must be JSON-serializable for checkpointing.
/// The Key property serves as the type discriminator during serialization.
/// </para>
/// </remarks>
public interface IMiddlewareState
{
    /// <summary>
    /// Unique key for this state type.
    /// Convention: "Namespace.MiddlewareName" (e.g., "HPD.Agent.CircuitBreaker")
    /// </summary>
    /// <remarks>
    /// This key is used as the dictionary key in AgentLoopState.MiddlewareStates.
    /// It must be unique across all middleware state types in the application.
    /// </remarks>
    static abstract string Key { get; }

    /// <summary>
    /// Creates default/initial state.
    /// Called when no persisted state exists or at the start of a new conversation.
    /// </summary>
    /// <returns>A new instance with default values</returns>
    static abstract IMiddlewareState CreateDefault();
}
