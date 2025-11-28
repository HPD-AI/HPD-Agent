using HPD.Agent.Internal.MiddleWare;
using Microsoft.Extensions.AI;

namespace HPD.Agent;

/// <summary>
/// Tracks consecutive errors across iterations and terminates execution when threshold is exceeded.
/// Analyzes tool results in AfterIterationAsync to detect errors and manage failure state.
/// </summary>
/// <remarks>
/// This middleware follows the pattern established by <see cref="CircuitBreakerIterationMiddleware"/>.
///
/// Error detection happens in AfterIterationAsync after tool execution completes:
/// 1. Analyzes ToolResults for errors (exceptions or error patterns in results)
/// 2. Signals state updates via Properties (HasToolErrors, ShouldIncrementFailures)
/// 3. Checks threshold and triggers termination if exceeded
///
/// The agent loop processes these signals and updates AgentLoopState accordingly.
///
/// When triggered, the middleware:
/// 1. Emits a <see cref="MaxConsecutiveErrorsExceededEvent"/> for observability
/// 2. Signals termination via Properties["IsTerminated"] = true
/// 3. Provides a termination message explaining the error threshold trigger
/// </remarks>
/// <example>
/// <code>
/// // Register via AgentBuilder
/// var agent = new AgentBuilder()
///     .WithErrorTracking(maxConsecutiveErrors: 3)
///     .Build();
///
/// // Or with custom error detection
/// var agent = new AgentBuilder()
///     .WithErrorTracking(config =>
///     {
///         config.MaxConsecutiveErrors = 5;
///         config.CustomErrorDetector = result =>
///             result.Exception != null ||
///             result.Result?.ToString()?.Contains("CRITICAL") == true;
///     })
///     .Build();
/// </code>
/// </example>
public class ErrorTrackingIterationMiddleware : IIterationMiddleWare
{
    /// <summary>
    /// Maximum number of consecutive errors allowed before triggering termination.
    /// Default: 3 (matches typical agent configuration).
    /// </summary>
    public int MaxConsecutiveErrors { get; set; } = 3;

    /// <summary>
    /// Custom error detection function. If not provided, uses default error detection.
    /// Return true if the FunctionResultContent represents an error.
    /// </summary>
    public Func<FunctionResultContent, bool>? CustomErrorDetector { get; set; }

    /// <summary>
    /// Custom termination message template.
    /// Placeholders: {count}, {max}
    /// </summary>
    public string TerminationMessageTemplate { get; set; } =
        "Maximum consecutive errors ({count}/{max}) exceeded. " +
        "Stopping execution to prevent infinite error loop.";

    /// <summary>
    /// Called BEFORE the LLM call begins.
    /// Checks if consecutive failures already at/above threshold to prevent wasted LLM calls.
    /// </summary>
    public Task BeforeIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // Skip check on first iteration (no previous errors to check)
        if (context.Iteration == 0)
            return Task.CompletedTask;

        // Check if we've already exceeded the threshold (from previous iteration)
        var consecutiveFailures = context.State.ConsecutiveFailures;

        if (consecutiveFailures >= MaxConsecutiveErrors)
        {
            // Already at threshold - terminate before wasting LLM call
            TriggerTermination(context, consecutiveFailures);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER LLM returns tool calls but BEFORE tools execute.
    /// No error tracking logic here - we need to wait for tool results.
    /// </summary>
    public Task BeforeToolExecutionAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // No action needed before tool execution
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called AFTER tool execution completes.
    /// Analyzes tool results for errors and signals state updates.
    /// </summary>
    public Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // If no tool results, nothing to analyze
        if (context.ToolResults.Count == 0)
            return Task.CompletedTask;

        // Detect errors in tool results
        var hasErrors = context.ToolResults.Any(IsError);

        // Signal error state to agent loop
        context.Properties["HasToolErrors"] = hasErrors;

        if (hasErrors)
        {
            // Signal that failure counter should be incremented
            context.Properties["ShouldIncrementFailures"] = true;

            // Calculate what the count will be after increment
            var currentFailures = context.State.ConsecutiveFailures;
            var newFailureCount = currentFailures + 1;

            // Check if this will exceed threshold
            if (newFailureCount >= MaxConsecutiveErrors)
            {
                TriggerTermination(context, newFailureCount);
            }
        }
        else
        {
            // Signal that failure counter should be reset
            context.Properties["ShouldResetFailures"] = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if a function result represents an error.
    /// Uses custom detector if provided, otherwise uses default detection.
    /// </summary>
    private bool IsError(FunctionResultContent result)
    {
        // Use custom detector if provided
        if (CustomErrorDetector != null)
            return CustomErrorDetector(result);

        // Default error detection
        return IsDefaultError(result);
    }

    /// <summary>
    /// Default error detection logic matching the original AgentCore implementation.
    /// </summary>
    private static bool IsDefaultError(FunctionResultContent result)
    {
        // Primary signal: Exception present
        if (result.Exception != null)
            return true;

        // Secondary signal: Result contains error indicators
        var resultStr = result.Result?.ToString();
        if (string.IsNullOrEmpty(resultStr))
            return false;

        // Check for definitive error patterns (case-insensitive)
        return resultStr.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
               resultStr.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase) ||
               // Exception indicators
               resultStr.Contains("exception occurred", StringComparison.OrdinalIgnoreCase) ||
               resultStr.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase) ||
               resultStr.Contains("exception was thrown", StringComparison.OrdinalIgnoreCase) ||
               // Rate limit indicators
               resultStr.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase) ||
               resultStr.Contains("rate limited", StringComparison.OrdinalIgnoreCase) ||
               resultStr.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase) ||
               resultStr.Contains("quota reached", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Triggers termination, preventing further LLM calls and signaling the agent loop.
    /// </summary>
    private void TriggerTermination(IterationMiddleWareContext context, int errorCount)
    {
        // Skip the next LLM call
        context.SkipLLMCall = true;

        // Format termination message (with emoji matching original)
        var message = $"⚠️ {TerminationMessageTemplate}"
            .Replace("{count}", errorCount.ToString())
            .Replace("{max}", MaxConsecutiveErrors.ToString());

        // Provide final response
        context.Response = new ChatMessage(
            ChatRole.Assistant,
            message);

        // No further tool calls
        context.ToolCalls = Array.Empty<FunctionCallContent>();

        // Signal termination via properties
        context.Properties["IsTerminated"] = true;
        context.Properties["TerminationReason"] = $"Maximum consecutive errors ({errorCount}) exceeded";

        // Emit TextDeltaEvent for user visibility (matching original behavior)
        try
        {
            context.Emit(new TextDeltaEvent(message, Guid.NewGuid().ToString()));
        }
        catch (InvalidOperationException)
        {
            // EventCoordinator not configured - event emission is optional
        }

        // Emit observability event
        try
        {
            context.Emit(new MaxConsecutiveErrorsExceededEvent(
                AgentName: context.AgentName,
                ConsecutiveErrors: errorCount,
                MaxConsecutiveErrors: MaxConsecutiveErrors,
                Iteration: context.Iteration,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch (InvalidOperationException)
        {
            // EventCoordinator not configured - event emission is optional
        }
    }
}

/// <summary>
/// Event emitted when the maximum consecutive errors threshold is exceeded.
/// </summary>
public record MaxConsecutiveErrorsExceededEvent(
    string AgentName,
    int ConsecutiveErrors,
    int MaxConsecutiveErrors,
    int Iteration,
    DateTimeOffset Timestamp) : AgentEvent, IObservabilityEvent;
