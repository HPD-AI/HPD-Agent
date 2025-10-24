# Conversation Architecture (v0)

## Overview

HPD-Agent uses a **stateless conversation pattern** aligned with Microsoft's `AIAgent` framework. This architecture separates execution logic from state management for scalability, thread-safety, and multi-agent workflow support.

---

## Core Components

### 1. **Agent** - Execution Engine
The AI model interface that processes messages and executes tools.

```csharp
var agent = new AgentBuilder()
    .WithName("AI Assistant")
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .WithInstructions("You are a helpful assistant")
    .WithPlugin<MathPlugin>()
    .Build();
```

**Key Points**:
- ✅ **Stateless** - Can be reused across multiple conversations
- ✅ **Thread-safe** - Safe to use as singleton
- ✅ **Reusable** - One agent instance serves many threads

---

### 2. **ConversationThread** - State Container
Stores conversation history and metadata. This is your conversation state.

```csharp
var thread = new ConversationThread();
// Thread contains:
// - Messages (conversation history)
// - Metadata (custom key-value pairs)
// - Timestamps (created, last activity)
```

**Key Points**:
- ✅ **Stateful** - Holds conversation history
- ✅ **Serializable** - Can be saved/restored
- ✅ **Shareable** - Can be passed to different agents

---

### 3. **Conversation** - Orchestrator
Coordinates agent execution with thread state. This is stateless.

```csharp
var conversation = new Conversation(agent);
// Conversation is a stateless orchestrator
// It delegates execution to agent with provided thread
```

**Key Points**:
- ✅ **Stateless** - No internal state, can be singleton
- ✅ **Thread parameter required** - You pass the thread to each call
- ✅ **Microsoft-compatible** - Implements `AIAgent` interface

---

### 4. **Project** (Optional) - Organization Container
Manages multiple conversation threads and shared resources.

```csharp
var project = Project.Create("My Project");
var thread = project.CreateThread();
// Project tracks multiple threads + shared documents
```

**Key Points**:
- ✅ **Optional** - Not required for basic usage
- ✅ **Organizational** - Groups related conversations
- ✅ **Knowledge Base** - Shared documents across threads

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                        Project                          │
│  (Optional: Organizes threads + shared documents)       │
│                                                          │
│  ┌────────────────┐  ┌────────────────┐                │
│  │  Thread A      │  │  Thread B      │                │
│  │  - Messages    │  │  - Messages    │                │
│  │  - Metadata    │  │  - Metadata    │                │
│  └────────────────┘  └────────────────┘                │
└─────────────────────────────────────────────────────────┘
         │                      │
         │                      │
         ▼                      ▼
┌─────────────────────────────────────────────────────────┐
│                   Conversation                          │
│            (Stateless Orchestrator)                     │
│                                                          │
│           Coordinates execution with state              │
└─────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────┐
│                      Agent                              │
│              (Stateless Execution)                      │
│                                                          │
│    LLM + Plugins + Memory + Filters                     │
└─────────────────────────────────────────────────────────┘
```

---

## Usage Patterns

### **Pattern 1: Simple Conversation** (No Project)

The most basic usage - one conversation, one thread.

```csharp
// 1. Create agent
var agent = new AgentBuilder()
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .Build();

// 2. Create conversation (stateless orchestrator)
var conversation = new Conversation(agent);

// 3. Create thread (state container)
var thread = new ConversationThread();

// 4. Chat with the agent
var response = await conversation.RunAsync(
    [new ChatMessage(ChatRole.User, "Hello!")],
    thread  // ⚠️ Must pass thread
);

// 5. Continue conversation (same thread)
var response2 = await conversation.RunAsync(
    [new ChatMessage(ChatRole.User, "Tell me more")],
    thread  // ✅ Same thread = conversation history preserved
);

// Thread now has all messages
Console.WriteLine($"Total messages: {thread.Messages.Count}");
```

**When to use**: Simple CLI apps, single-user scripts, one-off conversations

---

### **Pattern 2: Project-Organized Conversations**

Use Project to organize multiple related conversations.

```csharp
// 1. Create project
var project = Project.Create("Customer Support");

// 2. Create agent
var agent = new AgentBuilder()
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .Build();

// 3. Create conversation (stateless)
var conversation = new Conversation(agent);

// 4. Create thread via project (automatically tracked)
var thread = project.CreateThread(conversation);

// 5. Chat
await conversation.RunAsync([message], thread);

// 6. Later: Find this thread
var foundThread = project.GetThread(thread.Id);

// 7. Create another thread in same project
var thread2 = project.CreateThread();

// All threads are organized under project
Console.WriteLine($"Total threads: {project.ThreadCount}");
```

**When to use**: Multi-conversation apps, web APIs, long-running workflows

---

### **Pattern 3: Web API with Multiple Users**

Scalable pattern for web servers - singleton conversation, many threads.

```csharp
public class ConversationService
{
    // ✅ Singleton: One conversation instance for all requests
    private readonly Conversation _conversation;
    private readonly ProjectManager _projectManager;

    public ConversationService(Agent agent, ProjectManager pm)
    {
        _conversation = new Conversation(agent);  // Stateless, thread-safe
        _projectManager = pm;
    }

    public async Task<Response> HandleUserRequest(string userId, string message)
    {
        // 1. Get user's project
        var project = _projectManager.GetUserProject(userId);

        // 2. Get or create thread for this user
        var thread = project.GetMostRecentThread()
                     ?? project.CreateThread();

        // 3. Use singleton conversation with user's thread
        var response = await _conversation.RunAsync(
            [new ChatMessage(ChatRole.User, message)],
            thread  // Each user has their own thread
        );

        return response;
    }
}

// Register as singleton
services.AddSingleton<Conversation>(sp =>
    new Conversation(sp.GetRequiredService<Agent>()));
```

**When to use**: Web APIs, multi-tenant applications, high-scale services

---

### **Pattern 4: Multi-Agent Workflows**

Pass the same thread to multiple agents for collaboration.

```csharp
// 1. Create agents
var researchAgent = new AgentBuilder()
    .WithName("Researcher")
    .Build();

var writerAgent = new AgentBuilder()
    .WithName("Writer")
    .Build();

// 2. Create conversations (stateless)
var researchConv = new Conversation(researchAgent);
var writerConv = new Conversation(writerAgent);

// 3. Create shared thread
var sharedThread = new ConversationThread();

// 4. Research agent goes first
await researchConv.RunAsync(
    [new ChatMessage(ChatRole.User, "Research topic: AI")],
    sharedThread
);

// 5. Writer agent sees research results
await writerConv.RunAsync(
    [new ChatMessage(ChatRole.User, "Write article based on research")],
    sharedThread  // ✅ Same thread = sees all previous messages
);

// Both agents' messages are in the same thread
Console.WriteLine($"Shared history: {sharedThread.Messages.Count} messages");
```

**When to use**: Multi-agent workflows, agent handoffs, collaborative tasks

---

### **Pattern 5: Streaming Conversations**

Real-time streaming with explicit thread management.

```csharp
var conversation = new Conversation(agent);
var thread = new ConversationThread();

await foreach (var update in conversation.RunStreamingAsync(
    [new ChatMessage(ChatRole.User, "Tell me a story")],
    thread,  // ⚠️ Must pass thread
    cancellationToken: cts.Token))
{
    foreach (var content in update.Contents ?? [])
    {
        if (content is TextContent text)
        {
            Console.Write(text.Text);  // Stream output
        }
    }
}

// Thread is updated with final messages
Console.WriteLine($"\nThread has {thread.Messages.Count} messages");
```

**When to use**: Interactive UIs, CLI apps, real-time applications

---

## AGUI Protocol (Frontend Communication)

For frontends using the AGUI streaming protocol, use the AGUI-specific methods:

```csharp
// AGUI Input (from frontend)
var aguiInput = new RunAgentInput
{
    ThreadId = "thread-123",
    RunId = "run-456",
    Messages = [/* AGUI messages */],
    Tools = [/* Frontend tools */],
    // ... other AGUI fields
};

// Create conversation and thread
var conversation = new Conversation(agent);
var thread = new ConversationThread();

// Streaming AGUI
await foreach (var evt in conversation.RunStreamingAsync(aguiInput, thread))
{
    // evt is BaseEvent (AGUI protocol)
    // Stream to frontend: TEXT_MESSAGE_CONTENT, TOOL_CALL_START, etc.
}
```

**When to use**: Web frontends, AGUI-compatible clients

---

## Thread Management

### **Creating Threads**

```csharp
// Option 1: Direct creation
var thread = new ConversationThread();

// Option 2: Via conversation
var conversation = new Conversation(agent);
var thread = (ConversationThread)conversation.GetNewThread();

// Option 3: Via project (recommended)
var project = Project.Create("My Project");
var thread = project.CreateThread();
```

### **Thread Metadata**

Store custom data in threads:

```csharp
// Add metadata
thread.AddMetadata("UserId", "user-123");
thread.AddMetadata("SessionType", "Support");
thread.AddMetadata("Priority", "High");

// Read metadata
if (thread.Metadata.TryGetValue("UserId", out var userId))
{
    Console.WriteLine($"User: {userId}");
}
```

### **Thread Properties**

```csharp
thread.Id              // Unique identifier
thread.Messages        // Full conversation history
thread.CreatedAt       // When thread was created
thread.LastActivity    // Last message timestamp
thread.Metadata        // Custom key-value pairs
thread.GetDisplayName() // Human-readable name
```

### **Thread Serialization**

Save and restore threads:

```csharp
// Serialize
var snapshot = thread.Serialize();
var json = JsonSerializer.Serialize(snapshot);
await File.WriteAllTextAsync("thread.json", json);

// Deserialize
var json = await File.ReadAllTextAsync("thread.json");
var snapshot = JsonSerializer.Deserialize<ConversationThreadSnapshot>(json);
var restoredThread = ConversationThread.Deserialize(snapshot);
```

---

## Project Features

### **Creating Projects**

```csharp
var project = Project.Create("Customer Support");
project.Description = "Support conversations for Q1 2025";
```

### **Managing Threads**

```csharp
// Create threads
var thread1 = project.CreateThread();
var thread2 = project.CreateThread();

// Find threads
var thread = project.GetThread(threadId);
var recent = project.GetMostRecentThread();
var all = project.Threads;

// Search threads
var results = project.SearchThreads("billing issue", maxResults: 10);

// Remove threads
project.RemoveThread(threadId);
```

### **Shared Documents**

Upload documents accessible to all threads in the project:

```csharp
// Upload documents
await project.UploadDocumentAsync("manual.pdf");
await project.UploadDocumentFromUrlAsync("https://docs.example.com/api.html");

// Get documents
var docs = await project.DocumentManager.GetDocumentsAsync();
```

### **Project Analytics**

```csharp
var summary = await project.GetSummaryAsync();
Console.WriteLine($"Project: {summary.Name}");
Console.WriteLine($"Threads: {summary.ConversationCount}");
Console.WriteLine($"Total Messages: {summary.TotalMessages}");
Console.WriteLine($"Documents: {summary.DocumentCount}");
```

---

## Key Concepts

### **Stateless vs Stateful**

| Component | Type | Can be Singleton? | Stores State? |
|-----------|------|-------------------|---------------|
| **Agent** | Stateless | ✅ Yes | ❌ No |
| **Conversation** | Stateless | ✅ Yes | ❌ No |
| **ConversationThread** | Stateful | ❌ No | ✅ Yes (messages) |
| **Project** | Stateful | ❌ No | ✅ Yes (threads, docs) |

### **Thread Parameter is Required**

```csharp
// ❌ WRONG: No thread provided
await conversation.RunAsync([message]);
// Creates temporary thread, loses history!

// ✅ CORRECT: Explicit thread
await conversation.RunAsync([message], thread);
// Thread tracks full history
```

### **Conversation Reusability**

```csharp
// One conversation can serve multiple threads
var conversation = new Conversation(agent);

var thread1 = new ConversationThread();
await conversation.RunAsync([msg1], thread1);

var thread2 = new ConversationThread();
await conversation.RunAsync([msg2], thread2);

// thread1 and thread2 are independent conversations
```

---

## Common Patterns

### **1. Session Management**

```csharp
// Store threads in session/database
var sessionStore = new Dictionary<string, ConversationThread>();

// On user message
var sessionId = GetUserSessionId();
var thread = sessionStore.GetOrAdd(sessionId, _ => new ConversationThread());

await conversation.RunAsync([message], thread);

// Save thread back to store
sessionStore[sessionId] = thread;
```

### **2. Thread Lifecycle**

```csharp
// Create thread
var thread = project.CreateThread();

// Use thread for multiple turns
await conversation.RunAsync([msg1], thread);
await conversation.RunAsync([msg2], thread);
await conversation.RunAsync([msg3], thread);

// Archive or delete when done
project.RemoveThread(thread.Id);
```

### **3. Thread Sharing**

```csharp
// Multiple agents can use the same thread
var agent1 = CreateAgent1();
var agent2 = CreateAgent2();

var conv1 = new Conversation(agent1);
var conv2 = new Conversation(agent2);

var sharedThread = new ConversationThread();

// Agent 1 processes
await conv1.RunAsync([userMsg], sharedThread);

// Agent 2 sees Agent 1's messages
await conv2.RunAsync([followUpMsg], sharedThread);
```

---

## Best Practices

### ✅ **DO**

1. **Always pass a thread to RunAsync**
   ```csharp
   await conversation.RunAsync([message], thread);
   ```

2. **Reuse conversation instances when possible**
   ```csharp
   // Good: One conversation, many threads
   var conversation = new Conversation(agent);
   ```

3. **Use Project for multi-thread scenarios**
   ```csharp
   var project = Project.Create("My App");
   var thread = project.CreateThread();
   ```

4. **Store thread ID for later retrieval**
   ```csharp
   var threadId = thread.Id;
   // Save to database/session
   ```

### ❌ **DON'T**

1. **Don't call RunAsync without a thread**
   ```csharp
   // Bad: Creates temp thread, loses history
   await conversation.RunAsync([message]);
   ```

2. **Don't create new conversation for each message**
   ```csharp
   // Bad: Wasteful
   var conv1 = new Conversation(agent);
   var conv2 = new Conversation(agent);
   ```

3. **Don't mix threads unintentionally**
   ```csharp
   // Bad: Each call uses different thread
   await conversation.RunAsync([msg1], thread1);
   await conversation.RunAsync([msg2], thread2);  // Different conversation!
   ```

---

## Migration from Other Patterns

If you're familiar with other conversation patterns:

### **From OpenAI SDK**
```csharp
// OpenAI SDK style
var messages = new List<ChatMessage>();
messages.Add(new ChatMessage(ChatRole.User, "Hello"));
var response = await client.CompleteChatAsync(messages);
messages.AddRange(response.Message);

// HPD-Agent equivalent
var thread = new ConversationThread();
await conversation.RunAsync([new ChatMessage(ChatRole.User, "Hello")], thread);
// Thread automatically tracks history
```

### **From Semantic Kernel**
```csharp
// SK style
var chatHistory = new ChatHistory();
chatHistory.AddUserMessage("Hello");
var response = await kernel.GetChatCompletion().GenerateChatMessageAsync(chatHistory);

// HPD-Agent equivalent
var thread = new ConversationThread();
await conversation.RunAsync([new ChatMessage(ChatRole.User, "Hello")], thread);
```

---

## Summary

- **Agent**: Stateless execution engine (LLM + plugins)
- **Conversation**: Stateless orchestrator (coordinates agent + thread)
- **ConversationThread**: Stateful container (stores history)
- **Project**: Optional organizer (groups threads + documents)

**Key Rule**: Always pass a thread to `conversation.RunAsync()` to maintain history.

**Architecture Benefits**:
- ✅ Thread-safe singleton conversations
- ✅ Scalable for web APIs
- ✅ Multi-agent workflow support
- ✅ Microsoft.Agents.AI compatible
- ✅ Explicit state management

For more details, see:
- [Thread Search Feature](./THREAD_SEARCH_FEATURE_DESIGN.md)
- [Message Store Architecture](./MESSAGE_STORE_ARCHITECTURE.md)
