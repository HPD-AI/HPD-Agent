# 13. Durable Execution

Durable Execution provides automatic checkpointing and crash recovery for your agent conversations. If your agent crashes mid-conversation, it can resume from the last checkpoint instead of losing all progress.

## What is Durable Execution?

Durable Execution is about **producing checkpoints automatically**. It handles:

- **Auto-checkpointing** - Saves state at configurable intervals (per turn, per iteration, or manually)
- **Retention policies** - Controls how many checkpoints to keep
- **Crash recovery** - Resume from the latest checkpoint after a crash
- **Pending writes** - Recover partial progress from interrupted operations

> **Important**: Durable Execution is separate from [Branching](./14%20Branching.md). Durable Execution *creates* checkpoints; Branching *operates on* checkpoints (fork, switch, delete). They're independent services that share the same checkpoint store.

## When to Use Durable Execution

| Scenario | Recommendation |
|----------|----------------|
| Short conversations (< 5 messages) | Optional |
| Long-running agents (> 10 iterations) | Recommended |
| Production environments | Strongly recommended |
| Multi-tool workflows | Recommended with pending writes |
| Debugging/development | Use with FullHistory retention |

## Quick Setup

### Using DI (Recommended)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
});
```

### Using AgentBuilder (Fluent API)

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4")
    .WithCheckpointStore(new JsonCheckpointStore("./checkpoints"))
    .WithDurableExecution(CheckpointFrequency.PerTurn, RetentionPolicy.LatestOnly)
    .Build();
```

### In-Memory (Development Only)

```csharp
builder.Services.AddInMemoryCheckpointing();
```

## Checkpoint Frequencies

Choose when checkpoints are created:

### PerTurn (Default, Recommended)

Checkpoints after each complete message turn (user message → agent response).

```csharp
opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
```

**Best for**: Most use cases. Balances durability with performance.

### PerIteration

Checkpoints after every agent iteration (each tool call cycle).

```csharp
opts.DurableExecution.Frequency = CheckpointFrequency.PerIteration;
```

**Best for**: Long-running agents with many tool calls (>10 iterations). Higher overhead but maximum durability.

### Manual

Only checkpoint when you explicitly call `SaveCheckpointAsync()`.

```csharp
opts.DurableExecution.Frequency = CheckpointFrequency.Manual;

// Later, manually save
await durableExecutionService.SaveCheckpointAsync(thread, state);
```

**Best for**: Full control over when state is persisted.

## Retention Policies

Control how many checkpoints to keep:

### LatestOnly (Default)

Keep only the most recent checkpoint. Minimizes storage.

```csharp
opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
```

**Storage**: Minimal (1 checkpoint per thread)  
**Best for**: Simple crash recovery, no need for history

### FullHistory

Keep all checkpoints. Enables time-travel debugging and branching.

```csharp
opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
```

**Storage**: Grows with conversation length  
**Best for**: Development, debugging, when using branching

### LastN

Keep the last N checkpoints.

```csharp
opts.DurableExecution.Retention = RetentionPolicy.LastN(10);
```

**Storage**: Bounded to N checkpoints  
**Best for**: Balance between history and storage

### TimeBased

Keep checkpoints from the last specified duration.

```csharp
opts.DurableExecution.Retention = RetentionPolicy.TimeBased(TimeSpan.FromDays(30));
```

**Storage**: Varies with time window  
**Best for**: Compliance requirements (e.g., audit logs)

## Crash Recovery

### Automatic Resume

Resume a crashed conversation from its latest checkpoint:

```csharp
var durableExecution = serviceProvider.GetRequiredService<DurableExecutionService>();

// Resume returns the thread at the last checkpoint
var thread = await durableExecution.ResumeFromLatestAsync(threadId);

if (thread != null)
{
    Console.WriteLine($"Resumed from checkpoint at message {thread.Messages.Count}");
    // Continue the conversation
}
else
{
    Console.WriteLine("No checkpoint found, starting fresh");
}
```

### Checking if Checkpoints Exist

```csharp
var store = serviceProvider.GetRequiredService<ICheckpointStore>();
var checkpoints = await store.GetCheckpointsAsync(threadId);

if (checkpoints.Any())
{
    Console.WriteLine($"Found {checkpoints.Count} checkpoints");
}
```

## Pending Writes (Advanced)

Pending writes provide partial failure recovery. When multiple tools execute in sequence, successful results are saved before the full checkpoint.

### The Problem

```
Tool A succeeds → result saved in memory
Tool B succeeds → result saved in memory
Tool C crashes  → 💥 Lost A and B results!
```

### The Solution

With pending writes enabled:

```
Tool A succeeds → SavePendingWriteAsync()  ✓
Tool B succeeds → SavePendingWriteAsync()  ✓
Tool C crashes  → 💥
Resume          → LoadPendingWritesAsync() → Restore A & B results
Checkpoint      → DeletePendingWritesAsync() → Cleanup
```

### Enabling Pending Writes

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.EnablePendingWrites = true;
});
```

### Using Pending Writes

```csharp
var durableExecution = serviceProvider.GetRequiredService<DurableExecutionService>();

// Save individual tool results as pending writes (fire-and-forget)
durableExecution.SavePendingWriteFireAndForget(threadId, toolCallId, toolResult);

// Later, load pending writes during recovery
var pendingWrites = await durableExecution.LoadPendingWritesAsync(threadId);

foreach (var write in pendingWrites)
{
    Console.WriteLine($"Recovered: {write.ToolCallId} = {write.Result}");
}

// Cleanup after successful checkpoint
await durableExecution.DeletePendingWritesAsync(threadId);
```

## Configuration Reference

### DurableExecutionConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Enable durable execution |
| `Frequency` | `CheckpointFrequency` | `PerTurn` | When to checkpoint |
| `Retention` | `RetentionPolicy` | `LatestOnly` | How many checkpoints to keep |
| `EnablePendingWrites` | `bool` | `false` | Enable partial failure recovery |

### CheckpointingOptions

```csharp
builder.Services.AddCheckpointing(opts =>
{
    // Storage
    opts.StoragePath = "./checkpoints";  // File system path
    
    // Durable Execution
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
    opts.DurableExecution.EnablePendingWrites = true;
    
    // Branching (separate feature)
    opts.Branching.Enabled = true;
});
```

## Common Patterns

### Production Setup

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./data/checkpoints";
    
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.LastN(5);
    opts.DurableExecution.EnablePendingWrites = true;
});
```

### Development Setup

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./dev-checkpoints";
    
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerIteration;
    opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
    
    // Enable branching for debugging
    opts.Branching.Enabled = true;
});
```

### High-Reliability Setup

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerIteration;
    opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
    opts.DurableExecution.EnablePendingWrites = true;
});
```

## Best Practices

### 1. Choose the Right Frequency

- Use `PerTurn` for most applications (good balance)
- Use `PerIteration` for long-running agents or critical workflows
- Use `Manual` only when you need precise control

### 2. Balance Retention vs Storage

- `LatestOnly` is sufficient for pure crash recovery
- Use `FullHistory` during development or when using branching
- Use `LastN` in production to bound storage growth

### 3. Enable Pending Writes for Multi-Tool Agents

If your agent calls multiple tools in sequence, enable pending writes to avoid re-executing successful operations after a crash.

### 4. Validate Configuration at Startup

```csharp
// In Program.cs
ValidateCheckpointingConfiguration(app.Services);

void ValidateCheckpointingConfiguration(IServiceProvider services)
{
    var durableExecution = services.GetService<DurableExecutionService>();
    if (durableExecution?.IsEnabled == true)
    {
        var store = services.GetService<ICheckpointStore>();
        if (store == null)
        {
            throw new InvalidOperationException(
                "DurableExecution is enabled but no ICheckpointStore is registered");
        }
    }
}
```

### 5. Use Branching with FullHistory

If you want to use branching features, use `FullHistory` retention so checkpoints are available to fork from:

```csharp
opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
opts.Branching.Enabled = true;
```

## Durable Execution vs Branching

| Feature | Durable Execution | Branching |
|---------|-------------------|-----------|
| **Purpose** | Create checkpoints | Operate on checkpoints |
| **Focus** | Crash recovery, state persistence | Forking, switching, exploration |
| **Creates checkpoints?** | Yes | No |
| **Requires checkpoints?** | No | Yes |
| **Typical retention** | LatestOnly or LastN | FullHistory |

They work together: Durable Execution produces the checkpoints that Branching consumes.

## API Reference

### DurableExecutionService

```csharp
public class DurableExecutionService
{
    // Properties
    bool IsEnabled { get; }
    CheckpointFrequency Frequency { get; }
    RetentionPolicy Retention { get; }
    
    // Core Methods
    Task SaveCheckpointAsync(ConversationThread thread, AgentLoopState state, ...);
    bool ShouldCheckpoint(int iteration, bool turnComplete);
    Task<ConversationThread?> ResumeFromLatestAsync(string threadId, ...);
    
    // Pending Writes (when EnablePendingWrites = true)
    void SavePendingWriteFireAndForget(string threadId, string toolCallId, object result);
    Task<IReadOnlyList<PendingWrite>> LoadPendingWritesAsync(string threadId, ...);
    Task DeletePendingWritesAsync(string threadId, ...);
}
```

### ShouldCheckpoint Logic

```csharp
Frequency switch
{
    PerIteration => true,           // Always checkpoint
    PerTurn      => turnComplete,   // Only when turn completes
    Manual       => false           // Never auto-checkpoint
}
```

## Troubleshooting

### Checkpoints Not Being Created

1. Verify `Enabled = true`
2. Check the storage path exists and is writable
3. Ensure frequency matches your expectation (PerTurn requires turn completion)

### Storage Growing Too Fast

1. Switch from `FullHistory` to `LastN` or `LatestOnly`
2. Reduce checkpoint frequency if using `PerIteration`

### Resume Not Finding Checkpoints

1. Verify the thread ID is correct
2. Check retention policy hasn't pruned the checkpoints
3. Ensure storage path is the same as when checkpoints were created

## See Also

- [14 Branching](./14%20Branching.md) - Fork and switch between conversation branches
- [ICheckpointStore](./14%20Branching.md#storage-backends) - Storage backend options
