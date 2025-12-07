# 13. Durable Execution

Durable Execution provides automatic crash recovery for your agents. When enabled, your agent's execution state is periodically saved, allowing seamless recovery from crashes, timeouts, or system failures.

## What is Durable Execution?

Durable Execution is **automatic, background checkpointing** that:

- **Saves execution state** - Captures agent progress including messages, tool calls, middleware state
- **Resumes on restart** - Detects incomplete conversations and continues where it left off
- **Works transparently** - No code changes needed in your agent logic
- **Fire-and-forget** - Doesn't block agent execution

> **Note**: Durable Execution is separate from [Branching](./15%20Branching.md). Durable Execution provides crash recovery; Branching creates conversation variants. They work together but are independent features.

## When to Use Durable Execution

| Scenario | Use Durable Execution? |
|----------|------------------------|
| Production agents | ✅ Yes |
| Long-running conversations (>5 min) | ✅ Yes |
| Multi-tool workflows | ✅ Yes |
| Expensive operations you don't want to redo | ✅ Yes |
| Short scripts (<1 min) | ❌ No |
| Simple testing | ❌ No |
| Fully stateless agents | ❌ No |

---

## Quick Setup

### Using DI (Recommended)

```csharp
using HPD_Agent.Checkpointing.Services;

var builder = WebApplication.CreateBuilder(args);

// Enable durable execution
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
});

// Inject DurableExecution where needed
public class AgentService
{
    private readonly DurableExecution _durable;
    
    public AgentService(DurableExecution durable)
    {
        _durable = durable;
    }
}
```

### Manual Setup

```csharp
var store = new JsonConversationThreadStore("./checkpoints");
var durable = new DurableExecution(store, new DurableExecutionConfig
{
    Enabled = true,
    Frequency = CheckpointFrequency.PerTurn,
    Retention = RetentionPolicy.LatestOnly
});
```

---

## How It Works

### The Flow

```
1. Agent starts conversation
   └─ DurableExecution: Checks for incomplete checkpoint
      ├─ Found? → Resume from saved state
      └─ None? → Start fresh

2. Agent processes turn
   ├─ User message
   ├─ Tool calls
   ├─ LLM responses
   └─ DurableExecution: Save checkpoint (background)
      └─ Fire-and-forget (doesn't block)

3. System crash! 💥

4. Agent restarts
   └─ DurableExecution: Detects incomplete checkpoint
      └─ Resumes from last saved state
         ├─ Restores messages
         ├─ Restores middleware state
         ├─ Restores iteration
         └─ Continues execution
```

### What Gets Saved

```
ExecutionCheckpoint (~120KB):
├─ Thread metadata
│  ├─ ThreadId
│  ├─ DisplayName
│  ├─ CreatedAt
│  └─ ActiveBranch
│
├─ Conversation history
│  ├─ Messages (user, assistant, tool)
│  ├─ Message metadata
│  └─ Turn boundaries
│
└─ Execution state (~100KB)
   ├─ Iteration count
   ├─ Middleware state
   ├─ Pending operations
   ├─ Tool call results
   └─ Internal loop state
```

---

## Configuration

### CheckpointFrequency

Controls when checkpoints are saved:

```csharp
public enum CheckpointFrequency
{
    PerTurn,      // After each user-assistant exchange (recommended)
    PerIteration, // After each agent loop iteration
    Manual        // Only when you call SaveCheckpointAsync()
}
```

**Recommendations:**

| Use Case | Frequency | Why |
|----------|-----------|-----|
| Most agents | `PerTurn` | Balanced - recovers to turn boundary |
| High-frequency tools | `PerIteration` | Fine-grained recovery (more storage) |
| Custom logic | `Manual` | You control when to checkpoint |

### RetentionPolicy

Controls how many checkpoints to keep:

```csharp
public enum RetentionPolicy
{
    LatestOnly,  // Keep only most recent (for recovery)
    FullHistory  // Keep all checkpoints (for tracing)
}
```

**Recommendations:**

| Use Case | Retention | Why |
|----------|-----------|-----|
| Production recovery | `LatestOnly` | Minimal storage overhead |
| Debugging/tracing | `FullHistory` | See full execution history |
| Compliance/audit | `FullHistory` | Keep complete records |

### DurableExecutionConfig

```csharp
public class DurableExecutionConfig
{
    // Enable/disable durable execution
    public bool Enabled { get; set; } = false;
    
    // When to save checkpoints
    public CheckpointFrequency Frequency { get; set; } = CheckpointFrequency.PerTurn;
    
    // How many to keep
    public RetentionPolicy Retention { get; set; } = RetentionPolicy.LatestOnly;
    
    // Timeout for detecting incomplete checkpoints (seconds)
    public int CheckpointTimeoutSeconds { get; set; } = 300; // 5 minutes
}
```

---

## Usage

### Basic Usage (Automatic)

Once configured, DurableExecution works automatically:

```csharp
// Just run your agent normally
var agent = builder.Build();
await agent.RunAsync(messages, thread);

// Checkpointing happens automatically in background
// If crash occurs, next run resumes from last checkpoint
```

### Manual Checkpointing

If you need fine control:

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.Manual;
});

// Inject DurableExecution
public class MyAgent
{
    private readonly DurableExecution _durable;
    
    public MyAgent(DurableExecution durable)
    {
        _durable = durable;
    }
    
    public async Task RunAsync(ConversationThread thread)
    {
        // ... do work ...
        
        // Checkpoint at strategic points
        await _durable.SaveCheckpointAsync(thread, 
            CheckpointSource.Application, 
            step: 1);
        
        // ... more work ...
        
        await _durable.SaveCheckpointAsync(thread, 
            CheckpointSource.Application, 
            step: 2);
    }
}
```

### Resume After Crash

```csharp
// On agent startup, check for incomplete conversations
var threads = await store.GetAllThreadsAsync();

foreach (var thread in threads)
{
    // DurableExecution automatically detects incomplete checkpoints
    var checkpoint = await store.GetLatestCheckpointAsync(thread.Id);
    
    if (checkpoint != null && checkpoint.Metadata.IsIncomplete)
    {
        Console.WriteLine($"Resuming thread {thread.Id}...");
        
        // Load and continue
        var restored = await store.LoadThreadAtCheckpointAsync(
            thread.Id, checkpoint.CheckpointId);
        
        await agent.RunAsync(pendingMessages, restored);
    }
}
```

---

## Branch-Agnostic Design

**Important concept:** DurableExecution doesn't care about branches!

### How It Works

```csharp
// When you save a checkpoint, DurableExecution just saves the thread object
await durableExecution.SaveCheckpointAsync(thread, CheckpointSource.Loop, step: 5);

// The thread object contains branch info as a property:
thread.ActiveBranch // e.g., "main", "feature-x", etc.

// On restore:
var restored = await store.LoadThreadAtCheckpointAsync(threadId, checkpointId);
Console.WriteLine(restored.ActiveBranch); // Still has branch info!
```

**What this means:**

```
DurableExecution doesn't check:
├─ "What branch is this?"
├─ "Should I save this branch?"
└─ "Should I restore this branch?"

It just:
├─ Saves whatever thread object you give it
└─ Restores complete thread object (including branch)
```

### Why Branch-Agnostic?

**Separation of concerns:**

```
High-level feature (BranchingService):
├─ Manages forks, switches, copies
├─ Decides when to create branches
└─ Branch-aware logic

Low-level infrastructure (DurableExecution):
├─ Saves execution state
├─ Detects crashes
├─ Resumes conversations
└─ Branch-agnostic (just serializes thread object)
```

**Example:**

```csharp
// You fork a conversation
var (forkedThread, _) = await branching.ForkFromCheckpointAsync(
    threadId, checkpointId, "experiment");

// forkedThread.ActiveBranch == "experiment"

// You run agent on this branch
await agent.RunAsync(messages, forkedThread);

// DurableExecution saves checkpoint
// ├─ Doesn't care it's on "experiment" branch
// └─ Just saves the thread (which happens to have ActiveBranch="experiment")

// Crash! 💥

// On restart, DurableExecution resumes
var restored = await store.LoadThreadAtCheckpointAsync(
    forkedThread.Id, latestCheckpointId);

// restored.ActiveBranch == "experiment" (preserved as part of thread)
```

---

## Recovery Scenarios

### Scenario 1: Mid-Turn Crash

```csharp
// Turn started
User: "Search for flights to Paris"

// Agent thinking...
  ├─ Tool call: search_flights("Paris")
  ├─ Checkpoint saved ✅
  ├─ System crashes! 💥

// On restart:
  └─ Resumes from checkpoint
     ├─ Knows search_flights was called
     ├─ Has tool result
     └─ Continues with LLM response
```

### Scenario 2: Between Turns

```csharp
Turn 1 complete: "Here are flights to Paris"
  └─ Checkpoint saved ✅

System crashes! 💥

// On restart:
  └─ Resumes from checkpoint
     ├─ Turn 1 complete
     └─ Ready for Turn 2
```

### Scenario 3: Long Tool Execution

```csharp
User: "Analyze this 1GB file"

Agent:
  ├─ Starts file analysis
  ├─ Checkpoint saved (before tool) ✅
  ├─ Tool runs for 10 minutes...
  ├─ System crashes during tool! 💥

// On restart:
  └─ Resumes from checkpoint
     ├─ Knows analysis started
     ├─ Re-executes tool (idempotent!)
     └─ Continues when done
```

---

## Performance

### Storage Overhead

```
Per checkpoint: ~120KB

LatestOnly mode:
├─ 1 thread, 10 turns → ~1.2MB total
└─ Overwrites old checkpoints

FullHistory mode:
├─ 1 thread, 100 turns → ~12MB total
└─ Keeps all checkpoints (grows over time)
```

### Runtime Overhead

**Checkpoint save:**
- Async fire-and-forget (non-blocking)
- ~1-5ms serialization time
- Runs in background thread

**Checkpoint load:**
- ~5-10ms deserialization
- Only on startup (after crash)

**Recommendation:** Use `PerTurn` frequency with `LatestOnly` retention for optimal balance.

---

## Events

DurableExecution emits events for observability:

```csharp
// Checkpoint saved
public record CheckpointSavedEvent
{
    public string ThreadId { get; init; }
    public string CheckpointId { get; init; }
    public CheckpointSource Source { get; init; }
    public int Step { get; init; }
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Checkpoint restored
public record CheckpointRestoredEvent
{
    public string ThreadId { get; init; }
    public string CheckpointId { get; init; }
    public int MessageCount { get; init; }
    public int Iteration { get; init; }
    public DateTime RestoredAt { get; init; }
}
```

---

## Common Patterns

### Pattern 1: Simple Recovery

```csharp
// Setup
builder.Services.AddCheckpointing(opts =>
{
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
});

// Use
await agent.RunAsync(messages, thread);
// That's it! Automatic recovery on crash
```

### Pattern 2: Debug with Full History

```csharp
// Setup for debugging
builder.Services.AddCheckpointing(opts =>
{
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerIteration;
    opts.DurableExecution.Retention = RetentionPolicy.FullHistory;
});

// Later: Analyze execution history
var manifest = await store.GetCheckpointManifestAsync(thread.Id);

Console.WriteLine($"Found {manifest.Count} checkpoints:");
foreach (var entry in manifest)
{
    Console.WriteLine($"  Step {entry.Step}: {entry.CreatedAt}");
}
```

### Pattern 3: Custom Checkpointing

```csharp
// Setup manual mode
builder.Services.AddCheckpointing(opts =>
{
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.Manual;
});

// Checkpoint at strategic points
await ProcessUserInput(thread);
await durable.SaveCheckpointAsync(thread, CheckpointSource.Application, step: 1);

await CallExpensiveAPI(thread);
await durable.SaveCheckpointAsync(thread, CheckpointSource.Application, step: 2);

await GenerateResponse(thread);
await durable.SaveCheckpointAsync(thread, CheckpointSource.Application, step: 3);
```

---

## Durable Execution + Branching

These features work independently but complement each other:

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    // Feature 1: Automatic recovery (DurableExecution)
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    
    // Feature 2: Manual forking (Branching)
    opts.Branching.Enabled = true;
});
```

**How they coexist:**

```
Main conversation:
├─ Turn 1: "Hello"
│  └─ DurableExecution: Saves ExecutionCheckpoint (~120KB)
│
├─ Turn 2: "Tell me more"
│  └─ DurableExecution: Saves ExecutionCheckpoint (~120KB)
│
└─ User: "Fork from Turn 1"
   └─ Branching: Loads ThreadSnapshot (~20KB)
      └─ Creates new branch
         └─ DurableExecution: Now tracks new branch's checkpoints
```

**Key points:**
- **DurableExecution**: Saves heavy checkpoints with execution state
- **Branching**: Uses light snapshots without execution state
- **Both**: Share same storage but different data formats
- **Independent**: Branching doesn't affect recovery, recovery doesn't affect branching

---

## Best Practices

### ✅ Do

- **Use `PerTurn` frequency** for most agents (balanced)
- **Use `LatestOnly` retention** in production (saves storage)
- **Use `FullHistory` for debugging** (see execution timeline)
- **Test recovery scenarios** (simulate crashes)
- **Monitor checkpoint sizes** (tune retention as needed)

### ❌ Don't

- **Don't use `PerIteration` unless needed** (too frequent)
- **Don't disable in production** (lose crash recovery)
- **Don't rely on in-memory storage** (lost on restart)
- **Don't checkpoint in tight loops** (performance impact)
- **Don't ignore checkpoint errors** (log and alert)

---

## Troubleshooting

### "Agent doesn't resume after crash"

**Check:**
1. Durable execution enabled?
2. Using persistent storage (not in-memory)?
3. Checkpoint timeout reasonable?

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.DurableExecution.Enabled = true; // ← Must be true
    opts.StoragePath = "./checkpoints";   // ← Must be persistent
    opts.DurableExecution.CheckpointTimeoutSeconds = 300; // ← Adjust if needed
});
```

### "Checkpoint files growing too large"

**Solution:** Use `LatestOnly` retention:
```csharp
opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
```

### "Recovery takes too long"

**Problem:** Too many messages or large execution state.

**Solutions:**
- Archive old conversations
- Prune old checkpoints
- Consider shorter conversations

### "Lost branch info after recovery"

**Not a bug!** Branch info is preserved in the thread object:
```csharp
var restored = await store.LoadThreadAtCheckpointAsync(threadId, checkpointId);
Console.WriteLine(restored.ActiveBranch); // Preserved!
```

---

## API Reference

### DurableExecution Service

```csharp
public class DurableExecution
{
    // Save checkpoint manually
    Task SaveCheckpointAsync(ConversationThread thread, 
        CheckpointSource source, int step = -1);
    
    // Load latest checkpoint
    Task<ConversationThread?> LoadLatestCheckpointAsync(string threadId);
    
    // Get checkpoint history
    Task<List<CheckpointMetadata>> GetCheckpointHistoryAsync(
        string threadId, int? limit = null, DateTime? before = null);
    
    // Prune old checkpoints
    Task PruneCheckpointsAsync(string threadId, int keepCount);
}
```

### CheckpointSource

```csharp
public enum CheckpointSource
{
    Loop,         // Automatic from agent loop
    User,         // User-requested save
    Fork,         // Branching operation
    Application,  // Application-level save
    Manual        // Explicit manual save
}
```

### CheckpointMetadata

```csharp
public record CheckpointMetadata
{
    public string CheckpointId { get; init; }
    public CheckpointSource Source { get; init; }
    public int Step { get; init; }
    public int MessageIndex { get; init; }
    public string? BranchName { get; init; }
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
    public bool IsIncomplete { get; init; }
}
```

---

## See Also

- [12. Checkpointing Overview](./12%20Checkpointing%20Overview.md) - High-level system overview
- [15. Branching](./15%20Branching.md) - Conversation forking
- [Conversation Threads](./08%20Conversation%20Threads.md) - Thread basics
