using System.Threading.Tasks;
using System.Collections.Concurrent;

public static class AgentBuilderPermissionExtensions
{
    /// <summary>
    /// Adds the unified permission filter that works with any protocol (Console, AGUI, Web, etc.).
    /// Permission requests are emitted as events that you handle in your application code.
    /// This gives you full control over how permission prompts are displayed to users.
    /// </summary>
    /// <param name="builder">The agent builder</param>
    /// <param name="permissionStorage">Optional permission storage for persistent decisions</param>
    /// <returns>The agent builder for chaining</returns>
    /// <example>
    /// // In your Program.cs:
    /// var agent = new AgentBuilder()
    ///     .WithPermissions(storage)  // Use new unified filter
    ///     .Build();
    ///
    /// // Then handle events in your event loop (see FILTER_EVENTS_USAGE.md)
    /// </example>
    public static AgentBuilder WithPermissions(
        this AgentBuilder builder,
        IPermissionStorage? permissionStorage = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var filter = new PermissionFilter(storage, builder.Config);
        return builder.WithPermissionFilter(filter);
    }

    /// <summary>
    /// Adds a console-based permission filter for command-line applications.
    /// DEPRECATED: Use WithPermissions() instead and handle InternalPermissionRequestEvent in your event loop.
    /// This allows you to customize the permission prompt text and options.
    /// </summary>
    [Obsolete("Use WithPermissions() instead. See FILTER_EVENTS_USAGE.md for migration guide.", false)]
    public static AgentBuilder WithConsolePermissions(
        this AgentBuilder builder,
        IPermissionStorage? permissionStorage = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var filter = new ConsolePermissionFilter(storage, builder.Config);
        return builder.WithPermissionFilter(filter);
    }

    /// <summary>
    /// Adds an AGUI-based permission filter for web applications.
    /// DEPRECATED: Use WithPermissions() instead and convert InternalPermissionRequestEvent to AGUI format in your event handler.
    /// This decouples the filter from the AGUI protocol and gives you control over event conversion.
    /// </summary>
    [Obsolete("Use WithPermissions() instead and handle event conversion in your AGUI adapter. See FILTER_EVENTS_USAGE.md for migration guide.", false)]
    public static AgentBuilder WithAGUIPermissions(
        this AgentBuilder builder,
        IPermissionEventEmitter eventEmitter,
        IPermissionStorage? permissionStorage = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var filter = new AGUIPermissionFilter(eventEmitter, storage, builder.Config);
        return builder.WithPermissionFilter(filter);
    }

    /// <summary>
    /// Adds an auto-approve permission filter for testing and automation scenarios.
    /// </summary>
    public static AgentBuilder WithAutoApprovePermissions(this AgentBuilder builder)
    {
        var filter = new AutoApprovePermissionFilter();
        return builder.WithPermissionFilter(filter);
    }
}

/// <summary>
/// A default, non-persistent implementation of IPermissionStorage for development and testing.
/// </summary>
public class InMemoryPermissionStorage : IPermissionStorage
{
    private readonly ConcurrentDictionary<string, PermissionChoice> _functionChoices = new();

    public Task<PermissionChoice?> GetStoredPermissionAsync(string functionName, string conversationId, string? projectId)
    {
        if (_functionChoices.TryGetValue(functionName, out var choice))
        {
            return Task.FromResult((PermissionChoice?)choice);
        }
        return Task.FromResult((PermissionChoice?)null);
    }

    public Task SavePermissionAsync(string functionName, PermissionChoice choice, PermissionScope scope, string conversationId, string? projectId)
    {
        if (choice != PermissionChoice.Ask)
        {
            _functionChoices[functionName] = choice;
        }
        return Task.CompletedTask;
    }
}
