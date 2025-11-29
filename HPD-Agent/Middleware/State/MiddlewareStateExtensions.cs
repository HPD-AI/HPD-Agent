using System.Collections.Immutable;

namespace HPD.Agent;

/// <summary>
/// Type-safe extensions for accessing middleware state.
/// No string keys in user code - the key is tied to the type via static abstract.
/// </summary>
/// <remarks>
/// <para><b>Thread Safety:</b></para>
/// <para>
/// These extensions work with immutable AgentLoopState records.
/// All operations return new instances, never mutating existing state.
/// This enables safe concurrent access from multiple RunAsync() calls.
/// </para>
///
/// <para><b>Usage in Middleware:</b></para>
/// <code>
/// // Read state (type-safe)
/// var state = context.State.GetState&lt;CircuitBreakerState&gt;();
///
/// // Update state via context (schedules update for after middleware chain)
/// context.UpdateState&lt;CircuitBreakerState&gt;(s => s with
/// {
///     ConsecutiveCountPerTool = s.ConsecutiveCountPerTool.SetItem(toolName, newCount)
/// });
/// </code>
/// </remarks>
public static class MiddlewareStateExtensions
{
    /// <summary>
    /// Gets middleware state of the specified type, returning default if not present.
    /// </summary>
    /// <typeparam name="TState">The middleware state type (must implement IMiddlewareState)</typeparam>
    /// <param name="agentState">The agent loop state to read from</param>
    /// <returns>The stored state if present and of correct type, otherwise a new default instance</returns>
    /// <example>
    /// <code>
    /// var cbState = context.State.GetState&lt;CircuitBreakerState&gt;();
    /// var count = cbState.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0);
    /// </code>
    /// </example>
    public static TState GetState<TState>(this AgentLoopState agentState)
        where TState : class, IMiddlewareState
    {
        if (agentState.MiddlewareStates.TryGetValue(TState.Key, out var stored) && stored is TState typed)
            return typed;

        return (TState)TState.CreateDefault();
    }

    /// <summary>
    /// Returns new AgentLoopState with updated middleware state.
    /// </summary>
    /// <typeparam name="TState">The middleware state type (must implement IMiddlewareState)</typeparam>
    /// <param name="agentState">The agent loop state to update</param>
    /// <param name="value">The new state value</param>
    /// <returns>A new AgentLoopState with the middleware state updated</returns>
    /// <example>
    /// <code>
    /// state = state.WithState(new CircuitBreakerState
    /// {
    ///     ConsecutiveCountPerTool = newCounts
    /// });
    /// </code>
    /// </example>
    public static AgentLoopState WithState<TState>(this AgentLoopState agentState, TState value)
        where TState : class, IMiddlewareState
    {
        return agentState with
        {
            MiddlewareStates = agentState.MiddlewareStates.SetItem(TState.Key, value)
        };
    }

    /// <summary>
    /// Returns new AgentLoopState with middleware state transformed by the given function.
    /// </summary>
    /// <typeparam name="TState">The middleware state type (must implement IMiddlewareState)</typeparam>
    /// <param name="agentState">The agent loop state to update</param>
    /// <param name="transform">Function that transforms the current state to new state</param>
    /// <returns>A new AgentLoopState with the transformed middleware state</returns>
    /// <example>
    /// <code>
    /// state = state.UpdateState&lt;CircuitBreakerState&gt;(s => s with
    /// {
    ///     ConsecutiveCountPerTool = s.ConsecutiveCountPerTool.SetItem(toolName, newCount)
    /// });
    /// </code>
    /// </example>
    public static AgentLoopState UpdateState<TState>(
        this AgentLoopState agentState,
        Func<TState, TState> transform)
        where TState : class, IMiddlewareState
    {
        var current = agentState.GetState<TState>();
        var updated = transform(current);
        return agentState.WithState(updated);
    }

    /// <summary>
    /// Checks if middleware state of the specified type exists.
    /// </summary>
    /// <typeparam name="TState">The middleware state type to check for</typeparam>
    /// <param name="agentState">The agent loop state to check</param>
    /// <returns>True if state exists and is of the correct type</returns>
    public static bool HasState<TState>(this AgentLoopState agentState)
        where TState : class, IMiddlewareState
    {
        return agentState.MiddlewareStates.TryGetValue(TState.Key, out var stored) && stored is TState;
    }

    /// <summary>
    /// Removes middleware state of the specified type.
    /// </summary>
    /// <typeparam name="TState">The middleware state type to remove</typeparam>
    /// <param name="agentState">The agent loop state</param>
    /// <returns>A new AgentLoopState with the middleware state removed</returns>
    public static AgentLoopState RemoveState<TState>(this AgentLoopState agentState)
        where TState : class, IMiddlewareState
    {
        return agentState with
        {
            MiddlewareStates = agentState.MiddlewareStates.Remove(TState.Key)
        };
    }
}
