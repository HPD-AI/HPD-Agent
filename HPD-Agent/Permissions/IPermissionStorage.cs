using System.Threading.Tasks;

/// <summary>
/// Interface for storing and retrieving permission preferences.
/// Implementations can use in-memory, file-based, or database storage.
/// Scoping is implicit based on which parameters are provided:
/// - functionName only: Global permission
/// - functionName + conversationId: Conversation-scoped permission
/// - functionName + conversationId + threadId: Thread-scoped permission
/// </summary>
public interface IPermissionStorage
{
    /// <summary>
    /// Gets a stored permission preference for a specific function.
    /// Pass conversationId and/or threadId to scope the lookup.
    /// Returns null if no stored permission exists.
    /// </summary>
    Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null,
        string? threadId = null);

    /// <summary>
    /// Saves a permission preference for a specific function.
    /// Pass conversationId and/or threadId to scope the storage.
    /// Scope is implicit: no IDs = global, conversationId = conversation-scoped, both = thread-scoped.
    /// </summary>
    Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null,
        string? threadId = null);
}