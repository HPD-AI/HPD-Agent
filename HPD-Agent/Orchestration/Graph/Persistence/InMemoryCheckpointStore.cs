/// <summary>
/// Simple in-memory checkpoint store for development/testing.
/// </summary>
public class InMemoryCheckpointStore<TState> : ICheckpointStore<TState> where TState : class, new()
{
    // Store context objects directly in memory (no JSON)
    private readonly Dictionary<string, WorkflowContext<TState>> _store = new();

    public Task SaveAsync(string conversationId, WorkflowContext<TState> context, CancellationToken cancellationToken)
    {
        _store[conversationId] = context;
        return Task.CompletedTask;
    }

    public Task<WorkflowContext<TState>?> LoadAsync(string conversationId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(conversationId, out var context);
        return Task.FromResult(context);
    }
}
