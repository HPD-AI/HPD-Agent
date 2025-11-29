# Clean Slate Middleware Architecture (No Backward Compatibility)

## The Vision: Pure Middleware-Based Execution

If we **don't care about breaking changes**, we can create a **clean, consistent architecture** where ALL execution flows through middleware.

---

## Core Principle: "Middleware All the Way Down"

### **Current (Mixed Architecture):**

```
LLM Calls:
  ✅ ExecuteLLMCallAsync(context, next, ct) → Middleware chain

Function Calls:
  ❌ BeforeSequentialFunctionAsync → Built-in retry → AfterFunctionAsync
```

**Problem:** Inconsistent - some logic in middleware, some in execution layer.

---

### **Clean Slate (Pure Middleware):**

```
LLM Calls:
  ✅ ExecuteLLMCallAsync(context, next, ct) → Middleware chain

Function Calls:
  ✅ ExecuteFunctionAsync(context, next, ct) → Middleware chain
```

**Benefit:** Everything is middleware. Execution is just the innermost layer.

---

## What We Remove

### **Delete These:**

```diff
- FunctionRetryExecutor class (move to middleware)
- ExecuteWithRetryAsync method in Agent (move to middleware)
- ErrorHandlingConfig.CustomRetryStrategy (replaced by custom middleware)
- Built-in timeout handling (move to middleware)
```

### **What Gets Replaced:**

| Current | Clean Slate |
|---------|-------------|
| `FunctionRetryExecutor` | `FunctionRetryMiddleware` |
| `ErrorHandlingConfig` | `FunctionRetryMiddleware` configuration |
| `CustomRetryStrategy` | Custom middleware implementation |
| `SingleFunctionTimeout` | `FunctionTimeoutMiddleware` |

---

## The New Architecture

### **1. Core Execution (Agent.cs)**

```csharp
// In ProcessFunctionCallsAsync
private async Task<List<ChatMessage>> ProcessFunctionCallsAsync(...)
{
    foreach (var functionCall in functionCalls)
    {
        var middlewareContext = new AgentMiddlewareContext
        {
            Function = function,
            FunctionArguments = functionCall.Arguments,
            // ... other properties
        };

        // Run BeforeSequentialFunctionAsync hooks (permissions, validation)
        var shouldExecute = await _middlewarePipeline.ExecuteBeforeSequentialFunctionAsync(
            middlewareContext, cancellationToken);

        if (shouldExecute)
        {
            // Execute through middleware chain
            middlewareContext.FunctionResult = await _middlewarePipeline.ExecuteFunctionAsync(
                middlewareContext,
                innerCall: async () =>
                {
                    // THIS IS THE ONLY BUILT-IN LOGIC: Call the function
                    var args = new AIFunctionArguments(middlewareContext.FunctionArguments);
                    return await middlewareContext.Function!.InvokeAsync(args, cancellationToken);
                },
                cancellationToken);
        }

        // Run AfterFunctionAsync hooks (logging, telemetry)
        await _middlewarePipeline.ExecuteAfterFunctionAsync(
            middlewareContext, cancellationToken);

        // Convert to FunctionResultContent
        var result = new FunctionResultContent(functionCall.CallId, middlewareContext.FunctionResult);
        resultMessages.Add(new ChatMessage(ChatRole.Tool, new AIContent[] { result }));
    }

    return resultMessages;
}
```

**Key Point:** The core execution is **just** calling the function. Everything else (retry, timeout, caching) is middleware.

---

### **2. Auto-Registered Middleware (AgentBuilder.cs)**

```csharp
public async Task<Agent> BuildAsync()
{
    // Auto-register standard middleware (in order)
    var middlewares = new List<IAgentMiddleware>();

    // ITERATION LEVEL
    if (_config.HistoryReduction?.Enabled == true)
    {
        middlewares.Add(new HistoryReductionMiddleware(...));
    }

    if (_config.PlanMode?.Enabled == true)
    {
        middlewares.Add(new PlanModeMiddleware(...));
    }

    // FUNCTION LEVEL (order matters - outermost to innermost)

    // 1. Telemetry (outermost - wraps everything)
    if (_observabilityEnabled)
    {
        middlewares.Add(new FunctionTelemetryMiddleware());
    }

    // 2. Caching (before retry - cache hits skip retry)
    if (_config.FunctionCaching?.Enabled == true)
    {
        middlewares.Add(new FunctionCachingMiddleware(_config.FunctionCaching));
    }

    // 3. Retry (before timeout - retry handles transient failures)
    if (_config.ErrorHandling?.MaxRetries > 0)
    {
        middlewares.Add(new FunctionRetryMiddleware(_config.ErrorHandling));
    }

    // 4. Timeout (before permissions - timeout the actual execution)
    if (_config.ErrorHandling?.SingleFunctionTimeout != null)
    {
        middlewares.Add(new FunctionTimeoutMiddleware(_config.ErrorHandling.SingleFunctionTimeout.Value));
    }

    // 5. Permissions (innermost - closest to execution)
    middlewares.Add(new PermissionMiddleware(...));

    // 6. Error Tracking (iteration-level, runs in AfterIterationAsync)
    if (_config.ErrorHandling?.MaxConsecutiveErrors > 0)
    {
        middlewares.Add(new ErrorTrackingMiddleware
        {
            MaxConsecutiveErrors = _config.ErrorHandling.MaxConsecutiveErrors
        });
    }

    // Add user-registered middleware (outermost)
    middlewares.InsertRange(0, _customMiddlewares);

    return new Agent(middlewares, _config);
}
```

**Order of Execution:**

```
User Middleware (custom)
  └─► Telemetry (starts timer)
      └─► Caching (checks cache)
          └─► Retry (retry loop)
              └─► Timeout (enforces timeout)
                  └─► Permissions (checks permission)
                      └─► Function.InvokeAsync() ◄─── ACTUAL EXECUTION
```

---

### **3. Standard Middleware Implementations**

#### **FunctionRetryMiddleware**

```csharp
public class FunctionRetryMiddleware : IAgentMiddleware
{
    private readonly ErrorHandlingConfig _config;
    private readonly IProviderErrorHandler _providerHandler;

    public FunctionRetryMiddleware(ErrorHandlingConfig config)
    {
        _config = config;
        _providerHandler = config.ProviderHandler ?? new GenericErrorHandler();
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
                // Call next middleware (or actual function)
                return await next();
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Check if we should retry
                var delay = await CalculateRetryDelayAsync(ex, attempt, ct);

                if (!delay.HasValue || attempt >= maxRetries)
                {
                    // Non-retryable or exhausted retries
                    throw;
                }

                // Emit retry event
                context.Emit(new FunctionRetryEvent(
                    FunctionName: context.Function!.Name,
                    Attempt: attempt + 1,
                    Delay: delay.Value,
                    Exception: ex));

                // Wait before retry
                await Task.Delay(delay.Value, ct);
            }
        }

        throw lastException!;
    }

    private async Task<TimeSpan?> CalculateRetryDelayAsync(Exception ex, int attempt, CancellationToken ct)
    {
        // Priority 1: Custom retry strategy
        if (_config.CustomRetryStrategy != null)
        {
            return await _config.CustomRetryStrategy(ex, attempt, ct);
        }

        // Priority 2: Provider-aware handling
        var errorDetails = _providerHandler.ParseError(ex);
        if (errorDetails != null)
        {
            // Check per-category retry limits
            if (_config.MaxRetriesByCategory != null &&
                _config.MaxRetriesByCategory.TryGetValue(errorDetails.Category, out var categoryMax))
            {
                if (attempt >= categoryMax)
                    return null; // Exceeded category limit
            }

            // Get provider-calculated delay
            return _providerHandler.GetRetryDelay(
                errorDetails,
                attempt,
                _config.RetryDelay,
                _config.BackoffMultiplier,
                _config.MaxRetryDelay);
        }

        // Priority 3: Exponential backoff
        var baseMs = _config.RetryDelay.TotalMilliseconds;
        var expDelayMs = baseMs * Math.Pow(_config.BackoffMultiplier, attempt);
        var cappedDelayMs = Math.Min(expDelayMs, _config.MaxRetryDelay.TotalMilliseconds);
        var jitter = 0.9 + (Random.Shared.NextDouble() * 0.2);

        return TimeSpan.FromMilliseconds(cappedDelayMs * jitter);
    }
}
```

---

#### **FunctionTimeoutMiddleware**

```csharp
public class FunctionTimeoutMiddleware : IAgentMiddleware
{
    private readonly TimeSpan _timeout;

    public FunctionTimeoutMiddleware(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            return await next();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Function '{context.Function?.Name}' timed out after {_timeout.TotalSeconds}s");
        }
    }
}
```

---

#### **FunctionCachingMiddleware**

```csharp
public class FunctionCachingMiddleware : IAgentMiddleware
{
    private readonly IDistributedCache _cache;
    private readonly FunctionCachingConfig _config;

    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        if (!_config.ShouldCache(context.Function!.Name))
            return await next();

        var cacheKey = ComputeCacheKey(context.Function.Name, context.FunctionArguments);

        // Check cache
        var cached = await _cache.GetAsync(cacheKey, ct);
        if (cached != null)
        {
            context.Properties["CacheHit"] = true;
            context.Emit(new FunctionCacheHitEvent(context.Function.Name, cacheKey));
            return Deserialize(cached);
        }

        // Execute function
        var result = await next();

        // Cache the result
        await _cache.SetAsync(
            cacheKey,
            Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _config.CacheDuration
            },
            ct);

        return result;
    }
}
```

---

#### **FunctionTelemetryMiddleware**

```csharp
public class FunctionTelemetryMiddleware : IAgentMiddleware
{
    public async ValueTask<object?> ExecuteFunctionAsync(
        AgentMiddlewareContext context,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? error = null;

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
            error = ex;

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

## Configuration: Clean API

### **Old (Mixed Architecture):**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config =>
    {
        config.MaxRetries = 3;
        config.SingleFunctionTimeout = TimeSpan.FromSeconds(30);
        config.CustomRetryStrategy = async (ex, attempt, ct) => { /* ... */ };
    })
    .BuildAsync();
```

---

### **New (Pure Middleware):**

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)

    // Auto-registered middleware (configured via config)
    .WithFunctionRetry(config =>
    {
        config.MaxRetries = 3;
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5
        };
    })
    .WithFunctionTimeout(TimeSpan.FromSeconds(30))
    .WithFunctionCaching(config =>
    {
        config.CacheDuration = TimeSpan.FromMinutes(5);
    })
    .WithFunctionTelemetry()

    // OR: Custom middleware (full control)
    .WithMiddleware(new MyCustomRetryMiddleware())

    .BuildAsync();
```

**Benefits:**
- ✅ Clear, declarative API
- ✅ Each middleware is explicit
- ✅ Easy to see what's enabled
- ✅ Easy to replace with custom implementations

---

## Advanced: Custom Middleware Replaces Built-In

```csharp
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)

    // Disable built-in retry
    .WithFunctionRetry(enabled: false)

    // Use custom Polly-based retry
    .WithMiddleware(new PollyRetryMiddleware(myPollyPolicy))

    .BuildAsync();
```

---

## User Migration Guide

### **Breaking Changes:**

1. **`ErrorHandlingConfig.CustomRetryStrategy` removed**
   - **Before:** `config.ErrorHandling.CustomRetryStrategy = async (ex, attempt, ct) => { /* ... */ };`
   - **After:** `builder.WithMiddleware(new MyCustomRetryMiddleware())`

2. **`SingleFunctionTimeout` moved to middleware**
   - **Before:** `config.ErrorHandling.SingleFunctionTimeout = TimeSpan.FromSeconds(30);`
   - **After:** `builder.WithFunctionTimeout(TimeSpan.FromSeconds(30))`

3. **`FunctionRetryExecutor` is now internal**
   - **Before:** Users could reference `FunctionRetryExecutor` (unlikely)
   - **After:** Use `FunctionRetryMiddleware` instead

---

### **Migration Path:**

```csharp
// ===== BEFORE (v1.x) =====
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithErrorHandling(config =>
    {
        config.MaxRetries = 3;
        config.SingleFunctionTimeout = TimeSpan.FromSeconds(30);
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5
        };
        config.CustomRetryStrategy = async (ex, attempt, ct) =>
        {
            // Custom logic
            return TimeSpan.FromSeconds(10);
        };
    })
    .BuildAsync();

// ===== AFTER (v2.0 - Clean Slate) =====
var agent = await new AgentBuilder()
    .WithOpenAI("gpt-4", apiKey)
    .WithFunctionRetry(config =>
    {
        config.MaxRetries = 3;
        config.MaxRetriesByCategory = new()
        {
            [ErrorCategory.RateLimitRetryable] = 5
        };
    })
    .WithFunctionTimeout(TimeSpan.FromSeconds(30))
    .WithMiddleware(new MyCustomRetryMiddleware()) // Custom strategy → middleware
    .BuildAsync();
```

---

## Comparison: Before vs. After

| Aspect | Mixed Architecture | Clean Slate (Pure Middleware) |
|--------|-------------------|------------------------------|
| **Retry Logic** | `FunctionRetryExecutor` class | `FunctionRetryMiddleware` |
| **Timeout Logic** | Built-in to retry executor | `FunctionTimeoutMiddleware` |
| **Caching** | Not possible | `FunctionCachingMiddleware` |
| **Telemetry** | AfterFunction only | `FunctionTelemetryMiddleware` |
| **Custom Logic** | `CustomRetryStrategy` delegate | Custom middleware class |
| **Composability** | Limited | Full chain-of-responsibility |
| **Testability** | Mixed (some logic in Agent) | Pure (all logic in middleware) |
| **Consistency** | Asymmetric (LLM ≠ Function) | Symmetric (LLM = Function) |
| **Code Clarity** | Execution + middleware | Middleware only |

---

## Benefits of Clean Slate

### **1. Architectural Purity**

✅ **Everything is middleware** - No special-case logic in Agent
✅ **Consistent patterns** - LLM and function execution work the same way
✅ **Clear separation** - Core execution vs. cross-cutting concerns

---

### **2. Composability**

✅ **Mix and match** - Enable/disable middleware as needed
✅ **Custom implementations** - Replace any middleware with your own
✅ **Middleware ordering** - Control execution order explicitly

**Example:**
```csharp
// Scenario: Skip retry for specific functions
.WithMiddleware(new ConditionalRetryMiddleware(
    shouldRetry: ctx => ctx.Function.Name != "ExpensiveAPICall"))
```

---

### **3. Testability**

✅ **Unit test middleware in isolation** - No Agent coupling
✅ **Integration test middleware chains** - Compose and test combinations
✅ **Mock next() delegate** - Control execution flow in tests

**Example:**
```csharp
[Fact]
public async Task RetryMiddleware_RetriesThreeTimes()
{
    var middleware = new FunctionRetryMiddleware(new ErrorHandlingConfig
    {
        MaxRetries = 3
    });

    int attempts = 0;
    Func<ValueTask<object?>> next = () =>
    {
        attempts++;
        throw new HttpRequestException("Transient error");
    };

    await Assert.ThrowsAsync<HttpRequestException>(async () =>
        await middleware.ExecuteFunctionAsync(mockContext, next, CancellationToken.None));

    Assert.Equal(4, attempts); // Initial + 3 retries
}
```

---

### **4. Extensibility**

✅ **Add new middleware without touching core** - Pure extension
✅ **Community middleware packages** - Share and reuse
✅ **Provider-specific middleware** - OpenAI retry middleware, Anthropic retry middleware, etc.

---

## Performance Considerations

### **Concern: "Won't middleware add overhead?"**

**Answer: Negligible for most cases.**

**Overhead breakdown:**

| Operation | Current | Clean Slate | Overhead |
|-----------|---------|-------------|----------|
| **No retry** | Direct call | 1 delegate call | ~10-20ns |
| **With retry** | Built-in | Middleware | ~10-20ns |
| **Cache hit** | N/A | Skip retry entirely | **Faster!** |

**Benchmark (conceptual):**

```
Direct function call:           100 ns
+ Built-in retry (current):     110 ns
+ Middleware retry (proposed):  120 ns  (←10ns overhead)

Cache hit scenario:
+ Built-in (current):           N/A (not possible)
+ Middleware (proposed):        150 ns  (cache check only, skip function!)
```

**Hot path optimization:**

```csharp
// AgentMiddlewarePipeline.ExecuteFunctionAsync
public ValueTask<object?> ExecuteFunctionAsync(...)
{
    // Fast path: No middleware registered
    if (_middlewares.Count == 0)
    {
        return innerCall(); // No overhead!
    }

    // Build chain only when needed
    Func<ValueTask<object?>> pipeline = innerCall;
    for (int i = _middlewares.Count - 1; i >= 0; i--)
    {
        // Chain building is O(n) where n = middleware count
        // Typical n = 3-5, negligible overhead
    }

    return pipeline();
}
```

---

## Migration Strategy: Big Bang vs. Gradual

### **Option 1: Big Bang (v2.0 Release)**

```
Release v2.0:
  - Remove FunctionRetryExecutor
  - Remove CustomRetryStrategy
  - Add ExecuteFunctionAsync to IAgentMiddleware
  - Auto-register FunctionRetryMiddleware
  - Update documentation
  - Provide migration guide
```

**Timeline:** 1 major release

---

### **Option 2: Gradual (Deprecation Path)**

```
v1.5:
  - Add ExecuteFunctionAsync (default = pass-through)
  - Add FunctionRetryMiddleware (opt-in)
  - Deprecate CustomRetryStrategy (still works)
  - Documentation: Recommend middleware approach

v2.0:
  - Remove FunctionRetryExecutor
  - Remove CustomRetryStrategy
  - Auto-register FunctionRetryMiddleware
  - Breaking change announcement
```

**Timeline:** 2 releases

---

## Recommendation: Clean Slate is THE Way

### **Why Clean Slate Wins:**

1. **Architectural purity** - Everything is middleware (no special cases)
2. **Consistency** - LLM and function execution work identically
3. **Composability** - Full chain-of-responsibility pattern
4. **Testability** - Pure middleware, easy to test
5. **Extensibility** - Add new capabilities without touching core
6. **Performance** - Negligible overhead, cache middleware is faster!

### **The Cost:**

- ⚠️ Breaking changes for users using `CustomRetryStrategy`
- ⚠️ Migration effort for existing codebases
- ⚠️ Documentation updates

### **The Value:**

- ✅ Future-proof architecture
- ✅ Better DX (declarative, composable)
- ✅ Easier maintenance (pure middleware)
- ✅ Competitive advantage (better than Microsoft!)

---

## Final Architecture Diagram

```
╔════════════════════════════════════════════════════════════╗
║                    Agent (Execution)                   ║
║  - Minimal logic: Build context, invoke pipeline           ║
╚════════════════════════════════════════════════════════════╝
                            │
                            ↓
╔════════════════════════════════════════════════════════════╗
║              AgentMiddlewarePipeline                       ║
║  - ExecuteLLMCallAsync(context, next, ct)                  ║
║  - ExecuteFunctionAsync(context, next, ct)                 ║
╚════════════════════════════════════════════════════════════╝
                            │
                            ↓
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ↓                   ↓                   ↓
╔═══════════════╗  ╔═══════════════╗  ╔═══════════════╗
║  Telemetry    ║  ║   Caching     ║  ║ User Middleware║
║  Middleware   ║  ║  Middleware   ║  ║               ║
╚═══════════════╝  ╚═══════════════╝  ╚═══════════════╝
        │                   │                   │
        └───────────────────┼───────────────────┘
                            ↓
                ╔═══════════════════════╗
                ║   Retry Middleware    ║
                ║ (Provider-aware)      ║
                ╚═══════════════════════╝
                            │
                            ↓
                ╔═══════════════════════╗
                ║  Timeout Middleware   ║
                ╚═══════════════════════╝
                            │
                            ↓
                ╔═══════════════════════╗
                ║ Permission Middleware ║
                ╚═══════════════════════╝
                            │
                            ↓
                ╔═══════════════════════╗
                ║ function.InvokeAsync()║
                ║   (ACTUAL EXECUTION)  ║
                ╚═══════════════════════╝
```

**Key:** Onion architecture - outer layers wrap inner layers.

---

## Conclusion

**If you don't care about breaking changes, go for Clean Slate.**

It's:
- ✅ Architecturally pure
- ✅ Highly composable
- ✅ Easily testable
- ✅ Future-proof
- ✅ Better than Microsoft (you have both defaults AND full control)

The migration cost is worth it for the long-term benefits.

---

## Next Steps

1. **Prototype** `ExecuteFunctionAsync` in a branch
2. **Implement** `FunctionRetryMiddleware`, `FunctionTimeoutMiddleware`, `FunctionCachingMiddleware`
3. **Benchmark** to verify performance claims
4. **Test** migration with existing codebases
5. **Document** breaking changes and migration guide
6. **Ship** v2.0 with confidence

**Let's build the best agent framework architecture! 🚀**
