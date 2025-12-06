namespace HPD.Agent.Checkpointing.Services;

#region Branch Types

/// <summary>
/// Represents the tree structure of conversation branches for visualization.
/// </summary>
public class BranchTree
{
    /// <summary>
    /// Thread this branch tree belongs to.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Root checkpoint ID (first checkpoint in the tree).
    /// </summary>
    public required string RootCheckpointId { get; init; }

    /// <summary>
    /// All nodes in the tree, keyed by checkpoint ID.
    /// </summary>
    public required Dictionary<string, BranchNode> Nodes { get; init; }

    /// <summary>
    /// Named branches with metadata.
    /// </summary>
    public required Dictionary<string, BranchMetadata> NamedBranches { get; init; }

    /// <summary>
    /// Currently active branch name.
    /// </summary>
    public string? ActiveBranch { get; init; }
}

/// <summary>
/// A single node in the branch tree, representing one checkpoint.
/// </summary>
public class BranchNode
{
    /// <summary>
    /// Checkpoint ID for this node.
    /// </summary>
    public required string CheckpointId { get; init; }

    /// <summary>
    /// Parent checkpoint ID, null for root.
    /// </summary>
    public string? ParentCheckpointId { get; init; }

    /// <summary>
    /// Child checkpoint IDs (branches from this point).
    /// </summary>
    public required List<string> ChildCheckpointIds { get; init; }

    /// <summary>
    /// Number of messages at this checkpoint.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// When this checkpoint was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Branch name if this is a branch head, null otherwise.
    /// </summary>
    public string? BranchName { get; init; }
}

/// <summary>
/// Metadata for a named branch - provides UI-friendly context.
/// </summary>
public class BranchMetadata
{
    /// <summary>
    /// Branch name (e.g., "main", "branch-1").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description (e.g., "Edited to ask about cats").
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Message index where this branch forked from parent.
    /// </summary>
    public int ForkMessageIndex { get; init; }

    /// <summary>
    /// Checkpoint ID at the tip of this branch.
    /// </summary>
    public required string HeadCheckpointId { get; set; }

    /// <summary>
    /// When this branch was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Last activity on this branch.
    /// </summary>
    public DateTime LastActivity { get; set; }

    /// <summary>
    /// Total messages in this branch.
    /// </summary>
    public int MessageCount { get; set; }
}

#endregion

#region Branch Events

/// <summary>
/// Event raised when a new branch is created.
/// </summary>
public record BranchCreatedEvent(
    string ThreadId,
    string BranchName,
    string CheckpointId,
    string ParentCheckpointId,
    int ForkMessageIndex,
    DateTime CreatedAt) : AgentEvent
{
    public BranchCreatedEvent(
        string threadId,
        string branchName,
        string checkpointId,
        string parentCheckpointId,
        int forkMessageIndex)
        : this(threadId, branchName, checkpointId, parentCheckpointId, forkMessageIndex, DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Event raised when the active branch is switched.
/// </summary>
public record BranchSwitchedEvent(
    string ThreadId,
    string? PreviousBranch,
    string? NewBranch,
    string CheckpointId,
    DateTime SwitchedAt) : AgentEvent
{
    public BranchSwitchedEvent(
        string threadId,
        string? previousBranch,
        string? newBranch,
        string checkpointId)
        : this(threadId, previousBranch, newBranch, checkpointId, DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Event raised when a branch is deleted.
/// </summary>
public record BranchDeletedEvent(
    string ThreadId,
    string BranchName,
    int CheckpointsPruned,
    DateTime DeletedAt) : AgentEvent
{
    public BranchDeletedEvent(
        string threadId,
        string branchName,
        int checkpointsPruned)
        : this(threadId, branchName, checkpointsPruned, DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Event raised when a branch is renamed.
/// </summary>
public record BranchRenamedEvent(
    string ThreadId,
    string OldName,
    string NewName,
    DateTime RenamedAt) : AgentEvent
{
    public BranchRenamedEvent(
        string threadId,
        string oldName,
        string newName)
        : this(threadId, oldName, newName, DateTime.UtcNow)
    {
    }
}

/// <summary>
/// Event raised when a thread is copied from another thread's checkpoint.
/// Unlike fork (which creates a branch within the same thread), copy creates
/// a new independent thread with lineage tracking back to the source.
/// </summary>
public record ThreadCopiedEvent(
    /// <summary>Source thread that was copied from.</summary>
    string SourceThreadId,
    /// <summary>Newly created thread ID.</summary>
    string NewThreadId,
    /// <summary>Checkpoint in source thread that was copied.</summary>
    string SourceCheckpointId,
    /// <summary>Root checkpoint ID in the new thread.</summary>
    string NewCheckpointId,
    /// <summary>Message index at the copy point.</summary>
    int MessageIndex,
    /// <summary>When the copy occurred.</summary>
    DateTime CopiedAt) : AgentEvent
{
    public ThreadCopiedEvent(
        string sourceThreadId,
        string newThreadId,
        string sourceCheckpointId,
        string newCheckpointId,
        int messageIndex)
        : this(sourceThreadId, newThreadId, sourceCheckpointId, newCheckpointId, messageIndex, DateTime.UtcNow)
    {
    }
}

#endregion
