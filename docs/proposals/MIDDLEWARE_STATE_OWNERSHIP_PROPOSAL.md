# Middleware State Ownership Proposal

## Problem Statement

The current `AgentLoopState` has hardcoded fields for built-in middleware features:

```csharp
public sealed record AgentLoopState
{
    // Circuit breaker state
    public required ImmutableDictionary<string, string> LastSignaturePerTool { get; init; }
    public required ImmutableDictionary<string, int> ConsecutiveCountPerTool { get; init; }

    // Error tracking state
    public required int ConsecutiveFailures { get; init; }

    // Plugin scoping state
    public required ImmutableHashSet<string> expandedScopedPluginContainers { get; init; }
    public required ImmutableHashSet<string> ExpandedSkillContainers { get; init; }
}
```

**Issues:**
1. Custom middleware cannot persist state across iterations
2. Built-in middleware uses a different pattern than custom middleware would
3. Adding new middleware features requires modifying `AgentLoopState`
4. No clear ownership - state lives in AgentCore but logic lives in middleware

## Proposed Solution: Middleware Owns Its State

Each middleware instance owns and manages its own state. AgentCore only:
1. Calls middleware hooks at the right time
2. Persists/restores opaque middleware state during checkpointing

### New Interface: `IStatefulIterationMiddleware`

```csharp
/// <summary>
/// Extended interface for middlewares that need to persist state across iterations.
/// State is owned by the middleware and opaquely serialized by AgentCore.
/// </summary>
public interface IStatefulIterationMiddleware : IIterationMiddleWare
{
    /// <summary>
    /// Unique identifier for this middleware type.
    /// Used as the key when persisting state.
    /// Convention: "Namespace.MiddlewareName" (e.g., "HPD.Agent.CircuitBreaker")
    /// </summary>
    string StateKey { get; }

    /// <summary>
    /// Exports the middleware's current state for persistence.
    /// Called by AgentCore during checkpointing.
    /// </summary>
    /// <returns>Serializable state object, or null if no state to persist</returns>
    object? ExportState();

    /// <summary>
    /// Imports previously persisted state.
    /// Called by AgentCore when restoring from a checkpoint.
    /// </summary>
    /// <param name="state">The state object previously returned by ExportState</param>
    void ImportState(object? state);

    /// <summary>
    /// Resets the middleware state to initial values.
    /// Called at the start of a new conversation/thread.
    /// </summary>
    void ResetState();
}
```

### Simplified `AgentLoopState`

```csharp
public sealed record AgentLoopState
{
    // ═══════════════════════════════════════════════════════
    // CORE AGENT STATE (minimal, non-middleware)
    // ═══════════════════════════════════════════════════════

    /// <summary>Current iteration number (0-based)</summary>
    public required int Iteration { get; init; }

    /// <summary>Whether the agent loop has been terminated</summary>
    public required bool IsTerminated { get; init; }

    /// <summary>Reason for termination (if terminated)</summary>
    public required string? TerminationReason { get; init; }

    /// <summary>ETag for optimistic concurrency</summary>
    public string? ETag { get; init; }

    /// <summary>Functions that have been executed</summary>
    public required ImmutableList<string> CompletedFunctions { get; init; }

    /// <summary>Full conversation history</summary>
    public required ImmutableList<ChatMessage> ConversationHistory { get; init; }

    // ═══════════════════════════════════════════════════════
    // MIDDLEWARE STATE (opaque, owned by middlewares)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Opaque state storage for stateful middlewares.
    /// Keys are middleware StateKey values.
    /// Values are serialized state from ExportState().
    /// </summary>
    public ImmutableDictionary<string, object> MiddlewareStates { get; init; }
        = ImmutableDictionary<string, object>.Empty;

    // ═══════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════

    public AgentLoopState WithMiddlewareState(string key, object state)
        => this with { MiddlewareStates = MiddlewareStates.SetItem(key, state) };

    public T? GetMiddlewareState<T>(string key) where T : class
        => MiddlewareStates.TryGetValue(key, out var state) && state is T typed
            ? typed : null;
}
```

### Example: Circuit Breaker Middleware

```csharp
public class CircuitBreakerIterationMiddleware : IStatefulIterationMiddleware
{
    public string StateKey => "HPD.Agent.CircuitBreaker";

    // Configuration (set at registration time)
    public int MaxConsecutiveCalls { get; set; } = 3;

    // Internal state (owned by this middleware)
    private ImmutableDictionary<string, string> _lastSignaturePerTool
        = ImmutableDictionary<string, string>.Empty;
    private ImmutableDictionary<string, int> _consecutiveCountPerTool
        = ImmutableDictionary<string, int>.Empty;

    // ═══════════════════════════════════════════════════════
    // STATE MANAGEMENT
    // ═══════════════════════════════════════════════════════

    public object? ExportState() => new CircuitBreakerState
    {
        LastSignaturePerTool = _lastSignaturePerTool,
        ConsecutiveCountPerTool = _consecutiveCountPerTool
    };

    public void ImportState(object? state)
    {
        if (state is CircuitBreakerState s)
        {
            _lastSignaturePerTool = s.LastSignaturePerTool;
            _consecutiveCountPerTool = s.ConsecutiveCountPerTool;
        }
    }

    public void ResetState()
    {
        _lastSignaturePerTool = ImmutableDictionary<string, string>.Empty;
        _consecutiveCountPerTool = ImmutableDictionary<string, int>.Empty;
    }

    // ═══════════════════════════════════════════════════════
    // MIDDLEWARE HOOKS
    // ═══════════════════════════════════════════════════════

    public Task BeforeIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task BeforeToolExecutionAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        if (context.ToolCalls.Count == 0)
            return Task.CompletedTask;

        foreach (var toolCall in context.ToolCalls)
        {
            var toolName = toolCall.Name ?? "_unknown";
            var signature = ComputeFunctionSignature(toolCall);

            // Use INTERNAL state, not AgentLoopState
            var lastSig = _lastSignaturePerTool.GetValueOrDefault(toolName);
            var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;

            var countAfterExecution = isIdentical
                ? _consecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1
                : 1;

            if (countAfterExecution >= MaxConsecutiveCalls)
            {
                TriggerCircuitBreaker(context, toolName, countAfterExecution);
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    public Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // Update internal state after tool execution
        foreach (var toolCall in context.ToolCalls)
        {
            var toolName = toolCall.Name ?? "_unknown";
            var signature = ComputeFunctionSignature(toolCall);

            var lastSig = _lastSignaturePerTool.GetValueOrDefault(toolName);
            var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;

            // Update state
            _lastSignaturePerTool = _lastSignaturePerTool.SetItem(toolName, signature);
            _consecutiveCountPerTool = _consecutiveCountPerTool.SetItem(
                toolName,
                isIdentical ? _consecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1 : 1);
        }

        // Signal state export (AgentCore will call ExportState)
        context.Properties["MiddlewareStateChanged"] = true;

        return Task.CompletedTask;
    }

    // ... helper methods ...
}

// Strongly-typed state record for serialization
public record CircuitBreakerState
{
    public ImmutableDictionary<string, string> LastSignaturePerTool { get; init; }
        = ImmutableDictionary<string, string>.Empty;
    public ImmutableDictionary<string, int> ConsecutiveCountPerTool { get; init; }
        = ImmutableDictionary<string, int>.Empty;
}
```

### Example: Custom Cost Tracking Middleware

```csharp
public class CostTrackingMiddleware : IStatefulIterationMiddleware
{
    public string StateKey => "MyCompany.CostTracker";

    public decimal MaxBudget { get; set; } = 10.00m;

    // Internal state
    private decimal _totalCost = 0m;
    private int _totalTokens = 0;

    public object? ExportState() => new CostState
    {
        TotalCost = _totalCost,
        TotalTokens = _totalTokens
    };

    public void ImportState(object? state)
    {
        if (state is CostState s)
        {
            _totalCost = s.TotalCost;
            _totalTokens = s.TotalTokens;
        }
    }

    public void ResetState()
    {
        _totalCost = 0m;
        _totalTokens = 0;
    }

    public Task AfterIterationAsync(
        IterationMiddleWareContext context,
        CancellationToken cancellationToken)
    {
        // Calculate cost from response
        var tokens = EstimateTokens(context.Response);
        var cost = tokens * 0.00001m; // Example rate

        _totalCost += cost;
        _totalTokens += tokens;

        if (_totalCost >= MaxBudget)
        {
            context.SkipLLMCall = true;
            context.Properties["IsTerminated"] = true;
            context.Properties["TerminationReason"] = $"Budget exceeded: ${_totalCost:F2}";
        }

        context.Properties["MiddlewareStateChanged"] = true;
        return Task.CompletedTask;
    }

    // ... other hooks return Task.CompletedTask ...
}

public record CostState
{
    public decimal TotalCost { get; init; }
    public int TotalTokens { get; init; }
}
```

### AgentCore Integration

```csharp
public partial class AgentCore
{
    private readonly List<IIterationMiddleWare> _iterationMiddlewares;

    // Called after each iteration
    private void SyncMiddlewareStates(
        IterationMiddleWareContext context,
        ref AgentLoopState state)
    {
        if (!context.Properties.TryGetValue("MiddlewareStateChanged", out var changed) ||
            changed is not true)
        {
            return;
        }

        foreach (var middleware in _iterationMiddlewares)
        {
            if (middleware is IStatefulIterationMiddleware stateful)
            {
                var exportedState = stateful.ExportState();
                if (exportedState != null)
                {
                    state = state.WithMiddlewareState(stateful.StateKey, exportedState);
                }
            }
        }
    }

    // Called when restoring from checkpoint
    private void RestoreMiddlewareStates(AgentLoopState state)
    {
        foreach (var middleware in _iterationMiddlewares)
        {
            if (middleware is IStatefulIterationMiddleware stateful)
            {
                var storedState = state.GetMiddlewareState<object>(stateful.StateKey);
                stateful.ImportState(storedState);
            }
        }
    }

    // Called at start of new conversation
    private void ResetMiddlewareStates()
    {
        foreach (var middleware in _iterationMiddlewares)
        {
            if (middleware is IStatefulIterationMiddleware stateful)
            {
                stateful.ResetState();
            }
        }
    }
}
```

## Migration Path

### Phase 1: Add New Interface (Non-Breaking)
1. Add `IStatefulIterationMiddleware` interface
2. Add `MiddlewareStates` to `AgentLoopState`
3. Add AgentCore integration for state sync/restore
4. Update built-in middlewares to implement new interface
5. Keep old `AgentLoopState` fields for backward compatibility

### Phase 2: Deprecate Old Fields (Warning)
1. Mark old state fields as `[Obsolete]`
2. Built-in middlewares stop using old fields
3. Migration guide for custom code using old fields

### Phase 3: Remove Old Fields (Breaking)
1. Remove deprecated fields from `AgentLoopState`
2. All state lives in `MiddlewareStates`

## Serialization Considerations

### JSON Serialization
```csharp
// State records must be JSON-serializable
public record CircuitBreakerState
{
    [JsonPropertyName("lastSig")]
    public ImmutableDictionary<string, string> LastSignaturePerTool { get; init; }

    [JsonPropertyName("count")]
    public ImmutableDictionary<string, int> ConsecutiveCountPerTool { get; init; }
}
```

### Type Resolution for Deserialization
```csharp
// AgentCore needs to know which types to deserialize to
public interface IStatefulIterationMiddleware
{
    // ... existing members ...

    /// <summary>
    /// Type of the state object for deserialization.
    /// </summary>
    Type StateType { get; }
}

// Implementation
public class CircuitBreakerMiddleware : IStatefulIterationMiddleware
{
    public Type StateType => typeof(CircuitBreakerState);
}
```

## Benefits

1. **Clean Ownership** - Middleware owns its state, not AgentCore
2. **Extensibility** - Custom middlewares get first-class state support
3. **Consistency** - Built-in and custom middlewares use same pattern
4. **Type Safety** - Each middleware defines its own strongly-typed state
5. **Testability** - Middlewares can be tested in isolation with their state
6. **Serialization** - State is explicitly designed for persistence

## Drawbacks

1. **Breaking Change** - Existing code using `AgentLoopState` fields needs migration
2. **Complexity** - More interfaces and patterns to understand
3. **Performance** - Slightly more overhead for state sync
4. **Serialization** - Each middleware must ensure its state is serializable

## Comparison with Alternatives

| Approach | Extensibility | Type Safety | Complexity | Breaking |
|----------|--------------|-------------|------------|----------|
| Current (fixed fields) | None | High | Low | No |
| State Bag (`Dictionary<string, object>`) | High | Low | Low | No |
| Per-Middleware State (this proposal) | High | High | Medium | Yes |

## Recommendation

**Implement in Phase 1** (non-breaking) to validate the pattern with built-in middlewares.
Then evaluate whether to proceed with Phases 2-3 based on adoption and feedback.

The key insight is that **middleware state is middleware's concern**, not AgentCore's concern.
AgentCore should only facilitate persistence, not define the shape of middleware state.
