using System.Threading.Tasks;
using System.Collections.Concurrent;

public static class AgentBuilderPermissionExtensions
{
    /// <summary>
    /// Adds a console-based permission filter for command-line applications.
    /// </summary>
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
    /// </summary>
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
