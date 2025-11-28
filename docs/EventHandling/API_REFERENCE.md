# Event Handling API Reference

## Table of Contents
- [Core Interfaces](#core-interfaces)
- [Event Categories](#event-categories)
- [Message Turn Events](#message-turn-events)
- [Agent Turn Events](#agent-turn-events)
- [Content Events](#content-events)
- [Reasoning Events](#reasoning-events)
- [Tool Events](#tool-events)
- [Middleware Events](#middleware-events)
- [Observability Events](#observability-events)
- [Execution Context](#execution-context)
- [Built-in Event Handlers](#built-in-event-handlers)

---

## Core Interfaces

### `IEventHandler`

**Recommended** interface for implementing event handlers. Alias for `IAgentEventObserver`.

```csharp
public interface IEventHandler : IAgentEventObserver
{
    // Inherits all methods from IAgentEventObserver
}
```

**Inherited Methods:**
- `bool ShouldProcess(AgentEvent evt)` - Filter events before processing
- `Task OnEventAsync(AgentEvent evt, CancellationToken ct)` - Handle filtered events

### `IAgentEventObserver`

**Base** interface for observing agent events. Use `IEventHandler` for better semantics.

```csharp
public interface IAgentEventObserver
{
    /// <summary>
    /// Determines if this observer should process the given event.
    /// Return false to skip processing (improves performance).
    /// </summary>
    bool ShouldProcess(AgentEvent evt);

    /// <summary>
    /// Process the event asynchronously.
    /// Called in a fire-and-forget pattern (doesn't block agent execution).
    /// </summary>
    Task OnEventAsync(AgentEvent evt, CancellationToken ct = default);
}
```

**Circuit Breaker Protection:**
- Observers are automatically disabled after **10 consecutive failures**
- Re-enabled after **3 successful calls**
- Check logs for "Observer disabled due to circuit breaker" warnings

---

## Event Categories

HPD-Agent emits **58 different event types** organized into 7 categories:

| Category | Event Count | Purpose |
|----------|------------|---------|
| [Message Turn Events](#message-turn-events) | 3 | Track entire user interaction lifecycle |
| [Agent Turn Events](#agent-turn-events) | 3 | Track individual LLM calls within message turns |
| [Content Events](#content-events) | 3 | Stream text content in real-time |
| [Reasoning Events](#reasoning-events) | 1 | Stream reasoning tokens (o1, Gemini, DeepSeek-R1) |
| [Tool Events](#tool-events) | 4 | Monitor tool call lifecycle |
| [Middleware Events](#middleware-events) | 10 | Handle permissions, continuations, clarifications |
| [Observability Events](#observability-events) | 34 | Internal diagnostics, metrics, and monitoring |

**Total**: 58 event types

---

## Message Turn Events

**Definition**: A **Message Turn** represents the entire user interaction from when they send a message until the agent finishes responding. May contain multiple agent turns if tools are called.

### `MessageTurnStartedEvent`

Emitted when a message turn starts (user sends message, agent begins processing).

```csharp
public record MessageTurnStartedEvent(
    string MessageTurnId,
    string ConversationId,
    string AgentName,
    DateTimeOffset Timestamp
) : AgentEvent;
```

**Properties:**
- `MessageTurnId` - Unique identifier for this message turn
- `ConversationId` - ID of the conversation thread
- `AgentName` - Name of the agent processing the message
- `Timestamp` - When the turn started

**Example:**
```csharp
case MessageTurnStartedEvent turnStart:
    Console.WriteLine($"[{turnStart.Timestamp:HH:mm:ss}] Turn started: {turnStart.MessageTurnId}");
    _metrics.IncrementCounter($"{turnStart.AgentName}.turns.started");
    break;
```

### `MessageTurnFinishedEvent`

Emitted when a message turn completes successfully.

```csharp
public record MessageTurnFinishedEvent(
    string MessageTurnId,
    string ConversationId,
    string AgentName,
    TimeSpan Duration,
    DateTimeOffset Timestamp
) : AgentEvent;
```

**Properties:**
- `Duration` - Total time spent on this turn
- All other properties same as `MessageTurnStartedEvent`

**Example:**
```csharp
case MessageTurnFinishedEvent turnEnd:
    Console.WriteLine($"Turn finished in {turnEnd.Duration.TotalSeconds:F2}s");
    _metrics.RecordDuration($"{turnEnd.AgentName}.turn.duration", turnEnd.Duration);
    break;
```

### `MessageTurnErrorEvent`

Emitted when an error occurs during message turn execution.

```csharp
public record MessageTurnErrorEvent(
    string Message,
    Exception? Exception = null
) : AgentEvent;
```

**Properties:**
- `Message` - Error description
- `Exception` - Full exception object (if available)

**Example:**
```csharp
case MessageTurnErrorEvent error:
    _logger.LogError(error.Exception, "Turn failed: {Message}", error.Message);
    await NotifyUserOfErrorAsync(error.Message);
    break;
```

---

## Agent Turn Events

**Definition**: An **Agent Turn** represents a single call to the LLM (one iteration in the agentic loop). Multiple agent turns may occur within one message turn when tools are called.

### `AgentTurnStartedEvent`

Emitted when an agent turn starts (single LLM call within the agentic loop).

```csharp
public record AgentTurnStartedEvent(
    int Iteration
) : AgentEvent;
```

**Properties:**
- `Iteration` - Current iteration number (1-indexed)

**Example:**
```csharp
case AgentTurnStartedEvent turnStart:
    if (turnStart.Iteration > 1)
    {
        Console.WriteLine($"\n🔄 Agent iteration {turnStart.Iteration}");
    }
    break;
```

### `AgentTurnFinishedEvent`

Emitted when an agent turn completes.

```csharp
public record AgentTurnFinishedEvent(
    int Iteration
) : AgentEvent;
```

**Example:**
```csharp
case AgentTurnFinishedEvent turnEnd:
    _logger.LogDebug("Iteration {Iteration} completed", turnEnd.Iteration);
    break;
```

### `StateSnapshotEvent`

Emitted during agent execution to expose internal state for testing/debugging. **NOT intended for production use**.

```csharp
public record StateSnapshotEvent(
    int CurrentIteration,
    int MaxIterations,
    bool IsTerminated,
    string? TerminationReason,
    int ConsecutiveErrorCount,
    List<string> CompletedFunctions,
    string AgentName,
    DateTimeOffset Timestamp
) : AgentEvent;
```

**Use Case**: Characterization tests, debugging agentic loops.

---

## Content Events

Events related to streaming text content from the agent.

### `TextMessageStartEvent`

Emitted when the agent starts producing text content.

```csharp
public record TextMessageStartEvent(
    string MessageId,
    string Role
) : AgentEvent;
```

**Properties:**
- `MessageId` - Unique ID for this message
- `Role` - Message role ("assistant", "user", etc.)

**Example:**
```csharp
case TextMessageStartEvent textStart:
    Console.WriteLine("\n📝 Response: ");
    _currentMessageId = textStart.MessageId;
    break;
```

### `TextDeltaEvent`

Emitted when the agent produces text content (streaming delta). **Most frequent event** during streaming responses.

```csharp
public record TextDeltaEvent(
    string Text,
    string MessageId
) : AgentEvent;
```

**Properties:**
- `Text` - Chunk of text to display
- `MessageId` - ID of the message being streamed

**Example:**
```csharp
case TextDeltaEvent textDelta:
    Console.Write(textDelta.Text);
    _textBuffer.Append(textDelta.Text);
    break;
```

### `TextMessageEndEvent`

Emitted when the agent finishes producing text content.

```csharp
public record TextMessageEndEvent(
    string MessageId
) : AgentEvent;
```

**Example:**
```csharp
case TextMessageEndEvent textEnd:
    Console.WriteLine(); // New line after text
    Console.ResetColor();
    break;
```

---

## Reasoning Events

Events for reasoning-capable models (OpenAI o1, Google Gemini Thinking, DeepSeek R1).

### `ReasoningPhase` Enum

```csharp
public enum ReasoningPhase
{
    SessionStart,    // Overall reasoning session begins
    MessageStart,    // Individual reasoning message starts
    Delta,           // Streaming reasoning content (delta)
    MessageEnd,      // Individual reasoning message ends
    SessionEnd       // Overall reasoning session ends
}
```

### `Reasoning`

Emitted for all reasoning-related events during agent execution.

```csharp
public record Reasoning(
    ReasoningPhase Phase,
    string MessageId,
    string? Role = null,
    string? Text = null
) : AgentEvent;
```

**Properties:**
- `Phase` - Current phase of reasoning
- `MessageId` - ID of the reasoning message
- `Role` - Message role (optional)
- `Text` - Reasoning text chunk (for Delta phase)

**Example:**
```csharp
case Reasoning reasoning:
    switch (reasoning.Phase)
    {
        case ReasoningPhase.MessageStart:
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("\n💭 Thinking: ");
            break;

        case ReasoningPhase.Delta:
            Console.Write(reasoning.Text);
            break;

        case ReasoningPhase.MessageEnd:
            Console.WriteLine();
            Console.ResetColor();
            break;
    }
    break;
```

**Note**: By default, reasoning tokens are **excluded from conversation history** to save tokens/cost. Set `PreserveReasoningInHistory = true` in `AgentConfig` to keep them.

---

## Tool Events

Events tracking the tool call lifecycle.

### `ToolCallStartEvent`

Emitted when the agent requests a tool call.

```csharp
public record ToolCallStartEvent(
    string CallId,
    string Name,
    string MessageId
) : AgentEvent;
```

**Properties:**
- `CallId` - Unique identifier for this tool call
- `Name` - Function name being called
- `MessageId` - ID of the message requesting the tool

**Example:**
```csharp
case ToolCallStartEvent toolStart:
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"\n🔧 Using tool: {toolStart.Name}");
    _toolStartTimes[toolStart.CallId] = DateTime.Now;
    break;
```

### `ToolCallArgsEvent`

Emitted when a tool call's arguments are fully available.

```csharp
public record ToolCallArgsEvent(
    string CallId,
    string ArgsJson
) : AgentEvent;
```

**Properties:**
- `CallId` - Tool call identifier
- `ArgsJson` - JSON-serialized arguments

**Example:**
```csharp
case ToolCallArgsEvent argsEvent:
    _logger.LogDebug("Tool args for {CallId}: {Args}",
        argsEvent.CallId, argsEvent.ArgsJson);
    break;
```

### `ToolCallEndEvent`

Emitted when a tool call completes execution.

```csharp
public record ToolCallEndEvent(
    string CallId
) : AgentEvent;
```

### `ToolCallResultEvent`

Emitted when a tool call result is available.

```csharp
public record ToolCallResultEvent(
    string CallId,
    string Result
) : AgentEvent;
```

**Properties:**
- `CallId` - Tool call identifier
- `Result` - String result from the tool
- `IsError` - Whether the result is an error (property available via base class)

**Example:**
```csharp
case ToolCallResultEvent toolResult:
    Console.ForegroundColor = toolResult.IsError ? ConsoleColor.Red : ConsoleColor.Green;
    Console.Write(toolResult.IsError ? " ✗" : " ✓");
    Console.ResetColor();

    if (_toolStartTimes.TryGetValue(toolResult.CallId, out var startTime))
    {
        var duration = DateTime.Now - startTime;
        _logger.LogInformation("Tool completed in {Duration}ms", duration.TotalMilliseconds);
    }
    break;
```

---

## Middleware Events

Events for bidirectional communication (permissions, continuations, clarifications).

### Marker Interfaces

#### `IBidirectionalEvent`

```csharp
public interface IBidirectionalEvent
{
    string SourceName { get; } // Name of the middleware that emitted this event
}
```

All middleware events implement this interface.

#### `IPermissionEvent : IBidirectionalEvent`

```csharp
public interface IPermissionEvent : IBidirectionalEvent
{
    string PermissionId { get; } // Unique identifier for this permission interaction
}
```

#### `IClarificationEvent : IBidirectionalEvent`

```csharp
public interface IClarificationEvent : IBidirectionalEvent
{
    string RequestId { get; } // Unique identifier for this clarification interaction
    string Question { get; }  // The question being asked
}
```

### Permission Events

#### `PermissionRequestEvent`

Middleware requests permission to execute a function. Handler should prompt user and send `PermissionResponseEvent`.

```csharp
public record PermissionRequestEvent(
    string PermissionId,
    string SourceName,
    string FunctionName,
    string? Description,
    string CallId,
    IDictionary<string, object?>? Arguments
) : AgentEvent, IPermissionEvent;
```

**Properties:**
- `PermissionId` - Unique ID for this permission request
- `SourceName` - Name of middleware requesting permission
- `FunctionName` - Name of the function requiring permission
- `Description` - Human-readable description of what the function does
- `CallId` - Tool call ID
- `Arguments` - Function arguments

**Example:**
```csharp
case PermissionRequestEvent permReq:
    Console.WriteLine($"\n🔐 Permission Request");
    Console.WriteLine($"   Function: {permReq.FunctionName}");
    Console.WriteLine($"   Purpose: {permReq.Description}");
    Console.Write($"   Approve? (Y/N): ");

    var input = await Task.Run(() => Console.ReadLine(), ct);
    var approved = input?.ToLower() == "y";

    _agent.SendMiddlewareResponse(
        permReq.PermissionId,
        new PermissionResponseEvent(
            permReq.PermissionId,
            "Console",
            approved,
            approved ? null : "User denied permission",
            PermissionChoice.Ask
        )
    );
    break;
```

#### `PermissionResponseEvent`

Response to permission request. Sent by external handler back to waiting middleware.

```csharp
public record PermissionResponseEvent(
    string PermissionId,
    string SourceName,
    bool Approved,
    string? Reason = null,
    PermissionChoice Choice = PermissionChoice.Ask
) : AgentEvent, IPermissionEvent;
```

**Properties:**
- `Approved` - Whether permission was granted
- `Reason` - Optional reason for denial
- `Choice` - Permission choice (Ask, AlwaysAllow, AlwaysDeny)

**PermissionChoice Enum:**
```csharp
public enum PermissionChoice
{
    Ask,          // Prompt user each time
    AlwaysAllow,  // Auto-approve this function forever
    AlwaysDeny    // Auto-deny this function forever
}
```

#### `PermissionApprovedEvent`

Emitted after permission is approved (for observability).

```csharp
public record PermissionApprovedEvent(
    string PermissionId,
    string SourceName
) : AgentEvent, IPermissionEvent;
```

#### `PermissionDeniedEvent`

Emitted after permission is denied (for observability).

```csharp
public record PermissionDeniedEvent(
    string PermissionId,
    string SourceName,
    string Reason
) : AgentEvent, IPermissionEvent;
```

### Continuation Events

#### `ContinuationRequestEvent`

Middleware requests permission to continue beyond max iterations.

```csharp
public record ContinuationRequestEvent(
    string ContinuationId,
    string SourceName,
    int CurrentIteration,
    int MaxIterations
) : AgentEvent, IPermissionEvent;
```

**Example:**
```csharp
case ContinuationRequestEvent contReq:
    Console.WriteLine($"\n⏱️  Continuation Request");
    Console.WriteLine($"   Iteration: {contReq.CurrentIteration} / {contReq.MaxIterations}");
    Console.Write($"   Continue? (Y/N): ");

    var input = await Task.Run(() => Console.ReadLine(), ct);
    var approved = input?.ToLower() == "y";

    _agent.SendMiddlewareResponse(
        contReq.ContinuationId,
        new ContinuationResponseEvent(
            contReq.ContinuationId,
            "Console",
            approved,
            approved ? 3 : 0 // Extend by 3 more iterations if approved
        )
    );
    break;
```

#### `ContinuationResponseEvent`

Response to continuation request.

```csharp
public record ContinuationResponseEvent(
    string ContinuationId,
    string SourceName,
    bool Approved,
    int ExtensionAmount = 0
) : AgentEvent, IPermissionEvent;
```

**Properties:**
- `Approved` - Whether continuation was granted
- `ExtensionAmount` - How many additional iterations to allow

### Clarification Events

#### `ClarificationRequestEvent`

Agent/plugin requests user clarification or additional input.

```csharp
public record ClarificationRequestEvent(
    string RequestId,
    string SourceName,
    string Question,
    string? AgentName = null,
    string[]? Options = null
) : AgentEvent, IClarificationEvent;
```

**Properties:**
- `RequestId` - Unique clarification ID
- `SourceName` - Source requesting clarification
- `Question` - The question being asked
- `AgentName` - Name of the agent (if applicable)
- `Options` - Optional list of valid answers

**Example:**
```csharp
case ClarificationRequestEvent clarReq:
    Console.WriteLine($"\n❓ Clarification Needed: {clarReq.Question}");

    if (clarReq.Options != null)
    {
        for (int i = 0; i < clarReq.Options.Length; i++)
            Console.WriteLine($"   {i + 1}. {clarReq.Options[i]}");
    }

    var answer = await Task.Run(() => Console.ReadLine(), ct);

    _agent.SendMiddlewareResponse(
        clarReq.RequestId,
        new ClarificationResponseEvent(
            clarReq.RequestId,
            "Console",
            clarReq.Question,
            answer ?? ""
        )
    );
    break;
```

#### `ClarificationResponseEvent`

Response to clarification request.

```csharp
public record ClarificationResponseEvent(
    string RequestId,
    string SourceName,
    string Question,
    string Answer
) : AgentEvent, IClarificationEvent;
```

### Progress & Error Events

#### `MiddlewareProgressEvent`

Middleware reports progress (one-way, no response needed).

```csharp
public record MiddlewareProgressEvent(
    string SourceName,
    string Message,
    int? PercentComplete = null
) : AgentEvent, IBidirectionalEvent;
```

**Example:**
```csharp
case MiddlewareProgressEvent progress:
    var percent = progress.PercentComplete.HasValue
        ? $"{progress.PercentComplete}%"
        : "";
    Console.WriteLine($"⏳ {progress.Message} {percent}");
    break;
```

#### `MiddlewareErrorEvent`

Middleware reports an error (one-way, no response needed).

```csharp
public record MiddlewareErrorEvent(
    string SourceName,
    string ErrorMessage,
    Exception? Exception = null
) : AgentEvent, IBidirectionalEvent;
```

---

## Observability Events

Events marked with `IObservabilityEvent` are designed for logging, metrics, and monitoring. **34 total observability event types**.

### Key Observability Events

#### `ScopedToolsVisibleEvent`

Emitted when scoped tools visibility is determined for an iteration.

```csharp
public record ScopedToolsVisibleEvent(
    string AgentName,
    int Iteration,
    IReadOnlyList<string> VisibleToolNames,
    ImmutableHashSet<string> ExpandedPlugins,
    ImmutableHashSet<string> ExpandedSkills,
    int TotalToolCount,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**Use Case**: Monitor plugin scoping behavior, track tool visibility.

#### `ContainerExpandedEvent`

Emitted when a plugin or skill container is expanded.

```csharp
public record ContainerExpandedEvent(
    string ContainerName,
    ContainerType Type,
    IReadOnlyList<string> UnlockedFunctions,
    int Iteration,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**ContainerType Enum:**
```csharp
public enum ContainerType { Plugin, Skill }
```

#### `MiddlewarePipelineStartEvent`

Emitted when middleware pipeline execution starts.

```csharp
public record MiddlewarePipelineStartEvent(
    string FunctionName,
    int MiddlewareCount,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

#### `MiddlewarePipelineEndEvent`

Emitted when middleware pipeline execution completes.

```csharp
public record MiddlewarePipelineEndEvent(
    string FunctionName,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**Example:**
```csharp
case MiddlewarePipelineEndEvent pipelineEnd:
    if (!pipelineEnd.Success)
    {
        _logger.LogError("Middleware pipeline failed for {Function}: {Error}",
            pipelineEnd.FunctionName, pipelineEnd.ErrorMessage);
    }
    _metrics.RecordDuration("middleware.pipeline.duration", pipelineEnd.Duration);
    break;
```

#### `PermissionCheckEvent`

Emitted when a permission check occurs.

```csharp
public record PermissionCheckEvent(
    string FunctionName,
    bool IsApproved,
    string? DenialReason,
    string AgentName,
    int Iteration,
    TimeSpan Duration,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

#### `IterationStartEvent`

Emitted when an iteration starts with full state snapshot.

```csharp
public record IterationStartEvent(
    string AgentName,
    int Iteration,
    int MaxIterations,
    int CurrentMessageCount,
    int HistoryMessageCount,
    int TurnHistoryMessageCount,
    int ExpandedPluginsCount,
    int ExpandedSkillsCount,
    int CompletedFunctionsCount,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**Use Case**: Full diagnostic snapshot of agent state at iteration start.

#### `InternalCircuitBreakerTriggeredEvent`

Emitted when circuit breaker is triggered (function fails too many times).

```csharp
public recordCircuitBreakerTriggeredEvent(
    string AgentName,
    string FunctionName,
    int ConsecutiveCount,
    int Iteration,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**Example:**
```csharp
caseCircuitBreakerTriggeredEvent circuitBreaker:
    _logger.LogWarning("Circuit breaker triggered for {Function} after {Count} failures",
        circuitBreaker.FunctionName, circuitBreaker.ConsecutiveCount);
    await NotifyOpsTeamAsync(circuitBreaker);
    break;
```

#### `HistoryReductionCacheEvent`

Emitted when history reduction cache is checked.

```csharp
public record HistoryReductionCacheEvent(
    string AgentName,
    bool IsHit,
    DateTime? ReductionCreatedAt,
    int? SummarizedUpToIndex,
    int CurrentMessageCount,
    int? TokenSavings,
    DateTimeOffset Timestamp
) : AgentEvent, IObservabilityEvent;
```

**Use Case**: Monitor history reduction effectiveness and token savings.

---

## Execution Context

All events include an optional `ExecutionContext` property for multi-agent scenarios.

### `AgentExecutionContext`

```csharp
public record AgentExecutionContext
{
    public required string AgentName { get; init; }
    public required string AgentId { get; init; }
    public required int Depth { get; init; }
    public string? ParentAgentName { get; init; }
    public string? ParentAgentId { get; init; }

    public bool IsRootAgent => Depth == 0;
    public bool IsSubAgent => Depth > 0;
}
```

**Properties:**
- `AgentName` - Name of the agent that emitted this event
- `AgentId` - Unique identifier for the agent instance
- `Depth` - Nesting level (0 = root, 1+ = SubAgent)
- `ParentAgentName` - Name of parent agent (if SubAgent)
- `ParentAgentId` - ID of parent agent (if SubAgent)

**Example:**
```csharp
public async Task OnEventAsync(AgentEvent evt, CancellationToken ct)
{
    var context = evt.ExecutionContext;
    if (context?.IsSubAgent == true)
    {
        Console.WriteLine($"[SubAgent: {context.AgentName}] Event: {evt.GetType().Name}");
    }
}
```

---

## Built-in Event Handlers

### `ConsoleEventHandler`

**Namespace**: `HPD.Agent`
**Purpose**: Interactive console display with color coding

**Features:**
- ✅ Streaming text (`TextDeltaEvent`)
- ✅ Reasoning tokens (`Reasoning`)
- ✅ Tool calls (`ToolCallStartEvent`, `ToolCallResultEvent`)
- ✅ Interactive permissions (`PermissionRequestEvent`)
- ✅ Interactive continuations (`ContinuationRequestEvent`)
- ✅ Iteration counter (`AgentTurnStartedEvent`)

**Usage:**
```csharp
var handler = new ConsoleEventHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();
handler.SetAgent(agent);
```

**Source**: `HPD-Agent/Observability/ConsoleEventHandler.cs`

### `LoggingEventObserver`

**Namespace**: `HPD.Agent` (internal)
**Purpose**: Structured logging for debugging and auditing

**Auto-registered via:**
```csharp
var agent = new AgentBuilder(config)
    .WithLogging()
    .BuildCoreAgent();
```

**Features:**
- Logs tool calls, permissions, errors
- Configurable log levels via `appsettings.json`
- Structured logging with context

**Source**: `HPD-Agent/Observability/LoggingEventObserver.cs`

### `TelemetryEventObserver`

**Namespace**: `HPD.Agent` (internal)
**Purpose**: OpenTelemetry integration for production monitoring

**Auto-registered via:**
```csharp
var agent = new AgentBuilder(config)
    .WithTelemetry()
    .BuildCoreAgent();
```

**Features:**
- OpenTelemetry traces, spans, metrics
- Distributed tracing across agent boundaries
- Custom metrics (tool calls, iterations, etc.)
- Error tracking with stack traces

**Source**: `HPD-Agent/Observability/TelemetryEventObserver.cs`

---

## AgentBuilder Integration

### `.WithObserver()`

Register an event handler with the agent.

```csharp
public AgentBuilder WithObserver(IAgentEventObserver observer)
```

**Parameters:**
- `observer` - Event handler implementing `IEventHandler` or `IAgentEventObserver`

**Returns**: `AgentBuilder` for method chaining

**Example:**
```csharp
var agent = new AgentBuilder(config)
    .WithObserver(new ConsoleEventHandler())
    .WithObserver(new TelemetryHandler())
    .WithObserver(new MetricsHandler())
    .BuildCoreAgent();
```

**Multiple Observers**: All observers run in **parallel** via fire-and-forget pattern.

---

## Bidirectional Communication

For events that require responses (permissions, continuations, clarifications):

### `SetAgent()` Pattern

```csharp
var handler = new InteractiveHandler();
var agent = new AgentBuilder(config)
    .WithObserver(handler)
    .BuildCoreAgent();

handler.SetAgent(agent); // Required for bidirectional events
```

### `SendMiddlewareResponse()`

Send responses back to waiting middleware:

```csharp
_agent.SendMiddlewareResponse(
    requestId,
    responseEvent
);
```

**Example:**
```csharp
case PermissionRequestEvent permReq:
    var approved = await PromptUserAsync();

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

---

## Event Filtering Best Practices

**✅ Good** - Filter aggressively:
```csharp
public bool ShouldProcess(AgentEvent evt)
    => evt is TextDeltaEvent
        or ToolCallStartEvent
        or PermissionRequestEvent;
```

**❌ Bad** - Process all events unnecessarily:
```csharp
public bool ShouldProcess(AgentEvent evt) => true;
```

**Performance Impact**: Filtering reduces CPU usage by 50-80% for high-frequency events.

---

## Next Steps

- **[User Guide](./USER_GUIDE.md)** - Learn event handling patterns and best practices
- **[Architecture Guide](./ARCHITECTURE.md)** - Deep dive into event system internals
- **[ConsoleEventHandler Source](../../HPD-Agent/Observability/ConsoleEventHandler.cs)** - Reference implementation
- **[TelemetryEventObserver Source](../../HPD-Agent/Observability/TelemetryEventObserver.cs)** - Advanced telemetry example
