# HPD-Agent Middleware Architecture - Comprehensive Analysis

## Executive Summary

The HPD-Agent middleware architecture implements a **sophisticated, composable filter pipeline system** that processes requests and responses at multiple levels. Rather than a single monolithic middleware chain, HPD-Agent uses **specialized filter pipelines** for different concerns, each with its own interface, registration mechanism, and execution model.

### Key Strengths
- **Separation of Concerns**: Four distinct filter types for different pipeline stages
- **Composability**: Filters combine without coupling to core logic
- **Flexibility**: Rich context with event emission, response waiting, and execution control
- **Observability**: Built-in event system for real-time insights
- **Scoping**: Function-level, plugin-level, and global filter scopes

---

## Architecture Overview

### Four Filter Types

| Filter Type | Interface | Purpose | Context | Execution |
|---|---|---|---|---|
| **Prompt** | `IPromptMiddleware` | Modify messages before/after LLM | `PromptMiddlewareContext` | Pre/post-LLM |
| **AI Function** | `IAIFunctionMiddleware` | Intercept function calls | `FunctionInvocationContext` | Pre/post-function |
| **Permission** | `IPermissionMiddleware` | Control execution access | `FunctionInvocationContext` | Pre-function |
| **Message Turn** | `IMessageTurnMiddleware` | Analyze completed turns | `MessageTurnMiddlewareContext` | Post-turn |

---

## File Structure

```
HPD-Agent/Filters/
├── PromptMiddlewareing/
│   ├── IPromptMiddleware.cs              # Prompt filter interface
│   ├── PromptMiddlewareContext.cs        # Pre-LLM context
│   └── PostInvokeContext.cs          # Post-LLM context
├── AiFunctionOrchestrationContext.cs # AI function filter interface
├── ScopedFilterSystem.cs             # Scoping management
├── LoggingAIFunctionMiddleware.cs        # Logging implementation
├── ObservabilityAIFunctionMiddleware.cs  # Telemetry implementation
└── Conversation/
    └── IConversationFilter.cs        # Message turn filter interface

HPD-Agent/Permissions/
├── IPermissionMiddleware.cs              # Permission filter interface
└── PermissionMiddleware.cs               # Permission implementation

HPD-Agent/Agent/
└── Agent.cs
    ├── Agent class (core engine)
    ├── FunctionCallProcessor (delegates to)
    └── FilterChain (static class at line 4692)

HPD-Agent/Agent/AgentBuilder.cs
└── AgentBuilderFilterExtensions (line 1569)
```

---

## Core Components Deep Dive

### 1. Prompt Filters (IPromptMiddleware)

**Location**: `HPD-Agent/Filters/PromptMiddlewareing/IPromptMiddleware.cs`

**Purpose**: Modify messages before LLM invocation and process results after

**Interface**:
```csharp
internal interface IPromptMiddleware
{
    Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next);

    Task PostInvokeAsync(PostInvokeContext context, CancellationToken cancellationToken)
    {
        return Task.CompletedTask; // Default: do nothing
    }
}
```

**Capabilities**:
- Modify user/system messages before sending to LLM
- Extract memories or insights from LLM responses
- Analyze which context was useful
- Update rankings or knowledge bases
- Transform messages for specific LLM requirements

**Context Classes**:
- `PromptMiddlewareContext`: Messages, options, agent name, cancellation token, properties
- `PostInvokeContext`: Request messages, response messages, exception, properties

---

### 2. AI Function Filters (IAIFunctionMiddleware)

**Location**: `HPD-Agent/Filters/AiFunctionOrchestrationContext.cs`

**Purpose**: Intercept and process function calls with full orchestration capabilities

**Interface**:
```csharp
internal interface IAIFunctionMiddleware
{
    Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next);
}
```

**Key Features**:
- Access to full `FunctionInvocationContext` with bidirectional communication
- Emit events via `context.Emit()` for real-time observability
- Wait for responses via `context.WaitForResponseAsync<T>()` for interactive filters
- Terminate execution with `context.IsTerminated = true`
- Modify results via `context.Result`

**Concrete Implementations**:

1. **LoggingAIFunctionMiddleware**: Logs function names, arguments, and results
   - File: `HPD-Agent/Filters/LoggingAIFunctionMiddleware.cs`
   - Pre: Logs function name and arguments
   - Post: Logs result and completion

2. **ObservabilityAIFunctionMiddleware**: Creates OpenTelemetry spans and metrics
   - File: `HPD-Agent/Filters/ObservabilityAIFunctionMiddleware.cs`
   - Pre: Starts Activity span, increments counter
   - Post: Records duration, result tags
   - Error: Records exception details

3. **PermissionMiddleware**: Implements permission checking with user interaction
   - File: `HPD-Agent/Permissions/PermissionMiddleware.cs`
   - Emits permission request events
   - Waits for user responses
   - Stores preferences persistently

4. **DynamicMemoryFilter**: Extracts and stores conversation memories
   - File: `HPD-Agent/Memory/Agent/DynamicMemory/DynamicMemoryFilter.cs`
   - Post-execution: Analyzes responses for memory content
   - Stores memories for future context

5. **StaticMemoryFilter**: Applies static memory/context injection
   - File: `HPD-Agent/Memory/Agent/StaticMemory/StaticMemoryFilter.cs`
   - Pre-execution: Injects relevant stored memories

6. **AgentPlanFilter**: Handles plan mode execution
   - File: `HPD-Agent/Memory/Agent/PlanMode/AgentPlanFilter.cs`

---

### 3. Permission Filters (IPermissionMiddleware)

**Location**: `HPD-Agent/Permissions/IPermissionMiddleware.cs`

**Purpose**: Control function execution based on permissions

**Key Capabilities**:
- Check if function requires permission
- Emit permission request events
- Wait for user approval (request/response pattern)
- Store permission preferences persistently
- Handle continuation limits at iteration boundaries

**Primary Implementation**: `PermissionMiddleware`

**Flow**:
1. Checks continuation permission if approaching iteration limits
2. Checks function-level permission requirement
3. If required: Emits `InternalPermissionRequestEvent`
4. Waits for `InternalPermissionResponseEvent`
5. Handles timeout/cancellation gracefully
6. Stores preference if user requested persistence

---

### 4. Message Turn Filters (IMessageTurnMiddleware)

**Location**: `HPD-Agent/Filters/Conversation/IConversationFilter.cs`

**Purpose**: Post-process completed message turns for observation

**Characteristics**:
- Executes after complete message turn finishes
- Read-only access to conversation history
- Mutations are NOT persisted back to conversation
- Used for observation, logging, telemetry
- Can analyze overall turn strategy

**Interface**:
```csharp
internal interface IMessageTurnMiddleware
{
    Task InvokeAsync(
        MessageTurnMiddlewareContext context,
        Func<MessageTurnMiddlewareContext, Task> next);
}
```

---

## Filter Registration System

### Registration Methods

```csharp
// AI Function Filters (with scope support)
builder.WithFunctionInvocationFilters(filter1, filter2)
builder.WithFunctionInvocationFilter<LoggingAIFunctionMiddleware>()
builder.WithFilter(customFilter)

// Prompt Filters
builder.WithPromptMiddleware(PromptMiddleware)
builder.WithPromptMiddleware<MyPromptMiddleware>()
builder.WithPromptMiddlewares(filter1, filter2)

// Permission Filters
builder.WithPermissionMiddleware(PermissionMiddleware)

// Message Turn Filters
builder.WithMessageTurnMiddleware(turnFilter)
builder.WithMessageTurnMiddleware<MyTurnFilter>()
builder.WithMessageTurnMiddlewares(filter1, filter2)
```

### Filter Storage in AgentBuilder

```csharp
public class AgentBuilder
{
    private readonly List<IAIFunctionMiddleware> _globalFilters = new();
    internal readonly ScopedFunctionMiddlewareManager _ScopedFunctionMiddlewareManager = new();
    internal readonly BuilderScopeContext _scopeContext = new();
    internal readonly List<IPromptMiddleware> _PromptMiddlewares = new();
    internal readonly List<IPermissionMiddleware> _PermissionMiddlewares = new();
    internal readonly List<IMessageTurnMiddleware> _MessageTurnMiddlewares = new();
}
```

---

## Filter Scoping System

**Location**: `HPD-Agent/Filters/ScopedFilterSystem.cs`

### Scope Types

```csharp
internal enum FilterScope
{
    Global,    // Applies to all functions
    Plugin,    // Applies to all functions in a plugin
    Function   // Applies to specific function only
}
```

### ScopedFunctionMiddlewareManager

```csharp
internal class ScopedFunctionMiddlewareManager
{
    // Add filter with scope
    public void AddFilter(IAIFunctionMiddleware filter, FilterScope scope, string? target = null)

    // Register function-to-plugin mapping
    public void RegisterFunctionPlugin(string functionName, string pluginTypeName)

    // Get applicable filters (ordered by priority)
    // Priority: Function (2) → Plugin (1) → Global (0)
    public IEnumerable<IAIFunctionMiddleware> GetApplicableFilters(string functionName, string? pluginTypeName = null)
}
```

### BuilderScopeContext

Tracks current scope during builder configuration:

```csharp
internal class BuilderScopeContext
{
    public FilterScope CurrentScope { get; set; } = FilterScope.Global;
    public string? CurrentTarget { get; set; }

    public void SetGlobalScope()
    public void SetPluginScope(string pluginTypeName)
    public void SetFunctionScope(string functionName)
}
```

---

## Filter Pipeline Execution

**Location**: `HPD-Agent/Agent/Agent.cs` - `FilterChain` class at line 4692

### Pipeline Chain Building

Filters are composed using **reverse wrapping**:

```csharp
internal static class FilterChain
{
    public static Func<FunctionInvocationContext, Task> BuildAiFunctionPipeline(
        IEnumerable<IAIFunctionMiddleware> filters,
        Func<FunctionInvocationContext, Task> finalAction)
    {
        var pipeline = finalAction;
        
        // Reverse order wrapping ensures filters execute in provided order
        foreach (var filter in filters.Reverse())
        {
            var previous = pipeline;
            pipeline = ctx => filter.InvokeAsync(ctx, previous);
        }
        
        return pipeline;
    }
}
```

### Execution Flow

1. **Build phase**: Filters composed into chain via reverse wrapping
2. **Execute phase**: Composed pipeline invoked
3. **Chain flow**: Each filter calls `next(context)` to continue
4. **Final phase**: Last action is function invocation

**Example Chain**:
```
Filter A → Filter B → Filter C → Final Action
1. FilterA.InvokeAsync called
2. FilterA calls next(context)
3. FilterB.InvokeAsync called
4. FilterB calls next(context)
5. FilterC.InvokeAsync called
6. FilterC calls next(context)
7. Final Action executes (function invocation)
8. FilterC completes post-execution logic
9. FilterB completes post-execution logic
10. FilterA completes post-execution logic
```

---

## FunctionInvocationContext

**Location**: `HPD-Agent/Agent/Agent.cs` - Line 5134

The central hub for filter communication and orchestration:

### Properties

```csharp
internal class FunctionInvocationContext
{
    // Function Information
    public AIFunction? Function { get; set; }
    public string FunctionName { get; }
    public string? FunctionDescription { get; }
    public IDictionary<string, object?> Arguments { get; set; }
    
    // Call Metadata
    public string CallId { get; set; }
    public string? AgentName { get; set; }
    public int Iteration { get; set; }
    public int TotalFunctionCallsInRun { get; set; }
    
    // State & Context
    public AgentLoopState? State { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public ToolCallRequest? ToolCallRequest { get; set; }
    
    // Execution Control
    public bool IsTerminated { get; set; } = false;
    public object? Result { get; set; }
    
    // Communication Channels
    internal ChannelWriter<InternalAgentEvent>? OutboundEvents { get; set; }
    internal Agent? Agent { get; set; }
}
```

### Key Methods

**Event Emission**:
```csharp
// Synchronous emit
public void Emit(InternalAgentEvent evt)

// Asynchronous emit (for bounded channels)
public async Task EmitAsync(InternalAgentEvent evt, CancellationToken cancellationToken = default)
```

**Response Waiting (Request/Response Pattern)**:
```csharp
public async Task<T> WaitForResponseAsync<T>(
    string requestId,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default) where T : InternalAgentEvent
```

---

## Message Turn Flow

Complete execution from user message to response:

```
1. User Message Arrives
   ↓
2. Message Turn Starts (InternalMessageTurnStartedEvent)
   ↓
3. Prompt Filters Execute
   a. Filters modify messages (InvokeAsync)
   b. LLM receives modified messages
   c. PostInvokeAsync called with response
   ↓
4. Agent Parses LLM Response
   ↓
5. For Each Function Call:
   a. PermissionManager checks access (runs permission filters)
   b. Get scoped filters for function
   c. Build AI function pipeline (global + plugin + function)
   d. Execute pipeline:
      i. Pre-execution filters
      ii. Function invocation with retry logic
      iii. Post-execution filters
   ↓
6. Message Turn Filters Execute (read-only observation)
   ↓
7. Message Turn Ends (InternalMessageTurnFinishedEvent)
   ↓
8. Continue Loop or Finish
```

---

## Event System

### Event Architecture

Events emitted through channels in real-time:

```csharp
// Filter emits event
context.Emit(new InternalPermissionRequestEvent(...));

// Handler receives event
while (_eventCoordinator.EventReader.TryRead(out var evt))
{
    yield return evt;
}
```

### Event Categories

**Permission Events**:
- `InternalPermissionRequestEvent`: Request user approval
- `InternalPermissionResponseEvent`: User responds
- `InternalPermissionApprovedEvent`: Permission granted
- `InternalPermissionDeniedEvent`: Permission denied

**Continuation Events**:
- `InternalContinuationRequestEvent`: Request to continue beyond limit
- `InternalContinuationResponseEvent`: User responds

**Message Turn Events**:
- `InternalMessageTurnStartedEvent`: Turn begins
- `InternalMessageTurnFinishedEvent`: Turn ends
- `InternalMessageTurnErrorEvent`: Error occurred

**Agent Turn Events**:
- `InternalAgentTurnStartedEvent`: LLM call begins
- `InternalAgentTurnFinishedEvent`: LLM call ends
- `InternalStateSnapshotEvent`: Debug state info

**Content Events**:
- `InternalTextMessageStartEvent`: Text streaming starts
- `InternalTextDeltaEvent`: Text chunk received
- `InternalTextMessageEndEvent`: Text streaming ends
- `InternalToolCallEvent`: Tool invocation detected

**Error Events**:
- `InternalFilterErrorEvent`: Filter pipeline error
- `InternalProviderErrorEvent`: LLM provider error

---

## HPD.Memory Pipeline Architecture

Secondary middleware system for ingestion/retrieval:

### IPipelineHandler<TContext>

```csharp
public interface IPipelineHandler<in TContext> where TContext : IPipelineContext
{
    string StepName { get; }
    Task<PipelineResult> HandleAsync(TContext context, CancellationToken cancellationToken = default);
}
```

### IPipelineOrchestrator<TContext>

```csharp
public interface IPipelineOrchestrator<TContext> where TContext : IPipelineContext
{
    IReadOnlyList<string> HandlerNames { get; }
    Task AddHandlerAsync(IPipelineHandler<TContext> handler, CancellationToken cancellationToken = default);
    Task<TContext> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}
```

### Key Differences from Agent Filters

- **Linear execution**: Handlers run sequentially in order
- **Result-based**: Each handler returns `PipelineResult` (success/transient/fatal)
- **Retry support**: Built-in retry logic for transient failures
- **Distributed execution**: Supports queue-based pipeline execution
- **Generic context**: Works with any context implementing `IPipelineContext`

---

## Design Patterns

### 1. Chain of Responsibility
- Filters chained together
- Each calls `next(context)` to proceed
- Can terminate early by not calling `next()`

### 2. Event Emission
- Filters emit events for external handlers
- Events flow through channels
- Bidirectional communication via `WaitForResponseAsync`

### 3. Context Enrichment
- Contexts carry metadata and state
- Filters enrich without modifying original
- Multiple filters access same context

### 4. Scoped Registration
- Filters target global, plugin, or function level
- Priority: Function > Plugin > Global
- Precise control of filter application

### 5. Async/Await Composition
- All filters async-first
- Delegate-based pipelines
- No blocking operations

---

## Best Practices

1. **Single Responsibility**: Each filter handles one concern
2. **Non-Breaking Mutations**: Don't break downstream filter assumptions
3. **Error Handling**: Catch and handle gracefully; use `IsTerminated` for halt
4. **Event Naming**: Use hierarchical names (e.g., `Internal{Type}{Action}Event`)
5. **Metadata Usage**: Use `context.Metadata` for custom data
6. **Appropriate Scoping**: Global for universal, Plugin/Function for targeted logic
7. **Observability**: Emit events for user-visible actions
8. **Timeout Management**: Set appropriate timeouts for interactive filters

---

## Summary

The HPD-Agent middleware architecture provides:

- **Four specialized filter types** for different pipeline stages
- **Flexible scoping** (global, plugin, function levels)
- **Composable execution** via reverse-order wrapping
- **Rich communication** with event emission and response waiting
- **Memory pipeline abstraction** for ingestion/retrieval
- **Protocol-agnostic events** for real-time observability
- **Bidirectional communication** for interactive filters

This enables clean implementation of cross-cutting concerns (permissions, logging, observability, memory) without coupling to core agent logic.

---

## Key Files Reference

### Core Interfaces
- `HPD-Agent/Filters/PromptMiddlewareing/IPromptMiddleware.cs`
- `HPD-Agent/Filters/AiFunctionOrchestrationContext.cs`
- `HPD-Agent/Permissions/IPermissionMiddleware.cs`
- `HPD-Agent/Filters/Conversation/IConversationFilter.cs`

### Implementation & Management
- `HPD-Agent/Filters/ScopedFilterSystem.cs` (scoping)
- `HPD-Agent/Agent/Agent.cs` (FilterChain at line 4692)
- `HPD-Agent/Agent/AgentBuilder.cs` (extensions at line 1569)

### Concrete Implementations
- `HPD-Agent/Filters/LoggingAIFunctionMiddleware.cs`
- `HPD-Agent/Filters/ObservabilityAIFunctionMiddleware.cs`
- `HPD-Agent/Permissions/PermissionMiddleware.cs`
- `HPD-Agent/Memory/Agent/DynamicMemory/DynamicMemoryFilter.cs`

### Context Classes
- `HPD-Agent/Filters/PromptMiddlewareing/PromptMiddlewareContext.cs`
- `HPD-Agent/Filters/PromptMiddlewareing/PostInvokeContext.cs`

### Memory Pipeline
- `HPD.Memory/src/HPD.Memory.Abstractions/Abstractions/Pipeline/IPipelineHandler.cs`
- `HPD.Memory/src/HPD.Memory.Abstractions/Abstractions/Pipeline/IPipelineOrchestrator.cs`

