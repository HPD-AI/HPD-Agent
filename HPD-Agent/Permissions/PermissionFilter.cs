using System;
using HPD.Agent.Internal.MiddleWare;
using HPD.Agent;
using System.Threading.Tasks;

/// <summary>
/// Unified permission Middleware that works with any protocol (Console, AGUI, Web, etc.).
/// Emits standardized permission events that can be handled by application-specific UI code.
/// Replaces both ConsolePermissionMiddleware and AGUIPermissionMiddleware with a single, protocol-agnostic implementation.
/// </summary>
internal class PermissionMiddleware : IPermissionMiddleware
{
    private readonly IPermissionStorage? _storage;
    private readonly AgentConfig? _config;
    private readonly string _MiddlewareName;

    /// <summary>
    /// Creates a new unified permission Middleware.
    /// </summary>
    /// <param name="storage">Optional permission storage for persistent decisions</param>
    /// <param name="config">Optional agent configuration for continuation settings</param>
    /// <param name="MiddlewareName">Optional name for this Middleware instance (defaults to "PermissionMiddleware")</param>
    public PermissionMiddleware(IPermissionStorage? storage = null, AgentConfig? config = null, string? MiddlewareName = null)
    {
        _storage = storage;
        _config = config;
        _MiddlewareName = MiddlewareName ?? "PermissionMiddleware";
    }

    public async Task InvokeAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // Check: Function-level permission (if required)
        if (context.Function is not HPDAIFunctionFactory.HPDAIFunction hpdFunction ||
            !hpdFunction.HPDOptions.RequiresPermission)
        {
            await next(context);
            return;
        }

        var functionName = context.ToolCallRequest?.FunctionName ?? "Unknown";
        var conversationId = context.State?.ConversationId ?? string.Empty;

        // Get the unique call ID for this specific tool invocation
        var callId = context.Metadata.TryGetValue("CallId", out var idObj)
            ? idObj?.ToString()
            : null;

        // Check storage if available
        if (_storage != null && !string.IsNullOrEmpty(conversationId))
        {
            var storedChoice = await _storage.GetStoredPermissionAsync(functionName, conversationId);

            if (storedChoice == PermissionChoice.AlwaysAllow)
            {
                await next(context);
                return;
            }

            if (storedChoice == PermissionChoice.AlwaysDeny)
            {
                context.Result = $"Execution of '{functionName}' was denied by a stored user preference.";
                context.IsTerminated = true;
                return;
            }
        }

        // No stored preference - request permission via events
        var permissionId = Guid.NewGuid().ToString();

        // Emit permission request event (standardized, protocol-agnostic)
        context.Emit(new InternalPermissionRequestEvent(
            permissionId,
            _MiddlewareName,
            functionName,
            context.Function.Description ?? "No description available",
            callId ?? string.Empty,
            context.ToolCallRequest?.Arguments ?? new Dictionary<string, object?>()));

        // Wait for response from external handler (BLOCKS HERE while event is processed)
        InternalPermissionResponseEvent response;
        try
        {
            response = await context.WaitForResponseAsync<InternalPermissionResponseEvent>(
                permissionId,
                timeout: TimeSpan.FromMinutes(5));
        }
        catch (TimeoutException)
        {
            // Emit denial event for observability
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                _MiddlewareName,
                "Permission request timed out after 5 minutes"));

            context.Result = "Permission request timed out. Please respond to permission requests promptly.";
            context.IsTerminated = true;
            return;
        }
        catch (OperationCanceledException)
        {
            // Emit denial event for observability
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                _MiddlewareName,
                "Permission request was cancelled"));

            context.Result = "Permission request was cancelled.";
            context.IsTerminated = true;
            return;
        }

        // Process the response
        if (response.Approved)
        {
            // Emit approval event for observability
            context.Emit(new InternalPermissionApprovedEvent(permissionId, _MiddlewareName));

            // Store persistent choice if user requested it
            if (_storage != null && response.Choice != PermissionChoice.Ask)
            {
                // Scope is implicit based on conversationId parameter
                await _storage.SavePermissionAsync(
                    functionName,
                    response.Choice,
                    conversationId: conversationId);
            }

            // Continue execution
            await next(context);
        }
        else
        {
            // Use user-provided denial reason, or fall back to configured default message
            // The user's reason takes priority; if not provided, use the configurable default
            var denialReason = response.Reason
                ?? _config?.Messages?.PermissionDeniedDefault
                ?? "Permission denied by user.";

            // Emit denial event for observability
            context.Emit(new InternalPermissionDeniedEvent(
                permissionId,
                _MiddlewareName,
                denialReason));

            // Set result to denial reason - will be sent to LLM as tool result
            // Priority: user's custom reason > configured default > hardcoded fallback
            context.Result = denialReason;
            context.IsTerminated = true;
        }
    }

}
