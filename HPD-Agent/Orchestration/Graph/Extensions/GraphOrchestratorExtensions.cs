/// <summary>
/// Factory methods for creating common orchestrator configurations.
/// </summary>
public static class GraphOrchestratorExtensions
{
    /// <summary>
    /// Create a simple orchestrator with no state management.
    /// </summary>
    public static GraphOrchestrator<NoState> CreateSimple(
        this WorkflowDefinition workflow,
        IEnumerable<Agent> agents) => new(workflow, agents);

    /// <summary>
    /// Create an orchestrator with typed state management.
    /// </summary>
    public static GraphOrchestrator<TState> CreateWithState<TState>(
        this WorkflowDefinition workflow,
        IEnumerable<Agent> agents,
        IConditionEvaluator<TState>? evaluator = null,
        ICheckpointStore<TState>? store = null) where TState : class, new()
        => new(workflow, agents, evaluator, store);

    /// <summary>
    /// Create a resilient orchestrator with checkpoint persistence.
    /// </summary>
    public static GraphOrchestrator<TState> CreateResilient<TState>(
        this WorkflowDefinition workflow,
        IEnumerable<Agent> agents,
        ICheckpointStore<TState> checkpointStore,
        IConditionEvaluator<TState>? evaluator = null) where TState : class, new()
        => new(workflow, agents, evaluator, checkpointStore);
}
