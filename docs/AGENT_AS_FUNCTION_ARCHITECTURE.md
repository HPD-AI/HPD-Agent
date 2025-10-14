# Agent-as-Function Architecture: AsAIFunction() Deep Dive

## Overview

The `AsAIFunction()` extension method transforms an entire AI agent (with its own tools, thread, and agentic loop) into a single callable function that can be used by other agents or AI models. This enables **agent-as-tool** patterns and multi-agent orchestration.

**Key Insight:** An agent with potentially hundreds of tools and complex behavior becomes a single function call from another agent's perspective.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [The Abstraction Layers](#the-abstraction-layers)
3. [Complete Flow: Step-by-Step](#complete-flow-step-by-step)
4. [Thread Management Strategies](#thread-management-strategies)
5. [Performance Considerations](#performance-considerations)
6. [Usage Patterns](#usage-patterns)
7. [Implementation Details](#implementation-details)
8. [Comparison with IChatClient](#comparison-with-ichatclient)
9. [Advanced Scenarios](#advanced-scenarios)
10. [References](#references)

---

## Architecture Overview

### The Big Picture

```
┌─────────────────────────────────────────────────────────────────┐
│ Orchestrator Agent (e.g., "Task Router")                        │
│                                                                  │
│  Available Tools:                                               │
│  ├─ CodingAgent(query: string) → string                        │
│  ├─ ResearchAgent(query: string) → string                      │
│  └─ AnalyticsAgent(query: string) → string                     │
│                                                                  │
│  Each "tool" is actually a FULL AGENT with:                    │
│  • Its own agentic loop (MaxAgenticIterations)                 │
│  • Its own tools (FileSystem, Git, WebSearch, etc.)            │
│  • Its own conversation thread (state management)              │
│  • Its own system instructions and behavior                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Function Call
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ CodingAgent - Full Agent Execution                              │
│                                                                  │
│  1. Receives query string                                       │
│  2. Runs complete agentic loop (up to N iterations)            │
│  3. Can call its own tools (ReadFile, WriteFile, Shell, etc.)  │
│  4. Returns final response as string                            │
│                                                                  │
│  This entire agent execution appears as ONE function call       │
│  from the orchestrator's perspective!                           │
└─────────────────────────────────────────────────────────────────┘
```

### Why This Matters

- **Composability:** Build complex systems from simpler agents
- **Encapsulation:** Each agent maintains its own state and behavior
- **Scalability:** Add new specialized agents without modifying existing ones
- **Separation of Concerns:** Orchestrator doesn't need to know implementation details

---

## The Abstraction Layers

### Layer 1: IChatClient (Lowest Level)

```csharp
public interface IChatClient
{
    Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,  // ← YOU provide ALL messages
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

**Characteristics:**
- ❌ No state management
- ❌ No conversation history
- ❌ No identity (no name/description)
- ✅ Thread-safe for concurrent use
- ✅ Pure request/response

**Example:**
```csharp
var messages = new[]
{
    new ChatMessage(ChatRole.System, "You are helpful"),
    new ChatMessage(ChatRole.User, "Hello")
};
var response = await chatClient.GetResponseAsync(messages);
// ❌ Next call has no memory of this conversation
```

---

### Layer 2: Agent (Your Implementation)

```csharp
public class Agent : IChatClient
{
    public AgentConfig? Config { get; }  // Name, SystemInstructions, etc.
    public string Name { get; }
    public string? SystemInstructions { get; }

    // Has agentic loop with tool calling
    // Has filters, plugins, memory
    // But still stateless per-call
}
```

**Characteristics:**
- ✅ Has identity (name, description)
- ✅ Agentic loop with tool calling
- ✅ Rich configuration
- ⚠️ Still stateless (you manage history)
- ⚠️ Cannot be directly used as function

---

### Layer 3: Conversation (AIAgent Wrapper)

```csharp
public class Conversation : AIAgent
{
    private readonly ConversationThread _thread;  // ← STATE!
    private readonly Agent _agent;

    public override string? Name => _agent?.Config?.Name;
    public override string? Description => _agent?.Config?.SystemInstructions;

    public ConversationThread Thread => _thread;
}
```

**Characteristics:**
- ✅ Stateful (has thread)
- ✅ Inherits from AIAgent (Microsoft pattern)
- ✅ Automatic history management
- ✅ Thread serialization/deserialization
- ✅ Compatible with agent orchestration

---

### Layer 4: AIFunction (Function Wrapper)

```csharp
public abstract class AIFunction : AITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonElement JsonSchema { get; }  // Parameter schema

    public ValueTask<object?> InvokeAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken);
}
```

**Characteristics:**
- ✅ AI models can call it as a tool
- ✅ Has JSON schema for parameters
- ✅ Can be registered with any agent
- ✅ Encapsulates any logic (including calling agents!)

---

### Layer 5: AsAIFunction() Bridge

**This is where the magic happens!**

```csharp
public static AIFunction AsAIFunction(
    this AIAgent agent,  // Your Conversation
    AIFunctionFactoryOptions? options = null,
    AgentThread? thread = null)  // ← STATE MANAGEMENT!
```

**Transforms:**
```
AIAgent (Conversation)
    ↓ AsAIFunction()
    ↓
AIFunction (callable by other agents)
```

---

## Complete Flow: Step-by-Step

### Phase 1: Setup - Creating Agent-as-Function

#### Step 1.1: Create Specialized Agent

```csharp
// AgentConsoleTest/Program.cs pattern
var codingAgentConfig = new AgentConfig
{
    Name = "CodingAgent",  // ← Will become function name
    SystemInstructions = @"You are an expert coding assistant.
You can read files, write code, run tests, and analyze codebases.",
    MaxAgenticIterations = 15,
    Provider = new ProviderConfig { /* ... */ }
};

var codingAgent = new AgentBuilder(codingAgentConfig)
    .WithPlugin(new FileSystemPlugin(new FileSystemContext(
        workspaceRoot: @"C:\Projects\MyApp",
        enableShell: true
    )))
    .WithPlugin<GitPlugin>()
    .Build();
```

**Agent.cs:80-139** - Agent construction with config

#### Step 1.2: Wrap in Conversation (for thread management)

```csharp
var codingConversation = new Conversation(codingAgent);
// Now has:
// - _thread: ConversationThread (state container)
// - _agent: Agent (execution engine)
```

**Conversation.cs:283-296** - Conversation constructors

#### Step 1.3: Convert to AIFunction

```csharp
var codingTool = codingConversation.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "CodingAgent",  // Override default (sanitized agent name)
        Description = @"Delegate coding tasks to specialized agent.
Use when user needs: file operations, code writing, tests, git",
        SerializerOptions = null  // Use defaults
    },
    thread: null  // Strategy: stateless (new thread each call)
    // OR
    thread: codingConversation.Thread  // Strategy: stateful (shared thread)
);
```

**AgentExtensions.cs:67-85** - AsAIFunction implementation

---

### Phase 2: The Wrapper Function Creation

Inside `AsAIFunction()`:

```csharp
public static AIFunction AsAIFunction(this AIAgent agent, ...)
{
    // 1. Create wrapper delegate (closure captures agent + thread)
    [Description("Invoke an agent to retrieve some information.")]
    async Task<string> InvokeAgentAsync(
        [Description("Input query to invoke the agent.")] string query,
        CancellationToken cancellationToken)
    {
        // This closure captures:
        // - agent (the Conversation instance)
        // - thread (the optional AgentThread)

        var response = await agent.RunAsync(
            query,  // Simple string input
            thread: thread,  // Use captured thread
            cancellationToken: cancellationToken
        );

        return response.Text;  // Extract text response
    }

    // 2. Configure metadata
    options ??= new();
    options.Name ??= SanitizeAgentName(agent.Name);  // "CodingAgent"
    options.Description ??= agent.Description;  // SystemInstructions

    // 3. Use AIFunctionFactory to create AIFunction from delegate
    return AIFunctionFactory.Create(InvokeAgentAsync, options);
    //                         ↑
    //                         This does the heavy lifting!
}
```

**Key Points:**
- **Closure Capture:** `agent` and `thread` are captured in the wrapper's closure
- **Simplified Signature:** From complex agent to `(string) → string`
- **Delegation Pattern:** All complexity hidden behind simple interface

---

### Phase 3: AIFunctionFactory Processing

#### Step 3.1: Extract Method Metadata

```csharp
// AIFunctionFactory.cs:111-116
public static AIFunction Create(Delegate method, AIFunctionFactoryOptions? options)
{
    return ReflectionAIFunction.Build(
        method.Method,    // MethodInfo for InvokeAgentAsync
        method.Target,    // Closure with captured variables
        options
    );
}
```

#### Step 3.2: Build Function Descriptor

```csharp
// AIFunctionFactory.cs:630-705
private ReflectionAIFunctionDescriptor(DescriptorKey key, JsonSerializerOptions opts)
{
    // Analyze parameters
    ParameterInfo[] parameters = key.Method.GetParameters();
    // For InvokeAgentAsync: [string query, CancellationToken ct]

    // Create parameter marshallers
    for (int i = 0; i < parameters.Length; i++)
    {
        ParameterMarshallers[i] = GetParameterMarshaller(opts, options, parameters[i]);
    }
    // Result:
    // - Marshaller for 'query': extracts from AIFunctionArguments["query"]
    // - Marshaller for 'ct': binds to invocation cancellation token

    // Generate JSON Schema
    JsonSchema = AIJsonUtilities.CreateFunctionJsonSchema(
        key.Method,
        /* ... */
    );
    // Result:
    // {
    //   "type": "object",
    //   "properties": {
    //     "query": {
    //       "type": "string",
    //       "description": "Input query to invoke the agent."
    //     }
    //   },
    //   "required": ["query"]
    // }

    // Create return marshaller (for Task<string>)
    ReturnParameterMarshaller = GetReturnParameterMarshaller(key, opts, out Type? returnType);
    // Result: Awaits task, extracts string, serializes to JsonElement
}
```

**AIFunctionFactory.cs:630-705** - Descriptor creation with caching

#### Step 3.3: Create ReflectionAIFunction Instance

```csharp
// AIFunctionFactory.cs:518-533
private ReflectionAIFunction(
    ReflectionAIFunctionDescriptor functionDescriptor,
    object? target,  // The closure
    AIFunctionFactoryOptions options)
{
    FunctionDescriptor = functionDescriptor;
    Target = target;  // Stores closure with agent + thread
    AdditionalProperties = options.AdditionalProperties ?? /* ... */;
}
```

**Result:** We now have an `AIFunction` that:
- Has name: "CodingAgent"
- Has description: "Delegate coding tasks..."
- Has JSON schema describing the `query` parameter
- Has precomputed marshallers for parameters and return value
- Has a closure containing the actual agent and thread

---

### Phase 4: Registration with Orchestrator

```csharp
var orchestratorAgent = new AgentBuilder(orchestratorConfig)
    .WithTool(codingTool)  // Register the agent-as-function
    .WithTool(researchTool)
    .Build();
```

**What happens:**
- `codingTool` (AIFunction) is added to orchestrator's tools list
- AI model sees it as a normal function in the schema
- Model doesn't know it's calling a full agent!

---

### Phase 5: Invocation - User Request

**User Input:** "Create an Express.js server with a /hello endpoint"

#### Step 5.1: Orchestrator Receives Request

```csharp
var orchestratorConversation = new Conversation(orchestratorAgent);

await foreach (var update in orchestratorConversation.RunStreamingAsync(
    [new ChatMessage(ChatRole.User, "Create an Express.js server...")]))
{
    // Process streaming updates
}
```

**Orchestrator's Agent Loop Starts:**
- Iteration 0: LLM receives user message + available tools
- LLM sees: `CodingAgent(query: string)`, `ResearchAgent(query: string)`
- LLM decides: "This is a coding task, call CodingAgent"

#### Step 5.2: Orchestrator Calls Function

**LLM Response:**
```json
{
  "role": "assistant",
  "content": null,
  "tool_calls": [
    {
      "id": "call_abc123",
      "type": "function",
      "function": {
        "name": "CodingAgent",
        "arguments": "{\"query\": \"Create an Express.js server with a /hello endpoint that responds with 'Hello World!'\"}"
      }
    }
  ]
}
```

**Agent.cs:805-806** - Tool execution via ToolScheduler

#### Step 5.3: Function Invocation (The Magic!)

```csharp
// AIFunctionFactory.cs:547-594
protected override async ValueTask<object?> InvokeCoreAsync(
    AIFunctionArguments arguments,  // { "query": "Create Express server..." }
    CancellationToken cancellationToken)
{
    // 1. Marshal parameters
    object?[] args = new object?[ParameterMarshallers.Length];
    for (int i = 0; i < args.Length; i++)
    {
        args[i] = ParameterMarshallers[i](arguments, cancellationToken);
    }
    // Result:
    // args[0] = "Create an Express.js server..."  (from arguments["query"])
    // args[1] = cancellationToken  (from invocation parameter)

    // 2. Invoke via reflection
    var result = ReflectionInvoke(FunctionDescriptor.Method, Target, args);
    // This calls: InvokeAgentAsync("Create Express server...", ct)
    //
    // Which executes:
    //   await agent.RunAsync(query, thread: thread, ct)
    //
    // Which triggers the ENTIRE CodingAgent agentic loop!

    // 3. Marshal return value
    return await FunctionDescriptor.ReturnParameterMarshaller(result, cancellationToken);
    // Awaits Task<string>, serializes to JsonElement
}
```

#### Step 5.4: CodingAgent Execution (Nested Agent Loop!)

Inside the wrapper function:
```csharp
var response = await agent.RunAsync(query, thread: thread, ct);
```

This triggers **Conversation.cs:114-177** which calls:
```csharp
var response = await _agent.GetResponseAsync(
    targetThread.Messages,  // All messages in thread
    chatOptions,
    cancellationToken
);
```

Which triggers **Agent.cs:212-225** - the full agentic loop:
```
CodingAgent Iteration 0:
  LLM receives: "Create an Express.js server..."
  LLM sees tools: [ReadFile, WriteFile, ExecuteShell, SearchFiles, ...]
  LLM decides: "I need to create server.js"
  LLM calls: WriteFile(path: "server.js", content: "const express = ...")
  Tool executes: ✅ File written

CodingAgent Iteration 1:
  LLM receives: Tool result: "File server.js created successfully"
  LLM decides: "Need to install dependencies"
  LLM calls: ExecuteShell(command: "npm init -y && npm install express")
  Tool executes: ✅ Dependencies installed

CodingAgent Iteration 2:
  LLM receives: Tool result: "Dependencies installed"
  LLM decides: "Task complete, provide summary"
  LLM returns text: "I've created an Express.js server at server.js..."
  ✅ No more tool calls - loop exits
```

**Result:**
```csharp
return response.Text;  // "I've created an Express.js server..."
```

This string is serialized to JSON and returned as the function result!

#### Step 5.5: Orchestrator Receives Tool Result

```csharp
// Back in orchestrator's agent loop (Agent.cs:805-830)
var toolResultMessage = await _toolScheduler.ExecuteToolsAsync(...);
// Tool result:
// {
//   "tool_call_id": "call_abc123",
//   "role": "tool",
//   "content": "I've created an Express.js server at server.js with a /hello endpoint..."
// }

// Add to orchestrator's message history
currentMessages.Add(toolResultMessage);
```

#### Step 5.6: Orchestrator Synthesizes Response

**Orchestrator Iteration 1:**
```
LLM receives:
  [User: "Create Express server"]
  [Assistant: tool_call CodingAgent]
  [Tool: "I've created an Express server..."]

LLM generates:
  "I've delegated this task to our coding specialist, and it's been completed!

   ✅ Created server.js with Express
   ✅ Set up /hello endpoint
   ✅ Installed dependencies

   You can run it with: node server.js"

✅ No more tool calls - done
```

**User receives final response!**

---

## Thread Management Strategies

### Strategy 1: Stateless (thread: null)

```csharp
var codingTool = codingConversation.AsAIFunction(options, thread: null);
```

**Behavior:**
```
Call 1: "Create server.js"
  → New thread created
  → Thread: [User: "Create server.js", Assistant: "Created!"]
  → Thread discarded after return

Call 2: "Add logging to server.js"
  → New thread created
  → Thread: [User: "Add logging..."]  ❌ No memory of server.js
  → CodingAgent: "What file are you referring to?"
```

**Use Cases:**
- ✅ Independent tasks
- ✅ No context needed between calls
- ✅ Maximum isolation
- ❌ Multi-step workflows

---

### Strategy 2: Stateful (shared thread)

```csharp
var sharedThread = codingConversation.Thread;
var codingTool = codingConversation.AsAIFunction(options, thread: sharedThread);
```

**Behavior:**
```
Call 1: "Create server.js"
  → Uses sharedThread
  → Thread: [User: "Create server.js", Assistant: "Created!"]
  → Thread persists

Call 2: "Add logging to it"
  → Uses SAME sharedThread
  → Thread: [User: "Create server.js", Assistant: "Created!",
             User: "Add logging", Assistant: "Added logging to server.js!"]
  → ✅ CodingAgent remembers context!
```

**Use Cases:**
- ✅ Multi-step workflows
- ✅ Iterative refinement
- ✅ Context preservation
- ⚠️ All users/conversations share same history (usually not desired)

---

### Strategy 3: Per-Conversation Thread (Recommended)

```csharp
// Custom orchestration plugin
public class MultiAgentOrchestrationPlugin
{
    // Map: conversationId → agent thread
    private Dictionary<string, ConversationThread> _codingAgentThreads = new();

    [AIFunction]
    [Description("Delegate coding tasks")]
    public async Task<string> CodingAgent(
        [Description("Coding task")] string query,
        CancellationToken ct)
    {
        // Get conversation ID from AsyncLocal context
        var conversationId = ConversationContext.Current.ConversationId;

        // Get or create thread for THIS conversation
        if (!_codingAgentThreads.ContainsKey(conversationId))
        {
            _codingAgentThreads[conversationId] = new ConversationThread();
        }

        var thread = _codingAgentThreads[conversationId];

        // Run with conversation-scoped thread
        var response = await _codingConversation.RunAsync(
            query,
            thread: thread,  // ← This user's thread
            ct
        );

        return response.Text;
    }
}
```

**Behavior:**
```
User A's Conversation:
  Call 1: "Create server.js" → Uses User A's thread
  Call 2: "Modify it" → ✅ Uses User A's thread (has context)

User B's Conversation (concurrent):
  Call 1: "Create app.js" → Uses User B's thread
  Call 2: "Add routes" → ✅ Uses User B's thread (isolated from User A)
```

**Use Cases:**
- ✅ Production multi-user systems
- ✅ Per-user context
- ✅ Concurrent conversations
- ✅ Proper isolation

**Implementation:** Custom plugin with thread management logic

---

## Performance Considerations

### Caching: ReflectionAIFunctionDescriptor

```csharp
// AIFunctionFactory.cs:603
private static readonly ConditionalWeakTable<JsonSerializerOptions,
    ConcurrentDictionary<DescriptorKey, ReflectionAIFunctionDescriptor>> _descriptorCache = new();
```

**What's Cached:**
- Parameter marshallers (how to extract arguments)
- Return marshallers (how to serialize results)
- JSON schema (function signature for AI models)
- Method metadata

**Performance Impact:**
- ✅ First call: ~1-2ms for reflection + schema generation
- ✅ Subsequent calls: <0.1ms (cached lookup)
- ✅ Per (MethodInfo, Options) combination

---

### Token Usage Implications

**Example: Orchestrator with 3 agent-as-function tools**

**Without Agent-as-Function (all tools inline):**
```
Orchestrator tools in prompt:
├─ ReadFile(path: string) → string
├─ WriteFile(path: string, content: string) → void
├─ ExecuteShell(command: string) → string
├─ SearchFiles(pattern: string) → string[]
├─ GitStatus() → string
├─ GitCommit(message: string) → void
├─ WebSearch(query: string) → string
├─ AnalyzeCode(path: string) → object
└─ ... (100+ tools total)

Token usage: ~5,000 tokens for tool schemas alone!
```

**With Agent-as-Function (encapsulated):**
```
Orchestrator tools in prompt:
├─ CodingAgent(query: string) → string
│   Description: "Expert coding assistant with file operations, git, analysis"
├─ ResearchAgent(query: string) → string
│   Description: "Web research and information gathering"
└─ AnalyticsAgent(query: string) → string
    Description: "Data analysis and visualization"

Token usage: ~500 tokens for tool schemas
```

**Savings: 90% reduction in tool schema tokens!**

**Trade-off:**
- ❌ Nested agent invocation adds latency (another LLM call)
- ✅ But orchestrator can make better decisions with simpler tool set
- ✅ Overall system is more maintainable and scalable

---

## Usage Patterns

### Pattern 1: Simple Delegation

```csharp
// Orchestrator just routes to appropriate specialist
var orchestratorConfig = new AgentConfig
{
    SystemInstructions = @"You are a task router.
Analyze user requests and delegate to the appropriate specialist:
- CodingAgent: For code, files, shell commands
- ResearchAgent: For web research and information"
};

var orchestrator = new AgentBuilder(orchestratorConfig)
    .WithTool(codingTool)
    .WithTool(researchTool)
    .Build();
```

---

### Pattern 2: Sequential Multi-Agent Workflow

```csharp
var orchestratorConfig = new AgentConfig
{
    SystemInstructions = @"You orchestrate complex workflows.

Example workflow for 'Build a todo app':
1. Call ResearchAgent('Best practices for todo apps')
2. Call CodingAgent('Create todo app following best practices: {research results}')
3. Call CodingAgent('Write tests for the todo app')
4. Synthesize final report for user"
};
```

---

### Pattern 3: Parallel Multi-Agent (with aggregation)

```csharp
var orchestratorConfig = new AgentConfig
{
    SystemInstructions = @"You coordinate parallel research.

For 'Research topic X':
1. Call ResearchAgent('Academic papers on X')
2. Call ResearchAgent('Industry applications of X')
3. Call ResearchAgent('Latest news on X')
4. Aggregate and synthesize all results into coherent report"
};
```

**Note:** Current architecture executes tools **sequentially** (not parallel)
- See `Agent.cs:805` - `await _toolScheduler.ExecuteToolsAsync(...)`
- Tools execute one-by-one in agent turn
- Parallel execution would require architectural changes

---

### Pattern 4: Iterative Refinement

```csharp
// Use STATEFUL agent (shared thread) for iterative tasks
var sharedThread = codingConversation.Thread;
var codingTool = codingConversation.AsAIFunction(
    options,
    thread: sharedThread  // ← Preserves context
);

var orchestratorConfig = new AgentConfig
{
    SystemInstructions = @"You iteratively refine outputs.

For 'Create polished code':
1. Call CodingAgent('Create initial implementation')
2. Call CodingAgent('Add error handling')  ← Remembers previous code!
3. Call CodingAgent('Add logging')  ← Still has context
4. Call CodingAgent('Write documentation')  ← Full context preserved"
};
```

---

## Implementation Details

### Name Sanitization

```csharp
// AgentExtensions.cs:94-98
private static string? SanitizeAgentName(string? agentName)
{
    return agentName is null
        ? agentName
        : InvalidNameCharsRegex().Replace(agentName, "_");
}

// AgentExtensions.cs:103-104
[GeneratedRegex("[^0-9A-Za-z]+")]
private static partial Regex InvalidNameCharsRegex();
```

**Purpose:** AI models and some systems require alphanumeric-only function names

**Examples:**
- "Coding-Agent" → "Coding_Agent"
- "Research Agent (v2)" → "Research_Agent__v2_"
- "Analytics/Data Agent" → "Analytics_Data_Agent"

---

### Parameter Binding Rules

From `AIFunctionFactory.cs:630-805`, parameters are handled specially:

1. **CancellationToken**: Always bound to invocation token (excluded from schema)
2. **IServiceProvider**: Bound from `AIFunctionArguments.Services` (excluded from schema)
3. **AIFunctionArguments**: Bound to raw arguments (excluded from schema)
4. **Everything else**: Extracted from arguments dictionary, included in schema

**For AsAIFunction wrapper:**
```csharp
async Task<string> InvokeAgentAsync(
    string query,             // ← From AIFunctionArguments["query"]
    CancellationToken ct)     // ← From invocation token
```

---

### Return Value Marshaling

```csharp
// AIFunctionFactory.cs:875-999
private static Func<object?, CancellationToken, ValueTask<object?>>
    GetReturnParameterMarshaller(...)
{
    // For Task<string>:
    return async (taskObj, cancellationToken) =>
    {
        await ((Task)taskObj);  // Await the task
        object? result = GetTaskResult(taskObj);  // Extract string
        return SerializeToJsonElement(result);  // Convert to JsonElement
    };
}
```

**Result:** String → JsonElement → Passed to AI model as tool result

---

## Comparison with IChatClient

### Why AsAIFunction Requires AIAgent (not IChatClient)

| Feature | IChatClient | AIAgent | Required for AsAIFunction? |
|---------|-------------|---------|---------------------------|
| **State Management** | ❌ Stateless | ✅ Has AgentThread | ✅ **YES** - Need conversation context |
| **Identity** | ❌ No name/description | ✅ Name & Description | ✅ **YES** - Used for function metadata |
| **Thread Management** | ❌ Manual | ✅ Automatic | ✅ **YES** - Preserve context across calls |
| **Serialization** | ❌ N/A | ✅ Thread.Serialize() | ⚠️ Optional - For persistence |
| **Multi-conversation** | ⚠️ Must track externally | ✅ One agent, many threads | ✅ **YES** - Per-user isolation |

### Attempting AsAIFunction with IChatClient (FAILS)

```csharp
// ❌ This would not work:
public static AIFunction AsAIFunction(this IChatClient chatClient)
{
    var history = new List<ChatMessage>();  // ← Shared mutable state!

    async Task<string> InvokeAsync(string query, CancellationToken ct)
    {
        history.Add(new ChatMessage(ChatRole.User, query));
        var response = await chatClient.GetResponseAsync(history, ct);
        history.AddRange(response.Messages);
        return response.Text;
    }

    return AIFunctionFactory.Create(InvokeAsync);
}

// Problems:
// 1. ⚠️ Thread-unsafe: Concurrent calls corrupt history
// 2. ❌ No identity: Can't generate meaningful function name/description
// 3. ❌ No serialization: Can't persist state
// 4. ❌ Memory leak: History grows forever
// 5. ❌ No per-user isolation: All callers share same history!
```

---

## Advanced Scenarios

### Scenario 1: Agent Calling Itself (Recursion)

**Question:** Can an agent have itself as a tool?

```csharp
var agent = CreateAgent();
var conversation = new Conversation(agent);
var selfTool = conversation.AsAIFunction(options, thread: null);

// ⚠️ Register agent as its own tool
var recursiveAgent = new AgentBuilder(config)
    .WithTool(selfTool)  // Agent can call itself!
    .Build();
```

**Answer:** Yes, but be careful!

**Protections Needed:**
- ✅ MaxAgenticIterations prevents infinite loops
- ⚠️ Stateless thread (thread: null) prevents context explosion
- ⚠️ Cost limits (each recursive call = full agent execution)

**Use Case:** Meta-reasoning, self-reflection, planning

---

### Scenario 2: Agent Mesh (All agents can call each other)

```csharp
var codingTool = codingConversation.AsAIFunction(...);
var researchTool = researchConversation.AsAIFunction(...);
var analyticsTool = analyticsConversation.AsAIFunction(...);

// Each agent has access to the other agents!
var codingAgent = new AgentBuilder(codingConfig)
    .WithTool(researchTool)     // Coding can call research
    .WithTool(analyticsTool)    // Coding can call analytics
    .Build();

var researchAgent = new AgentBuilder(researchConfig)
    .WithTool(codingTool)       // Research can call coding
    .WithTool(analyticsTool)    // Research can call analytics
    .Build();

var analyticsAgent = new AgentBuilder(analyticsConfig)
    .WithTool(codingTool)       // Analytics can call coding
    .WithTool(researchTool)     // Analytics can call research
    .Build();
```

**Benefits:**
- ✅ Maximum flexibility
- ✅ Emergent collaboration patterns
- ✅ No single orchestrator bottleneck

**Risks:**
- ⚠️ Circular calling (A → B → A → B...)
- ⚠️ Explosion of complexity
- ⚠️ Hard to reason about behavior
- ⚠️ Cost can spiral

**Mitigation:**
- Use `MaxAgenticIterations` as circuit breaker
- Monitor for circular patterns
- Consider adding call depth tracking

---

### Scenario 3: Conditional Tool Availability

```csharp
public class DynamicToolOrchestrationPlugin
{
    private Dictionary<string, AIFunction> _availableAgents = new();

    [AIFunction]
    [Description("Get appropriate specialist for task type")]
    public async Task<string> GetSpecialist(
        [Description("Task type: coding, research, analytics")] string taskType,
        [Description("Task details")] string task,
        CancellationToken ct)
    {
        // Dynamically choose which agent to invoke
        AIFunction? agent = taskType.ToLower() switch
        {
            "coding" => _availableAgents["coding"],
            "research" => _availableAgents["research"],
            "analytics" => _availableAgents["analytics"],
            _ => throw new ArgumentException($"Unknown task type: {taskType}")
        };

        // Invoke the chosen agent
        var result = await agent.InvokeAsync(
            new AIFunctionArguments { ["query"] = task },
            ct
        );

        return result?.ToString() ?? "No response";
    }
}
```

**Use Case:** Dynamic routing, A/B testing different agents, cost optimization

---

### Scenario 4: Agent Composition (Agent of Agents)

```csharp
// Meta-orchestrator that routes to specialized orchestrators
var frontendOrchestrator = CreateFrontendOrchestrator();  // React, CSS, HTML
var backendOrchestrator = CreateBackendOrchestrator();    // API, DB, Auth
var devopsOrchestrator = CreateDevOpsOrchestrator();      // Docker, CI/CD, Deploy

var frontendTool = frontendOrchestrator.AsAIFunction(...);
var backendTool = backendOrchestrator.AsAIFunction(...);
var devopsTool = devopsOrchestrator.AsAIFunction(...);

var metaOrchestrator = new AgentBuilder(metaConfig)
    .WithTool(frontendTool)   // Each tool is itself an orchestrator!
    .WithTool(backendTool)
    .WithTool(devopsTool)
    .Build();

// Meta-orchestrator → Backend orchestrator → Database specialist agent
//                                          → API specialist agent
//                                          → Auth specialist agent
```

**Hierarchy:**
```
Meta-Orchestrator
├─ Frontend Orchestrator
│  ├─ React Specialist
│  ├─ CSS Specialist
│  └─ HTML Specialist
├─ Backend Orchestrator
│  ├─ Database Specialist
│  ├─ API Specialist
│  └─ Auth Specialist
└─ DevOps Orchestrator
   ├─ Docker Specialist
   ├─ CI/CD Specialist
   └─ Deploy Specialist
```

**Benefits:**
- ✅ Hierarchical organization
- ✅ Each level has manageable complexity
- ✅ Easy to add new specialists

**Trade-offs:**
- ⚠️ Multiple LLM calls (latency)
- ⚠️ Context may be lost at boundaries
- ⚠️ Debugging complexity

---

## References

### Core Implementation Files

| File | Lines | Purpose |
|------|-------|---------|
| `AgentExtensions.cs` | 67-109 | `AsAIFunction()` implementation |
| `AIFunctionFactory.cs` | 111-1219 | Function creation from delegates |
| `AIFunction.cs` | 12-76 | Base abstraction for functions |
| `AIFunctionFactoryOptions.cs` | 18-155 | Configuration options |
| `Conversation.cs` | 14-525 | AIAgent wrapper with thread |
| `ConversationThread.cs` | 10-285 | State container implementation |
| `Agent.cs` | 14-1200+ | Agent with agentic loop |
| `AIAgent.cs` | 23-355 | Microsoft's base agent abstraction |

### Key Concepts

- **Closure Capture:** `agent` and `thread` captured in wrapper function
- **Reflection-based Invocation:** `ReflectionAIFunction.InvokeCoreAsync`
- **Parameter Marshalling:** JSON deserialization of arguments
- **Return Marshalling:** JSON serialization of results
- **Descriptor Caching:** Performance optimization
- **Thread Strategies:** Stateless, stateful, per-conversation

### Related Documentation

- `docs/HUMAN_IN_THE_LOOP_LIMITATION.md` - Nested clarification edge case
- `docs/orchestration-framework.md` - Multi-agent patterns
- `docs/plugin-scoping.md` - Tool visibility optimization
- `Conversation/CHAT_REDUCTION.md` - History management

### External References

- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/dotnet/ai/)
- [Microsoft Agents AI Framework](https://github.com/microsoft/agents)
- [AIFunction Design](https://learn.microsoft.com/dotnet/ai/quickstarts/use-function-calling)

---

## Glossary

- **AIAgent:** Microsoft's base abstraction for stateful agents (with threads)
- **AgentThread:** State container for conversation history and metadata
- **AIFunction:** Callable tool that AI models can invoke
- **AIFunctionFactory:** Reflection-based factory for creating AIFunctions from delegates
- **AsAIFunction():** Extension method that wraps an agent as a callable function
- **Agentic Loop:** Iterative LLM execution with tool calling (Agent Turns)
- **Agent Turn:** Single iteration within agentic loop
- **Message Turn:** Complete user message → agent response cycle
- **Closure:** Anonymous function that captures variables from outer scope
- **Marshalling:** Converting between data formats (JSON ↔ .NET objects)
- **ReflectionAIFunction:** Concrete AIFunction implementation using reflection
- **Tool Scheduler:** Component that executes tool calls during agent turns

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-13 | 1.0 | Initial documentation of AsAIFunction architecture |

---

## Future Enhancements

### Planned
- [ ] Parallel tool execution within agent turns
- [ ] Agent-to-agent streaming (progressive results)
- [ ] Built-in circuit breakers for recursive calls
- [ ] Agent mesh topology visualization
- [ ] Performance profiling for nested agents

### Under Consideration
- [ ] Agent versioning and A/B testing
- [ ] Automatic agent selection via embeddings
- [ ] Agent reputation/quality scoring
- [ ] Cost tracking per agent invocation
- [ ] Agent result caching strategies

---

## Acknowledgments

The agent-as-function pattern draws inspiration from:
- **Microsoft Agents AI Framework:** AIAgent and AgentThread abstractions
- **Microsoft.Extensions.AI:** IChatClient and AIFunction interfaces
- **LangChain:** Agent composition patterns
- **AutoGen:** Multi-agent orchestration
- **Function calling protocols:** OpenAI, Anthropic, Google Gemini

This architecture enables **true multi-agent systems** where agents are first-class citizens that can call each other, compose hierarchically, and maintain independent state while participating in complex workflows.
