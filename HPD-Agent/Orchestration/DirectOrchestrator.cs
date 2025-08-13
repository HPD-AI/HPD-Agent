using Microsoft.Extensions.AI;

/// <summary>
/// Default orchestration: invokes the first agent in the list.
/// </summary>
public class DirectOrchestrator : IOrchestrator
{
    public async Task<ChatResponse> OrchestrateAsync(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<Agent> agents,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (agents == null || agents.Count == 0)
        {
            throw new InvalidOperationException("No agents available for orchestration.");
        }

        // Invoke the first agent, preserving existing behavior
        return await agents[0].GetResponseAsync(history, options, cancellationToken);
    }
}
