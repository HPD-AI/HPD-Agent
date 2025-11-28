# HPD-Agent

**A production-ready .NET agent framework with event-driven architecture, durable execution, and intelligent tool management.**

[![NuGet](https://img.shields.io/nuget/v/HPD-Agent.svg)](https://www.nuget.org/packages/HPD-Agent/)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Proprietary-blue.svg)](LICENSE.md)

---

## What is HPD-Agent?

HPD-Agent is a comprehensive framework for building AI agents in .NET. It provides everything you need to create production-grade agents: an event-driven observer architecture, durable execution with crash recovery, intelligent token management, and multi-provider support.

```csharp
using HPD.Agent;

// Configure agent
var config = new AgentConfig
{
    Name = "AI Assistant",
    SystemInstructions = "You are a helpful assistant.",
    Provider = new ProviderConfig { ProviderKey = "openai", ModelName = "gpt-4o" }
};

// Build with your observer (console, web, custom)
var agent = new AgentBuilder(config)
    .WithObserver(myObserver)  // IAgentEventObserver - you decide how to present events
    .WithPlugin<FileSystemPlugin>()
    .WithPermissions()
    .BuildCoreAgent();

var thread = agent.CreateThread();
await foreach (var _ in agent.RunAsync("Hello!", thread)) { }
```

---

## Key Features

### Event-Driven Observer Architecture
- **Decoupled Display** - Observers handle UI, logging, telemetry independently
- **Real-Time Events** - Streaming text, tool calls, reasoning tokens, permissions
- **Interactive Middleware** - Permissions and continuations work via events
- **Flexible Presentation** - Same event stream, different observer behaviors (streaming vs buffered)

### Durable Execution
- **Automatic Checkpointing** - Agent state saved during execution, not just after
- **Mid-Run Recovery** - Resume from exact iteration after crashes
- **Pending Writes** - Partial failure recovery for parallel tool calls
- **Time-Travel Debugging** - Full checkpoint history with `FullHistory` mode

### Intelligent Token Management
- **Scoping System** - 87.5% token reduction by hierarchically organizing tools
- **History Reduction** - Automatic conversation compression with cache-aware optimization
- **Skills System** - Load specialized knowledge only when needed

### Multi-Provider Support
11 LLM providers out of the box:
> OpenAI • Anthropic • Azure OpenAI • Azure AI Inference • Google AI • Mistral • Ollama • HuggingFace • AWS Bedrock • OnnxRuntime • OpenRouter

### Protocol Support
- **A2A Protocol** - Agent-to-agent communication
- **AG-UI Protocol** - Real-time streaming for frontends
- **MCP Protocol** - Model Context Protocol integration

### Production Features
- **Permissions** - Function-level authorization with human-in-the-loop
- **Error Handling** - Provider-aware retry, circuit breakers, Retry-After headers
- **Observability** - OpenTelemetry integration, structured logging
- **Native AOT** - Full compatibility for ahead-of-time compilation

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  HPD-Agent Architecture                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Observers (IAgentEventObserver)                        │   │
│  │                                                          │   │
│  │  • Your implementation - console, web, mobile, etc.     │   │
│  │  • Receive all events (fire-and-forget)                 │   │
│  │  • Handle bidirectional events (permissions, continuations) │
│  │  • Multiple observers run in parallel                   │   │
│  └──────────────────────────┬──────────────────────────────┘   │
│                             │                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  AgentCore (Event-Driven Engine)                        │   │
│  │                                                          │   │
│  │  • Emits AgentEvent stream                      │   │
│  │  • Middleware pipeline (permissions, continuations)     │   │
│  │  • Internal checkpointing (fire-and-forget)             │   │
│  │  • Protocol-agnostic execution                          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                            │                                    │
│         ┌──────────────────┼──────────────────┐                │
│         ▼                  ▼                  ▼                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│  │ Plugins     │    │ Skills      │    │ Memory      │         │
│  │ (Tools)     │    │ (Knowledge) │    │ Systems     │         │
│  └─────────────┘    └─────────────┘    └─────────────┘         │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  ConversationThread + IConversationThreadStore          │   │
│  │                                                          │   │
│  │  • Full execution state (AgentLoopState)                │   │
│  │  • Message history with token tracking                  │   │
│  │  • Checkpoint metadata and versioning                   │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Providers (11+)                                         │   │
│  │  OpenAI • Anthropic • Azure • Google • Mistral • ...    │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Concepts

### Observer Pattern (IAgentEventObserver)
The core of HPD-Agent's architecture. Observers receive all agent events and decide how to present them.

```csharp
public interface IAgentEventObserver
{
    bool ShouldProcess(AgentEvent evt) => true;  // Filter events
    Task OnEventAsync(AgentEvent evt, CancellationToken ct);  // Handle events
}
```

```csharp
// Register any observer implementation
var agent = new AgentBuilder(config)
    .WithObserver(new MyWebSocketObserver())   // For web apps
    .WithObserver(new TelemetryObserver())     // For monitoring
    .BuildCoreAgent();
```

**Why event-driven?** Interactive middleware (permissions, continuations) requires real-time event flow. The observer pattern enables:
- **Streaming display** - Text appears as it's generated
- **Interactive prompts** - Permission requests mid-execution
- **Flexible presentation** - Same events, different UIs (console, web, silent)
- **Multiple observers** - Log, display, and track metrics simultaneously

### ConversationThread
The unit of conversation state. Contains messages, metadata, and execution state for durable execution.

```csharp
var thread = agent.CreateThread();

// Thread persists across runs
await foreach (var _ in agent.RunAsync("Hello", thread)) { }
await foreach (var _ in agent.RunAsync("Follow up", thread)) { }  // Continues conversation
```

### IConversationThreadStore
Persistence layer for threads with full checkpointing support.

```csharp
// Development
var store = new InMemoryConversationThreadStore();

// Production (implement your own)
var store = new PostgresConversationThreadStore(connectionString);
```

### Plugins
Tools for the agent, with source-generated schemas for Native AOT.

```csharp
[HPDPlugin]
public class FileSystemPlugin
{
    [HPDFunction("Read a file from disk")]
    public string ReadFile(string path) => File.ReadAllText(path);

    [HPDFunction("Write content to a file")]
    public void WriteFile(string path, string content) => File.WriteAllText(path, content);
}
```

### Skills
Package domain expertise with functions - load knowledge only when needed.

```csharp
var codeReviewSkill = Skill.Create(
    name: "code_review",
    description: "Activate for code review tasks",
    instructions: "Follow these code review guidelines...",
    references: new[] { "CodeAnalysisPlugin.AnalyzeCode", "GitPlugin.GetDiff" }
);
```

---

## Memory Systems

### Dynamic Memory
Agent-controlled working memory with automatic eviction.

```csharp
config.DynamicMemory = new DynamicMemoryConfig
{
    Enabled = true,
    MaxTokens = 4000,
    EvictionThreshold = 0.85  // Evict when 85% full
};
```

### Static Memory
Read-only knowledge base - RAG without vector databases.

```csharp
config.StaticMemory = new StaticMemoryConfig
{
    Enabled = true,
    Strategy = StaticMemoryStrategy.FullTextInjection,
    MaxTokens = 8000
};
```

### Plan Mode
Goal-oriented execution with step tracking.

```csharp
config.PlanMode = new PlanModeConfig
{
    Enabled = true,
    AutoCreatePlan = true
};
```

---

## Durable Execution

HPD-Agent checkpoints execution state internally, enabling recovery from any point:

```csharp
// Configure durable execution
var config = new AgentConfig
{
    ThreadStore = new InMemoryConversationThreadStore(),
    CheckpointFrequency = CheckpointFrequency.PerIteration,
    EnablePendingWrites = true  // Partial failure recovery
};

// First run - crashes at iteration 5
var thread = agent.CreateThread();
await agent.RunAsync(messages, thread);  // Checkpoints saved internally

// After restart - resume from iteration 5
var restored = await store.LoadThreadAsync(thread.Id);
if (restored?.ExecutionState != null)
{
    await agent.RunAsync(Array.Empty<ChatMessage>(), restored);  // Resumes!
}
```

**What gets checkpointed:**
- Full message history
- Current iteration number
- Expanded plugins/skills (scoping state)
- Circuit breaker state
- Pending writes (completed tool calls)
- Active history reduction state

---

## Documentation

### User Guides
- **[Quick Start](docs/QUICK_START.md)** - Get running in 5 minutes
- **[Conversation Architecture](docs/CONVERSATION_ARCHITECTURE.md)** - Thread and persistence model
- **[Skills Guide](docs/skills/SKILLS_GUIDE.md)** - Package domain expertise
- **[Permissions Guide](docs/permissions/PERMISSION_SYSTEM_GUIDE.md)** - Authorization and human-in-the-loop

### API Reference
- **[ConversationThread API](docs/ConversationThread-API-Reference.md)** - Thread operations
- **[Skills API](docs/skills/SKILLS_API_REFERENCE.md)** - Skill configuration
- **[Permissions API](docs/permissions/PERMISSION_SYSTEM_API.md)** - Permission management
- **[Event Handling API](docs/EventHandling/API_REFERENCE.md)** - Observability

### Architecture
- **[Architecture Overview](docs/ARCHITECTURE_OVERVIEW.md)** - System design
- **[Scoping System](docs/SCOPING_SYSTEM.md)** - Token reduction via tool hierarchy
- **[Durable Execution](docs/THREAD_SCOPED_DURABLE_EXECUTION.md)** - Checkpointing deep-dive
- **[Message Store Architecture](docs/MESSAGE_STORE_ARCHITECTURE.md)** - Storage internals

### Developer Guides
- **[Agent Developer Guide](docs/Agent-Developer-Documentation.md)** - Build agents
- **[Plugin Development](docs/PLUGIN_CLARIFICATION.md)** - Create plugins
- **[SubAgents](docs/SubAgents/ARCHITECTURE.md)** - Multi-agent patterns

---

## Example: Full-Featured Agent

```csharp
using HPD.Agent;

// Configuration
var config = new AgentConfig
{
    Name = "Code Review Assistant",
    SystemInstructions = "You are a senior software engineer assistant.",
    Provider = new ProviderConfig
    {
        ProviderKey = "anthropic",
        ModelName = "claude-sonnet-4-20250514"
    },
    MaxAgenticIterations = 25,
    DynamicMemory = new DynamicMemoryConfig
    {
        StorageDirectory = "./agent-memory",
        MaxTokens = 4000
    },
    Scoping = new ScopingConfig { Enabled = true }
};

// Build with observers
var agent = new AgentBuilder(config)
    .WithObserver(new WebSocketObserver(socket))  // Stream to frontend
    .WithObserver(new TelemetryObserver())        // Metrics & tracing
    .WithPlugin<FileSystemPlugin>()
    .WithPlugin<GitPlugin>()
    .WithPermissions()
    .WithLogging()
    .BuildCoreAgent();

var thread = agent.CreateThread();
await foreach (var _ in agent.RunAsync("Review the latest commit", thread)) { }
```

**Observer Implementation Example:**
```csharp
public class WebSocketObserver : IAgentEventObserver
{
    private readonly WebSocket _socket;

    public WebSocketObserver(WebSocket socket) => _socket = socket;

    public bool ShouldProcess(AgentEvent evt) =>
        evt is TextDeltaEvent or ToolCallStartEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        var payload = evt switch
        {
            TextDeltaEvent text => new { type = "text", data = text.Text },
            ToolCallStartEvent tool => new { type = "tool", name = tool.Name },
            _ => null
        };
        if (payload != null) await _socket.SendAsync(payload, ct);
    }
}
```

---

## Why HPD-Agent?

| Challenge | HPD-Agent Solution |
|-----------|-------------------|
| Agent crashes mid-execution | **Durable execution** - Resume from any iteration |
| Token limits with many tools | **Scoping** - 87.5% reduction via hierarchy |
| Long conversations | **History reduction** - Automatic compression |
| Production reliability | **Circuit breakers**, retries, observability |
| Multi-provider support | **11 providers** with unified API |
| Native AOT deployment | **100% compatible** |

---

## Getting Started

```bash
dotnet add package HPD-Agent
```

```csharp
using HPD.Agent;

// 1. Configure
var config = new AgentConfig
{
    Name = "Assistant",
    SystemInstructions = "You are a helpful assistant.",
    Provider = new ProviderConfig { ProviderKey = "openai", ModelName = "gpt-4o" }
};

// 2. Build with observer
var agent = new AgentBuilder(config)
    .WithObserver(myObserver)  // Your IAgentEventObserver implementation
    .BuildCoreAgent();

// 3. Run
var thread = agent.CreateThread();
await foreach (var _ in agent.RunAsync("Hello!", thread)) { }
```

See the **[Quick Start Guide](docs/QUICK_START.md)** for more.

---

## License

Proprietary. See [LICENSE.md](LICENSE.md) for details.

---

## Support

- **Documentation**: [docs.hpd-agent.com](https://docs.hpd-agent.com)
- **Email**: [support@hpd-agent.com](mailto:support@hpd-agent.com)

---

<div align="center">

**Production-Ready .NET Agent Framework**

*Event-Driven Architecture · Durable Execution · Intelligent Token Management*

[Quick Start](docs/QUICK_START.md) · [Documentation](docs/) · [Examples](examples/)

</div>
