using System.Diagnostics;

namespace HPD.Agent.Filters;

/// <summary>
/// Example filter that emits progress events during function execution.
/// Demonstrates one-way event emission (no response needed).
/// </summary>
public class ProgressLoggingFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        // Emit progress start event (one-way, no response needed)
        context.Emit(new InternalFilterProgressEvent(
            "ProgressLoggingFilter",
            $"Starting execution of {context.ToolCallRequest.FunctionName}",
            PercentComplete: 0));

        var sw = Stopwatch.StartNew();

        try
        {
            // Execute the next filter/function in the pipeline
            await next(context);

            // Emit progress complete event
            context.Emit(new InternalFilterProgressEvent(
                "ProgressLoggingFilter",
                $"Completed {context.ToolCallRequest.FunctionName} in {sw.ElapsedMilliseconds}ms",
                PercentComplete: 100));
        }
        catch (Exception ex)
        {
            // Emit error event
            context.Emit(new InternalFilterErrorEvent(
                "ProgressLoggingFilter",
                $"Error in {context.ToolCallRequest.FunctionName}: {ex.Message}",
                ex));

            // Re-throw to maintain error propagation
            throw;
        }
    }
}

/// <summary>
/// Example custom event for cost tracking.
/// Demonstrates how to create custom filter events that implement IFilterEvent.
/// </summary>
public record CostEstimateEvent(
    string SourceName,
    string FunctionName,
    decimal EstimatedCost,
    string Currency
) : InternalAgentEvent, IBidirectionalEvent;

/// <summary>
/// Example custom event for actual cost tracking.
/// </summary>
public record ActualCostEvent(
    string SourceName,
    string FunctionName,
    decimal ActualCost,
    string Currency,
    double DurationMs
) : InternalAgentEvent, IBidirectionalEvent;

/// <summary>
/// Example filter that emits custom events.
/// Demonstrates user extensibility - users can define their own event types.
/// </summary>
public class CostTrackingFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var startTime = DateTime.UtcNow;

        // Emit cost estimate using custom event type
        context.Emit(new CostEstimateEvent(
            "CostTrackingFilter",
            context.ToolCallRequest.FunctionName,
            0.05m,
            "USD"));

        await next(context);

        var duration = DateTime.UtcNow - startTime;
        var actualCost = CalculateCost(duration);

        // Emit actual cost using custom event type
        context.Emit(new ActualCostEvent(
            "CostTrackingFilter",
            context.ToolCallRequest.FunctionName,
            actualCost,
            "USD",
            duration.TotalMilliseconds));
    }

    private decimal CalculateCost(TimeSpan duration)
    {
        // Simple example: $0.01 per second
        return (decimal)duration.TotalSeconds * 0.01m;
    }
}

/// <summary>
/// Example filter demonstrating bidirectional communication.
/// Requests permission before executing dangerous operations.
/// </summary>
public class SimplePermissionFilter : IAiFunctionFilter
{
    private readonly HashSet<string> _dangerousFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DeleteFile",
        "ExecuteCommand",
        "ModifySystemSettings"
    };

    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        // Check if this function requires permission
        if (!_dangerousFunctions.Contains(context.ToolCallRequest.FunctionName))
        {
            // Safe function, proceed without permission
            await next(context);
            return;
        }

        // Emit permission request event
        var permissionId = Guid.NewGuid().ToString();
        context.Emit(new InternalPermissionRequestEvent(
            permissionId,
            "SimplePermissionFilter",
            context.ToolCallRequest.FunctionName,
            $"Permission required to execute {context.ToolCallRequest.FunctionName}",
            context.Metadata.TryGetValue("CallId", out var callId) ? callId?.ToString() ?? "" : "",
            context.ToolCallRequest.Arguments));

        // Wait for response from external handler (BLOCKS HERE)
        // While blocked, background drainer yields event to handler
        InternalPermissionResponseEvent response;
        try
        {
            response = await context.WaitForResponseAsync<InternalPermissionResponseEvent>(
                permissionId,
                timeout: TimeSpan.FromMinutes(5));
        }
        catch (TimeoutException)
        {
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                "SimplePermissionFilter",
                "Permission request timed out"));
            context.Result = "Permission request timed out";
            context.IsTerminated = true;
            return;
        }
        catch (OperationCanceledException)
        {
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                "SimplePermissionFilter",
                "Permission request cancelled"));
            context.Result = "Permission request cancelled";
            context.IsTerminated = true;
            return;
        }

        // Handle response
        if (response.Approved)
        {
            context.Emit(new InternalPermissionApprovedEvent(permissionId, "SimplePermissionFilter"));

            // Continue execution
            await next(context);
        }
        else
        {
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                "SimplePermissionFilter",
                response.Reason ?? "Permission denied"));
            context.Result = response.Reason ?? "Permission denied";
            context.IsTerminated = true;
        }
    }
}
