# HPD-Agent Middleware Architecture - Complete Documentation Index

## Overview

This document serves as an index to comprehensive documentation of the HPD-Agent middleware architecture. The architecture implements a **specialized filter pipeline system** with four distinct filter types for handling different concerns across the agent execution lifecycle.

---

## Quick Navigation

### For Quick Understanding
- **Start here**: See "Architecture at a Glance" below
- **Quick reference**: `/tmp/middleware_summary.txt` (terminal quick reference)
- **Executive summary**: See "Key Components" section below

### For Deep Understanding
- **Full analysis**: `/Users/einsteinessibu/Documents/HPD-Agent/MIDDLEWARE_ARCHITECTURE_ANALYSIS.md`
- **Code examples**: `/tmp/middleware_code_examples.md`
- **Message flow diagram**: See "Message Turn Flow" in full analysis

### For Implementation
- **Filter creation**: See code examples document
- **Registration patterns**: See "Filter Registration System" section
- **Event patterns**: See "Event System" section

---

## Architecture at a Glance

### Four Filter Types

```
┌─────────────────────────────────────────────────────────────────────┐
│                    AGENT EXECUTION PIPELINE                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  USER MESSAGE                                                        │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  PROMPT FILTERS                 │ (pre-LLM)                     │
│  │  • Modify messages              │                               │
│  │  • Inject context               │                               │
│  │  • Transform for LLM model      │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  LLM INVOCATION                 │                               │
│  │  • Call language model          │                               │
│  │  • Get response                 │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  POST-INVOKE FILTERS            │ (post-LLM)                    │
│  │  • Extract memories             │                               │
│  │  • Analyze response             │                               │
│  │  • Update knowledge base        │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  PARSE FUNCTION CALLS                                               │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  FOR EACH FUNCTION CALL         │                               │
│  ├─────────────────────────────────┤                               │
│  │  PERMISSION FILTERS             │ (pre-function)                │
│  │  • Check permissions            │                               │
│  │  • Request user approval        │                               │
│  │  • Store preferences            │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  AI FUNCTION FILTERS            │ (pre & post-function)         │
│  │  • Logging                      │                               │
│  │  • Observability/telemetry      │                               │
│  │  • Validation                   │                               │
│  │  • Transformation               │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  ┌─────────────────────────────────┐                               │
│  │  FUNCTION EXECUTION             │                               │
│  │  • Call actual function         │                               │
│  │  • With retry logic             │                               │
│  └─────────────────────────────────┘                               │
│      ↓ (repeat for next function)                                  │
│  ┌─────────────────────────────────┐                               │
│  │  MESSAGE TURN FILTERS           │ (post-turn)                   │
│  │  • Analyze completed turn       │                               │
│  │  • Extract insights             │                               │
│  │  • Update external systems      │                               │
│  │  • [READ-ONLY]                  │                               │
│  └─────────────────────────────────┘                               │
│      ↓                                                               │
│  RESPONSE TO USER                                                   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Key Components

### 1. Prompt Filters (IPromptFilter)
**Location**: `HPD-Agent/Filters/PromptFiltering/IPromptFilter.cs`

**When**: Before and after LLM invocation
**What**: Modify messages, extract memories, analyze context usage
**Context**: `PromptFilterContext` (pre), `PostInvokeContext` (post)

**Key Methods**:
- `InvokeAsync()`: Modify messages before LLM call
- `PostInvokeAsync()`: Process response after LLM call

---

### 2. AI Function Filters (IAiFunctionFilter)
**Location**: `HPD-Agent/Filters/AiFunctionOrchestrationContext.cs`

**When**: Before and after function execution
**What**: Logging, validation, transformation, observability
**Context**: `FunctionInvocationContext` (full orchestration)

**Key Capabilities**:
- Emit events via `context.Emit()`
- Wait for responses via `context.WaitForResponseAsync<T>()`
- Modify results via `context.Result`
- Control execution via `context.IsTerminated`

**Implementations**:
- `LoggingAiFunctionFilter`: Logs calls and results
- `ObservabilityAiFunctionFilter`: OpenTelemetry spans/metrics
- `PermissionFilter`: Permission checking
- `DynamicMemoryFilter`: Memory extraction
- `StaticMemoryFilter`: Memory injection
- `AgentPlanFilter`: Plan mode orchestration

---

### 3. Permission Filters (IPermissionFilter)
**Location**: `HPD-Agent/Permissions/IPermissionFilter.cs`

**When**: Before function execution
**What**: Check permissions, request user approval
**Context**: `FunctionInvocationContext` (inherits from IAiFunctionFilter)

**Primary Implementation**: `PermissionFilter`
- Checks function permission requirements
- Emits `InternalPermissionRequestEvent`
- Waits for user response
- Stores preferences persistently
- Handles timeouts gracefully

---

### 4. Message Turn Filters (IMessageTurnFilter)
**Location**: `HPD-Agent/Filters/Conversation/IConversationFilter.cs`

**When**: After complete message turn finishes
**What**: Analyze turns, extract insights, update external systems
**Context**: `MessageTurnFilterContext` (read-only)

**Characteristics**:
- Read-only access (mutations not persisted)
- Observational/analytical focus
- Used for telemetry and logging
- Executes after all tool calls complete

---

## Scoping System

### Three Scope Levels
1. **Global** (FilterScope.Global)
   - Applies to all functions
   - Use for universal concerns

2. **Plugin** (FilterScope.Plugin)
   - Applies to all functions in a plugin
   - Use for plugin-specific logic

3. **Function** (FilterScope.Function)
   - Applies to specific function only
   - Use for targeted logic

### Priority Order
When multiple filters apply, execution order:
1. Function-scoped filters (highest priority)
2. Plugin-scoped filters
3. Global filters (lowest priority)

### Management
- `ScopedFilterManager`: Stores and retrieves filters by scope
- `BuilderScopeContext`: Tracks current scope during configuration
- `GetApplicableFilters()`: Returns filters in priority order

---

## Filter Pipeline Execution

### Pipeline Building
Filters are composed using **reverse wrapping**:

```
Register: [Filter1, Filter2, Filter3]
Wrap in reverse: Filter1(Filter2(Filter3(Final)))
Execute: Filter1 → Filter2 → Filter3 → Final Action
```

### Static FilterChain Class
**Location**: `HPD-Agent/Agent/Agent.cs` (line 4692)

**Methods**:
- `BuildAiFunctionPipeline()`: Builds filter chain for functions
- `BuildPromptPipeline()`: Builds filter chain for prompts
- `BuildPermissionPipeline()`: Builds filter chain for permissions
- `BuildMessageTurnPipeline()`: Builds filter chain for turns

### Execution Control
Each filter can:
- Call `next(context)` to continue to next filter
- Skip calling `next()` to terminate early
- Modify `context.Result` to change output
- Set `context.IsTerminated = true` to signal halt

---

## Communication Patterns

### 1. Event Emission
Filter emits event for external handlers:
```csharp
context.Emit(new InternalPermissionRequestEvent(...));
```

### 2. Request/Response Pattern
Interactive filters wait for responses:
```csharp
var response = await context.WaitForResponseAsync<InternalPermissionResponseEvent>(
    requestId,
    timeout: TimeSpan.FromMinutes(5));
```

### 3. Result Modification
Filters can transform output:
```csharp
context.Result = customValue;
```

### 4. Execution Control
Filters can halt execution:
```csharp
context.IsTerminated = true;
```

---

## Event System

### Event Categories

**Permission Events**:
- `InternalPermissionRequestEvent`
- `InternalPermissionResponseEvent`
- `InternalPermissionApprovedEvent`
- `InternalPermissionDeniedEvent`

**Continuation Events**:
- `InternalContinuationRequestEvent`
- `InternalContinuationResponseEvent`

**Message Turn Events**:
- `InternalMessageTurnStartedEvent`
- `InternalMessageTurnFinishedEvent`
- `InternalMessageTurnErrorEvent`

**Agent Turn Events**:
- `InternalAgentTurnStartedEvent`
- `InternalAgentTurnFinishedEvent`
- `InternalStateSnapshotEvent`

**Content Events**:
- `InternalTextMessageStartEvent`
- `InternalTextDeltaEvent`
- `InternalTextMessageEndEvent`
- `InternalToolCallEvent`

**Error Events**:
- `InternalFilterErrorEvent`
- `InternalProviderErrorEvent`

### Event Flow
Events emitted through channels in real-time:
1. Filter emits event via `context.Emit()`
2. Background task reads from channel
3. External handler processes event
4. Handler sends response back
5. Waiting filter receives response

---

## Filter Registration

### Fluent API (AgentBuilder)

**AI Function Filters**:
```csharp
builder.WithFilter(filter)
builder.WithFilter<LoggingAiFunctionFilter>()
builder.WithFunctionInvocationFilters(filter1, filter2)
```

**Prompt Filters**:
```csharp
builder.WithPromptFilter(filter)
builder.WithPromptFilter<MyFilter>()
builder.WithPromptFilters(filter1, filter2)
```

**Permission Filters**:
```csharp
builder.WithPermissionFilter(filter)
```

**Message Turn Filters**:
```csharp
builder.WithMessageTurnFilter(filter)
builder.WithMessageTurnFilter<MyFilter>()
builder.WithMessageTurnFilters(filter1, filter2)
```

### Scoped Registration

```csharp
// Global filter
builder.WithFilter(globalFilter);

// Plugin-scoped filter
builder._scopeContext.SetPluginScope("MyPlugin");
builder.WithFilter(pluginFilter);

// Function-scoped filter
builder._scopeContext.SetFunctionScope("MyFunction");
builder.WithFilter(functionFilter);

// Back to global
builder._scopeContext.SetGlobalScope();
```

---

## Message Turn Flow

```
1. User sends message
   ↓
2. Message turn starts
   ↓
3. Prompt filters execute
   ├─ Modify messages
   ├─ Call LLM
   └─ Post-process response
   ↓
4. Parse function calls from response
   ↓
5. For each function call:
   ├─ Permission filters check access
   ├─ Build AI function pipeline
   │  ├─ Global filters
   │  ├─ Plugin-scoped filters
   │  └─ Function-scoped filters
   ├─ Execute pipeline
   │  ├─ Pre-execution filters
   │  ├─ Function invocation
   │  └─ Post-execution filters
   └─ Record result
   ↓
6. Message turn filters analyze completed turn
   ↓
7. Continue loop or finish
```

---

## Design Patterns

### Chain of Responsibility
Filters chained together, each calls next() to continue

### Event Emission
Filters emit events for external handlers to process

### Context Enrichment
Contexts carry state, filters enrich without modifying

### Scoped Registration
Filters target specific scopes (global/plugin/function)

### Async/Await Composition
Fully async-first, delegate-based pipelines

---

## Best Practices

1. **Single Responsibility**: Each filter handles one concern
2. **Non-Breaking Mutations**: Don't break downstream assumptions
3. **Error Handling**: Catch and handle gracefully
4. **Event Naming**: Use hierarchical names (`Internal{Type}{Action}Event`)
5. **Metadata Usage**: Use `context.Metadata` for custom data
6. **Appropriate Scoping**: Choose right scope level
7. **Observability**: Emit events for user-visible actions
8. **Timeout Management**: Set reasonable timeouts for interactive filters

---

## Key Files Reference

### Core Interfaces
| File | Purpose |
|------|---------|
| `HPD-Agent/Filters/PromptFiltering/IPromptFilter.cs` | Prompt filter interface |
| `HPD-Agent/Filters/AiFunctionOrchestrationContext.cs` | AI function filter interface |
| `HPD-Agent/Permissions/IPermissionFilter.cs` | Permission filter interface |
| `HPD-Agent/Filters/Conversation/IConversationFilter.cs` | Message turn filter interface |

### Implementation & Management
| File | Purpose |
|------|---------|
| `HPD-Agent/Filters/ScopedFilterSystem.cs` | Scoping management |
| `HPD-Agent/Agent/Agent.cs` (line 4692) | FilterChain class |
| `HPD-Agent/Agent/AgentBuilder.cs` (line 1569) | Registration extensions |

### Concrete Implementations
| File | Purpose |
|------|---------|
| `HPD-Agent/Filters/LoggingAiFunctionFilter.cs` | Logging implementation |
| `HPD-Agent/Filters/ObservabilityAiFunctionFilter.cs` | Telemetry implementation |
| `HPD-Agent/Permissions/PermissionFilter.cs` | Permission checking |
| `HPD-Agent/Memory/Agent/DynamicMemory/DynamicMemoryFilter.cs` | Memory extraction |
| `HPD-Agent/Memory/Agent/StaticMemory/StaticMemoryFilter.cs` | Memory injection |
| `HPD-Agent/Memory/Agent/PlanMode/AgentPlanFilter.cs` | Plan mode orchestration |

### Context Classes
| File | Purpose |
|------|---------|
| `HPD-Agent/Filters/PromptFiltering/PromptFilterContext.cs` | Pre-LLM context |
| `HPD-Agent/Filters/PromptFiltering/PostInvokeContext.cs` | Post-LLM context |

### Secondary System (HPD.Memory)
| File | Purpose |
|------|---------|
| `HPD.Memory/.../Pipeline/IPipelineHandler.cs` | Pipeline handler interface |
| `HPD.Memory/.../Pipeline/IPipelineOrchestrator.cs` | Pipeline orchestrator interface |

---

## Documentation Files

1. **MIDDLEWARE_ARCHITECTURE_ANALYSIS.md** (THIS PROJECT)
   - Full detailed analysis with code
   - Message turn flow diagrams
   - All components explained
   - Start here for deep understanding

2. **middleware_summary.txt** (TERMINAL)
   - Quick reference guide
   - All filter types at a glance
   - Key facts and locations
   - Best for quick lookup

3. **middleware_code_examples.md** (TERMINAL)
   - 8 practical code examples
   - Custom filter creation
   - Registration patterns
   - Event patterns
   - Integration patterns

---

## Next Steps

### For Implementation
1. Read "Four Filter Types" section above
2. Review code examples for your use case
3. Reference "Best Practices" section
4. Check concrete implementations for patterns

### For Understanding
1. Review "Architecture at a Glance" diagram
2. Read "Message Turn Flow" section
3. Study "Communication Patterns" section
4. Review "Design Patterns" section

### For Integration
1. Study "Filter Registration" section
2. Review AgentBuilder in `HPD-Agent/Agent/AgentBuilder.cs`
3. Follow "Scoped Registration" example
4. Implement custom filter using code examples

---

## Summary

The HPD-Agent middleware architecture provides a **flexible, composable system** for implementing cross-cutting concerns without coupling to core agent logic. It uses **four specialized filter types**, each with its own interface and execution stage, enabling clean separation of concerns while maintaining rich communication capabilities through events, responses, and context objects.

The architecture is **production-ready**, **well-tested**, and **extensively used** throughout the HPD-Agent codebase for logging, observability, permissions, memory management, and plan mode orchestration.

