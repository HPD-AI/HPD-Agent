using System.Threading.Tasks;
using System.Collections.Concurrent;

public static class AgentBuilderPermissionExtensions
{
    /// <summary>
    /// Enables Type 1 (function-level) permissions by registering the FunctionPermissionFilter.
    /// </summary>
    public static AgentBuilder WithFunctionPermissions(
        this AgentBuilder builder,
        IPermissionHandler permissionHandler,
        IPermissionStorage? permissionStorage = null)
    {
        var storage = permissionStorage ?? new InMemoryPermissionStorage();
        var filter = new FunctionPermissionFilter(permissionHandler, storage);
        
        // Uses the existing WithFilter method on AgentBuilder
        return builder.WithFilter(filter);
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
        
        // Calls the internal method we will add to AgentBuilder in the next step
        return builder.WithContinuationManager(manager);
    }
    
    /// <summary>
    /// Enables both Type 1 and Type 2 permissions using the same handler and storage.
    /// </summary>
    public static AgentBuilder WithFullPermissions(
        this AgentBuilder builder,
        IPermissionHandler permissionHandler,
        IPermissionStorage? permissionStorage = null,
        ContinuationOptions? options = null)
    {
        return builder
            .WithFunctionPermissions(permissionHandler, permissionStorage)
            .WithContinuationPermissions(permissionHandler, permissionStorage, options);
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
