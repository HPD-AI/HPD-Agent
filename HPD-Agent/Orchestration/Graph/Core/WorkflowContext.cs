using Microsoft.Extensions.AI;

/// <summary>
/// Immutable workflow context with optional typed state.
/// </summary>
public record WorkflowContext<TState>(
    TState State,
    IReadOnlyList<ChatMessage> History,
    string ConversationId,
    string? CurrentNodeId,
    DateTime LastUpdatedAt
) where TState : class, new()
{
    /// <summary>Last message in the workflow history.</summary>
    public ChatMessage? LastMessage => History.Count > 0 ? History[^1] : null;
}
