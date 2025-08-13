using Microsoft.Extensions.AI;

/// <summary>
/// Graph orchestrator that executes a workflow definition over multiple agents.
/// </summary>
public class GraphOrchestrator<TState> : IOrchestrator where TState : class, new()
{
    private readonly WorkflowDefinition _workflow;
    private readonly IReadOnlyDictionary<string, Agent> _agents;
    private readonly IConditionEvaluator<TState> _conditionEvaluator;
    private readonly ICheckpointStore<TState>? _checkpointStore;
    // Track which agents were used during execution
    private readonly List<string> _usedAgentsDuringExecution = new();

    public GraphOrchestrator(
        WorkflowDefinition workflow,
        IEnumerable<Agent> agents,
        IConditionEvaluator<TState>? conditionEvaluator = null,
        ICheckpointStore<TState>? checkpointStore = null)
    {
        _workflow = workflow;
        _agents = agents.ToDictionary(a => a.Name, a => a);
        _conditionEvaluator = conditionEvaluator ?? new SmartDefaultEvaluator<TState>();
        _checkpointStore = checkpointStore;
        ValidateWorkflow();
    }

    public async Task<ChatResponse> OrchestrateAsync(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<Agent> agents,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Initialize or restore context
        var conversationId = Guid.NewGuid().ToString();
        var context = await RestoreOrCreateContext(conversationId, history);

        while (context.CurrentNodeId is not null)
        {
            // Execute current node
            context = await ExecuteNodeAsync(context, options, cancellationToken);
            // Save checkpoint if enabled
            if (_checkpointStore != null)
                await _checkpointStore.SaveAsync(conversationId, context, cancellationToken);
            // Determine next node
            context = context with { CurrentNodeId = await GetNextNodeAsync(context, cancellationToken) };
        }

        // Return the last message or empty response
        return context.LastMessage is ChatMessage last
            ? new ChatResponse(last)
            : new ChatResponse();
    }

    private async Task<WorkflowContext<TState>> RestoreOrCreateContext(
        string conversationId,
        IReadOnlyList<ChatMessage> history)
    {
        if (_checkpointStore != null)
        {
            var restored = await _checkpointStore.LoadAsync(conversationId, default);
            if (restored != null)
                return restored;
        }

        return new WorkflowContext<TState>(
            State: new TState(),
            History: history,
            ConversationId: conversationId,
            CurrentNodeId: _workflow.StartNodeId,
            LastUpdatedAt: DateTime.UtcNow
        );
    }

    private async Task<WorkflowContext<TState>> ExecuteNodeAsync(
        WorkflowContext<TState> context,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        // CurrentNodeId is non-null when executing
        var node = GetNode(context.CurrentNodeId!);
    var agent = GetAgent(node.AgentName);
    _usedAgentsDuringExecution.Add(agent.Name); // Track usage
        var effectiveHistory = ApplyInputMappings(context, node);
        var response = await agent.GetResponseAsync(effectiveHistory, options, cancellationToken);

        var newMessages = ExtractMessages(response);
        var updatedHistory = new List<ChatMessage>(context.History);
        updatedHistory.AddRange(newMessages);
        var updatedState = await UpdateStateAsync(context.State, node, response, cancellationToken);

        return context with
        {
            State = updatedState,
            History = updatedHistory,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    // Determine next node based on condition evaluation
    private async Task<string?> GetNextNodeAsync(
        WorkflowContext<TState> context,
        CancellationToken cancellationToken)
    {
        // Evaluate outgoing edges; return first matching target or null
        foreach (var edge in _workflow.Edges.Where(e => e.FromNodeId == context.CurrentNodeId!))
        {
            if (await _conditionEvaluator.EvaluateAsync(edge.Condition, context, cancellationToken))
                return edge.ToNodeId;
        }
        return null;
    }

    /// <summary>
    /// Override this method for custom state update logic. Default is no-op.
    /// </summary>
    protected virtual Task<TState> UpdateStateAsync(
        TState currentState,
        WorkflowNode node,
        ChatResponse response,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(currentState);
    }

    /// <summary>
    /// Override this method for node-specific input mapping. Default passes full history.
    /// </summary>
    protected virtual IReadOnlyList<ChatMessage> ApplyInputMappings(
        WorkflowContext<TState> context,
        WorkflowNode node)
    {
        return context.History;
    }

    private WorkflowNode GetNode(string nodeId)
        => _workflow.Nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new InvalidOperationException($"Node '{nodeId}' not found in workflow '{_workflow.Name}'");

    private Agent GetAgent(string agentName)
        => _agents.TryGetValue(agentName, out var agent)
            ? agent
            : throw new InvalidOperationException($"Agent '{agentName}' not registered. Available agents: {string.Join(", ", _agents.Keys)}");

    private static IEnumerable<ChatMessage> ExtractMessages(ChatResponse response)
        => response.Messages?.Any() == true
            ? response.Messages
            : Array.Empty<ChatMessage>();

    private void ValidateWorkflow()
    {
        var nodeIds = _workflow.Nodes.Select(n => n.Id).ToHashSet();
        if (!nodeIds.Contains(_workflow.StartNodeId))
            throw new ArgumentException($"Start node '{_workflow.StartNodeId}' not found in workflow '{_workflow.Name}'");
        foreach (var edge in _workflow.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
                throw new ArgumentException($"Invalid edge in workflow '{_workflow.Name}': {edge.FromNodeId} -> {edge.ToNodeId}");
        }
    }
}
