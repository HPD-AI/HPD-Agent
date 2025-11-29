# WithErrorHandling() Convenience Method

## Overview

We've added a comprehensive set of convenience methods to `AgentBuilder` that make it easy to register all error handling middleware in the correct order with a single method call.

## Problem Solved

**Before:** Developers had to manually register each middleware in the correct order:

```csharp
var agent = new AgentBuilder(config)
    .WithCircuitBreaker(maxConsecutiveCalls: 5)      // ❓ What order?
    .WithErrorTracking(maxConsecutiveErrors: 3)      // ❓ Should this be first?
    .WithTotalErrorThreshold(maxTotalErrors: 10)     // ❓ Where does this go?
    // ❌ Missing function-level middleware (retry, timeout)
    .Build();
```

**After:** One method call with guaranteed correct ordering:

```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling()  // ✅ All middleware, correct order, done!
    .Build();
```

## What Was Implemented

### 1. Function-Level Middleware Extension Methods

#### `WithFunctionRetry()`

Adds provider-aware retry middleware that uses settings from `AgentConfig.ErrorHandling`.

```csharp
// Simple usage (uses config.ErrorHandling settings)
.WithFunctionRetry()

// Custom configuration
.WithFunctionRetry(config =>
{
    config.MaxRetries = 5;
    config.RetryDelay = TimeSpan.FromSeconds(2);
    config.MaxRetriesByCategory = new Dictionary<ErrorCategory, int>
    {
        [ErrorCategory.RateLimitRetryable] = 10,
        [ErrorCategory.ServerError] = 3
    };
})
```

**Features:**
- 3-tier retry priority: Custom strategy → Provider-aware → Exponential backoff
- Respects Retry-After headers automatically
- Per-category retry limits
- Intelligent error classification (7 categories)
- Emits retry events for observability

#### `WithFunctionTimeout()`

Adds timeout enforcement for function execution.

```csharp
// Simple usage (uses config.ErrorHandling.SingleFunctionTimeout)
.WithFunctionTimeout()

// Custom timeout
.WithFunctionTimeout(TimeSpan.FromMinutes(5))
```

**Features:**
- Enforces timeout per function execution
- When combined with retry, timeout applies to EACH attempt
- Throws descriptive `TimeoutException` with function name

### 2. Convenience Method: `WithErrorHandling()`

#### Simple Overload

Registers all 5 error handling middleware with sensible defaults:

```csharp
public static AgentBuilder WithErrorHandling(
    this AgentBuilder builder,
    int maxConsecutiveCalls = 5,
    int maxConsecutiveErrors = 3,
    int maxTotalErrors = 10)
```

**Usage:**

```csharp
// Default thresholds
var agent = new AgentBuilder(config)
    .WithErrorHandling()
    .Build();

// Custom thresholds
var agent = new AgentBuilder(config)
    .WithErrorHandling(
        maxConsecutiveCalls: 3,
        maxConsecutiveErrors: 5,
        maxTotalErrors: 15)
    .Build();
```

#### Advanced Overload

Fine-grained control over each middleware component:

```csharp
public static AgentBuilder WithErrorHandling(
    this AgentBuilder builder,
    Action<CircuitBreakerMiddleware>? configureCircuitBreaker = null,
    Action<ErrorTrackingMiddleware>? configureErrorTracking = null,
    Action<TotalErrorThresholdMiddleware>? configureTotalThreshold = null,
    Action<ErrorHandlingConfig>? configureFunctionRetry = null,
    TimeSpan? configureFunctionTimeout = null)
```

**Usage:**

```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling(
        configureCircuitBreaker: cb =>
        {
            cb.MaxConsecutiveCalls = 3;
            cb.TerminationMessageTemplate = "Loop detected: {toolName}";
        },
        configureFunctionRetry: retry =>
        {
            retry.MaxRetries = 10;
            retry.RetryDelay = TimeSpan.FromSeconds(2);
            retry.MaxRetriesByCategory = new Dictionary<ErrorCategory, int>
            {
                [ErrorCategory.RateLimitRetryable] = 15
            };
        },
        configureFunctionTimeout: TimeSpan.FromMinutes(3))
    .Build();
```

## Middleware Registration Order

The convenience method registers middleware in this **guaranteed correct order**:

### Iteration-Level Middleware (outer to inner):
1. **CircuitBreakerMiddleware** - Detects stuck loops (same function called N times)
2. **ErrorTrackingMiddleware** - Tracks consecutive errors (resets on success)
3. **TotalErrorThresholdMiddleware** - Tracks cumulative errors (never resets)

### Function-Level Middleware (onion pattern):
4. **FunctionRetryMiddleware** - Outermost, retries entire timeout operation
5. **FunctionTimeoutMiddleware** - Inner, applies timeout to each retry attempt

### Why This Order?

**Iteration-level:** Circuit breaker runs first to detect infinite loops early, before error tracking.

**Function-level (onion pattern):**
```
┌─────────────────────────────────────┐
│ FunctionRetryMiddleware             │  ← Retry entire operation
│  ┌───────────────────────────────┐  │
│  │ FunctionTimeoutMiddleware     │  │  ← Timeout each attempt
│  │  ┌─────────────────────────┐  │  │
│  │  │ Actual Function         │  │  │  ← Execute function
│  │  └─────────────────────────┘  │  │
│  │  If timeout → throw           │  │
│  └───────────────────────────────┘  │
│  If timeout → retry with delay      │
└─────────────────────────────────────┘
```

This ensures:
- Timeout applies to EACH retry attempt (not total time)
- Retry wraps timeout (can retry after timeout)
- Correct layering of concerns

## Files Modified

1. **HPD-Agent/Agent/AgentBuilder.cs**
   - Added `using HPD.Agent.Middleware.Function;`
   - Added `using HPD.Agent.ErrorHandling;`
   - Added new section: "FUNCTION-LEVEL ERROR HANDLING MIDDLEWARE"
   - Added 6 new extension methods:
     - `WithFunctionRetry()` (2 overloads)
     - `WithFunctionTimeout()` (2 overloads)
     - `WithErrorHandling()` (2 overloads)

## Benefits

### Developer Experience

✅ **Zero Cognitive Load** - One method call, all middleware registered
✅ **Guaranteed Correct Order** - No more guessing or consulting docs
✅ **Progressive Complexity** - Simple by default, advanced when needed
✅ **Type-Safe** - Compile-time errors, IntelliSense support
✅ **Self-Documenting** - XML comments explain everything
✅ **Discoverable** - Fluent API, method names explain intent

### Code Quality

✅ **Reduces Boilerplate** - 5 lines → 1 line
✅ **Prevents Mistakes** - Can't get the order wrong
✅ **Consistent Defaults** - Industry-standard settings
✅ **Testable** - Each middleware independently testable

### Backward Compatibility

✅ **100% Backward Compatible** - Existing code works unchanged
✅ **Opt-In** - Developers choose when to use convenience method
✅ **Incremental Adoption** - Can mix old and new style

## Usage Examples

### Before & After

**Before:**
```csharp
var agent = new AgentBuilder(config)
    .WithCircuitBreaker(maxConsecutiveCalls: 5)
    .WithErrorTracking(maxConsecutiveErrors: 3)
    .WithTotalErrorThreshold(maxTotalErrors: 10)
    // Missing function-level middleware!
    .Build();
```

**After:**
```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling()  // All 5 middleware, correct order
    .Build();
```

### Real-World Examples

#### Example 1: Production API with Strict Error Handling

```csharp
var config = new AgentConfig
{
    Provider = new ProviderConfig
    {
        ProviderKey = "openai",
        ModelName = "gpt-4"
    },
    ErrorHandling = new ErrorHandlingConfig
    {
        MaxRetries = 5,
        RetryDelay = TimeSpan.FromSeconds(2),
        SingleFunctionTimeout = TimeSpan.FromMinutes(2),
        MaxRetriesByCategory = new Dictionary<ErrorCategory, int>
        {
            [ErrorCategory.RateLimitRetryable] = 10,  // Patient with rate limits
            [ErrorCategory.ServerError] = 2            // Less patient with 5xx
        }
    }
};

var agent = new AgentBuilder(config)
    .WithErrorHandling(
        maxConsecutiveCalls: 3,      // Fail fast on loops
        maxConsecutiveErrors: 3,      // Fail fast on errors
        maxTotalErrors: 10)           // Stop after 10 total errors
    .Build();
```

#### Example 2: Development/Testing with Permissive Settings

```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling(
        maxConsecutiveCalls: 10,     // Allow more exploration
        maxConsecutiveErrors: 5,      // More forgiving
        maxTotalErrors: 20)           // Allow more attempts
    .Build();
```

#### Example 3: Advanced Custom Configuration

```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling(
        configureCircuitBreaker: cb =>
        {
            cb.MaxConsecutiveCalls = 3;
            cb.TerminationMessageTemplate = "⚠️ Agent stuck in loop calling {toolName}";
        },
        configureFunctionRetry: retry =>
        {
            retry.MaxRetries = 10;
            retry.BackoffMultiplier = 3.0;  // Aggressive backoff
            retry.MaxRetryDelay = TimeSpan.FromMinutes(2);
            retry.CustomRetryStrategy = async (ex, attempt, ct) =>
            {
                // Custom logic for specific exceptions
                if (ex is MyCustomException custom)
                    return TimeSpan.FromSeconds(custom.RecommendedDelay);
                return null; // Use default
            };
        },
        configureFunctionTimeout: TimeSpan.FromMinutes(5))
    .Build();
```

## Testing

The implementation includes comprehensive tests in:
- `test/HPD-Agent.Tests/Middleware/ErrorHandlingConvenienceMethodTest.cs`

Tests verify:
- ✅ Middleware registration count (5 middleware)
- ✅ Middleware registration order
- ✅ Default values
- ✅ Custom configuration
- ✅ Standalone method usage

## Documentation

All methods include comprehensive XML documentation with:
- Summary of functionality
- Parameter descriptions
- Return value documentation
- Remarks explaining middleware order and behavior
- Usage examples
- Recommended patterns

## Future Enhancements

Potential additions:
- Preset configurations (Strict, Balanced, Permissive)
- Fluent configuration API
- Event-based configuration validation
- Integration with observability platforms

## Migration Guide

### For Existing Code

No changes required! Existing code continues to work:

```csharp
// Old style - still works
var agent = new AgentBuilder(config)
    .WithCircuitBreaker(5)
    .WithErrorTracking(3)
    .Build();
```

### To Use New Convenience Method

Simply replace individual middleware calls:

```csharp
// New style - simpler
var agent = new AgentBuilder(config)
    .WithErrorHandling()
    .Build();
```

### Incremental Adoption

Mix and match as needed:

```csharp
// Use convenience method + add custom middleware
var agent = new AgentBuilder(config)
    .WithErrorHandling()         // All error handling middleware
    .WithPermissions()            // Add permission checking
    .WithLogging()                // Add logging
    .Build();
```

## Summary

The `WithErrorHandling()` convenience method provides:

✅ **One-line solution** for complete error handling setup
✅ **Guaranteed correct order** - no mistakes possible
✅ **Progressive complexity** - simple to advanced
✅ **100% backward compatible** - existing code works
✅ **Comprehensive documentation** - XML comments + examples
✅ **Type-safe** - compile-time errors
✅ **Tested** - comprehensive test coverage

This significantly improves the developer experience while maintaining the flexibility and power of the underlying middleware architecture.
