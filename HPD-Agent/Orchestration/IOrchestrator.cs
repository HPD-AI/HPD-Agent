using Microsoft.Extensions.AI;

/// <summary>
/// Core orchestration interface for v0.
/// Combines Microsoft's message+context pattern with Pydantic's serializable state approach.
/// Separates serializable orchestration data from runtime services for better persistence and extensibility.
///
/// IMPORTANT: Orchestrator implementations must pass through history reduction metadata
/// from StreamingTurnResult.Reduction to OrchestrationMetadata.Context to support
/// conversation history reduction. See example implementation below.
/// </summary>
/// <example>
/// Example orchestrator implementation showing new request+context pattern:
/// <code>
/// public async Task&lt;OrchestrationResult&gt; OrchestrateAsync(
///     OrchestrationRequest request,
///     IOrchestrationContext context,
///     CancellationToken cancellationToken = default)
/// {
///     // 1. Get runtime agents from context
///     var agents = context.GetAgents();
///     var selectedAgent = SelectBestAgent(request.GetChatHistory(), agents);
///
///     // 2. Get chat options from context
///     var options = context.GetChatOptions();
///
///     // 3. Call agent and get streaming result
///     var streamingResult = await selectedAgent.ExecuteStreamingTurnAsync(
///         request.GetChatHistory(), options, cancellationToken: cancellationToken);
///
///     // 4. Consume stream
///     await foreach (var evt in streamingResult.EventStream.WithCancellation(cancellationToken))
///     {
///         // Process events as needed
///     }
///
///     // 5. Get final history
///     var finalHistory = await streamingResult.FinalHistory;
///
///     // 6. Package reduction metadata using helper (RECOMMENDED)
///     var reductionContext = OrchestrationHelpers.PackageReductionMetadata(streamingResult.Reduction);
///
///     // 7. Save orchestrator state if needed
///     await context.UpdateStateAsync("last_agent", selectedAgent.Name);
///
///     // 8. Return orchestration result
///     return new OrchestrationResult
///     {
///         Response = new ChatResponse(finalHistory),
///         PrimaryAgent = selectedAgent,
///         RunId = request.RunId ?? Guid.NewGuid().ToString("N"),
///         CreatedAt = DateTimeOffset.UtcNow,
///         Status = OrchestrationStatus.Completed,
///         Metadata = new OrchestrationMetadata
///         {
///             StrategyName = "YourStrategy",
///             DecisionDuration = TimeSpan.Zero,
///             Context = reductionContext
///         }
///     };
/// }
/// </code>
/// </example>
public interface IOrchestrator
{
    /// <summary>
    /// Simple orchestration using request+context pattern for better serialization and extensibility.
    /// Separates serializable orchestration data (request) from runtime services (context).
    /// </summary>
    /// <param name="request">Serializable orchestration request containing history, agent IDs, and configuration.</param>
    /// <param name="context">Runtime context providing agents, services, and state management.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Rich orchestration result containing response, selected agent, and metadata.</returns>
    Task<OrchestrationResult> OrchestrateAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming orchestration with BaseEvent emission for full observability.
    /// Uses request+context pattern for consistency with non-streaming method.
    /// </summary>
    /// <param name="request">Serializable orchestration request containing history, agent IDs, and configuration.</param>
    /// <param name="context">Runtime context providing agents, services, and state management.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Streaming result with BaseEvent stream and completion tasks.</returns>
    Task<OrchestrationStreamingResult> OrchestrateStreamingAsync(
        OrchestrationRequest request,
        IOrchestrationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Serializable orchestration request containing all data needed for orchestration.
/// Separates serializable state from runtime objects following Microsoft Workflows and Pydantic Graph patterns.
/// Supports both conversation-based and generic orchestration scenarios.
/// </summary>
public record OrchestrationRequest
{
    /// <summary>
    /// Generic input data for orchestration. Can be any serializable object.
    /// For conversation scenarios, this should be an IReadOnlyList&lt;ChatMessage&gt;.
    /// For other scenarios, this could be files, structured data, API requests, etc.
    /// </summary>
    public required object Input { get; init; }

    /// <summary>
    /// Type descriptor for the input data (e.g., "chat", "file", "data", "api").
    /// Helps orchestrators understand how to interpret the Input object.
    /// </summary>
    public required string InputType { get; init; }

    /// <summary>
    /// The conversation history for chat-based orchestration (optional).
    /// When provided, orchestrators can use this directly without casting Input.
    /// For non-conversation scenarios, this should be null.
    /// </summary>
    public IReadOnlyList<ChatMessage>? History { get; init; }

    /// <summary>
    /// Agent identifiers for orchestration. Runtime Agent objects are provided via context.
    /// </summary>
    public required IReadOnlyList<string> AgentIds { get; init; }

    /// <summary>
    /// Unique identifier for this orchestration run.
    /// </summary>
    public string? RunId { get; init; }

    /// <summary>
    /// Optional conversation identifier for stateful orchestrators.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Orchestrator-specific configuration and extensions.
    /// Enables dynamic orchestrator behavior without breaking the interface.
    /// </summary>
    public IReadOnlyDictionary<string, object> Extensions { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Priority or urgency level for this orchestration (0-10, where 10 is highest priority).
    /// </summary>
    public int Priority { get; init; } = 5;

    /// <summary>
    /// Maximum execution time for this orchestration.
    /// </summary>
    public TimeSpan? MaxExecutionTime { get; init; }

    /// <summary>
    /// Convenience method to get a typed extension value.
    /// </summary>
    public T? GetExtension<T>(string key) where T : class
        => Extensions.TryGetValue(key, out var value) ? value as T : null;

    /// <summary>
    /// Convenience method to check if an extension exists.
    /// </summary>
    public bool HasExtension(string key) => Extensions.ContainsKey(key);

    /// <summary>
    /// Convenience method to get conversation history for chat-based orchestration.
    /// Returns History if available, otherwise attempts to cast Input to IReadOnlyList&lt;ChatMessage&gt;.
    /// </summary>
    public IReadOnlyList<ChatMessage>? GetChatHistory()
    {
        // If History is explicitly provided, use it
        if (History != null) return History;
        
        // If InputType indicates chat and Input is chat messages, use it
        if (InputType == "chat" && Input is IReadOnlyList<ChatMessage> chatMessages)
            return chatMessages;
            
        return null;
    }

    /// <summary>
    /// Convenience method to get typed input data.
    /// </summary>
    public T? GetInput<T>() where T : class => Input as T;

    /// <summary>
    /// Convenience method to check if this is a conversation-based orchestration.
    /// </summary>
    public bool IsConversationOrchestration => InputType == "chat" || History != null;
}

/// <summary>
/// Runtime context providing services and non-serializable objects to orchestrators.
/// Inspired by Microsoft Workflows' IWorkflowContext pattern.
/// </summary>
public interface IOrchestrationContext
{
    /// <summary>
    /// Gets the runtime Agent objects corresponding to the AgentIds in the request.
    /// </summary>
    IReadOnlyList<Agent> GetAgents();

    /// <summary>
    /// Gets the Agent object for a specific agent ID.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns>The agent, or null if not found.</returns>
    Agent? GetAgent(string agentId);

    /// <summary>
    /// Gets the chat options for this orchestration.
    /// </summary>
    ChatOptions? GetChatOptions();

    /// <summary>
    /// Reads orchestrator state from persistent storage.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="key">The state key.</param>
    /// <param name="scope">Optional scope for the state (defaults to orchestrator-specific scope).</param>
    ValueTask<T?> ReadStateAsync<T>(string key, string? scope = null);

    /// <summary>
    /// Updates orchestrator state in persistent storage.
    /// </summary>
    /// <typeparam name="T">The type of the state value.</typeparam>
    /// <param name="key">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <param name="scope">Optional scope for the state (defaults to orchestrator-specific scope).</param>
    ValueTask UpdateStateAsync<T>(string key, T? value, string? scope = null);

    /// <summary>
    /// Clears all state in the specified scope.
    /// </summary>
    /// <param name="scope">Optional scope to clear (defaults to orchestrator-specific scope).</param>
    ValueTask ClearStateAsync(string? scope = null);

    /// <summary>
    /// Emits an event that will be included in the orchestration result's event stream.
    /// </summary>
    ValueTask EmitEventAsync(BaseEvent orchestrationEvent);

    /// <summary>
    /// Gets contextual metadata for this orchestration (e.g., conversation metadata, project context).
    /// </summary>
    IReadOnlyDictionary<string, object> GetMetadata();

    /// <summary>
    /// Gets trace context for observability.
    /// </summary>
    IReadOnlyDictionary<string, string>? GetTraceContext();

    /// <summary>
    /// Creates a checkpoint for resuming orchestration later.
    /// </summary>
    /// <param name="checkpointData">Orchestrator-specific checkpoint data.</param>
    ValueTask<string> CreateCheckpointAsync(Dictionary<string, object>? checkpointData = null);
}

/// <summary>
/// Orchestration execution status.
/// </summary>
public enum OrchestrationStatus
{
    /// <summary>
    /// Orchestration has not started yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Orchestration is currently executing.
    /// </summary>
    Executing,

    /// <summary>
    /// Orchestration completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Orchestration failed or was terminated.
    /// </summary>
    Failed
}

/// <summary>
/// Orchestration decision metadata.
/// </summary>
public record OrchestrationMetadata
{
    public TimeSpan DecisionDuration { get; init; }
    public string StrategyName { get; init; } = "";
    public IReadOnlyDictionary<string, float> AgentScores { get; init; } = new Dictionary<string, float>();
    public IReadOnlyDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Whether orchestrator state was modified during this turn.
    /// Null if orchestrator is stateless.
    /// </summary>
    public bool? StateModified { get; init; }

    /// <summary>
    /// Which state keys were modified during this turn.
    /// Null if orchestrator doesn't track state changes.
    /// </summary>
    public IReadOnlyList<string>? ModifiedStateKeys { get; init; }
}

/// <summary>
/// Primary orchestration result.
/// Contains universal fields applicable to all orchestrator types.
/// </summary>
public record OrchestrationResult
{
    // ========================================
    // REQUIRED (All orchestrators must provide)
    // ========================================

    /// <summary>
    /// The final response from the orchestration.
    /// </summary>
    public required ChatResponse Response { get; init; }

    /// <summary>
    /// The primary agent that produced the response.
    /// For multi-agent orchestrations, this is the agent that generated the final output.
    /// </summary>
    public required Agent PrimaryAgent { get; init; }

    /// <summary>
    /// Unique identifier for this orchestration run.
    /// Use to correlate multiple turns in the same orchestration session.
    /// </summary>
    public required string RunId { get; init; }

    // ========================================
    // WITH DEFAULTS (All orchestrators benefit)
    // ========================================

    /// <summary>
    /// When this result was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Orchestration execution status.
    /// </summary>
    public OrchestrationStatus Status { get; init; } = OrchestrationStatus.Completed;

    /// <summary>
    /// Basic metadata about the orchestration decision.
    /// </summary>
    public OrchestrationMetadata Metadata { get; init; } = new();

    // ========================================
    // OPTIONAL (Only some orchestrators use)
    // ========================================

    /// <summary>
    /// All agents that were activated during this turn.
    /// Null if orchestrator only activated a single agent (the PrimaryAgent).
    /// </summary>
    public IReadOnlyList<Agent>? ActivatedAgents { get; init; }

    /// <summary>
    /// Current turn number in multi-turn orchestration (0-based).
    /// Null if orchestrator doesn't track turns.
    /// </summary>
    public int? TurnNumber { get; init; }

    /// <summary>
    /// Whether this orchestration requires user input before continuing.
    /// Only relevant for human-in-the-loop workflows.
    /// </summary>
    public bool RequiresUserInput { get; init; }

    /// <summary>
    /// Checkpoint for resuming orchestration.
    /// Null if orchestrator doesn't support checkpointing.
    /// </summary>
    public OrchestrationCheckpoint? Checkpoint { get; init; }

    /// <summary>
    /// Aggregated token usage across all agents in this orchestration.
    /// Null if no token usage information is available.
    /// </summary>
    public TokenUsage? AggregatedUsage { get; init; }

    /// <summary>
    /// Total number of agents/nodes that executed during orchestration.
    /// </summary>
    public int ExecutionCount { get; init; } = 1;

    /// <summary>
    /// Total execution time for the entire orchestration in milliseconds.
    /// </summary>
    public int ExecutionTimeMs { get; init; }

    /// <summary>
    /// Execution order (which agents ran in sequence).
    /// Null if orchestrator doesn't track execution order.
    /// </summary>
    public IReadOnlyList<string>? ExecutionOrder { get; init; }

    // ========================================
    // BACKWARD COMPATIBILITY
    // ========================================

    /// <summary>
    /// Whether the orchestration is complete (backward compatibility).
    /// - true: Orchestration completed successfully
    /// - false: Orchestration is pending, executing, or failed
    /// </summary>
    public bool IsComplete => Status == OrchestrationStatus.Completed;

    /// <summary>
    /// Implicit conversion for convenience.
    /// </summary>
    public static implicit operator ChatResponse(OrchestrationResult result)
        => result.Response;
}

/// <summary>
/// Checkpoint for resuming orchestration.
/// </summary>
public record OrchestrationCheckpoint
{
    /// <summary>
    /// The run this checkpoint belongs to.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Unique identifier for this specific checkpoint.
    /// </summary>
    public required string CheckpointId { get; init; }

    /// <summary>
    /// When this checkpoint was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Current state identifier (e.g., graph node, workflow step).
    /// Orchestrator-specific.
    /// </summary>
    public string? CurrentState { get; init; }

    /// <summary>
    /// Orchestrator-specific variables for resuming.
    /// </summary>
    public Dictionary<string, object>? Variables { get; init; }
}

/// <summary>
/// Streaming orchestration result with BaseEvent stream.
/// </summary>
public record OrchestrationStreamingResult
{
    public required IAsyncEnumerable<BaseEvent> EventStream { get; init; }
    public required Task<OrchestrationResult> FinalResult { get; init; }
    public required Task<IReadOnlyList<ChatMessage>> FinalHistory { get; init; }
}
