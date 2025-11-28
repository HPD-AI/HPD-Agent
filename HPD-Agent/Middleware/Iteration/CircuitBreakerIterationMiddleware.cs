using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HPD.Agent.Internal.MiddleWare;
using Microsoft.Extensions.AI;

namespace HPD.Agent;

/// <summary>
/// Prevents infinite loops by detecting repeated identical function calls.
/// Triggers circuit breaker when the same tool is called with identical arguments
/// more than <see cref="MaxConsecutiveCalls"/> times consecutively.
/// </summary>
/// <remarks>
/// This middleware uses the <see cref="IIterationMiddleWare.BeforeToolExecutionAsync"/> hook
/// which runs AFTER the LLM returns tool calls but BEFORE any tools execute.
/// This allows predictive checking: we calculate what the count WOULD BE if the tool executes.
///
/// Key behavior matching original AgentCore implementation:
/// 1. Computes function signature from tool name + serialized arguments
/// 2. Compares against last signature for that tool
/// 3. Calculates predicted count (current + 1 if identical, else 1)
/// 4. Triggers if predicted count >= MaxConsecutiveCalls
///
/// When triggered, the middleware:
/// 1. Sets <see cref="IterationMiddleWareContext.SkipToolExecution"/> to true
/// 2. Emits a <see cref="TextDeltaEvent"/> for user visibility (matching original)
/// 3. Emits a <see cref="CircuitBreakerTriggeredEvent"/> for observability
/// 4. Signals termination via Properties["IsTerminated"] = true
/// </remarks>
/// <example>
/// <code>
/// // Register via AgentBuilder
/// var agent = new AgentBuilder()
///     .WithCircuitBreaker(maxConsecutiveCalls: 3)
///     .Build();
///
/// // Or with custom configuration
/// var agent = new AgentBuilder()
///     .WithCircuitBreaker(config =>
///     {
///         config.MaxConsecutiveCalls = 5;
///         config.TerminationMessageTemplate = "Tool loop detected: {toolName}";
///     })
///     .Build();
/// </code>
/// </example>
public class CircuitBreakerIterationMiddleware : IIterationMiddleWare
{
    /// <summary>
    /// Maximum number of consecutive identical calls allowed before triggering.
    /// Default: 3 (matches typical agent configuration).
    /// </summary>
    public int MaxConsecutiveCalls { get; set; } = 3;

    /// <summary>
    /// Custom termination message template.
    /// Placeholders: {toolName}, {count}
    /// </summary>
    public string TerminationMessageTemplate { get; set; } =
        "⚠️ Circuit breaker triggered: Function '{toolName}' with same arguments would be called {count} times consecutively. " +
        "Stopping to prevent infinite loop.";

    /// <summary>
    /// Called BEFORE the LLM call begins.
    /// No circuit breaker logic here - we need to wait for tool calls from LLM.
    /// </summary>
    public Task BeforeIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // No action needed before LLM call
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER LLM returns tool calls but BEFORE tools execute.
    /// This is where we check if executing these tools would exceed the threshold.
    /// </summary>
    public Task BeforeToolExecutionAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // No tool calls = nothing to check
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;

        var state = context.State;

        foreach (var toolCall in context.ToolCalls)
        {
            var toolName = toolCall.Name ?? "_unknown";
            var signature = ComputeFunctionSignature(toolCall);

            // Calculate what the count WOULD BE if we execute this tool
            var lastSig = state.LastSignaturePerTool.GetValueOrDefault(toolName);
            var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;

            var countAfterExecution = isIdentical
                ? state.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1
                : 1;

            // Check if executing this tool would exceed the limit
            if (countAfterExecution >= MaxConsecutiveCalls)
            {
                TriggerCircuitBreaker(context, toolName, countAfterExecution);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER tool execution completes.
    /// No action needed - state updates are handled by the agent loop.
    /// </summary>
    public Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // State tracking (RecordToolCall) is handled by the agent loop after tool execution.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Computes a deterministic signature for a function call.
    /// Matches the original AgentCore.ComputeFunctionSignatureFromContent implementation.
    /// </summary>
    private static string ComputeFunctionSignature(FunctionCallContent toolCall)
    {
        var name = toolCall.Name ?? "_unknown";

        // Serialize arguments to JSON for consistent comparison
        string argsJson;
        if (toolCall.Arguments == null || toolCall.Arguments.Count == 0)
        {
            argsJson = "{}";
        }
        else
        {
            try
            {
                // Sort keys for deterministic ordering
                var sortedArgs = toolCall.Arguments
                    .OrderBy(kvp => kvp.Key)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                argsJson = JsonSerializer.Serialize(sortedArgs);
            }
            catch
            {
                // Fallback if serialization fails
                argsJson = string.Join(",", toolCall.Arguments.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }
        }

        return $"{name}({argsJson})";
    }

    /// <summary>
    /// Triggers the circuit breaker, preventing tool execution and terminating the loop.
    /// </summary>
    private void TriggerCircuitBreaker(IterationMiddleWareContext context, string toolName, int count)
    {
        // Skip tool execution
        context.SkipToolExecution = true;

        // Format termination message
        var message = TerminationMessageTemplate
            .Replace("{toolName}", toolName)
            .Replace("{count}", count.ToString());

        // Signal termination via properties
        context.Properties["IsTerminated"] = true;
        context.Properties["TerminationReason"] = $"Circuit breaker: '{toolName}' with same arguments would be called {count} times consecutively";

        // Emit TextDeltaEvent for user visibility (matching original behavior)
        try
        {
            context.Emit(new TextDeltaEvent(message, Guid.NewGuid().ToString()));
        }
        catch (InvalidOperationException)
        {
            // EventCoordinator not configured - event emission is optional
        }

        // Emit CircuitBreakerTriggeredEvent for observability
        try
        {
            context.Emit(new CircuitBreakerTriggeredEvent(
                AgentName: context.AgentName,
                FunctionName: toolName,
                ConsecutiveCount: count,
                Iteration: context.Iteration,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch (InvalidOperationException)
        {
            // EventCoordinator not configured - event emission is optional
        }
    }
}
