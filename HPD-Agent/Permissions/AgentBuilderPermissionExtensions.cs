using System.Threading.Tasks;
using HPD.Agent;
using System.Collections.Concurrent;

public static class AgentBuilderPermissionExtensions
{
    /// <summary>
    /// Adds the unified permission Middleware that works with any protocol (Console, AGUI, Web, etc.).
    /// Permission requests are emitted as events that you handle in your application code.
    /// This gives you full control over how permission prompts are displayed to users.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="permissionStorage">Optional permission storage for persistent decisions</param>
    /// <returns>The agent builder for chaining</returns>
    /// <example>
    /// // In your Program.cs:
    /// var agent = new AgentBuilder()
    ///     .WithPermissions(storage)  // Use new unified Middleware
    ///     .Build();
    ///
    /// // Then handle events in your event loop (see Middleware_EVENTS_USAGE.md)
    /// </example>
    public static AgentBuilder WithPermissions(
        this AgentBuilder builder,
        IPermissionStorage? permissionStorage = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var Middleware = new PermissionMiddleware(storage, builder.Config);
        return builder.WithPermissionMiddleware(Middleware);
    }

    /// <summary>
    /// Adds an auto-approve permission Middleware for testing and automation scenarios.
    /// </summary>
    public static AgentBuilder WithAutoApprovePermissions(this AgentBuilder builder)
    {
        var Middleware = new AutoApprovePermissionMiddleware();
        return builder.WithPermissionMiddleware(Middleware);
    }
}

/// <summary>
/// A default, non-persistent implementation of IPermissionStorage for development and testing.
/// Uses implicit scoping based on the parameters provided.
/// </summary>
public class InMemoryPermissionStorage : IPermissionStorage
{
    private readonly ConcurrentDictionary<string, PermissionChoice> _permissions = new();

    public Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null,
        string? threadId = null)
    {
        var key = BuildKey(functionName, conversationId, threadId);
        _permissions.TryGetValue(key, out var choice);
        return Task.FromResult((PermissionChoice?)choice);
    }

    public Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null,
        string? threadId = null)
    {
        if (choice != PermissionChoice.Ask)
        {
            var key = BuildKey(functionName, conversationId, threadId);
            _permissions[key] = choice;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds a storage key with implicit scoping based on provided parameters.
    /// </summary>
    private static string BuildKey(string functionName, string? conversationId, string? threadId)
    {
        // Scoping is implicit in the key structure:
        // - thread-scoped: "conv:thread:function"
        // - conversation-scoped: "conv:function"
        // - global: "function"
        if (!string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(conversationId))
            return $"{conversationId}:{threadId}:{functionName}";
        if (!string.IsNullOrEmpty(conversationId))
            return $"{conversationId}:{functionName}";
        return functionName;
    }
}
