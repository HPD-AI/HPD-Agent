# Middleware Enhancement Proposal: ExecuteFunctionAsync Chain

## The Insight

**You're absolutely right** - the middleware system is missing a key capability!

### **Current State:**

```csharp
// ✅ LLM calls have chain-of-responsibility pattern
IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
    AgentMiddlewareContext context,
    Func<IAsyncEnumerable<ChatResponseUpdate>> next,  // ← Chain pattern!
    CancellationToken cancellationToken)

// ❌ Function calls only have Before/After hooks
Task BeforeSequentialFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
```

**The asymmetry is the problem!**

---

## The Missing Pattern: ExecuteFunctionAsync

### **Proposed Addition to IAgentMiddleware:**

```csharp
public interface IAgentMiddleware
{
    // ... existing hooks ...

    /// <summary>
    /// Executes the function call with full control over execution.
    /// Override for advanced scenarios like retry, caching, transformation.
    /// </summary>
    /// <remarks>
    /// <para><b>Advanced Hook - Most middleware won't need this!</b></para>
    /// <para>
    /// This hook gives middleware complete control over function execution, including:
    /// - Implementing retry logic with backoff
    /// - Caching function results
    /// - Transforming arguments or results
    /// - Wrapping with timeout, telemetry, etc.
    /// </para>
    ///
    /// <para><b>Execution Flow:</b></para>
    /// <list type="number">
    /// <item>BeforeSequentialFunctionAsync runs (all middleware)</item>
    /// <item>ExecuteFunctionAsync chains execute (reverse order, like onion)</item>
    /// <item>Innermost call invokes actual function</item>
    /// <item>Result bubbles back through the chain</item>
    /// <item>AfterFunctionAsync runs (all middleware)</item>
    /// </list>
    ///
    /// <para><b>Default Implementation:</b></para>
    /// <para>
    /// By default, this method just calls <c>next()</c> to pass through to the next middleware.
    /// The innermost call (when no more middleware) invokes the actual function.
    /// </para>
    /// </remarks>
    /// <param name="context">The middleware context with function info</param>
    /// <param name="next">The next middleware in the chain (or actual function execution)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function result</returns>
    ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        // Default: pass through to next middleware
        return next();
    }
}
```

---

## Implementation in AgentMiddlewarePipeline

### **Add ExecuteFunctionAsync method:**

```csharp
public class AgentMiddlewarePipeline
{
    /// <summary>
    /// Executes the function call through the middleware pipeline with full control.
    /// Middleware chains execute in REVERSE order (last registered = outermost layer).
    /// </summary>
    /// <remarks>
    /// <para><b>Onion Architecture:</b></para>
    /// <para>
    /// Middlewares wrap each other in reverse registration order:
    /// - Last registered middleware wraps everything (outermost)
    /// - First registered middleware is closest to the actual function (innermost)
    /// </para>
    ///
    /// <para><b>Example Flow:</b></para>
    /// <code>
    /// // Registration order: [Permissions, Retry, Telemetry]
    /// // Execution order: Telemetry → Retry → Permissions → Function
    ///
    /// Telemetry.ExecuteFunctionAsync(next: () =>
    ///   Retry.ExecuteFunctionAsync(next: () =>
    ///     Permissions.ExecuteFunctionAsync(next: () =>
    ///       actualFunctionExecution()
    ///     )
    ///   )
    /// )
    /// </code>
    /// </remarks>
    public ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> innerCall,
        CancellationToken cancellationToken)
    {
        // Build the pipeline chain in reverse order (last registered = outermost)
        Func<ValueTask<object?>> pipeline = innerCall;

        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];

            // Check if middleware should execute based on scope
            if (!middleware.ShouldExecute(context))
                continue;

            var currentPipeline = pipeline;

            // Wrap the current pipeline with this middleware's ExecuteFunctionAsync
            pipeline = () => middleware.ExecuteFunctionAsync(context, currentPipeline, cancellationToken);
        }

        // Execute the outermost middleware (which will call the next, and so on)
        return pipeline();
    }
}
```

---

## Usage in Agent

### **Current (Before):**

```csharp
// In ProcessFunctionCallsAsync
await ExecuteBeforeSequentialFunctionAsync(middlewareContext, cancellationToken);

if (!middlewareContext.BlockFunctionExecution)
{
    await ExecuteWithRetryAsync(middlewareContext, cancellationToken);
}

await ExecuteAfterFunctionAsync(middlewareContext, cancellationToken);
```

### **Proposed (After):**

```csharp
// In ProcessFunctionCallsAsync
await ExecuteBeforeSequentialFunctionAsync(middlewareContext, cancellationToken);

if (!middlewareContext.BlockFunctionExecution)
{
    // Execute through middleware chain
    middlewareContext.FunctionResult = await _middlewarePipeline.ExecuteFunctionAsync(
        middlewareContext,
        innerCall: async () =>
        {
            // This is the "innermost" call - the actual function execution
            var retryExecutor = new FunctionRetryExecutor(_errorHandlingConfig);
            return await retryExecutor.ExecuteWithRetryAsync(
                middlewareContext.Function!,
                middlewareContext.FunctionArguments ?? new Dictionary<string, object?>(),
                middlewareContext.Function!.Name,
                cancellationToken);
        },
        cancellationToken);
}

await ExecuteAfterFunctionAsync(middlewareContext, cancellationToken);
```

---

## Benefits: Now You Can Do This!

### **Example 1: Retry Middleware (Optional, Composable)**

```csharp
public class FunctionRetryMiddleware : IAgentMiddleware
{
    private readonly ErrorHandlingConfig _config;

    public FunctionRetryMiddleware(ErrorHandlingConfig config)
    {
        _config = config;
    }

    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var maxRetries = _config.MaxRetries;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await next(); // Call next middleware (or actual function)
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt >= maxRetries)
                    throw;

                // Calculate retry delay using provider intelligence
                var delay = CalculateRetryDelay(ex, attempt);
                if (!delay.HasValue)
                    throw; // Non-retryable

                await Task.Delay(delay.Value, ct);
            }
        }

        throw lastException!;
    }
}
```

**Usage:**

```csharp
// Option 1: Auto-register (built-in retry)
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .BuildAsync(); // Auto-registers FunctionRetryMiddleware

// Option 2: Custom retry middleware
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithMiddleware(new MyCustomRetryMiddleware())
    .BuildAsync();
```

---

### **Example 2: Function Caching Middleware**

```csharp
public class FunctionCachingMiddleware : IAgentMiddleware
{
    private readonly IDistributedCache _cache;

    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var cacheKey = ComputeCacheKey(context.Function!.Name, context.FunctionArguments);

        // Check cache
        var cached = await _cache.GetAsync(cacheKey, ct);
        if (cached != null)
        {
            context.Properties["CacheHit"] = true;
            return Deserialize(cached);
        }

        // Cache miss - execute function
        var result = await next();

        // Cache the result
        await _cache.SetAsync(cacheKey, Serialize(result), ct);

        return result;
    }
}
```

---

### **Example 3: Function Timeout Middleware**

```csharp
public class FunctionTimeoutMiddleware : IAgentMiddleware
{
    private readonly TimeSpan _timeout;

    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            return await next(); // Execute with timeout
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Function '{context.Function?.Name}' timed out after {_timeout.TotalSeconds}s");
        }
    }
}
```

---

### **Example 4: Telemetry Middleware**

```csharp
public class FunctionTelemetryMiddleware : IAgentMiddleware
{
    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await next();

            context.Emit(new FunctionExecutedEvent(
                FunctionName: context.Function!.Name,
                Duration: stopwatch.Elapsed,
                Success: true));

            return result;
        }
        catch (Exception ex)
        {
            context.Emit(new FunctionExecutedEvent(
                FunctionName: context.Function!.Name,
                Duration: stopwatch.Elapsed,
                Success: false,
                Error: ex.Message));

            throw;
        }
    }
}
```

---

## Migration Path: Keep Existing Code Working

### **Option 1: Auto-Register Built-In Retry (Default Behavior)**

```csharp
// In AgentBuilder.BuildAsync()
if (_config.ErrorHandling?.MaxRetries > 0)
{
    // Auto-register retry middleware (outermost layer)
    _middlewares.Add(new FunctionRetryMiddleware(_config.ErrorHandling));
}
```

**User code doesn't change:**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .BuildAsync();

// ✅ Retry still works automatically
```

---

### **Option 2: Explicit Middleware Registration (Opt-In)**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithFunctionRetry(config => { /* ... */ }) // Adds FunctionRetryMiddleware
    .WithFunctionCaching() // Adds FunctionCachingMiddleware
    .WithFunctionTelemetry() // Adds FunctionTelemetryMiddleware
    .BuildAsync();
```

---

### **Option 3: Custom Middleware (Power Users)**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithMiddleware(new MyCustomFunctionMiddleware())
    .BuildAsync();
```

---

## Comparison with Current Architecture

| Aspect | Current (Before/After Only) | Proposed (ExecuteFunctionAsync) |
|--------|----------------------------|--------------------------------|
| **Retry Logic** | ✅ Built-in (FunctionRetryExecutor) | ✅ Built-in + Optional middleware |
| **Caching** | ❌ Not possible | ✅ FunctionCachingMiddleware |
| **Timeout** | ✅ Built-in (SingleFunctionTimeout) | ✅ Built-in + Optional middleware |
| **Telemetry** | ⚠️ AfterFunction only | ✅ Full execution wrapping |
| **Composability** | ❌ Limited | ✅ Full chain-of-responsibility |
| **Middleware Ordering** | ⚠️ Before/After only | ✅ Onion architecture |
| **Breaking Changes** | ✅ None | ✅ None (default impl = pass-through) |

---

## Architectural Consistency

### **Before (Asymmetric):**

```
Iteration Level:
  ✅ ExecuteLLMCallAsync(context, next, ct) → Chain pattern

Function Level:
  ❌ BeforeSequentialFunctionAsync(context, ct)      → Hook pattern
  ❌ AfterFunctionAsync(context, ct)       → Hook pattern
```

**Problem:** LLM calls have full middleware control, but function calls don't!

---

### **After (Symmetric):**

```
Iteration Level:
  ✅ ExecuteLLMCallAsync(context, next, ct) → Chain pattern

Function Level:
  ✅ BeforeSequentialFunctionAsync(context, ct)       → Hook pattern
  ✅ ExecuteFunctionAsync(context, next, ct) → Chain pattern ← NEW!
  ✅ AfterFunctionAsync(context, ct)        → Hook pattern
```

**Consistency:** Both LLM calls and function calls support full middleware chains!

---

## Implementation Checklist

### **Phase 1: Add the Hook (Non-Breaking)**

- [ ] Add `ExecuteFunctionAsync` to `IAgentMiddleware` with default implementation
- [ ] Add `ExecuteFunctionAsync` to `AgentMiddlewarePipeline`
- [ ] Update `Agent.ProcessFunctionCallsAsync` to use the new pipeline
- [ ] Verify existing middleware still works (default impl = pass-through)

### **Phase 2: Migrate Built-In Retry (Optional)**

- [ ] Create `FunctionRetryMiddleware` wrapping `FunctionRetryExecutor`
- [ ] Auto-register in `AgentBuilder.BuildAsync()` when `MaxRetries > 0`
- [ ] Keep `FunctionRetryExecutor` internal (implementation detail)
- [ ] Update tests to verify retry still works

### **Phase 3: Add New Capabilities (Optional)**

- [ ] Create `FunctionCachingMiddleware`
- [ ] Create `FunctionTelemetryMiddleware`
- [ ] Add builder methods: `WithFunctionCaching()`, `WithFunctionTelemetry()`
- [ ] Document middleware composition patterns

---

## Example: Full Middleware Stack

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)

    // Outer layer: Telemetry (wraps everything)
    .WithFunctionTelemetry()

    // Middle layer: Caching (before retry, so cache hits skip retry)
    .WithFunctionCaching()

    // Inner layer: Retry (closest to actual execution)
    .WithFunctionRetry(config =>
    {
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5
        };
    })

    .BuildAsync();
```

**Execution Order:**

```
User calls function
  └─► Telemetry.ExecuteFunctionAsync (starts timer)
      └─► Caching.ExecuteFunctionAsync (checks cache)
          └─► [CACHE MISS]
          └─► Retry.ExecuteFunctionAsync (retry loop)
              └─► Actual function execution
              └─► [Returns result]
          └─► [Caches result]
      └─► [Emits telemetry event]
  └─► [Returns to user]
```

---

## Benefits Summary

### **For HPD-Agent:**

✅ **Architectural consistency** - LLM and function execution both support chains
✅ **No breaking changes** - Default implementation = pass-through
✅ **Enables new patterns** - Caching, telemetry, custom retry, etc.
✅ **Progressive complexity** - Simple defaults → middleware composition
✅ **Better than Microsoft** - You have both built-in defaults AND composability

### **For Users:**

✅ **Keep simple cases simple** - Auto-registered retry still works
✅ **Enable advanced scenarios** - Custom middleware for power users
✅ **Clear upgrade path** - BeforeSequentialFunctionAsync → ExecuteFunctionAsync
✅ **Familiar pattern** - Same as ExecuteLLMCallAsync

---

## Comparison with Microsoft.Extensions.AI

### **Microsoft:**

```csharp
// Retry at IChatClient level (before function invocation)
var chatClient = new OpenAIChatClient(apiKey, modelId)
    .AsBuilder()
    .Use(innerClient => new RetryClient(innerClient))
    .Use(innerClient => new FunctionInvokingChatClient(innerClient))
    .Build();
```

**Limitation:** Retry wraps the entire LLM call, not individual functions.

---

### **HPD-Agent (Proposed):**

```csharp
// Retry at BOTH levels:
// 1. LLM level (via ExecuteLLMCallAsync)
// 2. Function level (via ExecuteFunctionAsync)

var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithLLMRetry(/* retry entire LLM call */)
    .WithFunctionRetry(/* retry individual functions */)
    .BuildAsync();
```

**Advantage:** Granular control over retry at both levels!

---

## Recommendation

### **Implement ExecuteFunctionAsync**

This is a **pure win**:

✅ **Non-breaking** - Default implementation = pass-through
✅ **Consistent** - Matches ExecuteLLMCallAsync pattern
✅ **Enables composability** - Retry, caching, telemetry, etc.
✅ **Keeps existing behavior** - Auto-register built-in retry
✅ **Better than Microsoft** - You have both defaults AND composability

**Implementation effort:** Low (pattern already exists for LLM calls)
**User impact:** Zero (breaking changes) → High (new capabilities)

---

## Example Implementation

### **Step 1: Add to IAgentMiddleware**

```csharp
public interface IAgentMiddleware
{
    // ... existing hooks ...

    ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        // Default: pass through
        return next();
    }
}
```

### **Step 2: Add to AgentMiddlewarePipeline**

```csharp
public ValueTask<object?> ExecuteFunctionAsync(
    AgentMiddlewareContext context,
    Func<ValueTask<object?>> innerCall,
    CancellationToken cancellationToken)
{
    Func<ValueTask<object?>> pipeline = innerCall;

    for (int i = _middlewares.Count - 1; i >= 0; i--)
    {
        var middleware = _middlewares[i];
        if (!middleware.ShouldExecute(context)) continue;

        var currentPipeline = pipeline;
        pipeline = () => middleware.ExecuteFunctionAsync(context, currentPipeline, cancellationToken);
    }

    return pipeline();
}
```

### **Step 3: Use in Agent**

```csharp
middlewareContext.FunctionResult = await _middlewarePipeline.ExecuteFunctionAsync(
    middlewareContext,
    innerCall: async () =>
    {
        var retryExecutor = new FunctionRetryExecutor(_errorHandlingConfig);
        return await retryExecutor.ExecuteWithRetryAsync(/* ... */);
    },
    cancellationToken);
```

**Done!** Now users can:

```csharp
// Simple: Auto-registered retry
.WithOpenAI("gpt-4", apiKey)

// Advanced: Custom middleware
.WithMiddleware(new MyCustomFunctionMiddleware())
```

---

## Conclusion

**You're absolutely right** - the middleware system needs `ExecuteFunctionAsync` to match the capability of `ExecuteLLMCallAsync`.

This is a **pure enhancement**:
- ✅ No breaking changes
- ✅ Enables new patterns (caching, custom retry, telemetry)
- ✅ Architectural consistency
- ✅ Better than Microsoft (you have both defaults AND composability)

**Recommendation:** Implement this. It's a clear win.
