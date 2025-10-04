using Microsoft.Extensions.AI;

/// <summary>
/// Internal orchestrator that handles agent selection and execution for conversations.
/// Supports both single-agent (direct execution) and multi-agent (orchestrator-based) scenarios.
/// </summary>
internal class ConversationOrchestrator
{
    private readonly IReadOnlyList<Agent> _agents;
    private IOrchestrator? _defaultOrchestrator;

    /// <summary>
    /// Creates an orchestrator for the given agents
    /// </summary>
    public ConversationOrchestrator(IReadOnlyList<Agent> agents, IOrchestrator? defaultOrchestrator = null)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _defaultOrchestrator = defaultOrchestrator;
    }

    /// <summary>
    /// Gets or sets the default orchestrator used for multi-agent scenarios
    /// </summary>
    public IOrchestrator? DefaultOrchestrator
    {
        get => _defaultOrchestrator;
        set => _defaultOrchestrator = value;
    }

    /// <summary>
    /// Get the list of agents managed by this orchestrator
    /// </summary>
    public IReadOnlyList<Agent> Agents => _agents;

    /// <summary>
    /// Execute a turn with agent selection (non-streaming)
    /// </summary>
    public async Task<OrchestrationResult> ExecuteTurnAsync(
        IReadOnlyList<ChatMessage> history,
        string? conversationId,
        ChatOptions? options,
        IOrchestrator? explicitOrchestrator = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAgents();

        if (_agents.Count == 1)
        {
            // Single agent - direct execution
            return await ExecuteSingleAgentAsync(history, options, cancellationToken);
        }
        else
        {
            // Multi-agent - use orchestrator
            var orchestrator = explicitOrchestrator ?? _defaultOrchestrator;
            if (orchestrator == null)
            {
                throw new InvalidOperationException(
                    $"Multi-agent conversations ({_agents.Count} agents) require an orchestrator. " +
                    $"Set DefaultOrchestrator or pass an orchestrator parameter.");
            }

            return await orchestrator.OrchestrateAsync(history, _agents, conversationId, options, cancellationToken);
        }
    }

    /// <summary>
    /// Execute a turn with agent selection (streaming)
    /// </summary>
    public async Task<OrchestrationStreamingResult> ExecuteStreamingTurnAsync(
        IReadOnlyList<ChatMessage> history,
        string? conversationId,
        ChatOptions? options,
        IOrchestrator? explicitOrchestrator = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAgents();

        if (_agents.Count == 1)
        {
            // Single agent - direct streaming execution
            return await ExecuteSingleAgentStreamingAsync(history, conversationId, options, cancellationToken);
        }
        else
        {
            // Multi-agent - use orchestrator streaming
            var orchestrator = explicitOrchestrator ?? _defaultOrchestrator;
            if (orchestrator == null)
            {
                throw new InvalidOperationException(
                    $"Multi-agent conversations ({_agents.Count} agents) require an orchestrator. " +
                    $"Set DefaultOrchestrator or pass an orchestrator parameter.");
            }

            return await orchestrator.OrchestrateStreamingAsync(history, _agents, conversationId, options, cancellationToken);
        }
    }

    /// <summary>
    /// Execute a single agent turn (non-streaming)
    /// Internally uses streaming and collects results
    /// </summary>
    private async Task<OrchestrationResult> ExecuteSingleAgentAsync(
        IReadOnlyList<ChatMessage> history,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var agent = _agents[0];
        var streamingResult = await agent.ExecuteStreamingTurnAsync(history, options, cancellationToken: cancellationToken);

        // Consume the stream (don't yield, just collect)
        await foreach (var _ in streamingResult.EventStream.WithCancellation(cancellationToken))
        {
            // Just consume events
        }

        // Get final history
        var finalHistory = await streamingResult.FinalHistory;

        // Package reduction metadata into Context dictionary
        var reductionContext = new Dictionary<string, object>();
        if (streamingResult.Reduction != null)
        {
            if (streamingResult.Reduction.SummaryMessage != null)
            {
                reductionContext["SummaryMessage"] = streamingResult.Reduction.SummaryMessage;
            }
            reductionContext["MessagesRemovedCount"] = streamingResult.Reduction.MessagesRemovedCount;
        }

        return new OrchestrationResult
        {
            Response = new ChatResponse(finalHistory.ToList()),
            SelectedAgent = agent,
            Metadata = new OrchestrationMetadata
            {
                StrategyName = "SingleAgent",
                DecisionDuration = TimeSpan.Zero,
                Context = reductionContext
            }
        };
    }

    /// <summary>
    /// Execute a single agent turn (streaming)
    /// Returns streaming result directly from agent
    /// </summary>
    private async Task<OrchestrationStreamingResult> ExecuteSingleAgentStreamingAsync(
        IReadOnlyList<ChatMessage> history,
        string? conversationId,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        var agent = _agents[0];
        var streamingResult = await agent.ExecuteStreamingTurnAsync(history, options, cancellationToken: cancellationToken);

        // Create task that packages the final result with reduction metadata
        var finalResultTask = streamingResult.FinalHistory.ContinueWith(async historyTask =>
        {
            var finalHistory = await historyTask;

            // Package reduction metadata
            var reductionContext = new Dictionary<string, object>();
            if (streamingResult.Reduction != null)
            {
                if (streamingResult.Reduction.SummaryMessage != null)
                {
                    reductionContext["SummaryMessage"] = streamingResult.Reduction.SummaryMessage;
                }
                reductionContext["MessagesRemovedCount"] = streamingResult.Reduction.MessagesRemovedCount;
            }

            return new OrchestrationResult
            {
                Response = new ChatResponse(finalHistory.ToList()),
                SelectedAgent = agent,
                Metadata = new OrchestrationMetadata
                {
                    StrategyName = "SingleAgent",
                    DecisionDuration = TimeSpan.Zero,
                    Context = reductionContext
                }
            };
        }, cancellationToken).Unwrap();

        return new OrchestrationStreamingResult
        {
            EventStream = streamingResult.EventStream,
            FinalResult = finalResultTask,
            FinalHistory = streamingResult.FinalHistory
        };
    }

    private void ValidateAgents()
    {
        if (_agents.Count == 0)
        {
            throw new InvalidOperationException("No agents configured for this conversation");
        }
    }
}
