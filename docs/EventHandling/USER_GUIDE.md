# Event Handling User Guide

## Table of Contents
- [Introduction](#introduction)
- [Why Use Event Handling?](#why-use-event-handling)
- [Quick Start](#quick-start)
- [Event Handler Complexity Spectrum](#event-handler-complexity-spectrum)
- [Built-in Event Handlers](#built-in-event-handlers)
- [Common Patterns](#common-patterns)
- [Best Practices](#best-practices)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

---

## Introduction

HPD-Agent features a powerful **Observer Pattern** event handling system that allows you to monitor and respond to all agent activities in real-time. The framework emits **58 different event types** covering everything from text streaming to tool calls, permissions, reasoning tokens, and middleware execution.

**Key Benefits:**
- **Write Once, Reuse Everywhere**: Create event handlers once and share them across unlimited agents
- **Fire-and-Forget**: Event handling doesn't block agent execution
- **Circuit Breaker Protection**: Failing handlers automatically disable after 10 failures
- **Selective Filtering**: Only process events you care about via `ShouldProcess()`
- **Bidirectional Events**: Handle interactive events (permissions, continuations, clarifications)

---

## Why Use Event Handling?

### **1. Eliminate Code Duplication**

**Before (Manual Event Loop)** - 300+ lines per agent:
```csharp
await foreach (var evt in agent.RunAsync(messages, thread: thread))
{
    switch (evt)
    {
        case TextDeltaEvent textDelta:
            Console.Write(textDelta.Text);
            break;
        case PermissionRequestEvent permReq:
            // 50+ lines of permission handling code
            break;
        case ToolCallStartEvent toolStart:
            // Tool call display logic
            break;
        // ... 20+ more event types
    }
}
```

**After (Observer Pattern)** - 15 lines total:
```csharp
var handler = new ConsoleEventHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();
handler.SetAgent(agent);

// Simple consumption - handler processes everything
await foreach (var _ in agent.RunAsync(messages, thread: thread))
{
    // Observer handles ALL events automatically!
}
```

**Result**: 95% code reduction, infinite reusability.

### **2. Real-Time Observability**

Monitor agent behavior for debugging, telemetry, or UI updates:

```csharp
public class DebugEventHandler : IEventHandler
{
    public bool ShouldProcess(AgentEvent evt) => true; // All events

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        _logger.LogDebug($"[{evt.GetType().Name}] at {DateTime.Now:HH:mm:ss.fff}");

        if (evt is ToolCallStartEvent toolStart)
        {
            _metrics.IncrementCounter($"tool.{toolStart.Name}.calls");
        }
    }
}
```

### **3. Bidirectional Interaction**

Handle events that require user responses (permissions, continuations):

```csharp
case PermissionRequestEvent permReq:
    var approved = await PromptUserAsync(
        $"Allow '{permReq.FunctionName}'? (Y/N)"
    );

    _agent.SendMiddlewareResponse(
        permReq.PermissionId,
        new PermissionResponseEvent(
            permReq.PermissionId,
            "Console",
            approved,
            approved ? null : "User denied"
        )
    );
    break;
```

### **4. Multi-Agent Scenarios**

Share one handler across multiple agents with different configurations:

```csharp
var sharedHandler = new ConsoleEventHandler();

var codeAgent = new AgentBuilder(codeConfig)
    .WithObserver(sharedHandler)
    .BuildCoreAgent();

var writeAgent = new AgentBuilder(writeConfig)
    .WithObserver(sharedHandler)
    .BuildCoreAgent();

sharedHandler.SetAgent(codeAgent); // Or writeAgent for permissions
```

---

## Quick Start

### **Step 1: Choose Your Interface**

HPD-Agent offers two equivalent interfaces (use whichever you prefer):

```csharp
// Option 1: IEventHandler (recommended - more intuitive)
public class MyHandler : IEventHandler
{
    public bool ShouldProcess(AgentEvent evt) { }
    public Task OnEventAsync(AgentEvent evt, CancellationToken ct) { }
}

// Option 2: IAgentEventObserver (backwards compatible)
public class MyHandler : IAgentEventObserver
{
    public bool ShouldProcess(AgentEvent evt) { }
    public Task OnEventAsync(AgentEvent evt, CancellationToken ct) { }
}
```

Both interfaces are **identical** - `IEventHandler` is just a friendly alias for `IAgentEventObserver`.

### **Step 2: Implement Event Filtering**

Use `ShouldProcess()` to filter events (improves performance):

```csharp
public bool ShouldProcess(AgentEvent evt)
{
    // Only process events you care about
    return evt is TextDeltaEvent
        or ToolCallStartEvent
        or PermissionRequestEvent;
}
```

### **Step 3: Handle Events**

Process events asynchronously in `OnEventAsync()`:

```csharp
public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
{
    switch (evt)
    {
        case TextDeltaEvent textDelta:
            Console.Write(textDelta.Text);
            break;

        case ToolCallStartEvent toolStart:
            Console.WriteLine($"\n🔧 Using tool: {toolStart.Name}");
            break;

        case PermissionRequestEvent permReq:
            await HandlePermissionAsync(permReq, ct);
            break;
    }
}
```

### **Step 4: Register with AgentBuilder**

```csharp
var handler = new MyHandler();

var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();

// If using bidirectional events (permissions, continuations)
handler.SetAgent(agent);
```

### **Step 5: Run the Agent**

```csharp
// Events are handled automatically - just consume the stream
await foreach (var _ in agent.RunAsync(messages, thread: thread))
{
    // Handler processes everything in the background
}
```

---

## Event Handler Complexity Spectrum

HPD-Agent supports a wide range of complexity from simple to production-grade:

### **Level 1: Simplest (3 Lines)**
```csharp
var agent = new AgentBuilder(config)
    .WithObserver(new ConsoleEventHandler())
    .BuildCoreAgent();
```

Uses the built-in `ConsoleEventHandler` for console display.

### **Level 2: Selective Filtering (20 Lines)**
```csharp
public class TextOnlyHandler : IEventHandler
{
    public bool ShouldProcess(AgentEvent evt)
        => evt is TextDeltaEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        if (evt is TextDeltaEvent textDelta)
            Console.Write(textDelta.Text);
    }
}
```

### **Level 3: Multi-Event Handler (50 Lines)**
```csharp
public class BasicTelemetryHandler : IEventHandler
{
    private readonly ILogger _logger;

    public bool ShouldProcess(AgentEvent evt)
        => evt is ToolCallStartEvent
            or ToolCallResultEvent
            or AgentTurnStartedEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case ToolCallStartEvent toolStart:
                _logger.LogInformation("Tool called: {Name}", toolStart.Name);
                break;
            case ToolCallResultEvent toolResult:
                _logger.LogInformation("Tool completed: {CallId}", toolResult.CallId);
                break;
            case AgentTurnStartedEvent turnStart:
                _logger.LogInformation("Iteration: {Iteration}", turnStart.Iteration);
                break;
        }
    }
}
```

### **Level 4: Bidirectional Handler (100 Lines)**
```csharp
public class InteractiveHandler : IEventHandler
{
    private AgentCore? _agent;

    internal void SetAgent(AgentCore agent) => _agent = agent;

    public bool ShouldProcess(AgentEvent evt)
        => evt is PermissionRequestEvent
            or ContinuationRequestEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case PermissionRequestEvent permReq:
                await HandlePermissionAsync(permReq, ct);
                break;
            case ContinuationRequestEvent contReq:
                await HandleContinuationAsync(contReq, ct);
                break;
        }
    }

    private async Task HandlePermissionAsync(
        PermissionRequestEvent permReq, CancellationToken ct)
    {
        Console.Write($"Allow '{permReq.FunctionName}'? (Y/N): ");
        var input = await Task.Run(() => Console.ReadLine(), ct);
        var approved = input?.ToLower() == "y";

        _agent?.SendMiddlewareResponse(
            permReq.PermissionId,
            new PermissionResponseEvent(
                permReq.PermissionId,
                "Console",
                approved,
                approved ? null : "User denied",
                PermissionChoice.Ask
            )
        );
    }
}
```

### **Level 5: Production Telemetry (500+ Lines)**

See `HPD-Agent/Observability/TelemetryEventObserver.cs` for a full production example with:
- OpenTelemetry integration
- Structured logging
- Metrics collection
- Distributed tracing
- Error tracking

---

## Built-in Event Handlers

HPD-Agent provides three production-ready event handlers:

### **1. ConsoleEventHandler**

**Location**: `HPD.Agent.ConsoleEventHandler`
**Purpose**: Interactive console display with color coding, streaming text, permissions, and tool calls.

```csharp
var handler = new ConsoleEventHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();
handler.SetAgent(agent);
```

**Features**:
- ✅ Streaming text with "📝 Response:" header
- ✅ Reasoning tokens with "💭 Thinking:" header (o1, Gemini, DeepSeek-R1)
- ✅ Tool calls with "🔧 Using tool: {name}" ✓
- ✅ Interactive permissions (Allow/Forever/Deny/Never)
- ✅ Interactive continuations (Yes/No)
- ✅ Iteration counter ("🔄 Agent iteration 2")
- ✅ Color-coded console output

### **2. LoggingEventObserver**

**Location**: `HPD.Agent.LoggingEventObserver` (auto-registered via `.WithLogging()`)
**Purpose**: Structured logging for debugging and auditing.

```csharp
var agent = new AgentBuilder(config)
    .WithLogging() // Auto-registers LoggingEventObserver
    .BuildCoreAgent();
```

**Features**:
- Logs all tool calls, permissions, continuations, and errors
- Configurable log levels via `appsettings.json`
- Structured logging with context (MessageId, CallId, etc.)

### **3. TelemetryEventObserver**

**Location**: `HPD.Agent.TelemetryEventObserver` (auto-registered via `.WithTelemetry()`)
**Purpose**: OpenTelemetry integration for production monitoring.

```csharp
var agent = new AgentBuilder(config)
    .WithTelemetry() // Auto-registers TelemetryEventObserver
    .BuildCoreAgent();
```

**Features**:
- OpenTelemetry traces, spans, and metrics
- Distributed tracing across agent boundaries
- Custom metrics (tool call counts, iteration counts, etc.)
- Error tracking with full stack traces

---

## Common Patterns

### **Pattern 1: Shared Handler Across Agents**

```csharp
var sharedHandler = new ConsoleEventHandler();

var agent1 = new AgentBuilder(config1)
    .WithObserver(sharedHandler)
    .BuildCoreAgent();

var agent2 = new AgentBuilder(config2)
    .WithObserver(sharedHandler)
    .BuildCoreAgent();

// Set agent for bidirectional events (only one can send responses)
sharedHandler.SetAgent(agent1);
```

### **Pattern 2: Multiple Handlers per Agent**

```csharp
var agent = new AgentBuilder(config)
    .WithObserver(new ConsoleEventHandler())
    .WithObserver(new TelemetryHandler())
    .WithObserver(new DebugHandler())
    .BuildCoreAgent();
```

All observers run **in parallel** via fire-and-forget.

### **Pattern 3: Conditional Event Processing**

```csharp
public bool ShouldProcess(AgentEvent evt)
{
    // Only process errors in production
    if (_isProduction)
        return evt is MessageTurnErrorEvent or MiddlewareErrorEvent;

    // Process everything in development
    return true;
}
```

### **Pattern 4: Event Aggregation**

```csharp
public class MetricsHandler : IEventHandler
{
    private readonly ConcurrentDictionary<string, int> _toolCalls = new();

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        if (evt is ToolCallStartEvent toolStart)
        {
            _toolCalls.AddOrUpdate(toolStart.Name, 1, (_, count) => count + 1);
        }
    }

    public void PrintStats()
    {
        foreach (var (tool, count) in _toolCalls)
            Console.WriteLine($"{tool}: {count} calls");
    }
}
```

### **Pattern 5: UI Updates**

```csharp
public class UIEventHandler : IEventHandler
{
    private readonly IDispatcher _dispatcher;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        await _dispatcher.InvokeAsync(() =>
        {
            switch (evt)
            {
                case TextDeltaEvent textDelta:
                    _chatView.AppendText(textDelta.Text);
                    break;
                case ToolCallStartEvent toolStart:
                    _statusBar.Text = $"Using tool: {toolStart.Name}";
                    break;
            }
        });
    }
}
```

---

## Best Practices

### **1. Filter Events Aggressively**

```csharp
// ✅ Good - Only process relevant events
public bool ShouldProcess(AgentEvent evt)
    => evt is TextDeltaEvent or ToolCallStartEvent;

// ❌ Bad - Processes all 58 event types unnecessarily
public bool ShouldProcess(AgentEvent evt) => true;
```

### **2. Use Circuit Breaker Protection**

The framework **automatically disables** observers after 10 consecutive failures:

```csharp
public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
{
    try
    {
        // Your event handling logic
    }
    catch (Exception ex)
    {
        // Log error - circuit breaker tracks failures
        _logger.LogError(ex, "Event handler failed");
        throw; // Re-throw to trigger circuit breaker
    }
}
```

After 3 successful calls, the circuit breaker **re-enables** the observer.

### **3. Don't Block the Event Loop**

```csharp
// ✅ Good - Fire-and-forget async
public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
{
    _ = Task.Run(async () =>
    {
        await SlowOperationAsync();
    }, ct);
}

// ❌ Bad - Blocks event processing
public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
{
    await Task.Delay(5000); // Blocks all other events!
}
```

### **4. Use `SetAgent()` Only for Bidirectional Events**

```csharp
// ✅ Only needed for permissions, continuations, clarifications
var handler = new InteractiveHandler();
var agent = builder.WithObserver(handler).BuildCoreAgent();
handler.SetAgent(agent); // Required for SendMiddlewareResponse()

// ✅ Not needed for read-only events
var readOnlyHandler = new LoggingHandler();
builder.WithObserver(readOnlyHandler); // No SetAgent() needed
```

### **5. Use Built-in Handlers When Possible**

```csharp
// ✅ Use ConsoleEventHandler for console apps
.WithObserver(new ConsoleEventHandler())

// ❌ Don't reimplement console display logic
.WithObserver(new MyCustomConsoleHandler())
```

---

## Examples

### **Example 1: Simple Text Streaming**

```csharp
public class StreamingHandler : IEventHandler
{
    public bool ShouldProcess(AgentEvent evt)
        => evt is TextDeltaEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        if (evt is TextDeltaEvent textDelta)
            Console.Write(textDelta.Text);
    }
}

// Usage
var agent = new AgentBuilder(config)
    .WithObserver(new StreamingHandler())
    .BuildCoreAgent();
```

### **Example 2: Tool Call Monitoring**

```csharp
public class ToolMonitor : IEventHandler
{
    private readonly Dictionary<string, DateTime> _startTimes = new();

    public bool ShouldProcess(AgentEvent evt)
        => evt is ToolCallStartEvent or ToolCallResultEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case ToolCallStartEvent toolStart:
                _startTimes[toolStart.CallId] = DateTime.Now;
                Console.WriteLine($"🔧 Started: {toolStart.Name}");
                break;

            case ToolCallResultEvent toolResult:
                if (_startTimes.TryGetValue(toolResult.CallId, out var startTime))
                {
                    var duration = DateTime.Now - startTime;
                    Console.WriteLine($"✅ Completed in {duration.TotalMilliseconds}ms");
                    _startTimes.Remove(toolResult.CallId);
                }
                break;
        }
    }
}
```

### **Example 3: Reasoning Token Display**

```csharp
public class ReasoningHandler : IEventHandler
{
    private bool _isFirstChunk = true;

    public bool ShouldProcess(AgentEvent evt)
        => evt is Reasoning;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        if (evt is not Reasoning reasoning) return;

        switch (reasoning.Phase)
        {
            case ReasoningPhase.MessageStart:
                if (_isFirstChunk)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("\n💭 Thinking: ");
                    _isFirstChunk = false;
                }
                break;

            case ReasoningPhase.Delta:
                Console.Write(reasoning.Text);
                break;

            case ReasoningPhase.MessageEnd:
                Console.WriteLine();
                Console.ResetColor();
                _isFirstChunk = true;
                break;
        }
    }
}
```

### **Example 4: Permission Handling**

```csharp
public class PermissionHandler : IEventHandler
{
    private AgentCore? _agent;

    internal void SetAgent(AgentCore agent) => _agent = agent;

    public bool ShouldProcess(AgentEvent evt)
        => evt is PermissionRequestEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        if (evt is not PermissionRequestEvent permReq) return;

        Console.WriteLine($"\n🔐 Permission Request");
        Console.WriteLine($"   Function: {permReq.FunctionName}");
        Console.WriteLine($"   Description: {permReq.Description}");
        Console.Write($"   Approve? (Y/N): ");

        var input = await Task.Run(() => Console.ReadLine(), ct);
        var approved = input?.ToLower() == "y";

        _agent?.SendMiddlewareResponse(
            permReq.PermissionId,
            new PermissionResponseEvent(
                permReq.PermissionId,
                "Console",
                approved,
                approved ? null : "User denied permission",
                PermissionChoice.Ask
            )
        );

        Console.WriteLine($"   {(approved ? "✓ Approved" : "✗ Denied")}");
    }
}
```

### **Example 5: Telemetry Integration**

```csharp
public class OpenTelemetryHandler : IEventHandler
{
    private readonly ActivitySource _activitySource;
    private readonly Dictionary<string, Activity> _activities = new();

    public OpenTelemetryHandler()
    {
        _activitySource = new ActivitySource("HPD.Agent");
    }

    public bool ShouldProcess(AgentEvent evt)
        => evt is ToolCallStartEvent
            or ToolCallResultEvent
            or AgentTurnStartedEvent
            or AgentTurnFinishedEvent;

    public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case AgentTurnStartedEvent turnStart:
                var turnActivity = _activitySource.StartActivity($"AgentTurn-{turnStart.Iteration}");
                _activities[$"turn-{turnStart.Iteration}"] = turnActivity!;
                break;

            case AgentTurnFinishedEvent turnEnd:
                if (_activities.TryGetValue($"turn-{turnEnd.Iteration}", out var activity))
                {
                    activity.Stop();
                    _activities.Remove($"turn-{turnEnd.Iteration}");
                }
                break;

            case ToolCallStartEvent toolStart:
                var toolActivity = _activitySource.StartActivity($"ToolCall-{toolStart.Name}");
                toolActivity?.SetTag("tool.name", toolStart.Name);
                toolActivity?.SetTag("call.id", toolStart.CallId);
                _activities[toolStart.CallId] = toolActivity!;
                break;

            case ToolCallResultEvent toolResult:
                if (_activities.TryGetValue(toolResult.CallId, out var toolActivity))
                {
                    toolActivity.SetTag("result.is_error", toolResult.IsError);
                    toolActivity.Stop();
                    _activities.Remove(toolResult.CallId);
                }
                break;
        }
    }
}
```

---

## Troubleshooting

### **Problem: Events Not Being Received**

**Symptoms**: `OnEventAsync()` never called.

**Solutions**:
1. Check `ShouldProcess()` returns `true` for your events
2. Verify observer was registered via `.WithObserver()`
3. Ensure you're consuming the `RunAsync()` stream:
   ```csharp
   // ✅ Correct - Consumes stream
   await foreach (var _ in agent.RunAsync(...)) { }

   // ❌ Wrong - Stream never consumed
   var stream = agent.RunAsync(...);
   ```

### **Problem: Bidirectional Events Not Working**

**Symptoms**: Permission/continuation requests hang indefinitely.

**Solutions**:
1. Ensure `SetAgent()` was called after building agent:
   ```csharp
   var handler = new MyHandler();
   var agent = builder.WithObserver(handler).BuildCoreAgent();
   handler.SetAgent(agent); // Required!
   ```
2. Verify you're calling `SendMiddlewareResponse()` in your handler
3. Check the response event ID matches the request ID

### **Problem: Circuit Breaker Disabled Observer**

**Symptoms**: Observer stops working after errors.

**Solutions**:
1. Check logs for "Observer disabled due to circuit breaker"
2. Fix the exception in your `OnEventAsync()` method
3. Circuit breaker re-enables after 3 successful calls
4. Use try-catch to handle errors gracefully:
   ```csharp
   public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
   {
       try
       {
           // Your logic
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Event handling failed");
           // Don't rethrow - prevents circuit breaker
       }
   }
   ```

### **Problem: Performance Issues**

**Symptoms**: Agent runs slowly with observers.

**Solutions**:
1. Filter events aggressively in `ShouldProcess()`
2. Use fire-and-forget for slow operations:
   ```csharp
   _ = Task.Run(async () => await SlowOperationAsync(), ct);
   ```
3. Don't use `await Task.Delay()` in event handlers
4. Consider using multiple specialized handlers instead of one giant handler

### **Problem: Memory Leaks**

**Symptoms**: Memory usage grows over time.

**Solutions**:
1. Don't store event objects in collections (they hold references)
2. Use `ConcurrentDictionary.TryRemove()` for tracking dictionaries
3. Clear state after agent turns:
   ```csharp
   case AgentTurnFinishedEvent:
       _eventCache.Clear();
       break;
   ```

---

## Next Steps

- **[API Reference](./API_REFERENCE.md)** - Complete event type reference and API documentation
- **[Architecture Guide](./ARCHITECTURE.md)** - Deep dive into event system internals
- **[ConsoleEventHandler Source](../../HPD-Agent/Observability/ConsoleEventHandler.cs)** - Production-ready example
- **[TelemetryEventObserver Source](../../HPD-Agent/Observability/TelemetryEventObserver.cs)** - Advanced telemetry example

---

**Questions or Issues?**
- Check the [API Reference](./API_REFERENCE.md) for event type details
- Review the [Architecture Guide](./ARCHITECTURE.md) for system internals
- Examine built-in handlers for implementation patterns
