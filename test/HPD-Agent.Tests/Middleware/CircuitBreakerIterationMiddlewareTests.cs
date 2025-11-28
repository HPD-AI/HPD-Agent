using HPD.Agent;
using HPD_Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;
using System.Collections.Immutable;
using Xunit;

namespace HPD_Agent.Tests.Middleware;

/// <summary>
/// Characterization tests for CircuitBreakerIterationMiddleware.
/// These tests document and verify the expected behavior of the circuit breaker.
/// </summary>
public class CircuitBreakerIterationMiddlewareTests
{
    [Fact]
    public async Task BeforeIteration_FirstIteration_DoesNotTrigger()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 3
        };

        var context = CreateContext(iteration: 0); // First iteration

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.False(context.SkipLLMCall);
        Assert.Null(context.Response);
        Assert.False(context.Properties.ContainsKey("IsTerminated"));
    }

    [Fact]
    public async Task BeforeIteration_BelowThreshold_DoesNotTrigger()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 3
        };

        // State shows 2 consecutive calls (below threshold of 3)
        var state = CreateStateWithConsecutiveCalls("test_tool", 2);
        var context = CreateContext(iteration: 2, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.False(context.SkipLLMCall);
        Assert.Null(context.Response);
    }

    [Fact]
    public async Task BeforeIteration_AtThreshold_TriggersCricuitBreaker()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 3
        };

        // State shows 3 consecutive calls (at threshold)
        var state = CreateStateWithConsecutiveCalls("test_tool", 3);
        var context = CreateContext(iteration: 3, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.True(context.SkipLLMCall);
        Assert.NotNull(context.Response);
        Assert.Contains("test_tool", context.Response.Text);
        Assert.Contains("3", context.Response.Text);
        Assert.True((bool)context.Properties["IsTerminated"]);
        Assert.Contains("Circuit breaker", (string)context.Properties["TerminationReason"]);
    }

    [Fact]
    public async Task BeforeIteration_AboveThreshold_TriggersCricuitBreaker()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 3
        };

        // State shows 5 consecutive calls (above threshold)
        var state = CreateStateWithConsecutiveCalls("stuck_function", 5);
        var context = CreateContext(iteration: 5, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.True(context.SkipLLMCall);
        Assert.NotNull(context.Response);
        Assert.Contains("stuck_function", context.Response.Text);
        Assert.Empty(context.ToolCalls);
    }

    [Fact]
    public async Task BeforeIteration_MultipleTools_OnlyTriggersOnExceedingTool()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 3
        };

        // State shows multiple tools, only one exceeds threshold
        var state = CreateEmptyState() with
        {
            ConsecutiveCountPerTool = ImmutableDictionary<string, int>.Empty
                .Add("tool_a", 1) // Under threshold
                .Add("tool_b", 3) // At threshold - should trigger
                .Add("tool_c", 2), // Under threshold
            LastSignaturePerTool = ImmutableDictionary<string, string>.Empty
                .Add("tool_a", "sig_a")
                .Add("tool_b", "sig_b")
                .Add("tool_c", "sig_c")
        };
        var context = CreateContext(iteration: 3, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.True(context.SkipLLMCall);
        Assert.NotNull(context.Response);
        Assert.Contains("tool_b", context.Response.Text);
    }

    [Fact]
    public async Task BeforeIteration_CustomMessageTemplate_UsesTemplate()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 2,
            TerminationMessageTemplate = "LOOP DETECTED: {toolName} was called {count} times!"
        };

        var state = CreateStateWithConsecutiveCalls("my_function", 2);
        var context = CreateContext(iteration: 2, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.True(context.SkipLLMCall);
        Assert.NotNull(context.Response);
        Assert.Equal("LOOP DETECTED: my_function was called 2 times!", context.Response.Text);
    }

    [Fact]
    public async Task AfterIteration_DoesNothing()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware();
        var context = CreateContext(iteration: 1);

        // Act
        await middleware.AfterIterationAsync(context, CancellationToken.None);

        // Assert - no changes to context
        Assert.False(context.SkipLLMCall);
        Assert.Null(context.Response);
    }

    [Fact]
    public void DefaultConfiguration_UsesReasonableDefaults()
    {
        // Arrange & Act
        var middleware = new CircuitBreakerIterationMiddleware();

        // Assert
        Assert.Equal(3, middleware.MaxConsecutiveCalls);
        Assert.Contains("Circuit breaker triggered", middleware.TerminationMessageTemplate);
        Assert.Contains("{toolName}", middleware.TerminationMessageTemplate);
        Assert.Contains("{count}", middleware.TerminationMessageTemplate);
    }

    [Fact]
    public async Task BeforeIteration_EmptyState_DoesNotTrigger()
    {
        // Arrange
        var middleware = new CircuitBreakerIterationMiddleware
        {
            MaxConsecutiveCalls = 1 // Very low threshold
        };

        // Empty state (no tool calls recorded yet)
        var state = CreateEmptyState();
        var context = CreateContext(iteration: 1, state: state);

        // Act
        await middleware.BeforeIterationAsync(context, CancellationToken.None);

        // Assert
        Assert.False(context.SkipLLMCall);
    }

    // ═══════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════

    private static AgentLoopState CreateEmptyState()
    {
        return AgentLoopState.Initial(
            messages: Array.Empty<ChatMessage>(),
            runId: "test-run",
            conversationId: "test-conversation",
            agentName: "TestAgent");
    }

    private static IterationMiddleWareContext CreateContext(
        int iteration,
        AgentLoopState? state = null)
    {
        return new IterationMiddleWareContext
        {
            Iteration = iteration,
            AgentName = "TestAgent",
            CancellationToken = CancellationToken.None,
            Messages = new List<ChatMessage>(),
            State = state ?? CreateEmptyState(),
            Options = new ChatOptions()
        };
    }

    private static AgentLoopState CreateStateWithConsecutiveCalls(string toolName, int count)
    {
        return CreateEmptyState() with
        {
            ConsecutiveCountPerTool = ImmutableDictionary<string, int>.Empty.Add(toolName, count),
            LastSignaturePerTool = ImmutableDictionary<string, string>.Empty.Add(toolName, $"{toolName}(arg=value)")
        };
    }
}
