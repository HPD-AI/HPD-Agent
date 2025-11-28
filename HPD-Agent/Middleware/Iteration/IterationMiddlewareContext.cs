using Microsoft.Extensions.AI;

namespace HPD.Agent;

/// <summary>
/// Context provided to iteration Middlewares during each iteration of the agentic loop.
/// Contains input (messages, options, state), LLM output (response, tool calls), and tool results.
/// </summary>
/// <remarks>
/// This context follows a four-phase lifecycle:
/// - BEFORE ITERATION (BeforeIterationAsync): Messages, Options, and State are available for inspection/modification
/// - AFTER LLM CALL: Response, ToolCalls, and Exception are populated with LLM results
/// - BEFORE TOOL EXECUTION (BeforeToolExecutionAsync): ToolCalls populated, can prevent execution via SkipToolExecution
/// - AFTER TOOL EXECUTION (AfterIterationAsync): ToolResults are populated with function execution outcomes
///
/// The State property is immutable (record type) and provides a snapshot of the agent's execution state.
/// To signal state changes, use the Properties dictionary to communicate with the agent loop.
///
/// Key properties for circuit breaker:
/// - ToolCalls: Contains pending tool calls from LLM (available in BeforeToolExecutionAsync)
/// - State.ConsecutiveCountPerTool: Track repeated identical tool calls
/// - State.LastSignaturePerTool: Track tool call signatures for comparison
///
/// Key properties for error tracking:
/// - ToolResults: Contains FunctionResultContent with Result or Exception from each tool call
/// - State.ConsecutiveFailures: Number of consecutive iterations with errors
/// </remarks>
public class IterationMiddleWareContext
{
    // ═══════════════════════════════════════════════════════
    // METADATA (What iteration is this?)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Current iteration number in the agentic loop (0-based).
    /// Iteration 0 is the first LLM call, iteration 1 is after first set of tool calls, etc.
    /// </summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// Name of the agent executing this iteration.
    /// </summary>
    public required string AgentName { get; init; }

    /// <summary>
    /// Cancellation token for this operation.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    // ═══════════════════════════════════════════════════════
    // INPUT - MUTABLE (Middlewares can modify before LLM call)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Messages to send to the LLM.
    /// MUTABLE: Middlewares can add, remove, or modify messages.
    /// Includes conversation history and tool results from previous iterations.
    /// </summary>
    public required IList<ChatMessage> Messages { get; set; }

    /// <summary>
    /// Chat options for this LLM call.
    /// MUTABLE: Middlewares can modify Instructions, Tools, Temperature, etc.
    /// Most common use case: Appending to Instructions property.
    /// </summary>
    public ChatOptions? Options { get; set; }

    // ═══════════════════════════════════════════════════════
    // STATE - READ-ONLY (Full agent state snapshot)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Current agent loop state (immutable snapshot).
    /// Provides context about the agent's execution state including:
    /// - ActiveSkillInstructions: Skills activated in this message turn
    /// - CompletedFunctions: Functions executed so far
    /// - ExpandedSkillContainers/expandedScopedPluginContainers: Scoping state
    /// - ConsecutiveFailures: Error tracking
    /// - Circuit breaker state
    /// - Full conversation history
    /// </summary>
    /// <remarks>
    /// This is a record type and cannot be modified. Middlewares observe state but
    /// cannot change it directly. To request state changes, use Properties to signal intent.
    /// </remarks>
    public required AgentLoopState State { get; init; }

    // ═══════════════════════════════════════════════════════
    // OUTPUT - POPULATED AFTER next() (LLM response)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// The assistant message returned by the LLM.
    /// NULL before next() is called (pre-invoke phase).
    /// POPULATED after next() returns (post-invoke phase).
    /// Contains text content, reasoning content, and tool call requests.
    /// </summary>
    public ChatMessage? Response { get; set; }

    /// <summary>
    /// Tool calls requested by the LLM in this iteration.
    /// EMPTY before next() is called (pre-invoke phase).
    /// POPULATED after next() returns (post-invoke phase).
    /// If empty after next(), this is likely the final iteration (no more tool calls).
    /// </summary>
    public IReadOnlyList<FunctionCallContent> ToolCalls { get; set; }
        = Array.Empty<FunctionCallContent>();

    /// <summary>
    /// Results from tool execution in this iteration.
    /// EMPTY before tool execution completes.
    /// POPULATED after all tools have been executed (in AfterIterationAsync phase).
    /// Each FunctionResultContent contains the result or exception from a tool call.
    /// Use this to analyze tool outcomes for error tracking, logging, or custom logic.
    /// </summary>
    public IReadOnlyList<FunctionResultContent> ToolResults { get; set; }
        = Array.Empty<FunctionResultContent>();

    /// <summary>
    /// Exception that occurred during LLM invocation, or null if successful.
    /// NULL before next() is called.
    /// POPULATED after next() returns if an error occurred.
    /// </summary>
    public Exception? Exception { get; set; }

    // ═══════════════════════════════════════════════════════
    // CONTROL (Middlewares can signal actions)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Set to true to skip the LLM call entirely.
    /// Useful for caching, short-circuiting, or conditional execution.
    /// If set before next() is called, the LLM invocation will be skipped.
    /// The Middleware that sets this flag should populate Response and ToolCalls with cached/computed values.
    /// </summary>
    public bool SkipLLMCall { get; set; }

    /// <summary>
    /// Set to true in BeforeToolExecutionAsync to skip ALL pending tool executions.
    /// Used by circuit breaker middleware to prevent infinite loops.
    /// When set:
    /// - No tools from ToolCalls will be executed
    /// - The agent loop will terminate or continue based on Properties["IsTerminated"]
    /// - The middleware should set Response with an appropriate message
    /// </summary>
    public bool SkipToolExecution { get; set; }

    /// <summary>
    /// Extensible property bag for inter-Middleware communication and signaling.
    /// Use this to:
    /// - Pass data between Middlewares in the pipeline
    /// - Signal cleanup actions to the agent loop
    /// - Store computed values for reuse
    /// Example: Properties["ShouldClearSkills"] = true;
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    // ═══════════════════════════════════════════════════════
    // BIDIRECTIONAL EVENTS (For interactive Middlewares)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Event coordinator for bidirectional communication patterns.
    /// Used for emitting events and waiting for responses (permissions, approvals, etc.)
    /// Decoupled from AgentCore for testability and clean architecture.
    /// </summary>
    internal IEventCoordinator? EventCoordinator { get; init; }

    /// <summary>
    /// Emits an event to the agent's event stream for external handling.
    /// Used for bidirectional communication patterns like permission requests.
    /// </summary>
    /// <param name="evt">The event to emit</param>
    /// <exception cref="InvalidOperationException">If EventCoordinator is not configured</exception>
    public void Emit(AgentEvent evt)
    {
        if (evt == null)
            throw new ArgumentNullException(nameof(evt));

        if (EventCoordinator == null)
            throw new InvalidOperationException("Event coordination not configured for this context");

        EventCoordinator.Emit(evt);
    }

    /// <summary>
    /// Waits for a response event from external handlers (blocking operation).
    /// Used for interactive patterns like permission requests, clarifications, etc.
    /// </summary>
    /// <typeparam name="T">Type of response event expected</typeparam>
    /// <param name="requestId">Unique identifier for this request</param>
    /// <param name="timeout">Maximum time to wait for response</param>
    /// <returns>The response event</returns>
    /// <exception cref="TimeoutException">If no response received within timeout</exception>
    /// <exception cref="OperationCanceledException">If operation was cancelled</exception>
    /// <exception cref="InvalidOperationException">If EventCoordinator is not configured</exception>
    public async Task<T> WaitForResponseAsync<T>(
        string requestId,
        TimeSpan? timeout = null) where T : AgentEvent
    {
        if (EventCoordinator == null)
            throw new InvalidOperationException("Event coordination not configured for this context");

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);

        return await EventCoordinator.WaitForResponseAsync<T>(
            requestId,
            effectiveTimeout,
            CancellationToken);
    }

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if this is the first iteration (before any tool calls).
    /// </summary>
    public bool IsFirstIteration => Iteration == 0;

    /// <summary>
    /// Returns true if the LLM call succeeded (no exception).
    /// Only valid after next() returns.
    /// </summary>
    public bool IsSuccess => Exception == null && Response != null;

    /// <summary>
    /// Returns true if the LLM call failed (exception occurred).
    /// Only valid after next() returns.
    /// </summary>
    public bool IsFailure => Exception != null || Response == null;

    /// <summary>
    /// Returns true if this appears to be the final iteration.
    /// Determined by: IsSuccess AND no tool calls requested.
    /// Only valid after next() returns.
    /// </summary>
    public bool IsFinalIteration => IsSuccess && !ToolCalls.Any();
}
