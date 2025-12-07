# 12. Checkpointing Overview

Checkpointing enables your agent to save conversation state and resume after crashes. This document explains the overall checkpointing system and how its two main features work together.

## Two Independent Features

HPD-Agent's checkpointing system provides two separate capabilities:

```
┌─────────────────────────────────────────────────────────────┐
│                   CHECKPOINTING SYSTEM                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────────────────┐        ┌──────────────────────┐   │
│  │ DURABLE EXECUTION  │        │     BRANCHING        │   │
│  │  (Automatic)       │        │    (Manual)          │   │
│  ├────────────────────┤        ├──────────────────────┤   │
│  │ • Crash recovery   │        │ • Fork conversations │   │
│  │ • Auto-checkpoint  │        │ • Try alternatives   │   │
│  │ • Resume state     │        │ • Switch branches    │   │
│  │ • Background       │        │ • Explicit control   │   │
│  └────────────────────┘        └──────────────────────┘   │
│           │                             │                   │
│           └─────────────┬───────────────┘                   │
│                         │                                   │
│                   ICheckpointStore                          │
│                  (Shared Storage)                           │
└─────────────────────────────────────────────────────────────┘
```

### Durable Execution (Infrastructure)

**What it does:** Automatically saves execution state for crash recovery

**When to use:** Production environments, long-running agents

**How it works:** Runs in background, saves checkpoints at configured intervals

**Learn more:** [13. Durable Execution](./13%20Durable%20Execution.md)

### Branching (Feature)

**What it does:** Creates conversation forks and alternatives

**When to use:** Chat UIs, experimentation, A/B testing

**How it works:** Explicit operations (fork, switch, copy)

**Learn more:** [15. Branching](./15%20Branching.md)

---

## Quick Decision Guide

**Choose based on what you need:**

```
┌───────────────────────────────────────────────────────────┐
│ Need crash recovery?                                      │
│ ├─ YES → Enable Durable Execution                        │
│ └─ NO  → Skip it                                          │
├───────────────────────────────────────────────────────────┤
│ Need conversation forking?                                │
│ ├─ YES → Enable Branching                                │
│ └─ NO  → Skip it                                          │
├───────────────────────────────────────────────────────────┤
│ Need both?                                                │
│ └─ Enable both (they work together independently)        │
└───────────────────────────────────────────────────────────┘
```

**Common scenarios:**

| Scenario | Durable Execution | Branching |
|----------|------------------|-----------|
| Production agent, needs recovery | ✅ Yes | ❌ No |
| Chat UI with "fork" button | ❌ No | ✅ Yes |
| Production + exploration features | ✅ Yes | ✅ Yes |
| Simple short conversations | ❌ No | ❌ No |
| Development/testing | ❌ No | ❌ No |

---

## How They Work Together

Both features share the same storage but serve different purposes:

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    // Feature 1: Crash recovery (automatic)
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    
    // Feature 2: Forking (manual)
    opts.Branching.Enabled = true;
});
```

### Example: Both Enabled

```
User conversation:

Turn 1: "Hello"
  ├─ DurableExecution: Saves checkpoint (120KB) for recovery
  └─ You: Save snapshot (20KB) if users might fork here

Turn 2: "Tell me more"  
  ├─ DurableExecution: Saves checkpoint (120KB) for recovery
  └─ You: Save snapshot (20KB) if users might fork here

User clicks "Fork from Turn 1":
  └─ Branching: Loads snapshot (fast!), creates new branch

System crashes during Turn 2:
  └─ DurableExecution: Resumes from checkpoint (has full state)
```

**Key insight:** They use different data formats for different purposes:
- **Checkpoints** (~120KB): Full execution state for recovery
- **Snapshots** (~20KB): Lightweight conversation copy for forking

---

## Storage Concepts

### Checkpoints vs Snapshots

```
ExecutionCheckpoint (~120KB):
├─ ThreadId
├─ Messages
├─ Metadata  
├─ Branch info
└─ ExecutionState ← Recovery data
   ├─ Iteration
   ├─ Middleware state
   ├─ Pending operations
   └─ ~100KB of runtime state

ThreadSnapshot (~20KB):
├─ ThreadId
├─ Messages
├─ Metadata
├─ Branch info
└─ ❌ NO ExecutionState (6x smaller!)
```

**Why two formats?**

- **Branching doesn't need execution state** → Use snapshots (fast, small)
- **Recovery needs execution state** → Use checkpoints (slower, complete)

### File Organization

```
checkpoints/
└── threads/
    └── thread-123/
        ├── manifest.json           ← Index of all saves
        ├── ckpt-1.json            ← ExecutionCheckpoint (recovery)
        ├── ckpt-2.json            ← ExecutionCheckpoint (recovery)
        ├── snap-1.snapshot.json   ← ThreadSnapshot (branching)
        └── snap-2.snapshot.json   ← ThreadSnapshot (branching)
```

The manifest tracks which is which:
```json
{
  "Checkpoints": [
    {
      "CheckpointId": "ckpt-1",
      "IsSnapshot": false,  // ← Full checkpoint
      "Source": "Loop",
      "CreatedAt": "..."
    },
    {
      "CheckpointId": "snap-1", 
      "IsSnapshot": true,   // ← Lightweight snapshot
      "Source": "Fork",
      "CreatedAt": "..."
    }
  ]
}
```

---

## Setup

### Minimal Setup (Both Features)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.DurableExecution.Enabled = true;
    opts.Branching.Enabled = true;
});
```

### Just Durable Execution

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.DurableExecution.Enabled = true;
    // Branching defaults to false
});
```

### Just Branching

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.Branching.Enabled = true;
    // DurableExecution defaults to false
});
```

### In-Memory (Development)

```csharp
builder.Services.AddInMemoryCheckpointing();
// Both features available but data lost on restart
```

---

## Storage Implementations

### JsonConversationThreadStore (Production)

File-based storage with full history:

```csharp
var store = new JsonConversationThreadStore("./checkpoints");
```

**Features:**
- ✅ Persistent (survives restarts)
- ✅ Full checkpoint history
- ✅ Both checkpoints and snapshots
- ✅ File locking for thread safety

**Storage:** Each checkpoint/snapshot is a separate JSON file

### InMemoryConversationThreadStore (Development)

In-memory storage for testing:

```csharp
var store = new InMemoryConversationThreadStore(
    CheckpointRetentionMode.FullHistory);
```

**Features:**
- ✅ Fast
- ✅ Thread-safe (ConcurrentDictionary)
- ✅ Both checkpoints and snapshots
- ❌ Lost on restart

---

## Shared Interface

Both features use the same storage interface:

```csharp
public interface ICheckpointStore
{
    // Basic thread operations
    Task SaveThreadAsync(ConversationThread thread);
    Task<ConversationThread?> LoadThreadAsync(string threadId);
    
    // Checkpoint operations (DurableExecution)
    Task SaveThreadAtCheckpointAsync(ConversationThread thread, 
        string checkpointId, CheckpointMetadata metadata);
    Task<ConversationThread?> LoadThreadAtCheckpointAsync(
        string threadId, string checkpointId);
    
    // Snapshot operations (Branching)
    Task SaveSnapshotAsync(string threadId, string snapshotId,
        ThreadSnapshot snapshot, CheckpointMetadata metadata);
    Task<ThreadSnapshot?> LoadSnapshotAsync(
        string threadId, string snapshotId);
    
    // Query
    Task<List<CheckpointManifestEntry>> GetCheckpointManifestAsync(
        string threadId, int? limit, DateTime? before);
    
    // Cleanup
    Task DeleteSnapshotsAsync(string threadId, 
        IEnumerable<string> snapshotIds);
    Task PruneSnapshotsAsync(string threadId, int keepCount);
}
```

---

## Architecture Principles

### 1. Feature Independence

```
DurableExecution:
└─ Doesn't know about branches
   (just saves whatever thread you give it)

Branching:
└─ Doesn't know about execution state
   (just loads conversation data)
```

### 2. Shared Storage, Different Data

```
Same ICheckpointStore:
├─ DurableExecution → Saves ExecutionCheckpoint
└─ Branching → Saves/loads ThreadSnapshot
```

### 3. Separation of Concerns

```
High-level (Feature):
├─ Branching: "User wants to fork" → Uses snapshots
└─ DurableExecution: "Agent needs recovery" → Uses checkpoints

Low-level (Infrastructure):
└─ ICheckpointStore: "Save this data, load that data" (dumb CRUD)
```

---

## When to Use What

### Use Durable Execution When:

- ✅ Running in production
- ✅ Long conversations (>5 minutes)
- ✅ Multi-tool workflows
- ✅ Expensive operations (don't want to redo)
- ✅ Need crash recovery

### Use Branching When:

- ✅ Building chat UI
- ✅ Users want "fork from here"
- ✅ Exploring alternatives
- ✅ A/B testing
- ✅ Rollback capability

### Use Both When:

- ✅ Production agent + exploration features
- ✅ Need both recovery AND forking
- ✅ Complex workflows with user experimentation

### Use Neither When:

- ✅ Simple scripts
- ✅ Short conversations (<5 messages)
- ✅ Development/testing only
- ✅ No persistence needed

---

## Common Patterns

### Pattern 1: Production Agent (Recovery Only)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    opts.DurableExecution.Retention = RetentionPolicy.LatestOnly;
});

// Agent runs normally, auto-checkpoints in background
await agent.RunAsync(messages, thread);
```

### Pattern 2: Chat UI (Forking Only)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    opts.Branching.Enabled = true;
});

// User clicks "Fork from here"
app.MapPost("/fork", async (Branching branching, ForkRequest req) =>
{
    var (forked, evt) = await branching.ForkFromCheckpointAsync(
        req.ThreadId, req.CheckpointId, req.BranchName);
    return Results.Ok(forked);
});
```

### Pattern 3: Production + Exploration (Both)

```csharp
builder.Services.AddCheckpointing(opts =>
{
    opts.StoragePath = "./checkpoints";
    
    // Auto-recovery
    opts.DurableExecution.Enabled = true;
    opts.DurableExecution.Frequency = CheckpointFrequency.PerTurn;
    
    // User forking
    opts.Branching.Enabled = true;
});

// Works seamlessly - each feature does its job
```

---

## Best Practices

### ✅ Do

- **Enable what you need** (don't enable both if you only need one)
- **Use persistent storage in production** (JsonConversationThreadStore)
- **Save snapshots at meaningful points** (after user messages)
- **Test recovery scenarios** (simulate crashes)

### ❌ Don't

- **Don't mix up checkpoints and snapshots** (different purposes!)
- **Don't manually manage both** (let DurableExecution handle recovery)
- **Don't use in-memory storage in production** (data loss!)
- **Don't save snapshots unnecessarily** (storage overhead)

---

## Troubleshooting

### "Which feature do I need?"

**Ask yourself:**
- Need to recover from crashes? → Durable Execution
- Need to fork conversations? → Branching  
- Need both? → Enable both

### "Can I use both features?"

**Yes!** They're designed to work together independently.

### "Performance concerns?"

**Durable Execution:**
- Uses fire-and-forget (no latency)
- Configurable frequency (PerTurn is balanced)

**Branching:**
- Snapshots are 6x smaller than checkpoints
- Load/fork operations are fast

### "Storage growing too large?"

**Solutions:**
- Use `RetentionPolicy.LatestOnly` for DurableExecution
- Prune old snapshots with `PruneSnapshotsAsync()`
- Delete unused branches

---

## Next Steps

- **[13. Durable Execution](./13%20Durable%20Execution.md)** - Learn about automatic crash recovery
- **[15. Branching](./15%20Branching.md)** - Learn about conversation forking
- **[Conversation Threads](./08%20Conversation%20Threads.md)** - Understand thread basics

---

## API Quick Reference

```csharp
// Setup
builder.Services.AddCheckpointing(opts => { ... });

// Durable Execution (automatic)
builder.Services.GetRequiredService<DurableExecution>();

// Branching (manual)
builder.Services.GetRequiredService<Branching>();

// Storage
builder.Services.GetRequiredService<ICheckpointStore>();
```
