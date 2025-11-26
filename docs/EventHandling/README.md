# Event Handling Documentation

Welcome to the HPD-Agent Event Handling system documentation. This guide covers the **Observer Pattern** event handling architecture that enables real-time monitoring and interaction with agent execution.

---

## 📚 Documentation Structure

| Guide | Purpose | Audience |
|-------|---------|----------|
| **[User Guide](./USER_GUIDE.md)** | Learn how to use event handling in your applications | Developers using HPD-Agent |
| **[API Reference](./API_REFERENCE.md)** | Complete reference of all 58 event types and APIs | Developers implementing event handlers |
| **[Architecture](./ARCHITECTURE.md)** | Deep dive into event system internals | Framework developers, advanced users |

---

## 🚀 Quick Start

### 1. Use Built-in ConsoleEventHandler

```csharp
var handler = new ConsoleEventHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();
handler.SetAgent(agent);

await foreach (var _ in agent.RunAsync(messages, thread: thread))
{
    // Handler displays everything automatically
}
```

### 2. Create Custom Event Handler

```csharp
public class MyHandler : IEventHandler
{
    public bool ShouldProcess(InternalAgentEvent evt)
        => evt is InternalTextDeltaEvent or InternalToolCallStartEvent;

    public async Task OnEventAsync(InternalAgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case InternalTextDeltaEvent textDelta:
                Console.Write(textDelta.Text);
                break;
            case InternalToolCallStartEvent toolStart:
                Console.WriteLine($"\n🔧 {toolStart.Name}");
                break;
        }
    }
}
```

### 3. Register with Agent

```csharp
var agent = new AgentBuilder(config)
    .WithObserver(new MyHandler())
    .BuildCoreAgent();
```

---

## 🎯 Key Features

### Observer Pattern
- **Write Once, Reuse Everywhere** - Share handlers across unlimited agents
- **Fire-and-Forget** - Event handling doesn't block agent execution
- **Circuit Breaker** - Failing handlers auto-disable after 10 failures

### 58 Event Types
- **Message Turn Events** - Track entire user interactions
- **Agent Turn Events** - Track individual LLM calls
- **Content Events** - Stream text content in real-time
- **Reasoning Events** - Display reasoning tokens (o1, Gemini, DeepSeek-R1)
- **Tool Events** - Monitor tool call lifecycle
- **Middleware Events** - Handle permissions, continuations, clarifications
- **Observability Events** - Internal diagnostics and metrics

### Bidirectional Communication
- **Permission Requests** - Prompt users for function approvals
- **Continuation Requests** - Ask to extend iteration limits
- **Clarification Requests** - Get additional user input during execution

### Multi-Agent Support
- **Event Bubbling** - SubAgent events automatically flow to orchestrator
- **Execution Context** - Full attribution (agent name, depth, parent)
- **Selective Filtering** - Process only root or SubAgent events

---

## 📖 Documentation Guide

### For New Users
1. Start with **[User Guide](./USER_GUIDE.md)** - Learn core concepts and patterns
2. Review **[API Reference](./API_REFERENCE.md)** - Understand available event types
3. Study built-in handlers:
   - [ConsoleEventHandler](../../HPD-Agent/Observability/ConsoleEventHandler.cs)
   - [LoggingEventObserver](../../HPD-Agent/Observability/LoggingEventObserver.cs)
   - [TelemetryEventObserver](../../HPD-Agent/Observability/TelemetryEventObserver.cs)

### For Advanced Users
1. Read **[Architecture](./ARCHITECTURE.md)** - Understand system internals
2. Explore **[BidirectionalEventCoordinator](../../HPD-Agent/Agent/AgentCore.cs#L6300)** - Core implementation
3. Review **[Event Definitions](../../HPD-Agent/Agent/AgentCore.cs#L6368)** - All event type definitions

---

## 🔍 Common Use Cases

### Console Applications
Use the built-in `ConsoleEventHandler` for interactive console apps:

```csharp
var handler = new ConsoleEventHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();
handler.SetAgent(agent);
```

**Features:**
- Streaming text with color coding
- Reasoning tokens (o1, Gemini, DeepSeek-R1)
- Tool calls with visual feedback
- Interactive permissions (Allow/Forever/Deny/Never)
- Interactive continuations (Yes/No)
- Iteration counter

### Web Applications
Create custom handlers for real-time UI updates:

```csharp
public class WebSocketHandler : IEventHandler
{
    private readonly IHubContext<AgentHub> _hubContext;

    public async Task OnEventAsync(InternalAgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case InternalTextDeltaEvent textDelta:
                await _hubContext.Clients.All.SendAsync("TextChunk", textDelta.Text, ct);
                break;
            case InternalToolCallStartEvent toolStart:
                await _hubContext.Clients.All.SendAsync("ToolStarted", toolStart.Name, ct);
                break;
        }
    }
}
```

### Telemetry & Monitoring
Use the built-in `TelemetryEventObserver` for OpenTelemetry integration:

```csharp
var agent = new AgentBuilder(config)
    .WithTelemetry() // Auto-registers TelemetryEventObserver
    .BuildCoreAgent();
```

**Features:**
- OpenTelemetry traces, spans, metrics
- Distributed tracing across agent boundaries
- Custom metrics (tool calls, iterations, etc.)
- Error tracking with stack traces

### Debugging & Development
Use the built-in `LoggingEventObserver` for structured logging:

```csharp
var agent = new AgentBuilder(config)
    .WithLogging() // Auto-registers LoggingEventObserver
    .BuildCoreAgent();
```

**Features:**
- Structured logging with context
- Configurable log levels
- Tool call tracking
- Permission tracking

---

## 🆚 Comparison with Microsoft Agent Framework

| Feature | HPD-Agent | Microsoft Agent Framework |
|---------|-----------|---------------------------|
| Observer Pattern | ✅ Built-in | ❌ Manual event loops |
| Code Reusability | ✅ Write once, reuse everywhere | ❌ Duplicate 300+ lines per agent |
| Circuit Breaker | ✅ Auto-disable failing handlers | ❌ None |
| Bidirectional Events | ✅ Permissions, continuations, clarifications | ❌ None |
| Event Bubbling | ✅ Multi-agent support | ❌ None |
| Event Types | ✅ 58 comprehensive events | ⚠️ ~15 basic events |
| Built-in Handlers | ✅ Console, Logging, Telemetry | ❌ None |

**Result**: HPD-Agent's event system is **significantly more advanced** than Microsoft's agent framework.

---

## 🔗 Related Documentation

### Core Documentation
- [Agent Developer Documentation](../Agent-Developer-Documentation.md)
- [Quick Start Guide](../QUICK_START.md)
- [Architecture Overview](../ARCHITECTURE_OVERVIEW.md)

### Feature-Specific Guides
- [SubAgents](../SubAgents/README.md) - Nested agent architecture
- [Skills Architecture](../SKILLS_ARCHITECTURE.md) - Plugin and skill system
- [Permission System](../permissions/PERMISSION_SYSTEM.md) - Permission middleware

### Internal Architecture
- [Bidirectional Event Coordinator](../BIDIRECTIONAL_EVENT_COORDINATOR_ARCHITECTURE.md) - Core event infrastructure
- [Nested Agent Event Bubbling](../NESTED_AGENT_EVENT_BUBBLING_IMPLEMENTATION.md) - Multi-agent event flow

---

## 📊 Statistics

- **Total Event Types**: 58
- **Built-in Event Handlers**: 3 (Console, Logging, Telemetry)
- **Event Emission Points in AgentCore**: 52 locations
- **Event Overhead per Event**: ~0.3ms (negligible)
- **Memory per Event**: ~100 bytes
- **Code Reduction vs. Manual Loops**: 95% (300 lines → 15 lines)

---

## ❓ FAQ

### Q: What's the difference between IEventHandler and IAgentEventObserver?

**A**: They're identical - `IEventHandler` is just a friendly alias for `IAgentEventObserver`. Use whichever name you prefer.

### Q: How do I handle bidirectional events (permissions, continuations)?

**A**: Call `handler.SetAgent(agent)` after building the agent, then use `agent.SendMiddlewareResponse()` in your handler. See [User Guide - Bidirectional Events](./USER_GUIDE.md#pattern-4-bidirectional-handler-100-lines) for details.

### Q: What happens if my event handler throws an exception?

**A**: The agent logs the error and continues execution. After **10 consecutive failures**, the circuit breaker auto-disables your handler. It re-enables after **3 successful calls**.

### Q: Can I have multiple event handlers on one agent?

**A**: Yes! Call `.WithObserver()` multiple times. All handlers run in **parallel** via fire-and-forget.

### Q: How do I filter events to only process what I care about?

**A**: Implement `ShouldProcess()` to return `false` for events you don't want:

```csharp
public bool ShouldProcess(InternalAgentEvent evt)
    => evt is InternalTextDeltaEvent or InternalToolCallStartEvent;
```

### Q: Do events from SubAgents automatically bubble to the orchestrator?

**A**: Yes! Events automatically flow to parent agents with full `ExecutionContext` attribution (agent name, depth, parent).

---

## 🐛 Troubleshooting

### Events Not Being Received
1. Check `ShouldProcess()` returns `true` for your events
2. Verify observer was registered via `.WithObserver()`
3. Ensure you're consuming the `RunAsync()` stream

### Bidirectional Events Hanging
1. Ensure `SetAgent()` was called after building agent
2. Verify you're calling `SendMiddlewareResponse()` with correct ID
3. Check timeout settings (default: 5 minutes)

### Circuit Breaker Disabled Observer
1. Check logs for "Observer disabled due to circuit breaker"
2. Fix exception in your `OnEventAsync()` method
3. Circuit breaker re-enables after 3 successful calls

See [User Guide - Troubleshooting](./USER_GUIDE.md#troubleshooting) for more details.

---

## 📝 Contributing

To contribute to event handling documentation:

1. Update relevant guide (User Guide, API Reference, or Architecture)
2. Keep examples simple and focused
3. Include performance considerations
4. Add troubleshooting tips for common issues

---

**Last Updated**: 2025-01-27
**Version**: 2.0
**Status**: ✅ Production Ready
