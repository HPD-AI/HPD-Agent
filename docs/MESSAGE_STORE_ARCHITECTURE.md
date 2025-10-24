# Message Store Architecture (v1.0)

**Status:** ✅ Implemented
**Version:** 1.0
**Date:** 2025-01-24
**Breaking Changes:** Yes (v0 → v1.0)

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture Design](#architecture-design)
3. [Key Concepts](#key-concepts)
4. [Getting Started](#getting-started)
5. [API Reference](#api-reference)
6. [Creating Custom Stores](#creating-custom-stores)
7. [Migration Guide](#migration-guide)
8. [Advanced Topics](#advanced-topics)

---

## Overview

The message store architecture provides a flexible, extensible system for storing conversation messages with built-in support for:

- **Cache-aware history reduction** - Avoids redundant summarization work
- **Token counting** - Tracks provider-accurate and estimated token usage
- **Pluggable storage backends** - In-memory (default), database, Redis, etc.
- **Async-first design** - Proper async/await for I/O operations
- **Microsoft compatibility** - Inherits from `Microsoft.Agents.AI.ChatMessageStore`

### Design Goals

1. ✅ **Separation of concerns** - Storage logic separate from thread management
2. ✅ **Extensibility** - Easy to add new storage backends
3. ✅ **Code reuse** - Cache-aware and token logic shared across all stores
4. ✅ **Standards compliance** - Compatible with Microsoft.Agents.AI patterns
5. ✅ **Performance** - Efficient in-memory default, scalable database option

---

## Architecture Design

### Template Method Pattern

The architecture uses the **Template Method** design pattern:

```
┌─────────────────────────────────────────────────────────┐
│ ConversationMessageStore (Abstract Base)               │
├─────────────────────────────────────────────────────────┤
│ Abstract Methods (Override These):                     │
│  - LoadMessagesAsync()      ← Storage-specific         │
│  - SaveMessagesAsync()      ← Storage-specific         │
│  - AppendMessageAsync()     ← Storage-specific         │
│  - ClearAsync()             ← Storage-specific         │
│  - Serialize()              ← Storage-specific         │
│                                                         │
│ Shared Logic (Inherited FREE):                         │
│  - ApplyReductionAsync()    ← Cache-aware reduction    │
│  - HasSummaryAsync()        ← Summary detection        │
│  - GetTokenCountAsync()     ← Token counting           │
│  - ... 10+ more methods                                │
└─────────────────────────────────────────────────────────┘
                        ▲
                        │ inherits
        ┌───────────────┴───────────────┐
        │                               │
┌───────▼─────────┐         ┌──────────▼───────────┐
│ InMemory Store  │         │ Database Store       │
│ (Built-in)      │         │ (User-created)       │
├─────────────────┤         ├──────────────────────┤
│ List<Message>   │         │ IDbConnection        │
│ Fast, no I/O    │         │ Async SQL queries    │
└─────────────────┘         └──────────────────────┘
```

### Class Hierarchy

```csharp
Microsoft.Agents.AI.ChatMessageStore (Microsoft's abstract base)
    ↑
    │ inherits
    │
ConversationMessageStore (Your abstract base - adds cache-aware logic)
    ↑
    │ inherits
    ├─ InMemoryConversationMessageStore (default implementation)
    └─ DatabaseConversationMessageStore (example custom implementation)
```

---

## Key Concepts

### 1. Storage Abstraction

The store separates **what to do** (cache-aware reduction logic) from **where to store** (in-memory, database, etc.).

**What to do** (shared logic in base class):
- Detect if summary exists
- Count messages after last summary
- Apply reduction (remove old, insert summary)
- Calculate token counts

**Where to store** (implemented by derived classes):
- Load messages from storage
- Save messages to storage
- Append individual messages
- Clear all messages

### 2. Cache-Aware Reduction

The store implements your existing cache-aware reduction algorithm:

```csharp
// Check for existing summary
var lastSummaryIndex = await store.GetLastSummaryIndexAsync();

if (lastSummaryIndex >= 0)
{
    // Count only messages AFTER the summary
    var messagesAfterSummary = await store.CountMessagesAfterLastSummaryAsync();

    if (messagesAfterSummary < threshold)
    {
        // Cache hit! Skip reduction
        return;
    }
}

// Need to reduce - create summary and apply
await store.ApplyReductionAsync(summaryMessage, removedCount);
```

### 3. Constructor Injection

Threads accept message stores via constructor, enabling dependency injection:

```csharp
// Default: in-memory
var thread = new ConversationThread();

// Custom: database
var dbStore = new DatabaseConversationMessageStore(db, conversationId);
var thread = new ConversationThread(dbStore);

// DI framework (ASP.NET Core)
services.AddScoped<ConversationMessageStore>(sp =>
    new DatabaseConversationMessageStore(sp.GetRequiredService<IDbConnection>(), conversationId));
```

### 4. Async-First Design

All operations are async to support I/O-bound storage backends:

```csharp
// In-memory: No I/O, but consistent interface
await thread.AddMessageAsync(message, cancellationToken);

// Database: Actual async I/O
await thread.AddMessageAsync(message, cancellationToken);
```

---

## Getting Started

### Basic Usage (In-Memory)

```csharp
using Microsoft.Extensions.AI;

// 1. Create a conversation thread (uses in-memory storage by default)
var thread = new ConversationThread();

// 2. Add messages
await thread.AddMessageAsync(
    new ChatMessage(ChatRole.User, "Hello!"),
    cancellationToken);

await thread.AddMessageAsync(
    new ChatMessage(ChatRole.Assistant, "Hi there!"),
    cancellationToken);

// 3. Access messages
var messages = thread.Messages;  // IReadOnlyList<ChatMessage>

// 4. Apply reduction (when Agent signals it)
await thread.ApplyReductionAsync(summaryMessage, removedCount, cancellationToken);

// 5. Check token usage
var stats = await thread.MessageStore.GetTokenStatisticsAsync(cancellationToken);
Console.WriteLine($"Total tokens: {stats.TotalTokens}");
Console.WriteLine($"Messages after summary: {stats.MessagesAfterSummary}");
```

### Custom Storage (Database Example)

```csharp
// 1. Create custom message store
var dbStore = new DatabaseConversationMessageStore(dbConnection, "conversation-123");

// 2. Create thread with custom store
var thread = new ConversationThread(dbStore);

// 3. Use normally - all operations now persist to database!
await thread.AddMessageAsync(message, cancellationToken);

// 4. Reduction still works - inherited logic operates on DB storage
await thread.ApplyReductionAsync(summaryMessage, removedCount, cancellationToken);
```

### Serialization & Deserialization

```csharp
// Serialize (stores the message store's state, not all messages for DB stores)
JsonElement serialized = thread.Serialize();
string json = JsonSerializer.Serialize(serialized);

// Save to file/database
await File.WriteAllTextAsync("thread.json", json);

// Later: Deserialize
string json = await File.ReadAllTextAsync("thread.json");
JsonElement serialized = JsonSerializer.Deserialize<JsonElement>(json);

var snapshot = serialized.Deserialize<ConversationThreadSnapshot>();
var restored = ConversationThread.Deserialize(snapshot);

// Message store is recreated based on serialized type
// InMemoryConversationMessageStore: Restores all messages
// DatabaseConversationMessageStore: Just stores conversation ID, loads messages on demand
```

---

## API Reference

### ConversationMessageStore (Abstract Base)

#### Abstract Methods (Must Override)

```csharp
/// <summary>
/// Load all messages from storage in chronological order.
/// </summary>
protected abstract Task<List<ChatMessage>> LoadMessagesAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Replace all messages in storage with the provided list.
/// Used by reduction to persist the reduced message set.
/// </summary>
protected abstract Task SaveMessagesAsync(
    IEnumerable<ChatMessage> messages,
    CancellationToken cancellationToken = default);

/// <summary>
/// Append a single message to storage.
/// Should maintain chronological order.
/// </summary>
protected abstract Task AppendMessageAsync(
    ChatMessage message,
    CancellationToken cancellationToken = default);

/// <summary>
/// Remove all messages from storage.
/// </summary>
public abstract Task ClearAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Serialize the message store's state to JSON.
/// </summary>
public abstract override JsonElement Serialize(
    JsonSerializerOptions? jsonSerializerOptions = null);
```

#### Shared Methods (Inherited)

**Cache-Aware Reduction:**

```csharp
/// <summary>
/// Apply history reduction by removing old messages and inserting a summary.
/// Validates: summary not null, removed count > 0, summary has marker, enough messages.
/// </summary>
public virtual async Task ApplyReductionAsync(
    ChatMessage summaryMessage,
    int removedCount,
    CancellationToken cancellationToken = default);

/// <summary>
/// Checks if the store contains a summary message.
/// </summary>
public virtual async Task<bool> HasSummaryAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Gets the index of the last summary message (-1 if none).
/// </summary>
public virtual async Task<int> GetLastSummaryIndexAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Gets messages after the last summary (or all non-system if no summary).
/// </summary>
public virtual async Task<IReadOnlyList<ChatMessage>> GetMessagesAfterLastSummaryAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Counts messages after the last summary.
/// </summary>
public virtual async Task<int> CountMessagesAfterLastSummaryAsync(
    CancellationToken cancellationToken = default);
```

**Token Counting:**

```csharp
/// <summary>
/// Calculates total token count for all messages.
/// Uses provider-accurate counts when available, estimates otherwise.
/// </summary>
public virtual async Task<int> GetTotalTokenCountAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Calculates token count for messages after the last summary.
/// </summary>
public virtual async Task<int> GetTokenCountAfterLastSummaryAsync(
    CancellationToken cancellationToken = default);

/// <summary>
/// Gets detailed token statistics.
/// </summary>
public virtual async Task<TokenStatistics> GetTokenStatisticsAsync(
    CancellationToken cancellationToken = default);
```

### InMemoryConversationMessageStore

```csharp
/// <summary>
/// Default in-memory implementation. Messages stored in List.
/// </summary>
public sealed class InMemoryConversationMessageStore : ConversationMessageStore
{
    /// <summary>
    /// Read-only view of messages (non-async accessor for convenience).
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; }

    /// <summary>
    /// Number of messages currently stored.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Creates an empty in-memory store.
    /// </summary>
    public InMemoryConversationMessageStore();

    /// <summary>
    /// Creates a store from serialized state (for deserialization).
    /// </summary>
    public InMemoryConversationMessageStore(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null);
}
```

### ConversationThread

```csharp
/// <summary>
/// Manages conversation state (messages, metadata, timestamps).
/// Uses ConversationMessageStore for message storage.
/// </summary>
public class ConversationThread : AgentThread
{
    // Properties
    public string Id { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastActivity { get; }
    public IReadOnlyList<ChatMessage> Messages { get; }
    public IReadOnlyDictionary<string, object> Metadata { get; }
    public int MessageCount { get; }
    public ConversationMessageStore MessageStore { get; }
    public string? ServiceThreadId { get; set; }

    // Constructors
    public ConversationThread();  // Default: in-memory
    public ConversationThread(ConversationMessageStore messageStore);  // Custom store

    // Message Operations (All Async)
    public async Task AddMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default);

    public async Task AddMessagesAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    public async Task ApplyReductionAsync(
        ChatMessage summaryMessage,
        int removedCount,
        CancellationToken cancellationToken = default);

    public async Task ClearAsync(
        CancellationToken cancellationToken = default);

    // Metadata Operations
    public void AddMetadata(string key, object value);

    // Serialization
    public override JsonElement Serialize(
        JsonSerializerOptions? jsonSerializerOptions = null);

    public static ConversationThread Deserialize(
        ConversationThreadSnapshot snapshot);

    // Utility
    public string GetDisplayName(int maxLength = 30);
}
```

### TokenStatistics

```csharp
/// <summary>
/// Token usage statistics for a message store.
/// </summary>
public record TokenStatistics
{
    public int TotalMessages { get; init; }
    public int TotalTokens { get; init; }
    public int SystemMessageCount { get; init; }
    public int SystemMessageTokens { get; init; }
    public bool HasSummary { get; init; }
    public int MessagesAfterSummary { get; init; }
    public int TokensAfterSummary { get; init; }
}
```

---

## Creating Custom Stores

### Step-by-Step Guide

#### 1. Create Your Store Class

```csharp
using Microsoft.Extensions.AI;
using System.Text.Json;

public class DatabaseConversationMessageStore : ConversationMessageStore
{
    private readonly IDbConnection _db;
    private readonly string _conversationId;

    public DatabaseConversationMessageStore(IDbConnection db, string conversationId)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _conversationId = conversationId ?? throw new ArgumentNullException(nameof(conversationId));
    }

    // Constructor for deserialization
    public DatabaseConversationMessageStore(JsonElement serializedState, JsonSerializerOptions? options = null)
    {
        var state = serializedState.Deserialize<StoreState>(options);
        _conversationId = state.ConversationId;
        // Re-create DB connection from DI or connection string
        _db = CreateDbConnection(state.ConnectionString);
    }

    // ... implement abstract methods (see below)
}
```

#### 2. Implement LoadMessagesAsync

```csharp
protected override async Task<List<ChatMessage>> LoadMessagesAsync(CancellationToken ct = default)
{
    var sql = @"
        SELECT Id, Role, Content, AdditionalProperties, Timestamp
        FROM Messages
        WHERE ConversationId = @ConversationId
        ORDER BY Timestamp ASC";

    var rows = await _db.QueryAsync<MessageRow>(sql, new { ConversationId = _conversationId });

    return rows.Select(row => new ChatMessage
    {
        Role = Enum.Parse<ChatRole>(row.Role),
        Content = row.Content,
        AdditionalProperties = JsonSerializer.Deserialize<AdditionalPropertiesDictionary>(row.AdditionalProperties)
    }).ToList();
}
```

#### 3. Implement SaveMessagesAsync

```csharp
protected override async Task SaveMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
{
    // Delete all existing messages for this conversation
    await _db.ExecuteAsync(
        "DELETE FROM Messages WHERE ConversationId = @Id",
        new { Id = _conversationId });

    // Insert all messages
    foreach (var message in messages)
    {
        await AppendMessageAsync(message, ct);
    }
}
```

#### 4. Implement AppendMessageAsync

```csharp
protected override async Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default)
{
    var sql = @"
        INSERT INTO Messages (ConversationId, Role, Content, AdditionalProperties, Timestamp)
        VALUES (@ConversationId, @Role, @Content, @AdditionalProperties, @Timestamp)";

    await _db.ExecuteAsync(sql, new
    {
        ConversationId = _conversationId,
        Role = message.Role.ToString(),
        Content = message.Text,
        AdditionalProperties = JsonSerializer.Serialize(message.AdditionalProperties),
        Timestamp = DateTime.UtcNow
    });
}
```

#### 5. Implement ClearAsync

```csharp
public override async Task ClearAsync(CancellationToken ct = default)
{
    await _db.ExecuteAsync(
        "DELETE FROM Messages WHERE ConversationId = @Id",
        new { Id = _conversationId });
}
```

#### 6. Implement Serialize

```csharp
public override JsonElement Serialize(JsonSerializerOptions? options = null)
{
    // Don't serialize all messages - just the conversation ID!
    // Messages are in the database
    var state = new StoreState
    {
        ConversationId = _conversationId,
        ConnectionString = _db.ConnectionString  // Or DI key
    };

    return JsonSerializer.SerializeToElement(state, options);
}

private class StoreState
{
    public string ConversationId { get; set; }
    public string ConnectionString { get; set; }
}
```

#### 7. Use Your Store

```csharp
// Create
var dbStore = new DatabaseConversationMessageStore(dbConnection, "conv-123");
var thread = new ConversationThread(dbStore);

// Use normally
await thread.AddMessageAsync(new ChatMessage(ChatRole.User, "Hello"), ct);

// Reduction works automatically - inherited logic operates on DB!
await thread.ApplyReductionAsync(summaryMessage, removedCount, ct);

// Serialize
var serialized = thread.Serialize();
// Only stores: { "ConversationId": "conv-123", "ConnectionString": "..." }
// NOT all messages!
```

### What You Get For FREE

When you inherit from `ConversationMessageStore`, you automatically get:

✅ **ApplyReductionAsync** - Cache-aware reduction algorithm
✅ **HasSummaryAsync** - Summary detection
✅ **GetLastSummaryIndexAsync** - Find summary position
✅ **GetMessagesAfterLastSummaryAsync** - Get unsummarized messages
✅ **CountMessagesAfterLastSummaryAsync** - Count for threshold checks
✅ **GetTotalTokenCountAsync** - Total token usage
✅ **GetTokenCountAfterLastSummaryAsync** - Tokens since last summary
✅ **GetTokenStatisticsAsync** - Detailed token breakdown
✅ **GetMessagesAsync** - ChatMessageStore contract
✅ **AddMessagesAsync** - ChatMessageStore contract

You only implement:
- `LoadMessagesAsync()` - How to load from YOUR storage
- `SaveMessagesAsync()` - How to save to YOUR storage
- `AppendMessageAsync()` - How to append to YOUR storage
- `ClearAsync()` - How to clear YOUR storage
- `Serialize()` - What state YOUR storage needs

---

## Migration Guide

### From Old API (v0) to New API (v1.0)

#### Breaking Changes

| Old (v0) | New (v1.0) | Notes |
|----------|------------|-------|
| `thread.AddMessage(msg)` | `await thread.AddMessageAsync(msg, ct)` | Now async |
| `thread.AddMessages(msgs)` | `await thread.AddMessagesAsync(msgs, ct)` | Now async |
| `thread.ApplyReduction(s, c)` | `await thread.ApplyReductionAsync(s, c, ct)` | Now async |
| `thread.Clear()` | `await thread.ClearAsync(ct)` | Now async |
| `ConversationThreadSnapshot.Messages` | `ConversationThreadSnapshot.MessageStoreState` | Delegates to store |
| N/A | `ConversationThreadSnapshot.MessageStoreType` | Stores type name |
| `new ConversationMessageStore()` | `new InMemoryConversationMessageStore()` | Now abstract |

#### Migration Steps

**Step 1: Update Method Calls**

```csharp
// OLD
thread.AddMessage(message);
thread.ApplyReduction(summary, count);

// NEW
await thread.AddMessageAsync(message, cancellationToken);
await thread.ApplyReductionAsync(summary, count, cancellationToken);
```

**Step 2: Update Conversation Class**

```csharp
// OLD
private void ApplyReductionIfPresent(OrchestrationResult result)
{
    // ...
    targetThread.ApplyReduction(summary, count);
}

// NEW
private async Task ApplyReductionIfPresentAsync(
    OrchestrationResult result,
    CancellationToken ct = default)
{
    // ...
    await targetThread.ApplyReductionAsync(summary, count, ct);
}
```

**Step 3: Update Serialization (If Custom)**

```csharp
// OLD
var snapshot = new ConversationThreadSnapshot
{
    Messages = thread.Messages.ToList(),  // All messages
    // ...
};

// NEW
var snapshot = new ConversationThreadSnapshot
{
    MessageStoreState = thread.MessageStore.Serialize(),  // Delegate to store
    MessageStoreType = thread.MessageStore.GetType().AssemblyQualifiedName,
    // ...
};
```

**Step 4: Update Direct Store Usage (If Any)**

```csharp
// OLD
var store = new ConversationMessageStore();

// NEW
var store = new InMemoryConversationMessageStore();
```

### Compatibility Notes

- ✅ Old serialized threads (v0) **will NOT deserialize** - schema changed
- ✅ Need to migrate persisted threads manually
- ✅ In-memory behavior unchanged (if not using persistence)
- ✅ All warnings are pre-existing, unrelated to migration

---

## Advanced Topics

### Performance Considerations

#### In-Memory Store
- **Load:** O(1) - returns reference to list
- **Append:** O(1) - list add
- **Reduction:** O(n) - removes and inserts in list
- **Token count:** O(n) - iterates all messages

#### Database Store
- **Load:** O(n) - SQL query
- **Append:** O(1) - single INSERT
- **Reduction:** O(n) - DELETE + INSERT
- **Token count:** O(n) - loads all messages, then calculates

**Optimization for Database:**

```csharp
// Option 1: Cache loaded messages
private List<ChatMessage>? _cache;

protected override async Task<List<ChatMessage>> LoadMessagesAsync(CancellationToken ct)
{
    if (_cache != null) return _cache;
    _cache = await LoadFromDatabaseAsync(ct);
    return _cache;
}

// Option 2: Override token counting with SQL
public override async Task<int> GetTotalTokenCountAsync(CancellationToken ct)
{
    // If tokens stored in DB, use SQL aggregation
    return await _db.ExecuteScalarAsync<int>(
        "SELECT SUM(TokenCount) FROM Messages WHERE ConversationId = @Id",
        new { Id = _conversationId });
}
```

### Thread Safety

**In-Memory Store:**
- ❌ Not thread-safe by default
- ✅ Use `lock` or `SemaphoreSlim` if concurrent access needed

```csharp
public class ThreadSafeInMemoryStore : ConversationMessageStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<ChatMessage> _messages = new();

    protected override async Task<List<ChatMessage>> LoadMessagesAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _messages;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ... other methods with locking
}
```

**Database Store:**
- ✅ Thread-safe if database connection pooling configured
- ✅ Use transactions for atomic operations

### Testing

**Mock Store for Unit Tests:**

```csharp
public class MockConversationMessageStore : ConversationMessageStore
{
    private List<ChatMessage> _messages = new();

    protected override Task<List<ChatMessage>> LoadMessagesAsync(CancellationToken ct)
        => Task.FromResult(_messages);

    protected override Task SaveMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
    {
        _messages = messages.ToList();
        return Task.CompletedTask;
    }

    protected override Task AppendMessageAsync(ChatMessage message, CancellationToken ct)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public override Task ClearAsync(CancellationToken ct)
    {
        _messages.Clear();
        return Task.CompletedTask;
    }

    public override JsonElement Serialize(JsonSerializerOptions? options = null)
        => JsonSerializer.SerializeToElement(new { });
}

// Test
[Fact]
public async Task Reduction_RemovesOldMessages_InsertsWithSummary()
{
    // Arrange
    var store = new MockConversationMessageStore();
    var thread = new ConversationThread(store);

    // Add 50 messages
    for (int i = 0; i < 50; i++)
    {
        await thread.AddMessageAsync(new ChatMessage(ChatRole.User, $"Message {i}"), ct);
    }

    // Act
    var summary = new ChatMessage(ChatRole.Assistant, "Summary of 30 messages");
    summary.AdditionalProperties["__summary__"] = true;

    await thread.ApplyReductionAsync(summary, 30, ct);

    // Assert
    Assert.Equal(21, thread.MessageCount);  // 50 - 30 + 1 (summary)
    Assert.True(await store.HasSummaryAsync(ct));
}
```

### Dependency Injection (ASP.NET Core)

```csharp
// Startup.cs or Program.cs
services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(Configuration.GetConnectionString("Default")));

services.AddScoped<ConversationMessageStore>(sp =>
{
    var db = sp.GetRequiredService<IDbConnection>();
    var conversationId = sp.GetRequiredService<IHttpContextAccessor>()
        .HttpContext?.User.FindFirst("conversation_id")?.Value;

    return new DatabaseConversationMessageStore(db, conversationId);
});

services.AddScoped<ConversationThread>(sp =>
{
    var store = sp.GetRequiredService<ConversationMessageStore>();
    return new ConversationThread(store);
});

// Controller
public class ChatController : ControllerBase
{
    private readonly ConversationThread _thread;

    public ChatController(ConversationThread thread)
    {
        _thread = thread;  // Injected with database store!
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] string message)
    {
        await _thread.AddMessageAsync(
            new ChatMessage(ChatRole.User, message),
            HttpContext.RequestAborted);

        // ... process with agent ...

        return Ok();
    }
}
```

---

## Related Documentation

- [CHAT_REDUCTION.md](./CHAT_REDUCTION.md) - Cache-aware reduction architecture
- [TOKEN_AWARE_REDUCTION.md](./TOKEN_AWARE_REDUCTION.md) - Token-based reduction (Phase 2)
- [ChatMessageExtensions.cs](./ChatMessageExtensions.cs) - Token counting extensions

---

## Version History

**v1.0 (2025-01-24)**
- ✅ Initial release
- ✅ Template Method pattern implementation
- ✅ Constructor injection support
- ✅ Async-first design
- ✅ Validation and guards
- ✅ `InMemoryConversationMessageStore` default implementation

**Future Enhancements:**
- Redis message store implementation
- Batch operations optimization
- Compression support for large message payloads
- Query interface for message filtering
