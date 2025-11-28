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
    private readonly HPD_Agent.Permissions.PermissionOverrideRegistry? _overrideRegistry;

    /// <summary>
    /// Creates a new unified permission Middleware.
    /// </summary>
    /// <param name="storage">Optional permission storage for persistent decisions</param>
    /// <param name="config">Optional agent configuration for continuation settings</param>
    /// <param name="MiddlewareName">Optional name for this Middleware instance (defaults to "PermissionMiddleware")</param>
    /// <param name="overrideRegistry">Optional registry for runtime permission overrides</param>
    public PermissionMiddleware(IPermissionStorage? storage = null, AgentConfig? config = null, string? MiddlewareName = null, HPD_Agent.Permissions.PermissionOverrideRegistry? overrideRegistry = null)
    {
        _storage = storage;
        _config = config;
        _MiddlewareName = MiddlewareName ?? "PermissionMiddleware";
        _overrideRegistry = overrideRegistry;
    }

    public async Task InvokeAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var functionName = context.ToolCallRequest?.FunctionName ?? "Unknown";

        // Get the attribute value for RequiresPermission
        var attributeRequiresPermission = context.Function is HPDAIFunctionFactory.HPDAIFunction hpdFunction
            && hpdFunction.HPDOptions.RequiresPermission;

        // Apply override if present, otherwise use attribute value
        var effectiveRequiresPermission = _overrideRegistry?.GetEffectivePermissionRequirement(functionName, attributeRequiresPermission)
            ?? attributeRequiresPermission;

        // Check: Function-level permission (if required)
        if (!effectiveRequiresPermission)
        {
            await next(context);
            return;
        }

        var conversationId = context.State?.ConversationId;

        // Get the unique call ID for this specific tool invocation
        var callId = context.Metadata.TryGetValue("CallId", out var idObj)
            ? idObj?.ToString()
            : null;

        // Hierarchical permission lookup: conversation-scoped → global → ask user
        if (_storage != null)
        {
            PermissionChoice? storedChoice = null;

            // 1. Try conversation-scoped permission first (if we have a conversationId)
            if (!string.IsNullOrEmpty(conversationId))
            {
                storedChoice = await _storage.GetStoredPermissionAsync(functionName, conversationId);
            }

            // 2. Fallback to global permission if no conversation-specific permission found
            storedChoice ??= await _storage.GetStoredPermissionAsync(functionName, conversationId: null);

            // 3. Apply stored choice if found
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
        context.Emit(new PermissionRequestEvent(
            permissionId,
            _MiddlewareName,
            functionName,
            context.Function.Description ?? "No description available",
            callId ?? string.Empty,
            context.ToolCallRequest?.Arguments ?? new Dictionary<string, object?>()));

        // Wait for response from external handler (BLOCKS HERE while event is processed)
        PermissionResponseEvent response;
        try
        {
            response = await context.WaitForResponseAsync<PermissionResponseEvent>(
                permissionId,
                timeout: TimeSpan.FromMinutes(5));
        }
        catch (TimeoutException)
        {
            // Emit denial event for observability
            context.Emit(new PermissionDeniedEvent(
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
            context.Emit(new PermissionDeniedEvent(
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
            context.Emit(new PermissionApprovedEvent(permissionId, _MiddlewareName));

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
            context.Emit(new PermissionDeniedEvent(
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
