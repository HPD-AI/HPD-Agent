# PROPOSAL: Unified Middleware Architecture

**Author:** Architecture Review
**Date:** 2025-11-28
**Status:** Proposed
**Complexity:** High (Breaking Change - Major Refactor)

---

## Executive Summary

This proposal recommends **unifying all middleware interfaces** into a single `IAgentMiddleware` interface with lifecycle hooks. This consolidation:

- Replaces 4 interfaces with 1
- Merges 3 context classes into 1
- Eliminates `PermissionManager` entirely
- Provides a consistent mental model for all middleware authors
- Reduces codebase by ~400-500 lines

**This is a breaking change.** All existing middleware implementations will need to be updated.

---

## Problem Statement

### Current Architecture Has Too Many Interfaces

```
┌─────────────────────────────────────────────────────────────────┐
│                    CURRENT MIDDLEWARE LANDSCAPE                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  IIterationMiddleware          IAIFunctionMiddleware            │
│  ├─ BeforeIterationAsync       ├─ InvokeAsync(context, next)    │
│  ├─ BeforeToolExecutionAsync   │                                │
│  └─ AfterIterationAsync        │   IPermissionMiddleware        │
│                                │   └─ (extends IAIFunction...)  │
│  Uses: IterationMiddlewareContext                               │
│                                Uses: AIFunctionInvocationContext │
│                                                                  │
│  IPromptMiddleware             IMessageTurnMiddleware           │
│  ├─ ProcessAsync               ├─ OnTurnAsync                   │
│  └─ PostInvokeAsync            │                                │
│                                                                  │
│  Uses: PromptMiddlewareContext Uses: MessageTurnMiddlewareContext│
└─────────────────────────────────────────────────────────────────┘
```

**Problems:**

1. **Cognitive overhead**: Developers must learn 4+ interfaces to understand the middleware system
2. **Context duplication**: `IterationMiddlewareContext` and `AIFunctionInvocationContext` share 80% of their properties
3. **Inconsistent patterns**: Some use lifecycle hooks, others use `next()` delegation
4. **Permission special-casing**: `IPermissionMiddleware` exists just to mark permission middlewares, requires a separate `PermissionManager` to orchestrate
5. **Pipeline complexity**: `MiddlewareChain` has separate builders for each middleware type

### The Permission System Is Overcomplicated

```
Current Permission Flow:
─────────────────────────────────────────────────────────────────
FunctionCallProcessor.ExecuteInParallelAsync()
    │
    ├─► PermissionManager.CheckPermissionsAsync()
    │       │
    │       ├─► Build function map (O(n))
    │       │
    │       └─► For each tool:
    │               │
    │               └─► CheckPermissionAsync()
    │                       │
    │                       ├─► Build AIFunctionInvocationContext
    │                       │
    │                       └─► ExecutePermissionPipeline()
    │                               │
    │                               └─► MiddlewareChain.BuildPermissionPipeline()
    │                                       │
    │                                       └─► PermissionMiddleware.InvokeAsync()
    │
    └─► Filter approved/denied → Execute approved tools
```

**This flow involves:**
- 1 manager class (`PermissionManager` - 140 lines)
- 1 marker interface (`IPermissionMiddleware`)
- 1 dedicated pipeline builder (`BuildPermissionPipeline`)
- 2 record types (`PermissionResult`, `PermissionBatchResult`)
- Separate context construction per function

All of this could be a single middleware with a `BeforeSequentialFunctionAsync` hook.

---

## Proposed Architecture

### Single Unified Interface

```csharp
/// <summary>
/// Unified middleware interface for all agent lifecycle hooks.
/// Implement only the hooks you need - all have default no-op implementations.
/// </summary>
public interface IAgentMiddleware
{
    // ═══════════════════════════════════════════════════════════════
    // MESSAGE TURN LEVEL (run once per user message)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Called BEFORE processing a user message turn.
    /// Use for: RAG injection, memory retrieval, context augmentation.
    /// </summary>
    /// <remarks>
    /// At this point:
    /// - Messages contains the conversation history + new user message
    /// - Options can be modified (tools, instructions, temperature)
    /// - No LLM call has been made yet
    /// </remarks>
    Task BeforeMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER a message turn completes (all iterations done).
    /// Use for: Memory extraction, analytics, turn-level logging.
    /// </summary>
    Task AfterMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════════
    // ITERATION LEVEL (run once per LLM call within a turn)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Called BEFORE each LLM call.
    /// Use for: Dynamic instruction injection, iteration-aware prompting.
    /// </summary>
    /// <remarks>
    /// Set SkipLLMCall = true to skip the LLM invocation entirely.
    /// If skipping, populate Response with the cached/computed response.
    /// </remarks>
    Task BeforeIterationAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER LLM returns but BEFORE any tools execute.
    /// Use for: Circuit breaker, batch validation, pre-execution guards.
    /// </summary>
    /// <remarks>
    /// At this point:
    /// - Response is populated with LLM output
    /// - ToolCalls contains pending function calls
    /// - Set SkipToolExecution = true to prevent ALL tools from running
    /// </remarks>
    Task BeforeToolExecutionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER all tools complete for this iteration.
    /// Use for: Error tracking, result analysis, state updates.
    /// </summary>
    /// <remarks>
    /// At this point:
    /// - ToolResults contains all function execution outcomes
    /// - Can inspect for errors, update state, emit events
    /// </remarks>
    Task AfterIterationAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════════
    // FUNCTION LEVEL (run once per function call)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Called BEFORE a specific function executes.
    /// Use for: Permission checking, argument validation, per-function guards.
    /// </summary>
    /// <remarks>
    /// At this point:
    /// - Function contains the AIFunction being called
    /// - FunctionCallId identifies this specific invocation
    /// - FunctionArguments contains the arguments
    /// - Set BlockFunctionExecution = true to prevent THIS function from running
    /// - Set FunctionResult to provide a result without execution
    /// </remarks>
    Task BeforeSequentialFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER a specific function completes.
    /// Use for: Result transformation, per-function logging, telemetry.
    /// </summary>
    /// <remarks>
    /// At this point:
    /// - FunctionResult contains the execution result
    /// - FunctionException contains any error (if failed)
    /// - Can modify FunctionResult to transform the output
    /// </remarks>
    Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;
}
```

### Single Unified Context

```csharp
/// <summary>
/// Unified context for all middleware hooks.
/// Properties are populated progressively as the agent executes.
/// </summary>
public class AgentMiddlewareContext
{
    // ═══════════════════════════════════════════════════════════════
    // IDENTITY (Always available)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Name of the agent executing this operation.</summary>
    public required string AgentName { get; init; }

    /// <summary>Unique identifier for this conversation/session.</summary>
    public required string? ConversationId { get; init; }

    /// <summary>Cancellation token for this operation.</summary>
    public required CancellationToken CancellationToken { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // STATE (Always available, immutable with scheduled updates)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Current agent loop state. Immutable - use UpdateState() to schedule changes.
    /// Includes: ActiveSkillInstructions, CompletedFunctions, MiddlewareStates, etc.
    /// </summary>
    public AgentLoopState State { get; private set; }

    /// <summary>
    /// Schedules a typed middleware state update.
    /// Updates are applied after the current middleware chain completes.
    /// </summary>
    public void UpdateState<TState>(Func<TState, TState> transform)
        where TState : class, IMiddlewareState;

    // ═══════════════════════════════════════════════════════════════
    // BIDIRECTIONAL EVENTS (Always available)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Emits an event to the agent's event stream for external handling.
    /// Events are delivered immediately (not batched).
    /// </summary>
    public void Emit(AgentEvent evt);

    /// <summary>
    /// Waits for a response event from external handlers.
    /// Used for interactive patterns: permissions, clarifications, approvals.
    /// </summary>
    public Task<T> WaitForResponseAsync<T>(
        string requestId,
        TimeSpan? timeout = null) where T : AgentEvent;

    // ═══════════════════════════════════════════════════════════════
    // MESSAGE TURN LEVEL (Available in BeforeMessageTurn, AfterMessageTurn)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>The user message that initiated this turn.</summary>
    public ChatMessage? UserMessage { get; set; }

    /// <summary>Complete conversation history.</summary>
    public IList<ChatMessage>? ConversationHistory { get; set; }

    /// <summary>Final assistant response for this turn (populated in AfterMessageTurn).</summary>
    public ChatMessage? FinalResponse { get; set; }

    /// <summary>All function calls made during this turn (populated in AfterMessageTurn).</summary>
    public IReadOnlyList<string>? TurnFunctionCalls { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // ITERATION LEVEL (Available in Before/AfterIteration, BeforeToolExecution)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Current iteration number (0-based). Iteration 0 = first LLM call.</summary>
    public int Iteration { get; set; }

    /// <summary>
    /// Messages to send to the LLM for this iteration.
    /// MUTABLE in BeforeIteration - add context, modify history.
    /// </summary>
    public IList<ChatMessage>? Messages { get; set; }

    /// <summary>
    /// Chat options for this LLM call.
    /// MUTABLE in BeforeIteration - modify tools, instructions, temperature.
    /// </summary>
    public ChatOptions? Options { get; set; }

    /// <summary>
    /// LLM response for this iteration.
    /// Populated AFTER LLM call completes (available in BeforeToolExecution, AfterIteration).
    /// </summary>
    public ChatMessage? Response { get; set; }

    /// <summary>
    /// Tool calls requested by LLM in this iteration.
    /// Populated AFTER LLM call (available in BeforeToolExecution, AfterIteration).
    /// </summary>
    public IReadOnlyList<FunctionCallContent> ToolCalls { get; set; }
        = Array.Empty<FunctionCallContent>();

    /// <summary>
    /// Results from tool execution.
    /// Populated AFTER tools execute (available in AfterIteration).
    /// </summary>
    public IReadOnlyList<FunctionResultContent> ToolResults { get; set; }
        = Array.Empty<FunctionResultContent>();

    /// <summary>Set to true in BeforeIteration to skip the LLM call.</summary>
    public bool SkipLLMCall { get; set; }

    /// <summary>Set to true in BeforeToolExecution to skip ALL pending tool executions.</summary>
    public bool SkipToolExecution { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // FUNCTION LEVEL (Available in Before/AfterFunction)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>The function being invoked (available in Before/AfterFunction).</summary>
    public AIFunction? Function { get; set; }

    /// <summary>Unique call ID for this function invocation.</summary>
    public string? FunctionCallId { get; set; }

    /// <summary>Arguments passed to this function call.</summary>
    public IDictionary<string, object?>? FunctionArguments { get; set; }

    /// <summary>
    /// Result of the function execution.
    /// In BeforeFunction: Set this to provide a result WITHOUT executing.
    /// In AfterFunction: Contains actual result, can be modified.
    /// </summary>
    public object? FunctionResult { get; set; }

    /// <summary>Exception from function execution (if failed).</summary>
    public Exception? FunctionException { get; set; }

    /// <summary>
    /// Set to true in BeforeFunction to block THIS function from executing.
    /// The function will not run; FunctionResult will be used as the result.
    /// </summary>
    public bool BlockFunctionExecution { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // EXTENSIBILITY
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Extensible property bag for inter-middleware communication.
    /// Use for: Passing data between middlewares, signaling, custom metadata.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>True if this is the first iteration (before any tool calls).</summary>
    public bool IsFirstIteration => Iteration == 0;

    /// <summary>True if LLM returned no tool calls (likely final response).</summary>
    public bool IsFinalIteration => Response != null && ToolCalls.Count == 0;

    /// <summary>True if currently in a function-level hook.</summary>
    public bool IsFunctionContext => Function != null;
}
```

---

## Migration: Existing Middlewares

### PermissionMiddleware (Before → After)

**BEFORE:**
```csharp
internal class PermissionMiddleware : IPermissionMiddleware
{
    public async Task InvokeAsync(
        AIFunctionInvocationContext context,
        Func<AIFunctionInvocationContext, Task> next)
    {
        var functionName = context.ToolCallRequest?.FunctionName ?? "Unknown";

        if (!RequiresPermission(context.Function))
        {
            await next(context);
            return;
        }

        // Check stored permissions...
        // Emit PermissionRequestEvent...
        // Wait for response...

        if (approved)
        {
            await next(context);
        }
        else
        {
            context.Result = denialReason;
            context.IsTerminated = true;
        }
    }
}
```

**AFTER:**
```csharp
public class PermissionMiddleware : IAgentMiddleware
{
    public async Task BeforeSequentialFunctionAsync(
        AgentMiddlewareContext context,
        CancellationToken ct)
    {
        var function = context.Function;
        if (function == null || !RequiresPermission(function))
            return;

        var functionName = function.Name;

        // Check stored permissions (same logic)
        var storedChoice = await GetStoredPermissionAsync(functionName, context.ConversationId);

        if (storedChoice == PermissionChoice.AlwaysAllow)
            return; // Approved - execution will proceed

        if (storedChoice == PermissionChoice.AlwaysDeny)
        {
            context.BlockFunctionExecution = true;
            context.FunctionResult = $"Execution of '{functionName}' was denied by stored preference.";
            return;
        }

        // No stored preference - request permission via events
        var permissionId = Guid.NewGuid().ToString();

        context.Emit(new PermissionRequestEvent(
            permissionId,
            "PermissionMiddleware",
            functionName,
            function.Description ?? "No description",
            context.FunctionCallId ?? "",
            context.FunctionArguments ?? new Dictionary<string, object?>()));

        var response = await context.WaitForResponseAsync<PermissionResponseEvent>(
            permissionId,
            TimeSpan.FromMinutes(5));

        if (response.Approved)
        {
            // Store choice if requested
            if (_storage != null && response.Choice != PermissionChoice.Ask)
            {
                await _storage.SavePermissionAsync(functionName, response.Choice, context.ConversationId);
            }
            // Don't set anything - execution will proceed
        }
        else
        {
            context.BlockFunctionExecution = true;
            context.FunctionResult = response.Reason ?? "Permission denied by user.";

            context.Emit(new PermissionDeniedEvent(permissionId, "PermissionMiddleware", response.Reason));
        }
    }
}
```

**What changed:**
- No more `next()` delegate - just return to allow execution
- Use `BlockFunctionExecution` instead of `IsTerminated`
- Use `FunctionResult` instead of `Result`
- Context properties have clearer names (`Function` not `context.Function`)

---

### CircuitBreakerMiddleware (Before → After)

**BEFORE:**
```csharp
public class CircuitBreakerIterationMiddleware : IIterationMiddleware
{
    public Task BeforeIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    public Task BeforeToolExecutionAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;

        var cbState = context.State.GetState<CircuitBreakerState>();

        foreach (var toolCall in context.ToolCalls)
        {
            var signature = ComputeSignature(toolCall);
            var predictedCount = cbState.GetPredictedCount(toolCall.Name, signature);

            if (predictedCount >= MaxConsecutiveCalls)
            {
                context.SkipToolExecution = true;
                context.Response = new ChatMessage(ChatRole.Assistant, "Circuit breaker triggered...");
                context.Properties["IsTerminated"] = true;
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    public Task AfterIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // Record executed tools
        context.UpdateState<CircuitBreakerState>(s => /* update */);
        return Task.CompletedTask;
    }
}
```

**AFTER:**
```csharp
public class CircuitBreakerMiddleware : IAgentMiddleware
{
    public Task BeforeToolExecutionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;

        var cbState = context.State.GetState<CircuitBreakerState>();

        foreach (var toolCall in context.ToolCalls)
        {
            var signature = ComputeSignature(toolCall);
            var predictedCount = cbState.GetPredictedCount(toolCall.Name, signature);

            if (predictedCount >= MaxConsecutiveCalls)
            {
                context.SkipToolExecution = true;
                context.Response = new ChatMessage(ChatRole.Assistant, "Circuit breaker triggered...");
                context.Properties["IsTerminated"] = true;
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    public Task AfterIterationAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        context.UpdateState<CircuitBreakerState>(s => /* update */);
        return Task.CompletedTask;
    }
}
```

**What changed:**
- Interface name: `IIterationMiddleware` → `IAgentMiddleware`
- Context type: `IterationMiddlewareContext` → `AgentMiddlewareContext`
- Removed empty `BeforeIterationAsync` (default implementation handles it)
- Everything else is identical!

---

### LoggingMiddleware (Before → After)

**BEFORE:**
```csharp
public class LoggingAIFunctionMiddleware : IAIFunctionMiddleware
{
    public async Task InvokeAsync(
        AIFunctionInvocationContext context,
        Func<AIFunctionInvocationContext, Task> next)
    {
        _logger.LogInformation("Calling {Function}", context.FunctionName);

        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        _logger.LogInformation("Completed {Function} in {Elapsed}ms",
            context.FunctionName, stopwatch.ElapsedMilliseconds);
    }
}
```

**AFTER:**
```csharp
public class LoggingMiddleware : IAgentMiddleware
{
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();

    public Task BeforeSequentialFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        var callId = context.FunctionCallId ?? Guid.NewGuid().ToString();
        _logger.LogInformation("Calling {Function}", context.Function?.Name);
        _timers[callId] = Stopwatch.StartNew();
        return Task.CompletedTask;
    }

    public Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        var callId = context.FunctionCallId ?? "";
        if (_timers.TryRemove(callId, out var stopwatch))
        {
            stopwatch.Stop();
            _logger.LogInformation("Completed {Function} in {Elapsed}ms",
                context.Function?.Name, stopwatch.ElapsedMilliseconds);
        }
        return Task.CompletedTask;
    }
}
```

**What changed:**
- No more wrapping with `next()` - use Before/After hooks
- Need to track timing across hooks (minor complexity increase)
- But: Can now also hook into iteration-level events if needed!

---

## Execution Model

### New Execution Flow

```
User Message Arrives
    │
    ├─► [BeforeMessageTurnAsync] ─── All middlewares, in order
    │
    └─► AGENTIC LOOP ◄─────────────────────────────────────┐
            │                                               │
            ├─► [BeforeIterationAsync] ─── All middlewares  │
            │                                               │
            ├─► LLM CALL (unless SkipLLMCall)               │
            │                                               │
            ├─► [BeforeToolExecutionAsync] ─── All middlewares
            │       │                                       │
            │       └─► Check SkipToolExecution             │
            │                                               │
            ├─► FOR EACH TOOL CALL ◄───────────────────┐    │
            │       │                                  │    │
            │       ├─► [BeforeSequentialFunctionAsync] ─── All  │    │
            │       │       │                          │    │
            │       │       └─► Check BlockFunctionExecution
            │       │                                  │    │
            │       ├─► EXECUTE FUNCTION (unless blocked)   │
            │       │                                  │    │
            │       ├─► [AfterFunctionAsync] ─── All   │    │
            │       │                                  │    │
            │       └───────────────────────────────────┘    │
            │                                               │
            ├─► [AfterIterationAsync] ─── All middlewares   │
            │                                               │
            └─► Continue if more tool calls ────────────────┘
    │
    └─► [AfterMessageTurnAsync] ─── All middlewares
```

### Middleware Execution Order

All middlewares run in **registration order** for each hook:

```csharp
var agent = new AgentBuilder()
    .WithMiddleware(new LoggingMiddleware())      // Runs 1st
    .WithMiddleware(new PermissionMiddleware())   // Runs 2nd
    .WithMiddleware(new CircuitBreakerMiddleware()) // Runs 3rd
    .Build();

// BeforeSequentialFunctionAsync execution order:
// 1. LoggingMiddleware.BeforeSequentialFunctionAsync
// 2. PermissionMiddleware.BeforeSequentialFunctionAsync  <-- Can block here
// 3. CircuitBreakerMiddleware.BeforeSequentialFunctionAsync

// AfterFunctionAsync execution order (reverse for "unwinding"):
// 1. CircuitBreakerMiddleware.AfterFunctionAsync
// 2. PermissionMiddleware.AfterFunctionAsync
// 3. LoggingMiddleware.AfterFunctionAsync
```

---

## What Gets Deleted

### Interfaces (4 → 1)

| Delete | Lines | Replacement |
|--------|-------|-------------|
| `IIterationMiddleware` | ~60 | `IAgentMiddleware` iteration hooks |
| `IAIFunctionMiddleware` | ~15 | `IAgentMiddleware` function hooks |
| `IPermissionMiddleware` | ~10 | Deleted entirely (was just a marker) |
| `IPromptMiddleware` | ~40 | `IAgentMiddleware.BeforeMessageTurnAsync` |

### Context Classes (3 → 1)

| Delete | Lines | Replacement |
|--------|-------|-------------|
| `IterationMiddlewareContext` | ~320 | `AgentMiddlewareContext` |
| `AIFunctionInvocationContext` | ~200 | `AgentMiddlewareContext` |
| `PromptMiddlewareContext` | ~80 | `AgentMiddlewareContext` |

### Manager Classes

| Delete | Lines | Reason |
|--------|-------|--------|
| `PermissionManager` | ~140 | Logic moves to `PermissionMiddleware.BeforeSequentialFunctionAsync` |
| `PermissionResult` | ~15 | No longer needed |
| `PermissionBatchResult` | ~10 | No longer needed |

### Pipeline Builders

| Delete | Lines | Reason |
|--------|-------|--------|
| `MiddlewareChain.BuildPermissionPipeline` | ~20 | Unified pipeline |
| `MiddlewareChain.BuildAiFunctionPipeline` | ~20 | Unified pipeline |
| `MiddlewareChain.BuildPromptPipeline` | ~20 | Unified pipeline |

### Estimated Total Deletion: ~450 lines

---

## Implementation Plan

### Phase 1: Create New Infrastructure (Non-Breaking)

**Files to create:**

1. `HPD-Agent/Middleware/IAgentMiddleware.cs`
   - New unified interface with all lifecycle hooks
   - Default implementations for all methods

2. `HPD-Agent/Middleware/AgentMiddlewareContext.cs`
   - New unified context class
   - All properties from existing contexts

3. `HPD-Agent/Middleware/AgentMiddlewarePipeline.cs`
   - New execution engine for lifecycle hooks
   - Handles Before/After ordering

**Estimated effort:** 4-6 hours

### Phase 2: Migrate Existing Middlewares

**Files to update:**

1. `PermissionMiddleware` → Implement `IAgentMiddleware.BeforeSequentialFunctionAsync`
2. `CircuitBreakerIterationMiddleware` → Implement `IAgentMiddleware` hooks
3. `ContinuationPermissionIterationMiddleware` → Implement `IAgentMiddleware.BeforeIterationAsync`
4. `ErrorTrackingIterationMiddleware` → Implement `IAgentMiddleware.AfterIterationAsync`
5. `LoggingAIFunctionMiddleware` → Implement `IAgentMiddleware` function hooks
6. `SkillInstructionIterationMiddleware` → Implement `IAgentMiddleware` hooks
7. All `IPromptMiddleware` implementations → Implement `IAgentMiddleware.BeforeMessageTurnAsync`

**Estimated effort:** 6-8 hours

### Phase 3: Update Agent Integration

**Files to update:**

1. `Agent.cs`
   - Replace multiple middleware lists with single `List<IAgentMiddleware>`
   - Update `RunAgenticLoopInternal` to call unified hooks
   - Remove `PermissionManager` integration
   - Update function execution to call Before/AfterFunction hooks

2. `AgentBuilder.cs`
   - Replace `WithIterationMiddleware`, `WithFunctionMiddleware`, etc.
   - Single `WithMiddleware(IAgentMiddleware)` method

3. `FunctionCallProcessor.cs`
   - Remove permission checking (now in middleware)
   - Call Before/AfterFunction hooks during execution

**Estimated effort:** 8-10 hours

### Phase 4: Delete Old Code

**Files to delete:**

1. `IIterationMiddleware.cs`
2. `IterationMiddlewareContext.cs`
3. `IAIFunctionMiddleware` from `AiFunctionOrchestrationContext.cs`
4. `AIFunctionInvocationContext` from `AiFunctionOrchestrationContext.cs`
5. `IPermissionMiddleware.cs`
6. `PermissionManager` section from `Agent.cs`
7. Old `MiddlewareChain` builder methods

**Estimated effort:** 2-3 hours

### Phase 5: Update Tests

**Test files to update:**

1. All middleware unit tests
2. Integration tests using middlewares
3. Permission system tests

**Estimated effort:** 6-8 hours

### Phase 6: Update Documentation

1. `ARCHITECTURE_OVERVIEW.md`
2. `PERMISSION_SYSTEM_GUIDE.md`
3. `PERMISSION_SYSTEM_API.md`
4. API documentation / XML comments

**Estimated effort:** 3-4 hours

---

## Total Effort Estimate

| Phase | Effort |
|-------|--------|
| Phase 1: Create Infrastructure | 4-6 hours |
| Phase 2: Migrate Middlewares | 6-8 hours |
| Phase 3: Update Agent | 8-10 hours |
| Phase 4: Delete Old Code | 2-3 hours |
| Phase 5: Update Tests | 6-8 hours |
| Phase 6: Documentation | 3-4 hours |
| **Total** | **29-39 hours** |

---

## Benefits Summary

| Aspect | Before | After |
|--------|--------|-------|
| Interfaces to learn | 4+ | 1 |
| Context classes | 3+ | 1 |
| Lines of code | ~950 | ~500 |
| Mental model | "Which interface?" | "Which hook?" |
| Permission handling | Manager + Interface + Pipeline | Single middleware |
| Adding new middleware | Choose interface, learn context | Implement hooks you need |

---

## Risks and Mitigations

### Risk 1: Breaking All Existing Middlewares

**Mitigation:**
- Clear migration guide (in this document)
- Each middleware migration is mechanical
- Can create adapter layer temporarily if needed

### Risk 2: Performance Impact from Hook Calls

**Mitigation:**
- Default implementations are no-ops (JIT should optimize)
- Benchmark before/after
- Hook calls are just method invocations (negligible overhead)

### Risk 3: Losing Pipeline Wrapping Capability

**Mitigation:**
- Before/After hooks can achieve the same result
- For timing: use Before to start timer, After to stop
- For try/catch: framework handles exceptions between hooks

### Risk 4: Context Becoming Too Large

**Mitigation:**
- Properties are nullable where appropriate
- Only populated at relevant lifecycle stages
- Clear documentation on when each property is available

---

## Decision

**RECOMMENDED: APPROVE**

This refactoring:

✅ Dramatically simplifies the middleware architecture
✅ Eliminates redundant abstractions
✅ Provides consistent patterns across all concerns
✅ Makes permission handling a first-class middleware (not special-cased)
✅ Reduces codebase by ~450 lines
✅ Easier for new developers to understand

The breaking change cost is justified by the long-term maintainability gains.

---

## Appendix A: Complete IAgentMiddleware Interface

```csharp
using Microsoft.Extensions.AI;

namespace HPD.Agent.Middleware;

/// <summary>
/// Unified middleware interface for all agent lifecycle hooks.
/// Implement only the hooks you need - all have default no-op implementations.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle Overview:</b></para>
/// <code>
/// BeforeMessageTurnAsync
///   └─► [LOOP] BeforeIterationAsync
///               └─► LLM Call
///               └─► BeforeToolExecutionAsync
///                     └─► [LOOP] BeforeSequentialFunctionAsync
///                                  └─► Function Execution
///                                  └─► AfterFunctionAsync
///               └─► AfterIterationAsync
///   └─► AfterMessageTurnAsync
/// </code>
///
/// <para><b>Execution Order:</b></para>
/// <para>
/// Before* hooks run in registration order.
/// After* hooks run in reverse registration order (stack unwinding).
/// </para>
///
/// <para><b>Blocking Execution:</b></para>
/// <list type="bullet">
/// <item>Set <c>SkipLLMCall = true</c> in BeforeIteration to skip LLM</item>
/// <item>Set <c>SkipToolExecution = true</c> in BeforeToolExecution to skip ALL tools</item>
/// <item>Set <c>BlockFunctionExecution = true</c> in BeforeFunction to skip ONE function</item>
/// </list>
/// </remarks>
public interface IAgentMiddleware
{
    /// <summary>
    /// Called BEFORE processing a user message turn.
    /// Use for: RAG injection, memory retrieval, context augmentation.
    /// </summary>
    Task BeforeMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER a message turn completes.
    /// Use for: Memory extraction, analytics, turn-level logging.
    /// </summary>
    Task AfterMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called BEFORE each LLM call within a turn.
    /// Use for: Dynamic instruction injection, caching, iteration-aware prompting.
    /// </summary>
    Task BeforeIterationAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER LLM returns but BEFORE tools execute.
    /// Use for: Circuit breaker, batch validation, pre-execution guards.
    /// </summary>
    Task BeforeToolExecutionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER all tools complete for an iteration.
    /// Use for: Error tracking, result analysis, state updates.
    /// </summary>
    Task AfterIterationAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called BEFORE a specific function executes.
    /// Use for: Permission checking, argument validation, per-function guards.
    /// </summary>
    Task BeforeSequentialFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Called AFTER a specific function completes.
    /// Use for: Result transformation, per-function logging, telemetry.
    /// </summary>
    Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;
}
```

---

**End of Proposal**