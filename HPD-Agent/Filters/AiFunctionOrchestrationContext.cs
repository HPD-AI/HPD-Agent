using Microsoft.Extensions.AI;

/// <summary>
/// Represents the context of an orchestration step where a tool may be invoked.
/// Native AOT compatible - does not inherit from FunctionInvocationContext.
/// </summary>
public class AiFunctionContext :  FunctionInvocationContext

{
    /// <summary>
    /// The raw tool call request from the Language Model.
    /// </summary>
    public ToolCallRequest ToolCallRequest { get; }

    /// <summary>
    /// The name of the agent executing this function
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Context about the current agent run/turn
    /// </summary>
    public AgentRunContext? RunContext { get; set; }

    /// <summary>
    /// A flag to allow a filter to terminate the pipeline.
    /// </summary>
    public bool IsTerminated { get; set; } = false;

    /// <summary>
    /// The result of the function invocation, to be set by the final step.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// The AI function being invoked (if available).
    /// </summary>
    public new AIFunction? Function { get; set; }

    /// <summary>
    /// Additional metadata for this function invocation context
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Arguments for the function call (AOT-safe access).
    /// </summary>
    public new AIFunctionArguments Arguments { get; }

    /// <summary>
    /// Channel writer for emitting events during filter execution.
    /// Points to Agent's shared channel - events are immediately visible to background drainer.
    ///
    /// Thread-safety: Multiple filters in the pipeline can emit concurrently.
    /// Event ordering: FIFO within each filter, interleaved across filters.
    /// Lifetime: Valid for entire filter execution.
    /// </summary>
    internal System.Threading.Channels.ChannelWriter<InternalAgentEvent>? OutboundEvents { get; set; }

    /// <summary>
    /// Reference to the agent for response coordination.
    /// Lifetime: Set by ProcessFunctionCallsAsync, valid for entire filter execution.
    /// </summary>
    internal Agent? Agent { get; set; }

    public AiFunctionContext(ToolCallRequest toolCallRequest)
    {
        ToolCallRequest = toolCallRequest ?? throw new ArgumentNullException(nameof(toolCallRequest));
        Arguments = new AIFunctionArguments(toolCallRequest.Arguments);
    }

    /// <summary>
    /// Emits an event that will be yielded by RunAgenticLoopInternal.
    /// Events are delivered immediately to background drainer (not batched).
    /// Automatically bubbles events to parent agent if this is a nested agent call.
    ///
    /// Thread-safety: Safe to call from any filter in the pipeline.
    /// Performance: Non-blocking write (unbounded channel).
    /// Event ordering: Guaranteed FIFO per filter, interleaved across filters.
    /// Real-time visibility: Handler sees event WHILE filter is executing (not after).
    /// Event bubbling: If Agent.RootAgent is set, events bubble to orchestrator.
    /// </summary>
    /// <param name="evt">The event to emit</param>
    /// <exception cref="ArgumentNullException">If event is null</exception>
    /// <exception cref="InvalidOperationException">If Agent reference is not configured</exception>
    public void Emit(InternalAgentEvent evt)
    {
        if (evt == null)
            throw new ArgumentNullException(nameof(evt));

        if (Agent == null)
            throw new InvalidOperationException("Agent reference not configured for this context");

        // Emit to local agent's coordinator
        Agent.EventCoordinator.Emit(evt);

        // If we're a nested agent (RootAgent is set and different from us), bubble to root
        // RootAgent is a static property on the global Agent class
        var rootAgent = global::Agent.RootAgent;
        if (rootAgent != null && rootAgent != Agent)
        {
            rootAgent.EventCoordinator.Emit(evt);
        }
    }

    /// <summary>
    /// Emits an event and returns immediately (async version for bounded channels if needed).
    /// Current implementation uses unbounded channels, so this is identical to Emit().
    /// Kept for future extensibility if bounded channels are introduced.
    /// </summary>
    public async Task EmitAsync(InternalAgentEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt == null)
            throw new ArgumentNullException(nameof(evt));

        if (OutboundEvents == null)
            throw new InvalidOperationException("Event emission not configured for this context");

        await OutboundEvents.WriteAsync(evt, cancellationToken);
    }

    /// <summary>
    /// Waits for a response event with automatic timeout and cancellation handling.
    /// Used for request/response patterns in interactive filters (permissions, approvals, etc.)
    ///
    /// Thread-safety: Safe to call from any filter.
    /// Cancellation: Respects both timeout and external cancellation token.
    /// Type safety: Validates response type and throws clear error on mismatch.
    /// Cleanup: Automatically removes TCS from waiters dictionary on completion/timeout/cancellation.
    /// </summary>
    /// <typeparam name="T">Type of response event to wait for</typeparam>
    /// <param name="requestId">Unique identifier for this request</param>
    /// <param name="timeout">Maximum time to wait for response (default: 5 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The response event</returns>
    /// <exception cref="TimeoutException">Thrown if no response received within timeout</exception>
    /// <exception cref="OperationCanceledException">Thrown if cancellation requested</exception>
    /// <exception cref="InvalidOperationException">Thrown if Agent reference not set or response type mismatch</exception>
    public async Task<T> WaitForResponseAsync<T>(
        string requestId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) where T : InternalAgentEvent
    {
        if (Agent == null)
            throw new InvalidOperationException("Agent reference not configured for this context");

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);

        return await Agent.WaitForFilterResponseAsync<T>(requestId, effectiveTimeout, cancellationToken);
    }
}


/// <summary>
/// The filter interface remains the same, but it will now operate
/// on the new, richer AiFunctionContext.
/// </summary>
public interface IAiFunctionFilter
{
    Task InvokeAsync(
        AiFunctionContext context,
        Func<AiFunctionContext, Task> next);
}

/// <summary>
/// Represents a function call request from the LLM.
/// </summary>
public class ToolCallRequest
{
    public required string FunctionName { get; set; }
    public required IDictionary<string, object?> Arguments { get; set; }
}

/// <summary>
/// Represents the context of an entire agent run/turn, providing
/// cross-function-call state and statistics
/// </summary>
public class AgentRunContext
{
    /// <summary>
    /// Unique identifier for this agent run
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Conversation ID for this agent run
    /// </summary>
    public string ConversationId { get; }

    /// <summary>
    /// Name of the agent executing in this run (optional).
    /// Used for telemetry, logging, and plugin context.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// When this agent run started
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Current iteration/function call number (0-based)
    /// </summary>
    public int CurrentIteration { get; set; } = 0;

    /// <summary>
    /// Maximum allowed function calls for this run
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// List of function names that have been completed in this run
    /// </summary>
    public List<string> CompletedFunctions { get; } = new();

    /// <summary>
    /// Set of tool call IDs that have been approved for execution in this run
    /// Used to prevent duplicate permission prompts in parallel execution
    /// </summary>
    private readonly HashSet<string> _approvedToolCalls = new();

    /// <summary>
    /// Additional metadata for this run
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();

    /// <summary>
    /// Whether this run has been terminated early
    /// </summary>
    public bool IsTerminated { get; set; } = false;

    /// <summary>
    /// Reason for termination (if terminated)
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// Tracks consecutive errors across iterations to prevent infinite error loops.
    /// Reset to 0 when a successful iteration occurs.
    /// </summary>
    public int ConsecutiveErrorCount { get; set; } = 0;

    /// <summary>
    /// Constructor for AgentRunContext
    /// </summary>
    public AgentRunContext(string runId, string conversationId, int maxIterations = 10, string? agentName = null)
    {
        RunId = runId ?? throw new ArgumentNullException(nameof(runId));
        ConversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
        AgentName = agentName;
        StartTime = DateTime.UtcNow;
        MaxIterations = maxIterations;
    }

    /// <summary>
    /// Marks a function as completed
    /// </summary>
    public void CompleteFunction(string functionName)
    {
        CompletedFunctions.Add(functionName);
    }

    /// <summary>
    /// Checks if a tool call has already been approved in this run
    /// </summary>
    public bool IsToolApproved(string callId) => _approvedToolCalls.Contains(callId);

    /// <summary>
    /// Marks a tool call as approved for execution in this run
    /// </summary>
    public void MarkToolApproved(string callId) => _approvedToolCalls.Add(callId);

    /// <summary>
    /// Gets the total elapsed time for this run
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Checks if we've hit the maximum iteration limit
    /// </summary>
    public bool HasReachedMaxIterations => CurrentIteration >= MaxIterations;

    /// <summary>
    /// Records a successful iteration and resets consecutive error count
    /// </summary>
    public void RecordSuccess()
    {
        ConsecutiveErrorCount = 0;
    }

    /// <summary>
    /// Records an error and increments consecutive error count
    /// </summary>
    public void RecordError()
    {
        ConsecutiveErrorCount++;
    }

    /// <summary>
    /// Checks if consecutive errors have exceeded the maximum allowed limit
    /// </summary>
    /// <param name="maxConsecutiveErrors">Maximum allowed consecutive errors</param>
    /// <returns>True if limit exceeded, false otherwise</returns>
    public bool HasExceededErrorLimit(int maxConsecutiveErrors)
    {
        return ConsecutiveErrorCount > maxConsecutiveErrors;
    }

    /// <summary>
    /// Checks if execution is approaching a timeout threshold.
    /// </summary>
    /// <param name="threshold">Time buffer before timeout (e.g., 30 seconds)</param>
    /// <param name="maxDuration">Maximum allowed duration (defaults to 5 minutes)</param>
    /// <returns>True if elapsed time is within threshold of max duration</returns>
    public bool IsNearTimeout(TimeSpan threshold, TimeSpan? maxDuration = null)
    {
        var max = maxDuration ?? TimeSpan.FromMinutes(5);
        return ElapsedTime > (max - threshold);
    }

    /// <summary>
    /// Checks if execution is near the iteration limit.
    /// </summary>
    /// <param name="buffer">Number of iterations before limit (e.g., 2 means stop if 2 iterations remain)</param>
    /// <returns>True if current iteration is within buffer of max iterations</returns>
    public bool IsNearIterationLimit(int buffer = 2)
    {
        return CurrentIteration >= MaxIterations - buffer;
    }
}
