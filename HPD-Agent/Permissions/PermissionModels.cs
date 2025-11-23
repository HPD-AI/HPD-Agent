using System.Collections.Generic;

/// <summary>
/// Represents the application's decision about a function permission request.
/// Used by permission Middlewares.
/// </summary>
public class PermissionDecision
{
    public bool Approved { get; set; }

    /// <summary>
    /// Optional: Remember this choice for future invocations.
    /// If set, the permission will be stored according to the scope implied by
    /// the conversationId/threadId parameters passed to SavePermissionAsync.
    /// </summary>
    public PermissionChoice? RememberAs { get; set; }
}

/// <summary>
/// User's preference for how to handle permission requests for a function.
/// </summary>
public enum PermissionChoice
{
    /// <summary>Always allow this function without asking</summary>
    AlwaysAllow,

    /// <summary>Always deny this function without asking</summary>
    AlwaysDeny,

    /// <summary>Ask for permission each time</summary>
    Ask
}