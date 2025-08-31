using Microsoft.Extensions.AI;

/// <summary>
/// Default orchestration: invokes the first agent in the list.
/// </summary>
public class DirectOrchestrator : IOrchestrator
{
    public async Task<ChatResponse> OrchestrateAsync(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<Agent> agents,
        string? conversationId = null,
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

    // Add this debugging to your DirectOrchestrator.OrchestrateStreamingAsync method

public StreamingTurnResult OrchestrateStreamingAsync(
    IReadOnlyList<ChatMessage> messages, 
    IReadOnlyList<Agent> agents, 
    string? conversationId = null, 
    ChatOptions? options = null, 
    CancellationToken cancellationToken = default)
{
    var primaryAgent = agents.FirstOrDefault();
    if (primaryAgent == null)
    {
        // Return empty result if no agent available
        async IAsyncEnumerable<ChatResponseUpdate> EmptyStream()
        {
            yield break;
        }
        var emptyHistory = Task.FromResult<IReadOnlyList<ChatMessage>>(new List<ChatMessage>());
        return new StreamingTurnResult(EmptyStream(), emptyHistory);
    }

    // We need to handle the async call properly
    var resultTask = primaryAgent.ExecuteStreamingTurnAsync(messages, options, cancellationToken);
    
    // Create a wrapper that delays the async call until the stream is consumed
    async IAsyncEnumerable<ChatResponseUpdate> DelayedStream()
    {
        var result = await resultTask;
        await foreach (var update in result.ResponseStream.WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }
    
    // Create delayed history task
    var delayedHistoryTask = Task.Run(async () =>
    {
        var result = await resultTask;
        return await result.FinalHistory;
    }, cancellationToken);
    
    return new StreamingTurnResult(DelayedStream(), delayedHistoryTask);
}
}
