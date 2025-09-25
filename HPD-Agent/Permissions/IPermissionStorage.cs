using System.Threading.Tasks;

/// <summary>
/// Interface for storing and retrieving permission preferences.
/// Consuming applications can implement custom storage backends.
/// </summary>
public interface IPermissionStorage
{
    /// <summary>
    /// Gets a stored permission preference for a specific function.
    /// </summary>
    Task<PermissionChoice?> GetStoredPermissionAsync(string functionName, string conversationId, string? projectId);
    
    /// <summary>
    /// Saves a permission preference for a specific function.
    /// </summary>
    Task SavePermissionAsync(string functionName, PermissionChoice choice, PermissionScope scope, string conversationId, string? projectId);
}