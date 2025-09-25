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
        var filter = new ConsolePermissionFilter(storage);
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
        var filter = new AGUIPermissionFilter(eventEmitter, storage);
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

    /// <summary>
    /// Enables Type 2 (continuation) permissions by configuring the ContinuationPermissionManager.
    /// </summary>
    public static AgentBuilder WithContinuationPermissions(
        this AgentBuilder builder,
        IPermissionHandler permissionHandler,
        IPermissionStorage? permissionStorage = null,
        ContinuationOptions? options = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var continuationOptions = options ?? new ContinuationOptions();
        var manager = new ContinuationPermissionManager(permissionHandler, storage, continuationOptions);

        return builder.WithContinuationManager(manager);
    }

    /// <summary>
    /// Enables both console permissions and continuation permissions using the same storage.
    /// </summary>
    public static AgentBuilder WithFullConsolePermissions(
        this AgentBuilder builder,
        IPermissionHandler permissionHandler,
        IPermissionStorage? permissionStorage = null,
        ContinuationOptions? options = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        return builder
            .WithConsolePermissions(storage)
            .WithContinuationPermissions(permissionHandler, storage, options);
    }

    /// <summary>
    /// Enables both AGUI permissions and continuation permissions using the same storage.
    /// </summary>
    public static AgentBuilder WithFullAGUIPermissions(
        this AgentBuilder builder,
        IPermissionEventEmitter eventEmitter,
        IPermissionHandler permissionHandler,
        IPermissionStorage? permissionStorage = null,
        ContinuationOptions? options = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        return builder
            .WithAGUIPermissions(eventEmitter, storage)
            .WithContinuationPermissions(permissionHandler, storage, options);
    }
}

/// <summary>
/// A default, non-persistent implementation of IPermissionStorage for development and testing.
/// </summary>
public class InMemoryPermissionStorage : IPermissionStorage
{
    private readonly ConcurrentDictionary<string, PermissionChoice> _functionChoices = new();
    private readonly ConcurrentDictionary<string, ContinuationPreference> _continuationChoices = new();

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

    public Task<ContinuationPreference?> GetContinuationPreferenceAsync(string conversationId, string? projectId)
    {
        _continuationChoices.TryGetValue(conversationId, out var preference);
        return Task.FromResult((ContinuationPreference?)preference);
    }

    public Task SaveContinuationPreferenceAsync(ContinuationStorage storage, string conversationId, string? projectId)
    {
        _continuationChoices[conversationId] = storage.Preference;
        return Task.CompletedTask;
    }
}
