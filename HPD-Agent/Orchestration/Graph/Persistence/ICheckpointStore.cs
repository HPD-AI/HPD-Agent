/// <summary>
/// Interface for checkpoint storage of workflow context.
/// </summary>
public interface ICheckpointStore<TState> where TState : class, new()
{
    /// <summary>
    /// Save the given context under the conversation ID.
    /// </summary>
    Task SaveAsync(string conversationId, WorkflowContext<TState> context, CancellationToken cancellationToken);

    /// <summary>
    /// Load a previously saved context or null if none.
    /// </summary>
    Task<WorkflowContext<TState>?> LoadAsync(string conversationId, CancellationToken cancellationToken);
}
