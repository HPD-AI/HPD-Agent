# Agent Thread Search Feature Design

## Overview
Enable agents to search through previous conversation threads within a project to find relevant historical context that isn't in agent memory or current thread.

## Use Cases

### 1. **Customer Support**
```
User: "What did you tell me about my refund last week?"
Agent: [Searches project threads] → Finds previous conversation about refund
Agent: "In our conversation on Jan 15th, I mentioned your refund was processed..."
```

### 2. **Long-Running Projects**
```
User: "What requirements did we discuss in the planning phase?"
Agent: [Searches old threads] → Finds planning conversation from 2 months ago
Agent: "From our planning discussion in November, you mentioned..."
```

### 3. **Multi-User Collaboration**
```
User: "What did Sarah ask about the API endpoints?"
Agent: [Searches other user's threads] → Finds Sarah's conversation
Agent: "Sarah asked about rate limiting on the /users endpoint..."
```

---

## Architecture

### **Current State**
```
Agent → DynamicMemory (explicit facts stored)
     → StaticMemory (documents uploaded)
     → Current Thread (this conversation only)
```

### **New Feature**
```
Agent → DynamicMemory
     → StaticMemory
     → Current Thread
     → 🆕 ThreadSearchPlugin (search historical threads in project)
```

---

## Implementation Plan

### **Phase 1: Basic Thread Search Plugin**

Create a plugin that gives the agent search capability:

```csharp
public class ThreadSearchPlugin
{
    private readonly Project _project;

    public ThreadSearchPlugin(Project project)
    {
        _project = project;
    }

    [Description("Search through previous conversations in this project to find relevant historical context")]
    public async Task<string> SearchPreviousConversations(
        [Description("The search query or keywords to find")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        // Search threads
        var threads = _project.SearchThreads(query, maxResults);

        if (!threads.Any())
            return "No previous conversations found matching your query.";

        // Format results
        var results = new StringBuilder();
        results.AppendLine($"Found {threads.Count()} relevant conversations:\n");

        foreach (var thread in threads)
        {
            results.AppendLine($"**Thread {thread.Id.Substring(0, 8)}... ({thread.CreatedAt:yyyy-MM-dd})**");
            results.AppendLine($"Messages: {thread.Messages.Count} | Last Activity: {thread.LastActivity:yyyy-MM-dd HH:mm}");

            // Include snippet of relevant messages
            var relevantMessages = thread.Messages
                .Where(m => m.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .Take(3);

            foreach (var msg in relevantMessages)
            {
                var snippet = msg.Text?.Substring(0, Math.Min(150, msg.Text.Length ?? 0));
                results.AppendLine($"  - {msg.Role}: {snippet}...");
            }
            results.AppendLine();
        }

        return results.ToString();
    }

    [Description("Get the full conversation history from a specific thread by ID")]
    public async Task<string> GetThreadHistory(
        [Description("The thread ID to retrieve")] string threadId)
    {
        var thread = _project.GetThread(threadId);
        if (thread == null)
            return $"Thread {threadId} not found in this project.";

        var history = new StringBuilder();
        history.AppendLine($"Thread {threadId} ({thread.CreatedAt:yyyy-MM-dd HH:mm}):\n");

        foreach (var msg in thread.Messages)
        {
            history.AppendLine($"{msg.Role}: {msg.Text}");
        }

        return history.ToString();
    }
}
```

### **Usage in AgentBuilder**

```csharp
var project = Project.Create("Customer Support");

var agent = new AgentBuilder()
    .WithName("Support Agent")
    .WithInstructions(@"
        You are a support agent with access to historical conversations.
        When users ask about previous discussions, use the SearchPreviousConversations tool.
        You can search by keywords, topics, dates, or any relevant information.
    ")
    .WithPlugin(new ThreadSearchPlugin(project))  // 🔑 Give agent search capability
    .Build();

// Now agent can search threads!
var conversation = new Conversation(agent);
var thread = project.CreateThread();
await conversation.RunAsync([
    new ChatMessage(ChatRole.User, "What did we discuss about refunds last week?")
], thread);

// Agent will use SearchPreviousConversations("refunds") tool
```

---

## Phase 2: Advanced Search (Future Enhancement)

### **Semantic Search**
Instead of keyword matching, use embeddings for semantic similarity:

```csharp
[Description("Search previous conversations using semantic similarity")]
public async Task<string> SemanticSearchThreads(
    [Description("Natural language description of what to find")] string query,
    int maxResults = 5)
{
    // 1. Generate embedding for query
    var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

    // 2. Compare with thread embeddings (cached)
    var results = _project.Threads
        .Select(t => new {
            Thread = t,
            Similarity = CosineSimilarity(queryEmbedding, t.CachedEmbedding)
        })
        .OrderByDescending(x => x.Similarity)
        .Take(maxResults);

    // 3. Return relevant threads
    return FormatResults(results);
}
```

### **Temporal Search**
Search by time ranges:

```csharp
[Description("Search conversations from a specific time period")]
public async Task<string> SearchThreadsByDate(
    [Description("Start date (YYYY-MM-DD)")] string startDate,
    [Description("End date (YYYY-MM-DD)")] string endDate,
    [Description("Optional keywords to filter")] string? keywords = null)
{
    var start = DateTime.Parse(startDate);
    var end = DateTime.Parse(endDate);

    var threads = _project.Threads
        .Where(t => t.CreatedAt >= start && t.CreatedAt <= end);

    if (keywords != null)
        threads = threads.Where(t => /* keyword match */);

    return FormatResults(threads);
}
```

### **User-Specific Search**
Search threads by user/participant:

```csharp
[Description("Search conversations involving a specific user")]
public async Task<string> SearchThreadsByUser(
    [Description("Username or user ID")] string userId)
{
    var threads = _project.Threads
        .Where(t => t.Metadata.TryGetValue("UserId", out var uid)
                    && uid?.ToString() == userId);

    return FormatResults(threads);
}
```

---

## Phase 3: RAG-Style Retrieval (Advanced)

### **Automatic Context Injection**
Instead of making the agent explicitly call search, automatically inject relevant context:

```csharp
public class ThreadRAGFilter : IPromptFilter
{
    private readonly Project _project;

    public async Task<ChatMessage> OnPromptAsync(ChatMessage message, ...)
    {
        // Automatically search for relevant threads based on user message
        var relevantThreads = await FindRelevantThreadsAsync(message.Text);

        if (relevantThreads.Any())
        {
            // Inject context into system message
            var context = FormatThreadContext(relevantThreads);
            var systemMessage = new ChatMessage(ChatRole.System,
                $"Relevant previous conversations:\n{context}");

            // Add to message history before agent processes
            messages.Insert(0, systemMessage);
        }

        return message;
    }
}

// Usage
var agent = new AgentBuilder()
    .WithPromptFilter(new ThreadRAGFilter(project))  // Automatic retrieval!
    .Build();
```

---

## Benefits vs Agent Memory

| Feature | Agent Memory (DynamicMemory) | Thread Search |
|---------|------------------------------|---------------|
| **Type** | Explicit facts | Full conversations |
| **Scope** | Agent-level | Project-level |
| **Granularity** | Individual facts | Entire threads |
| **Control** | Agent decides what to store | User controls via search |
| **Use Case** | "Remember my name is John" | "What did we discuss last week?" |
| **Persistence** | Long-term (stored in DB) | Permanent (project history) |
| **Search** | By key/fact | By semantic/keyword |

**They complement each other!**
- **Memory**: Structured, explicit facts (name, preferences, etc.)
- **Thread Search**: Unstructured historical context (past discussions)

---

## Implementation Checklist

- [x] Project.SearchThreads() already exists (basic search)
- [ ] Create ThreadSearchPlugin class
- [ ] Add plugin to AgentBuilder
- [ ] Test with multi-thread scenarios
- [ ] Add semantic search (Phase 2)
- [ ] Add temporal/user filters (Phase 2)
- [ ] Add automatic RAG injection (Phase 3)

---

## Example Conversation Flow

```
User: "What did I ask about API rate limits before?"

Agent: [Calls SearchPreviousConversations("API rate limits")]
       → Finds thread from 2 weeks ago

Agent: "I found a previous conversation from January 10th where you asked
       about rate limits. You wanted to know the limits for the /users endpoint.
       I explained that it's 100 requests per minute for authenticated users.
       Would you like me to retrieve the full conversation?"

User: "Yes"

Agent: [Calls GetThreadHistory("thread-id-123")]
       → Returns full conversation history

Agent: "Here's the complete discussion: [full history]"
```

---

## Technical Notes

1. **Performance**: For large projects, consider indexing or caching
2. **Privacy**: May need filters to prevent cross-user thread access
3. **Permissions**: Check if user has access to search all threads
4. **Token Limits**: Summarize large thread histories before returning
5. **Caching**: Cache search results to avoid repeated searches

---

## Integration with Existing Features

### **Works with existing Project features:**
- ✅ `project.SearchThreads()` - Basic search already works
- ✅ `project.GetThread(id)` - Get specific thread
- ✅ `project.Threads` - Access all threads
- ✅ `project.DocumentManager` - Search can also look at documents

### **Synergy with Memory:**
```csharp
// Agent can use BOTH memory AND thread search
var agent = new AgentBuilder()
    .WithDynamicMemory()              // Structured facts
    .WithPlugin<ThreadSearchPlugin>()  // Historical search
    .Build();

// Now agent has:
// - Memory: "User's name is John, prefers email notifications"
// - Thread Search: "Found 3 previous discussions about billing"
```

---

## Conclusion

Your feature is not only possible—the Project class is **already designed for it**!

Next steps:
1. Create `ThreadSearchPlugin` class
2. Add it to your AgentBuilder
3. Test with multiple threads
4. Enhance with semantic search (optional)

This will give your agent "conversational memory" across multiple sessions! 🚀
