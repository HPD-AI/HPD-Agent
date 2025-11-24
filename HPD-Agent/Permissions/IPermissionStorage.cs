using System.Threading.Tasks;

/// <summary>
/// Interface for storing and retrieving permission preferences.
/// Implementations can use in-memory, file-based, or database storage.
/// Scoping is implicit based on which parameters are provided:
/// - functionName only: Global permission (applies to all conversations)
/// - functionName + conversationId: Conversation-scoped permission (applies to specific conversation only)
/// </summary>
public interface IPermissionStorage
{
    /// <summary>
    /// Gets a stored permission preference for a specific function.
    /// Pass conversationId to scope the lookup to a specific conversation, or omit for global lookup.
    /// Returns null if no stored permission exists.
    /// </summary>
    Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null);

    /// <summary>
    /// Saves a permission preference for a specific function.
    /// Pass conversationId to scope to a specific conversation, or omit for global scope.
    /// Scope is implicit: no conversationId = global (all conversations), conversationId = conversation-scoped.
    /// </summary>
    Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null);
}