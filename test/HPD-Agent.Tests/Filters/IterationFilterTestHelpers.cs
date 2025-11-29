using HPD.Agent;
using HPD.Agent.Middleware;
using Microsoft.Extensions.AI;

namespace HPD.Agent.Tests.Filters;

/// <summary>
/// Helper methods for creating test middleware contexts.
/// </summary>
internal static class MiddlewareTestHelpers
{
    /// <summary>
    /// Creates a basic agent middleware context for testing.
    /// </summary>
    public static AgentMiddlewareContext CreateContext(int iteration = 0)
    {
        var context = new AgentMiddlewareContext
        {
            Iteration = iteration,
            AgentName = "TestAgent",
            Messages = new List<ChatMessage>(),
            Options = new ChatOptions(),
            ConversationId = "test-conv-id",
            CancellationToken = CancellationToken.None
        };
        return context;
    }
}
