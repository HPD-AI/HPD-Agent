# 15. Branching

Branching enables fork-based conversation exploration. Create alternative conversation paths, try different approaches, and explore "what if" scenarios without losing your original conversation.

## What is Branching?

Branching lets you:

- **Fork conversations** - Create a copy from any point and explore alternatives
- **Switch between branches** - Jump between different conversation paths
- **Compare approaches** - Try different strategies side-by-side
- **Recover from mistakes** - Go back and try a different approach

> **Note**: Branching is separate from [Durable Execution](./13%20Durable%20Execution.md). Branching creates conversation variants; Durable Execution provides crash recovery. They work together but are independent features.

## When to Use Branching

| Scenario | Use Branching? |
|----------|----------------|
| Chat UI with "fork from here" button | ✅ Yes |
| Exploring different conversation strategies | ✅ Yes |
| A/B testing agent responses | ✅ Yes |
| Rollback to previous conversation state | ✅ Yes |
| Simple linear conversations | ❌ No |
| Crash recovery only | ❌ No (use Durable Execution) |

---

## Quick Setup

### Using DI (Recommended)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.Branching.Enabled = true;
});

// Inject BranchingService where needed
public class ConversationController
{
    private readonly Branching _branching;
    
    public ConversationController(Branching branching)
    {
        _branching = branching;
    }
}
```

### Manual Setup

```csharp
var store = new JsonConversationThreadStore("./checkpoints");
var branching = new Branching(store, new BranchingConfig 
{ 
    Enabled = true 
});
```

---

## Core Operations

### 1. Fork - Create Alternative Path

Fork creates a new branch from an existing checkpoint, copying the conversation up to that point.

```csharp
// Save a snapshot for branching
await store.SaveSnapshotAsync(
    thread.Id,
    checkpointId,
    thread.ToSnapshot(),
    new CheckpointMetadata
    {
        Source = CheckpointSource.User,
        MessageIndex = thread.MessageCount
    });

// Fork from that checkpoint
var (forkedThread, evt) = await branching.ForkFromCheckpointAsync(
    thread.Id,
    checkpointId,
    "feature-exploration");

// Continue on the new branch
await agent.RunAsync(newMessages, forkedThread);
```

**What happens:**
- Creates new branch with name "feature-exploration"
- Copies messages and metadata from checkpoint
- Does NOT copy execution state (starts fresh)
- Original thread unchanged

### 2. Copy - Duplicate to New Thread

Copy creates an entirely new thread (new ID) from an existing checkpoint.

```csharp
// Copy to independent thread
var (copiedThread, evt) = await branching.CopyFromCheckpointAsync(
    sourceThread.Id,
    checkpointId,
    "Copied Conversation");

// copiedThread has new ID
Console.WriteLine(copiedThread.Id);  // Different from sourceThread.Id
```

**Use cases:**
- Share conversation with another user
- Create template from existing conversation
- Isolate experiments

### 3. Switch - Change Active Branch

Switch moves to a different branch within the same thread.

```csharp
var (switchedThread, evt) = await branching.SwitchBranchAsync(
    thread.Id,
    targetBranchName: "feature-exploration");

if (switchedThread != null)
{
    // Now on feature-exploration branch
    await agent.RunAsync(messages, switchedThread);
}
```

**Important**: Switching branches discards current execution state. You start fresh on the new branch.

### 4. Get Branch Tree - Visualize Structure

```csharp
var tree = await branching.GetBranchTreeAsync(thread.Id);

Console.WriteLine($"Root: {tree.RootCheckpointId}");
Console.WriteLine($"Active: {tree.ActiveBranch}");

foreach (var node in tree.Nodes)
{
    Console.WriteLine($"  {node.CheckpointId} -> {node.BranchName}");
}
```

---

## Snapshots vs Checkpoints

Branching uses **lightweight snapshots** (~20KB) instead of full checkpoints (~120KB):

```
ThreadSnapshot (for branching):
├─ ThreadId
├─ Messages
├─ Metadata
├─ Branch info
└─ ❌ NO execution state

ExecutionCheckpoint (for recovery):
├─ ThreadId
├─ Messages
├─ Metadata
├─ Branch info
└─ ✅ Execution state (~100KB)
```

**Why snapshots?**
- 6x smaller storage
- Faster to load/save
- No coupling to execution internals

---

## Working with Snapshots

### When to Save Snapshots

```csharp
// After each turn (if you want users to fork from any turn)
await agent.RunAsync(message, thread);

// Save snapshot for potential branching
await store.SaveSnapshotAsync(
    thread.Id,
    Guid.NewGuid().ToString(),
    thread.ToSnapshot(),
    new CheckpointMetadata
    {
        Source = CheckpointSource.User,
        Step = -1,
        MessageIndex = thread.MessageCount,
        BranchName = thread.ActiveBranch
    });
```

### List Available Fork Points

```csharp
var manifest = await store.GetCheckpointManifestAsync(thread.Id);
var snapshots = manifest.Where(e => e.IsSnapshot).ToList();

foreach (var snapshot in snapshots)
{
    Console.WriteLine($"Fork point: Message {snapshot.MessageIndex}, {snapshot.CreatedAt}");
}
```

---

## Branch Management

### Get Checkpoint History

```csharp
var checkpoints = await branching.GetCheckpointsAsync(thread.Id, limit: 10);

foreach (var checkpoint in checkpoints)
{
    Console.WriteLine($"Branch: {checkpoint.BranchName}");
    Console.WriteLine($"  Messages: {checkpoint.MessageIndex}");
    Console.WriteLine($"  Created: {checkpoint.CreatedAt}");
    Console.WriteLine($"  Has Execution State: {checkpoint.State != null}");
}
```

### Get Variants at Message

Find all branches that diverged from a specific message:

```csharp
var variants = await branching.GetVariantsAtMessageAsync(thread.Id, messageIndex: 5);

Console.WriteLine($"Found {variants.Count} variants at message 5:");
foreach (var variant in variants)
{
    Console.WriteLine($"  - {variant.BranchName}");
}
```

### Delete Branch

```csharp
await branching.DeleteBranchAsync(thread.Id, "old-experiment");
```

### Rename Branch

```csharp
await branching.RenameBranchAsync(thread.Id, "feature-x", "feature-final");
```

---

## Events

Branching operations emit events for observability:

```csharp
// Fork event
public record BranchCreatedEvent
{
    public string ThreadId { get; init; }
    public string CheckpointId { get; init; }
    public string BranchName { get; init; }
    public string? ParentCheckpointId { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Copy event
public record ThreadCopiedEvent
{
    public string SourceThreadId { get; init; }
    public string NewThreadId { get; init; }
    public string NewCheckpointId { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Switch event
public record BranchSwitchedEvent
{
    public string ThreadId { get; init; }
    public string FromBranch { get; init; }
    public string ToBranch { get; init; }
    public string ToCheckpointId { get; init; }
    public DateTime SwitchedAt { get; init; }
}
```

---

## Common Patterns

### Pattern 1: Chat UI with Fork Button

```csharp
// User clicks "Fork from here" after message N
app.MapPost("/conversations/{threadId}/fork", async (
    string threadId,
    int messageIndex,
    Branching branching,
    ICheckpointStore store) =>
{
    // Find snapshot at this message
    var manifest = await store.GetCheckpointManifestAsync(threadId);
    var snapshot = manifest
        .Where(e => e.IsSnapshot && e.MessageIndex == messageIndex)
        .OrderByDescending(e => e.CreatedAt)
        .FirstOrDefault();
    
    if (snapshot == null)
        return Results.NotFound("No fork point at this message");
    
    // Fork
    var (forkedThread, evt) = await branching.ForkFromCheckpointAsync(
        threadId,
        snapshot.CheckpointId,
        $"fork-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
    
    return Results.Ok(new { forkedThread.Id, evt.BranchName });
});
```

### Pattern 2: A/B Testing

```csharp
// Try two different approaches
var (branchA, _) = await branching.ForkFromCheckpointAsync(
    thread.Id, checkpointId, "approach-a");

var (branchB, _) = await branching.ForkFromCheckpointAsync(
    thread.Id, checkpointId, "approach-b");

// Run different strategies
await agent.RunAsync(strategyAMessages, branchA);
await agent.RunAsync(strategyBMessages, branchB);

// Compare results
CompareOutcomes(branchA, branchB);
```

### Pattern 3: Rollback and Retry

```csharp
// Conversation went wrong, go back to message 5
var manifest = await store.GetCheckpointManifestAsync(thread.Id);
var rollbackPoint = manifest
    .Where(e => e.IsSnapshot && e.MessageIndex == 5)
    .FirstOrDefault();

var (rolledBack, _) = await branching.ForkFromCheckpointAsync(
    thread.Id,
    rollbackPoint.CheckpointId,
    "retry");

// Try different approach
await agent.RunAsync(differentMessages, rolledBack);
```

---

## Branching + Durable Execution

These features work together independently:

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    // Auto-recovery (runs in background)
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    
    // User-driven forking
    opts.Branching.Enabled = true;
});
```

**What happens:**

```
User conversation flow:

Turn 1: "Hello"
  ├─ DurableExecution: Saves ExecutionCheckpoint (auto)
  └─ You: Save snapshot (manual, for potential fork)

Turn 2: "Tell me more"
  ├─ DurableExecution: Saves ExecutionCheckpoint (auto)
  └─ You: Save snapshot (manual, for potential fork)

User: "Fork from Turn 1"
  └─ Branching: Loads snapshot, creates new branch
      (Uses lightweight snapshot, not heavy checkpoint)

If crash during Turn 2:
  └─ DurableExecution: Resumes from latest checkpoint
      (Uses heavy checkpoint with execution state)
```

---

## Configuration

### BranchingConfig

```csharp
public class BranchingConfig
{
    // Enable/disable branching
    public bool Enabled { get; set; } = false;
    
    // Future: Add branch limits, retention policies, etc.
}
```

### CheckpointingOptions

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.Branching.Enabled = true;  // Enable branching
});
```

---

## Best Practices

### ✅ Do

- **Save snapshots at meaningful points** (after user messages)
- **Use descriptive branch names** ("bug-fix-attempt", not "branch1")
- **Clean up old branches** (delete experiments you don't need)
- **Check IsSnapshot flag** when listing checkpoints

### ❌ Don't

- **Don't fork during execution** (wait for turn to complete)
- **Don't assume all checkpoints are forkable** (only snapshots are)
- **Don't switch branches mid-execution** (you'll lose progress)
- **Don't mix up threadId and checkpointId** (they're different!)

---

## Troubleshooting

### "Snapshot not found when trying to fork"

**Problem:** Trying to fork from a checkpoint that doesn't have a snapshot.

**Solution:** Explicitly save snapshots at fork points:
```csharp
await store.SaveSnapshotAsync(thread.Id, checkpointId, thread.ToSnapshot(), metadata);
```

### "Branch disappeared after restart"

**Problem:** Using in-memory storage.

**Solution:** Use persistent storage:
```csharp
var store = new JsonConversationThreadStore("./checkpoints");
```

### "Can't switch to branch - checkpoint not found"

**Problem:** Branch checkpoint was deleted or doesn't exist.

**Solution:** Check branch exists before switching:
```csharp
var tree = await branching.GetBranchTreeAsync(thread.Id);
if (tree.NamedBranches.ContainsKey("target-branch"))
{
    await branching.SwitchBranchAsync(thread.Id, "target-branch");
}
```

---

## API Reference

### Branching Service

```csharp
public class Branching
{
    // Fork - create new branch
    Task<(ConversationThread Thread, BranchCreatedEvent Event)> 
        ForkFromCheckpointAsync(string threadId, string checkpointId, string? branchName);
    
    // Copy - create new thread
    Task<(ConversationThread Thread, ThreadCopiedEvent Event)> 
        CopyFromCheckpointAsync(string sourceThreadId, string sourceCheckpointId, string? newDisplayName);
    
    // Switch - change active branch
    Task<(ConversationThread Thread, BranchSwitchedEvent Event)?> 
        SwitchBranchAsync(string threadId, string? targetBranchName);
    
    // Query
    Task<BranchTree> GetBranchTreeAsync(string threadId);
    Task<List<CheckpointTuple>> GetCheckpointsAsync(string threadId, int? limit, DateTime? before);
    Task<List<CheckpointTuple>> GetVariantsAtMessageAsync(string threadId, int messageIndex);
    
    // Manage
    Task DeleteBranchAsync(string threadId, string branchName);
    Task RenameBranchAsync(string threadId, string oldName, string newName);
}
```

### Snapshot Storage

```csharp
public interface ICheckpointStore
{
    // Save snapshot for branching
    Task SaveSnapshotAsync(string threadId, string snapshotId, 
        ThreadSnapshot snapshot, CheckpointMetadata metadata);
    
    // Load snapshot
    Task<ThreadSnapshot?> LoadSnapshotAsync(string threadId, string snapshotId);
    
    // Delete snapshots
    Task DeleteSnapshotsAsync(string threadId, IEnumerable<string> snapshotIds);
    
    // Prune old snapshots
    Task PruneSnapshotsAsync(string threadId, int keepCount);
}
```

---

## See Also

- [13. Durable Execution](./13%20Durable%20Execution.md) - Automatic crash recovery
- [Conversation Threads](./08%20Conversation%20Threads.md) - Thread basics
- [Checkpointing Overview](./12%20Checkpointing%20Overview.md) - Storage concepts
