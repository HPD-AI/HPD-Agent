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
- `bool ShouldProcess(InternalAgentEvent evt)` - Filter events before processing
- `Task OnEventAsync(InternalAgentEvent evt, CancellationToken ct)` - Handle filtered events

### `IAgentEventObserver`

**Base** interface for observing agent events. Use `IEventHandler` for better semantics.

```csharp
public interface IAgentEventObserver
{
    /// <summary>
    /// Determines if this observer should process the given event.
    /// Return false to skip processing (improves performance).
    /// </summary>
    bool ShouldProcess(InternalAgentEvent evt);

    /// <summary>
    /// Process the event asynchronously.
    /// Called in a fire-and-forget pattern (doesn't block agent execution).
    /// </summary>
    Task OnEventAsync(InternalAgentEvent evt, CancellationToken ct = default);
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

### `InternalMessageTurnStartedEvent`

Emitted when a message turn starts (user sends message, agent begins processing).

```csharp
public record InternalMessageTurnStartedEvent(
    string MessageTurnId,
    string ConversationId,
    string AgentName,
    DateTimeOffset Timestamp
) : InternalAgentEvent;
```

**Properties:**
- `MessageTurnId` - Unique identifier for this message turn
- `ConversationId` - ID of the conversation thread
- `AgentName` - Name of the agent processing the message
- `Timestamp` - When the turn started

**Example:**
```csharp
case InternalMessageTurnStartedEvent turnStart:
    Console.WriteLine($"[{turnStart.Timestamp:HH:mm:ss}] Turn started: {turnStart.MessageTurnId}");
    _metrics.IncrementCounter($"{turnStart.AgentName}.turns.started");
    break;
```

### `InternalMessageTurnFinishedEvent`

Emitted when a message turn completes successfully.

```csharp
public record InternalMessageTurnFinishedEvent(
    string MessageTurnId,
    string ConversationId,
    string AgentName,
    TimeSpan Duration,
    DateTimeOffset Timestamp
) : InternalAgentEvent;
```

**Properties:**
- `Duration` - Total time spent on this turn
- All other properties same as `InternalMessageTurnStartedEvent`

**Example:**
```csharp
case InternalMessageTurnFinishedEvent turnEnd:
    Console.WriteLine($"Turn finished in {turnEnd.Duration.TotalSeconds:F2}s");
    _metrics.RecordDuration($"{turnEnd.AgentName}.turn.duration", turnEnd.Duration);
    break;
```

### `InternalMessageTurnErrorEvent`

Emitted when an error occurs during message turn execution.

```csharp
public record InternalMessageTurnErrorEvent(
    string Message,
    Exception? Exception = null
) : InternalAgentEvent;
```

**Properties:**
- `Message` - Error description
- `Exception` - Full exception object (if available)

**Example:**
```csharp
case InternalMessageTurnErrorEvent error:
    _logger.LogError(error.Exception, "Turn failed: {Message}", error.Message);
    await NotifyUserOfErrorAsync(error.Message);
    break;
```

---

## Agent Turn Events

**Definition**: An **Agent Turn** represents a single call to the LLM (one iteration in the agentic loop). Multiple agent turns may occur within one message turn when tools are called.

### `InternalAgentTurnStartedEvent`

Emitted when an agent turn starts (single LLM call within the agentic loop).

```csharp
public record InternalAgentTurnStartedEvent(
    int Iteration
) : InternalAgentEvent;
```

**Properties:**
- `Iteration` - Current iteration number (1-indexed)

**Example:**
```csharp
case InternalAgentTurnStartedEvent turnStart:
    if (turnStart.Iteration > 1)
    {
        Console.WriteLine($"\n🔄 Agent iteration {turnStart.Iteration}");
    }
    break;
```

### `InternalAgentTurnFinishedEvent`

Emitted when an agent turn completes.

```csharp
public record InternalAgentTurnFinishedEvent(
    int Iteration
) : InternalAgentEvent;
```

**Example:**
```csharp
case InternalAgentTurnFinishedEvent turnEnd:
    _logger.LogDebug("Iteration {Iteration} completed", turnEnd.Iteration);
    break;
```

### `InternalStateSnapshotEvent`

Emitted during agent execution to expose internal state for testing/debugging. **NOT intended for production use**.

```csharp
public record InternalStateSnapshotEvent(
    int CurrentIteration,
    int MaxIterations,
    bool IsTerminated,
    string? TerminationReason,
    int ConsecutiveErrorCount,
    List<string> CompletedFunctions,
    string AgentName,
    DateTimeOffset Timestamp
) : InternalAgentEvent;
```

**Use Case**: Characterization tests, debugging agentic loops.

---

## Content Events

Events related to streaming text content from the agent.

### `InternalTextMessageStartEvent`

Emitted when the agent starts producing text content.

```csharp
public record InternalTextMessageStartEvent(
    string MessageId,
    string Role
) : InternalAgentEvent;
```

**Properties:**
- `MessageId` - Unique ID for this message
- `Role` - Message role ("assistant", "user", etc.)

**Example:**
```csharp
case InternalTextMessageStartEvent textStart:
    Console.WriteLine("\n📝 Response: ");
    _currentMessageId = textStart.MessageId;
    break;
```

### `InternalTextDeltaEvent`

Emitted when the agent produces text content (streaming delta). **Most frequent event** during streaming responses.

```csharp
public record InternalTextDeltaEvent(
    string Text,
    string MessageId
) : InternalAgentEvent;
```

**Properties:**
- `Text` - Chunk of text to display
- `MessageId` - ID of the message being streamed

**Example:**
```csharp
case InternalTextDeltaEvent textDelta:
    Console.Write(textDelta.Text);
    _textBuffer.Append(textDelta.Text);
    break;
```

### `InternalTextMessageEndEvent`

Emitted when the agent finishes producing text content.

```csharp
public record InternalTextMessageEndEvent(
    string MessageId
) : InternalAgentEvent;
```

**Example:**
```csharp
case InternalTextMessageEndEvent textEnd:
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

### `InternalReasoningEvent`

Emitted for all reasoning-related events during agent execution.

```csharp
public record InternalReasoningEvent(
    ReasoningPhase Phase,
    string MessageId,
    string? Role = null,
    string? Text = null
) : InternalAgentEvent;
```

**Properties:**
- `Phase` - Current phase of reasoning
- `MessageId` - ID of the reasoning message
- `Role` - Message role (optional)
- `Text` - Reasoning text chunk (for Delta phase)

**Example:**
```csharp
case InternalReasoningEvent reasoning:
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

### `InternalToolCallStartEvent`

Emitted when the agent requests a tool call.

```csharp
public record InternalToolCallStartEvent(
    string CallId,
    string Name,
    string MessageId
) : InternalAgentEvent;
```

**Properties:**
- `CallId` - Unique identifier for this tool call
- `Name` - Function name being called
- `MessageId` - ID of the message requesting the tool

**Example:**
```csharp
case InternalToolCallStartEvent toolStart:
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"\n🔧 Using tool: {toolStart.Name}");
    _toolStartTimes[toolStart.CallId] = DateTime.Now;
    break;
```

### `InternalToolCallArgsEvent`

Emitted when a tool call's arguments are fully available.

```csharp
public record InternalToolCallArgsEvent(
    string CallId,
    string ArgsJson
) : InternalAgentEvent;
```

**Properties:**
- `CallId` - Tool call identifier
- `ArgsJson` - JSON-serialized arguments

**Example:**
```csharp
case InternalToolCallArgsEvent argsEvent:
    _logger.LogDebug("Tool args for {CallId}: {Args}",
        argsEvent.CallId, argsEvent.ArgsJson);
    break;
```

### `InternalToolCallEndEvent`

Emitted when a tool call completes execution.

```csharp
public record InternalToolCallEndEvent(
    string CallId
) : InternalAgentEvent;
```

### `InternalToolCallResultEvent`

Emitted when a tool call result is available.

```csharp
public record InternalToolCallResultEvent(
    string CallId,
    string Result
) : InternalAgentEvent;
```

**Properties:**
- `CallId` - Tool call identifier
- `Result` - String result from the tool
- `IsError` - Whether the result is an error (property available via base class)

**Example:**
```csharp
case InternalToolCallResultEvent toolResult:
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

#### `InternalPermissionRequestEvent`

Middleware requests permission to execute a function. Handler should prompt user and send `InternalPermissionResponseEvent`.

```csharp
public record InternalPermissionRequestEvent(
    string PermissionId,
    string SourceName,
    string FunctionName,
    string? Description,
    string CallId,
    IDictionary<string, object?>? Arguments
) : InternalAgentEvent, IPermissionEvent;
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
case InternalPermissionRequestEvent permReq:
    Console.WriteLine($"\n🔐 Permission Request");
    Console.WriteLine($"   Function: {permReq.FunctionName}");
    Console.WriteLine($"   Purpose: {permReq.Description}");
    Console.Write($"   Approve? (Y/N): ");

    var input = await Task.Run(() => Console.ReadLine(), ct);
    var approved = input?.ToLower() == "y";

    _agent.SendMiddlewareResponse(
        permReq.PermissionId,
        new InternalPermissionResponseEvent(
            permReq.PermissionId,
            "Console",
            approved,
            approved ? null : "User denied permission",
            PermissionChoice.Ask
        )
    );
    break;
```

#### `InternalPermissionResponseEvent`

Response to permission request. Sent by external handler back to waiting middleware.

```csharp
public record InternalPermissionResponseEvent(
    string PermissionId,
    string SourceName,
    bool Approved,
    string? Reason = null,
    PermissionChoice Choice = PermissionChoice.Ask
) : InternalAgentEvent, IPermissionEvent;
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

#### `InternalPermissionApprovedEvent`

Emitted after permission is approved (for observability).

```csharp
public record InternalPermissionApprovedEvent(
    string PermissionId,
    string SourceName
) : InternalAgentEvent, IPermissionEvent;
```

#### `InternalPermissionDeniedEvent`

Emitted after permission is denied (for observability).

```csharp
public record InternalPermissionDeniedEvent(
    string PermissionId,
    string SourceName,
    string Reason
) : InternalAgentEvent, IPermissionEvent;
```

### Continuation Events

#### `InternalContinuationRequestEvent`

Middleware requests permission to continue beyond max iterations.

```csharp
public record InternalContinuationRequestEvent(
    string ContinuationId,
    string SourceName,
    int CurrentIteration,
    int MaxIterations
) : InternalAgentEvent, IPermissionEvent;
```

**Example:**
```csharp
case InternalContinuationRequestEvent contReq:
    Console.WriteLine($"\n⏱️  Continuation Request");
    Console.WriteLine($"   Iteration: {contReq.CurrentIteration} / {contReq.MaxIterations}");
    Console.Write($"   Continue? (Y/N): ");

    var input = await Task.Run(() => Console.ReadLine(), ct);
    var approved = input?.ToLower() == "y";

    _agent.SendMiddlewareResponse(
        contReq.ContinuationId,
        new InternalContinuationResponseEvent(
            contReq.ContinuationId,
            "Console",
            approved,
            approved ? 3 : 0 // Extend by 3 more iterations if approved
        )
    );
    break;
```

#### `InternalContinuationResponseEvent`

Response to continuation request.

```csharp
public record InternalContinuationResponseEvent(
    string ContinuationId,
    string SourceName,
    bool Approved,
    int ExtensionAmount = 0
) : InternalAgentEvent, IPermissionEvent;
```

**Properties:**
- `Approved` - Whether continuation was granted
- `ExtensionAmount` - How many additional iterations to allow

### Clarification Events

#### `InternalClarificationRequestEvent`

Agent/plugin requests user clarification or additional input.

```csharp
public record InternalClarificationRequestEvent(
    string RequestId,
    string SourceName,
    string Question,
    string? AgentName = null,
    string[]? Options = null
) : InternalAgentEvent, IClarificationEvent;
```

**Properties:**
- `RequestId` - Unique clarification ID
- `SourceName` - Source requesting clarification
- `Question` - The question being asked
- `AgentName` - Name of the agent (if applicable)
- `Options` - Optional list of valid answers

**Example:**
```csharp
case InternalClarificationRequestEvent clarReq:
    Console.WriteLine($"\n❓ Clarification Needed: {clarReq.Question}");

    if (clarReq.Options != null)
    {
        for (int i = 0; i < clarReq.Options.Length; i++)
            Console.WriteLine($"   {i + 1}. {clarReq.Options[i]}");
    }

    var answer = await Task.Run(() => Console.ReadLine(), ct);

    _agent.SendMiddlewareResponse(
        clarReq.RequestId,
        new InternalClarificationResponseEvent(
            clarReq.RequestId,
            "Console",
            clarReq.Question,
            answer ?? ""
        )
    );
    break;
```

#### `InternalClarificationResponseEvent`

Response to clarification request.

```csharp
public record InternalClarificationResponseEvent(
    string RequestId,
    string SourceName,
    string Question,
    string Answer
) : InternalAgentEvent, IClarificationEvent;
```

### Progress & Error Events

#### `InternalMiddlewareProgressEvent`

Middleware reports progress (one-way, no response needed).

```csharp
public record InternalMiddlewareProgressEvent(
    string SourceName,
    string Message,
    int? PercentComplete = null
) : InternalAgentEvent, IBidirectionalEvent;
```

**Example:**
```csharp
case InternalMiddlewareProgressEvent progress:
    var percent = progress.PercentComplete.HasValue
        ? $"{progress.PercentComplete}%"
        : "";
    Console.WriteLine($"⏳ {progress.Message} {percent}");
    break;
```

#### `InternalMiddlewareErrorEvent`

Middleware reports an error (one-way, no response needed).

```csharp
public record InternalMiddlewareErrorEvent(
    string SourceName,
    string ErrorMessage,
    Exception? Exception = null
) : InternalAgentEvent, IBidirectionalEvent;
```

---

## Observability Events

Events marked with `IObservabilityEvent` are designed for logging, metrics, and monitoring. **34 total observability event types**.

### Key Observability Events

#### `InternalScopedToolsVisibleEvent`

Emitted when scoped tools visibility is determined for an iteration.

```csharp
public record InternalScopedToolsVisibleEvent(
    string AgentName,
    int Iteration,
    IReadOnlyList<string> VisibleToolNames,
    ImmutableHashSet<string> ExpandedPlugins,
    ImmutableHashSet<string> ExpandedSkills,
    int TotalToolCount,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

**Use Case**: Monitor plugin scoping behavior, track tool visibility.

#### `InternalContainerExpandedEvent`

Emitted when a plugin or skill container is expanded.

```csharp
public record InternalContainerExpandedEvent(
    string ContainerName,
    ContainerType Type,
    IReadOnlyList<string> UnlockedFunctions,
    int Iteration,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

**ContainerType Enum:**
```csharp
public enum ContainerType { Plugin, Skill }
```

#### `InternalMiddlewarePipelineStartEvent`

Emitted when middleware pipeline execution starts.

```csharp
public record InternalMiddlewarePipelineStartEvent(
    string FunctionName,
    int MiddlewareCount,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

#### `InternalMiddlewarePipelineEndEvent`

Emitted when middleware pipeline execution completes.

```csharp
public record InternalMiddlewarePipelineEndEvent(
    string FunctionName,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

**Example:**
```csharp
case InternalMiddlewarePipelineEndEvent pipelineEnd:
    if (!pipelineEnd.Success)
    {
        _logger.LogError("Middleware pipeline failed for {Function}: {Error}",
            pipelineEnd.FunctionName, pipelineEnd.ErrorMessage);
    }
    _metrics.RecordDuration("middleware.pipeline.duration", pipelineEnd.Duration);
    break;
```

#### `InternalPermissionCheckEvent`

Emitted when a permission check occurs.

```csharp
public record InternalPermissionCheckEvent(
    string FunctionName,
    bool IsApproved,
    string? DenialReason,
    string AgentName,
    int Iteration,
    TimeSpan Duration,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

#### `InternalIterationStartEvent`

Emitted when an iteration starts with full state snapshot.

```csharp
public record InternalIterationStartEvent(
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
) : InternalAgentEvent, IObservabilityEvent;
```

**Use Case**: Full diagnostic snapshot of agent state at iteration start.

#### `InternalCircuitBreakerTriggeredEvent`

Emitted when circuit breaker is triggered (function fails too many times).

```csharp
public record InternalCircuitBreakerTriggeredEvent(
    string AgentName,
    string FunctionName,
    int ConsecutiveCount,
    int Iteration,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
```

**Example:**
```csharp
case InternalCircuitBreakerTriggeredEvent circuitBreaker:
    _logger.LogWarning("Circuit breaker triggered for {Function} after {Count} failures",
        circuitBreaker.FunctionName, circuitBreaker.ConsecutiveCount);
    await NotifyOpsTeamAsync(circuitBreaker);
    break;
```

#### `InternalHistoryReductionCacheEvent`

Emitted when history reduction cache is checked.

```csharp
public record InternalHistoryReductionCacheEvent(
    string AgentName,
    bool IsHit,
    DateTime? ReductionCreatedAt,
    int? SummarizedUpToIndex,
    int CurrentMessageCount,
    int? TokenSavings,
    DateTimeOffset Timestamp
) : InternalAgentEvent, IObservabilityEvent;
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
public async Task OnEventAsync(InternalAgentEvent evt, CancellationToken ct)
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
- ✅ Streaming text (`InternalTextDeltaEvent`)
- ✅ Reasoning tokens (`InternalReasoningEvent`)
- ✅ Tool calls (`InternalToolCallStartEvent`, `InternalToolCallResultEvent`)
- ✅ Interactive permissions (`InternalPermissionRequestEvent`)
- ✅ Interactive continuations (`InternalContinuationRequestEvent`)
- ✅ Iteration counter (`InternalAgentTurnStartedEvent`)

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
case InternalPermissionRequestEvent permReq:
    var approved = await PromptUserAsync();

    _agent.SendMiddlewareResponse(
        permReq.PermissionId,
        new InternalPermissionResponseEvent(
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
public bool ShouldProcess(InternalAgentEvent evt)
    => evt is InternalTextDeltaEvent
        or InternalToolCallStartEvent
        or InternalPermissionRequestEvent;
```

**❌ Bad** - Process all events unnecessarily:
```csharp
public bool ShouldProcess(InternalAgentEvent evt) => true;
```

**Performance Impact**: Filtering reduces CPU usage by 50-80% for high-frequency events.

---

## Next Steps

- **[User Guide](./USER_GUIDE.md)** - Learn event handling patterns and best practices
- **[Architecture Guide](./ARCHITECTURE.md)** - Deep dive into event system internals
- **[ConsoleEventHandler Source](../../HPD-Agent/Observability/ConsoleEventHandler.cs)** - Reference implementation
- **[TelemetryEventObserver Source](../../HPD-Agent/Observability/TelemetryEventObserver.cs)** - Advanced telemetry example
