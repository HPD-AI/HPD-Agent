# Middleware State Ownership Proposal v2: Static Abstract Interface Pattern

## Summary

This proposal builds on the original Middleware State Ownership Proposal, adopting its core insight that **middleware should own its state**, while presenting a more C#-idiomatic implementation using **static abstract interface members** (C# 11+).

The key difference: instead of `ExportState()`/`ImportState()` methods with instance fields, we use **stateless middleware with typed state records** that flow through the context.

---

## Core Principle (Unchanged from Original)

> **Middleware state is middleware's concern, not Agent's concern.**

Agent should only:
1. Call middleware hooks at the right time
2. Persist/restore opaque middleware state during checkpointing
3. Flow state through context (never store in middleware instances)

---

## The Problem with Instance Fields

The original proposal stored state in middleware instance fields:

```csharp
public class CircuitBreakerMiddleware : IStatefulIterationMiddleware
{
    // ⚠️ PROBLEM: Instance fields break thread safety
    // Agent is documented as stateless - multiple concurrent RunAsync() 
    // calls share the same middleware instances
    private ImmutableDictionary<string, int> _consecutiveCountPerTool;
    
    public object? ExportState() => new CircuitBreakerState { ... };
    public void ImportState(object? state) { ... }
}
```

This violates `Agent`'s thread-safety guarantee documented in the class header:

> *"This Agent instance is now fully stateless and thread-safe. Multiple concurrent RunAsync() calls on the same instance are supported."*

---

## Proposed Solution: Static Abstract Interface Pattern

### 1. State Interface

```csharp
/// <summary>
/// Marker interface for middleware state records.
/// Uses C# 11 static abstract to tie the key to the type itself.
/// </summary>
public interface IMiddlewareState
{
    /// <summary>
    /// Unique key for this state type. Convention: "Namespace.MiddlewareName"
    /// </summary>
    static abstract string Key { get; }
    
    /// <summary>
    /// Creates default/initial state. Called when no persisted state exists.
    /// </summary>
    static abstract IMiddlewareState CreateDefault();
}
```

### 2. State Access Extensions

```csharp
/// <summary>
/// Type-safe extensions for accessing middleware state.
/// No string keys in user code - the key is tied to the type.
/// </summary>
public static class MiddlewareStateExtensions
{
    /// <summary>
    /// Gets middleware state, returning default if not present.
    /// </summary>
    public static TState GetState<TState>(this AgentLoopState state)
        where TState : class, IMiddlewareState
    {
        if (state.MiddlewareStates.TryGetValue(TState.Key, out var stored) && stored is TState typed)
            return typed;
        
        return (TState)TState.CreateDefault();
    }
    
    /// <summary>
    /// Returns new AgentLoopState with updated middleware state.
    /// </summary>
    public static AgentLoopState WithState<TState>(this AgentLoopState state, TState value)
        where TState : class, IMiddlewareState
    {
        return state with 
        { 
            MiddlewareStates = state.MiddlewareStates.SetItem(TState.Key, value) 
        };
    }
    
    /// <summary>
    /// Returns new AgentLoopState with middleware state transformed by the given function.
    /// </summary>
    public static AgentLoopState UpdateState<TState>(
        this AgentLoopState state, 
        Func<TState, TState> transform)
        where TState : class, IMiddlewareState
    {
        var current = state.GetState<TState>();
        var updated = transform(current);
        return state.WithState(updated);
    }
}
```

### 3. Context Update API

```csharp
public class IterationMiddlewareContext
{
    private AgentLoopState _originalState;
    private AgentLoopState? _pendingState;
    
    /// <summary>
    /// Current state (includes any pending updates from this middleware chain).
    /// </summary>
    public AgentLoopState State => _pendingState ?? _originalState;
    
    /// <summary>
    /// Schedules a state update. Updates are applied after the middleware chain completes.
    /// Multiple calls are composed (each transform sees the result of previous transforms).
    /// </summary>
    public void UpdateState(Func<AgentLoopState, AgentLoopState> transform)
    {
        _pendingState = transform(_pendingState ?? _originalState);
    }
    
    /// <summary>
    /// Convenience method for updating typed middleware state.
    /// </summary>
    public void UpdateState<TState>(Func<TState, TState> transform)
        where TState : class, IMiddlewareState
    {
        UpdateState(state => state.UpdateState(transform));
    }
    
    /// <summary>
    /// Gets pending state updates (called by Agent after middleware chain).
    /// </summary>
    internal AgentLoopState? GetPendingState() => _pendingState;
}
```

### 4. Simplified AgentLoopState

```csharp
public sealed record AgentLoopState
{
    // ═══════════════════════════════════════════════════════
    // CORE LOOP STATE (owned by Agent)
    // ═══════════════════════════════════════════════════════
    
    public required string RunId { get; init; }
    public required string ConversationId { get; init; }
    public required string AgentName { get; init; }
    public required int Iteration { get; init; }
    public required bool IsTerminated { get; init; }
    public string? TerminationReason { get; init; }
    public required IReadOnlyList<ChatMessage> CurrentMessages { get; init; }
    public required IReadOnlyList<ChatMessage> TurnHistory { get; init; }
    public required ImmutableHashSet<string> CompletedFunctions { get; init; }
    
    // ═══════════════════════════════════════════════════════
    // CHECKPOINTING
    // ═══════════════════════════════════════════════════════
    
    public string? ETag { get; init; }
    public int Version { get; init; } = 1;
    public CheckpointMetadata? Metadata { get; init; }
    
    // ═══════════════════════════════════════════════════════
    // MIDDLEWARE STATE (owned by middlewares)
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Opaque state storage for stateful middlewares.
    /// Keys are IMiddlewareState.Key values (tied to types).
    /// Access via extension methods: state.GetState&lt;T&gt;(), state.WithState(value)
    /// </summary>
    public ImmutableDictionary<string, object> MiddlewareStates { get; init; }
        = ImmutableDictionary<string, object>.Empty;
    
    // ═══════════════════════════════════════════════════════
    // CORE STATE TRANSITIONS (kept minimal)
    // ═══════════════════════════════════════════════════════
    
    public AgentLoopState NextIteration() => this with { Iteration = Iteration + 1 };
    
    public AgentLoopState Terminate(string reason) => 
        this with { IsTerminated = true, TerminationReason = reason };
    
    public AgentLoopState WithMessages(IReadOnlyList<ChatMessage> messages) =>
        this with { CurrentMessages = messages };
    
    public AgentLoopState CompleteFunction(string functionName) =>
        this with { CompletedFunctions = CompletedFunctions.Add(functionName) };
    
    // Factory
    public static AgentLoopState Initial(
        IReadOnlyList<ChatMessage> messages, 
        string runId, 
        string conversationId, 
        string agentName) => new()
    {
        RunId = runId,
        ConversationId = conversationId,
        AgentName = agentName,
        Iteration = 0,
        IsTerminated = false,
        CurrentMessages = messages,
        TurnHistory = ImmutableList<ChatMessage>.Empty,
        CompletedFunctions = ImmutableHashSet<string>.Empty,
        MiddlewareStates = ImmutableDictionary<string, object>.Empty,
        Version = 1
    };
}
```

---

## Example: Circuit Breaker Middleware

### State Record

```csharp
/// <summary>
/// State for circuit breaker tracking. Immutable record with static abstract key.
/// </summary>
public sealed record CircuitBreakerState : IMiddlewareState
{
    public static string Key => "HPD.Agent.CircuitBreaker";
    
    public static IMiddlewareState CreateDefault() => new CircuitBreakerState();
    
    /// <summary>
    /// Last function signature per tool (for detecting identical calls).
    /// </summary>
    public ImmutableDictionary<string, string> LastSignaturePerTool { get; init; }
        = ImmutableDictionary<string, string>.Empty;
    
    /// <summary>
    /// Consecutive identical call count per tool.
    /// </summary>
    public ImmutableDictionary<string, int> ConsecutiveCountPerTool { get; init; }
        = ImmutableDictionary<string, int>.Empty;
}
```

### Middleware Implementation

```csharp
/// <summary>
/// Prevents infinite loops by detecting repeated identical function calls.
/// STATELESS - all state flows through context.
/// </summary>
public class CircuitBreakerMiddleware : IIterationMiddleware
{
    // ═══════════════════════════════════════════════════════
    // CONFIGURATION (not state - set at registration time)
    // ═══════════════════════════════════════════════════════
    
    public int MaxConsecutiveCalls { get; set; } = 3;
    
    public string TerminationMessageTemplate { get; set; } =
        "⚠️ Circuit breaker triggered: Function '{toolName}' called {count} times with identical arguments.";
    
    // ═══════════════════════════════════════════════════════
    // HOOKS (stateless - read/write via context)
    // ═══════════════════════════════════════════════════════
    
    public Task BeforeIterationAsync(
        IterationMiddlewareContext context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    public Task BeforeToolExecutionAsync(
        IterationMiddlewareContext context,
        CancellationToken cancellationToken)
    {
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;
        
        // Read state from context (type-safe, no string keys!)
        var state = context.State.GetState<CircuitBreakerState>();
        
        foreach (var toolCall in context.ToolCalls)
        {
            var toolName = toolCall.Name ?? "_unknown";
            var signature = ComputeSignature(toolCall);
            
            var lastSig = state.LastSignaturePerTool.GetValueOrDefault(toolName);
            var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;
            
            var predictedCount = isIdentical
                ? state.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1
                : 1;
            
            if (predictedCount >= MaxConsecutiveCalls)
            {
                TriggerCircuitBreaker(context, toolName, predictedCount);
                return Task.CompletedTask;
            }
        }
        
        return Task.CompletedTask;
    }
    
    public Task AfterIterationAsync(
        IterationMiddlewareContext context,
        CancellationToken cancellationToken)
    {
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;
        
        // Update state immutably via context
        context.UpdateState<CircuitBreakerState>(state =>
        {
            var newSignatures = state.LastSignaturePerTool;
            var newCounts = state.ConsecutiveCountPerTool;
            
            foreach (var toolCall in context.ToolCalls)
            {
                var toolName = toolCall.Name ?? "_unknown";
                var signature = ComputeSignature(toolCall);
                
                var lastSig = state.LastSignaturePerTool.GetValueOrDefault(toolName);
                var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;
                
                newSignatures = newSignatures.SetItem(toolName, signature);
                newCounts = newCounts.SetItem(
                    toolName,
                    isIdentical ? state.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1 : 1);
            }
            
            return state with
            {
                LastSignaturePerTool = newSignatures,
                ConsecutiveCountPerTool = newCounts
            };
        });
        
        return Task.CompletedTask;
    }
    
    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════
    
    private static string ComputeSignature(FunctionCallContent toolCall)
    {
        var name = toolCall.Name ?? "_unknown";
        var argsJson = toolCall.Arguments?.Count > 0
            ? JsonSerializer.Serialize(toolCall.Arguments.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value))
            : "{}";
        return $"{name}({argsJson})";
    }
    
    private void TriggerCircuitBreaker(IterationMiddlewareContext context, string toolName, int count)
    {
        context.SkipToolExecution = true;
        context.Properties["IsTerminated"] = true;
        
        var message = TerminationMessageTemplate
            .Replace("{toolName}", toolName)
            .Replace("{count}", count.ToString());
        
        context.Emit(new TextDeltaEvent(message, Guid.NewGuid().ToString()));
        context.Emit(new CircuitBreakerTriggeredEvent(
            context.AgentName, toolName, count, context.Iteration, DateTimeOffset.UtcNow));
    }
}
```

---

## Example: Custom Cost Tracking Middleware

Shows how third-party middleware authors use the same pattern:

```csharp
// State record
public sealed record CostTrackingState : IMiddlewareState
{
    public static string Key => "MyCompany.CostTracking";
    public static IMiddlewareState CreateDefault() => new CostTrackingState();
    
    public decimal TotalCost { get; init; }
    public int TotalTokens { get; init; }
    public ImmutableList<CostEntry> History { get; init; } = ImmutableList<CostEntry>.Empty;
}

public record CostEntry(DateTime Timestamp, int Tokens, decimal Cost);

// Middleware
public class CostTrackingMiddleware : IIterationMiddleware
{
    public decimal MaxBudget { get; set; } = 10.00m;
    public decimal CostPerToken { get; set; } = 0.00001m;
    
    public Task BeforeIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // Check budget before making LLM call
        var state = context.State.GetState<CostTrackingState>();
        
        if (state.TotalCost >= MaxBudget)
        {
            context.SkipLLMCall = true;
            context.Properties["IsTerminated"] = true;
            context.Properties["TerminationReason"] = $"Budget exhausted: ${state.TotalCost:F4} / ${MaxBudget:F2}";
        }
        
        return Task.CompletedTask;
    }
    
    public Task BeforeToolExecutionAsync(IterationMiddlewareContext context, CancellationToken ct)
        => Task.CompletedTask;
    
    public Task AfterIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        var tokens = EstimateTokens(context.Response);
        var cost = tokens * CostPerToken;
        
        context.UpdateState<CostTrackingState>(state => state with
        {
            TotalCost = state.TotalCost + cost,
            TotalTokens = state.TotalTokens + tokens,
            History = state.History.Add(new CostEntry(DateTime.UtcNow, tokens, cost))
        });
        
        // Emit for observability
        context.Emit(new CostRecordedEvent(context.AgentName, tokens, cost, context.Iteration));
        
        return Task.CompletedTask;
    }
    
    private static int EstimateTokens(ChatMessage? response)
        => response?.Text?.Length / 4 ?? 0; // Rough estimate
}

public record CostRecordedEvent(
    string AgentName, 
    int Tokens, 
    decimal Cost, 
    int Iteration) : AgentEvent;
```

---

## Agent Integration

```csharp
internal sealed class Agent
{
    private async IAsyncEnumerable<AgentEvent> RunAgenticLoopInternal(...)
    {
        // ... existing setup ...
        
        while (!state.IsTerminated && state.Iteration <= config.MaxIterations)
        {
            var middlewareContext = new IterationMiddlewareContext
            {
                Iteration = state.Iteration,
                AgentName = _name,
                Messages = messagesToSend.ToList(),
                Options = scopedOptions,
                State = state,  // Pass current state
                CancellationToken = cancellationToken,
                EventCoordinator = _eventCoordinator
            };
            
            // Execute middleware chain
            foreach (var middleware in _iterationMiddlewares)
            {
                await middleware.BeforeIterationAsync(middlewareContext, cancellationToken);
            }
            
            // ... LLM call ...
            
            foreach (var middleware in _iterationMiddlewares)
            {
                await middleware.BeforeToolExecutionAsync(middlewareContext, cancellationToken);
            }
            
            // ... tool execution ...
            
            foreach (var middleware in _iterationMiddlewares)
            {
                await middleware.AfterIterationAsync(middlewareContext, cancellationToken);
            }
            
            // Apply pending state updates from middleware
            if (middlewareContext.GetPendingState() is { } pendingState)
            {
                state = pendingState;
            }
            
            // ... rest of loop ...
        }
    }
}
```

---

## Serialization

State records must be JSON-serializable. The `IMiddlewareState.Key` serves as the type discriminator:

```csharp
// Serialization
public string Serialize(AgentLoopState state)
{
    var serializable = new SerializableAgentLoopState
    {
        // ... core fields ...
        MiddlewareStates = state.MiddlewareStates
            .Select(kvp => new MiddlewareStateEntry
            {
                Key = kvp.Key,
                TypeName = kvp.Value.GetType().AssemblyQualifiedName,
                Data = JsonSerializer.SerializeToElement(kvp.Value)
            })
            .ToList()
    };
    
    return JsonSerializer.Serialize(serializable);
}

// Deserialization requires type registry (populated at startup from registered middlewares)
private readonly Dictionary<string, Type> _stateTypeRegistry = new();

public void RegisterMiddleware<TMiddleware>() where TMiddleware : IIterationMiddleware
{
    // Discover IMiddlewareState types via reflection/source generation
    // Register: Key -> Type mapping
}
```

---

## Migration Path

### Phase 1: Add Infrastructure (Non-Breaking)

1. Add `IMiddlewareState` interface
2. Add `MiddlewareStates` to `AgentLoopState`
3. Add extension methods
4. Add `UpdateState` to context
5. **Keep existing fields** for backward compatibility

### Phase 2: Migrate Built-in Middlewares

1. Create state records for `CircuitBreakerMiddleware`, `ErrorTrackingMiddleware`
2. Migrate to use `context.State.GetState<T>()` pattern
3. Remove reads from old `AgentLoopState` fields
4. Mark old fields `[Obsolete("Use MiddlewareStates with IMiddlewareState pattern")]`

### Phase 3: Remove Deprecated Fields (Breaking)

1. Remove deprecated fields from `AgentLoopState`
2. Update serialization/deserialization
3. Provide migration guide

---

## Comparison with Original Proposal

| Aspect | Original Proposal | This Proposal |
|--------|-------------------|---------------|
| State location | Middleware instance fields | Context (flows through) |
| Thread safety | ⚠️ Violated | ✅ Preserved |
| Type safety | ✅ Via state records | ✅ Via static abstract + generics |
| Boilerplate | Medium (Export/Import/Reset) | Low (just state record) |
| Key management | String in middleware | Tied to type (static abstract) |
| C# idiom | Moderate | High (modern C# patterns) |
| AOT compatible | ✅ | ✅ |

---

## Benefits

1. **Thread Safety Preserved** - No mutable instance fields, state flows through context
2. **Type Safety** - Static abstract ties key to type, extension methods provide typed access
3. **Clean Ownership** - Middleware defines its state record, Agent is agnostic
4. **Extensibility** - Third-party middlewares get first-class state support
5. **Consistency** - Built-in and custom middlewares use identical pattern
6. **Testability** - Middlewares are stateless, state can be injected via context
7. **Modern C#** - Leverages C# 11 static abstract members
8. **Minimal Boilerplate** - Just define a state record with `IMiddlewareState`

---

## Open Questions

1. **Serialization registry**: Should middleware registration auto-populate type registry, or require explicit registration?

2. **State validation**: Should `IMiddlewareState` include a `Validate()` method for post-deserialization checks?

3. **State versioning**: How to handle state schema changes across HPD-Agent versions? Add `Version` to `IMiddlewareState`?

4. **Middleware ordering**: Should state updates be applied after each middleware or after the full chain?

---

## Recommendation

Implement Phase 1 immediately - it's fully backward compatible and validates the pattern. The infrastructure is small and low-risk.

The core insight remains the same as the original proposal: **middleware state is middleware's concern**. This refinement simply ensures we maintain Agent's thread-safety guarantee while providing a more C#-idiomatic API.