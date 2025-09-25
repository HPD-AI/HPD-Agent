using System.Collections.Generic;

/// <summary>
/// Represents the application's decision about a function permission request.
/// Used by permission filters.
/// </summary>
public class PermissionDecision
{
    public bool Approved { get; set; }
    public PermissionStorage? Storage { get; set; }
}

/// <summary>
/// Optional preference storage request from the application.
/// </summary>
public class PermissionStorage
{
    public PermissionChoice Choice { get; set; }
    public PermissionScope Scope { get; set; }
}

public enum PermissionChoice
{
    AlwaysAllow,
    AlwaysDeny,
    Ask
}

public enum PermissionScope
{
    Conversation,
    Project,
    Global
}