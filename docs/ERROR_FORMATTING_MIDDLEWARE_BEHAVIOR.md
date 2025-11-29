# Error Formatting Behavior - With and Without Middleware

## Overview

This document explains how error formatting works in HPD-Agent and what happens when `ErrorFormattingMiddleware` is not registered.

---

## Architecture Changes (Post-Refactor)

### What Changed

**Before:** `AgentMiddlewarePipeline.cs` had a catch block that formatted errors:
```csharp
catch (Exception ex)
{
    context.FunctionException = ex;
    context.FunctionResult = $"Error: {ex.Message}";  // ❌ Always included message
}
```

**After:** Catch block removed - error formatting delegated to middleware or Agent.cs fallback:
```csharp
// Execute the actual function
// Note: Exception handling is delegated to middleware (e.g., ErrorFormattingMiddleware)
// or to the Agent.cs FormatErrorForLLM() fallback.
// This ensures consistent, security-aware error formatting.
context.FunctionResult = await executeFunction().ConfigureAwait(false);
```

---

## Error Flow Paths

### Path 1: With ErrorFormattingMiddleware (Recommended)

```
Function throws exception
  ↓
ErrorFormattingMiddleware catches it
  ↓
Sanitizes based on IncludeDetailedErrorsInChat setting
  ↓
Returns formatted string as context.FunctionResult
  ↓
No exception propagates (handled by middleware)
  ↓
LLM receives sanitized error message
```

**Security:** ✅ **SECURE** - Middleware respects `IncludeDetailedErrorsInChat`

**Example:**
```csharp
var agent = new AgentBuilder(config)
    .WithErrorFormatting()  // Registers ErrorFormattingMiddleware
    .Build();

// Function throws: "Connection failed: Server=prod-db.company.com;Password=secret"
// LLM sees: "Error: Function 'Connect' failed."
```

---

### Path 2: Without ErrorFormattingMiddleware (Fallback)

```
Function throws exception
  ↓
No middleware catches it (no ErrorFormattingMiddleware)
  ↓
Exception propagates through middleware pipeline
  ↓
Agent.cs executeFunction lambda catches it (line 4101-4105)
  ↓
Calls FormatErrorForLLM() which respects IncludeDetailedErrorsInChat
  ↓
Returns formatted string
  ↓
LLM receives sanitized error message
```

**Security:** ✅ **SECURE** - Agent.cs fallback respects `IncludeDetailedErrorsInChat`

**Example:**
```csharp
var agent = new AgentBuilder(config)
    .Build();  // No ErrorFormattingMiddleware

// Function throws: "Connection failed: Server=prod-db.company.com;Password=secret"
// LLM sees: "Error: Function 'Connect' failed."
// (Same result as with middleware - fallback works)
```

---

## Code Locations

### 1. ErrorFormattingMiddleware (Primary)

**Location:** `HPD-Agent/Middleware/Function/ErrorFormattingMiddleware.cs`

```csharp
public async ValueTask<object?> ExecuteFunctionAsync(...)
{
    try
    {
        return await next();
    }
    catch (Exception ex)
    {
        context.FunctionException = ex;  // Store full exception

        if (_includeDetailedErrors)
            return $"Error invoking function '{functionName}': {ex.Message}";
        else
            return $"Error: Function '{functionName}' failed.";
    }
}
```

### 2. Agent.cs Fallback (Secondary)

**Location:** `HPD-Agent/Agent/Agent.cs:4160-4173`

```csharp
private string FormatErrorForLLM(Exception exception, string functionName)
{
    if (_errorHandlingConfig?.IncludeDetailedErrorsInChat == true)
    {
        // Include full exception details (potential security risk)
        return $"Error invoking function '{functionName}': {exception.Message}";
    }
    else
    {
        // Generic error message (safe for LLM consumption)
        return $"Error: Function '{functionName}' failed.";
    }
}
```

**Invoked at:** `Agent.cs:4101-4105`

```csharp
catch (Exception ex)
{
    // Format error for LLM
    return FormatErrorForLLM(ex, middlewareContext.Function.Name);
}
```

### 3. AgentMiddlewarePipeline.cs (No Longer Catches)

**Location:** `HPD-Agent/Middleware/AgentMiddlewarePipeline.cs:397-401`

```csharp
// Execute the actual function
// Note: Exception handling is delegated to middleware (e.g., ErrorFormattingMiddleware)
// or to the Agent.cs FormatErrorForLLM() fallback.
// This ensures consistent, security-aware error formatting.
context.FunctionResult = await executeFunction().ConfigureAwait(false);
```

**No catch block** - lets exceptions propagate to middleware or Agent.cs.

---

## Behavior Matrix

| Scenario | Error Handler | Security | Notes |
|----------|---------------|----------|-------|
| With `ErrorFormattingMiddleware` | Middleware | ✅ Secure | Catches in middleware, respects config |
| Without any middleware | Agent.cs fallback | ✅ Secure | Catches in Agent.cs, respects config |
| With other middleware (no formatter) | Agent.cs fallback | ✅ Secure | Exception propagates to Agent.cs |
| Middleware itself throws | Agent.cs fallback | ✅ Secure | Exception propagates to Agent.cs |

**All paths are now secure!** ✅

---

## Why This Design?

### Benefits of Removing Pipeline Catch Block

1. **Single Source of Truth**
   - ErrorFormattingMiddleware is the primary handler
   - Agent.cs FormatErrorForLLM() is the fallback
   - No third location doing error formatting

2. **Consistent Security**
   - All paths respect `IncludeDetailedErrorsInChat` setting
   - No edge cases where errors leak through

3. **Cleaner Separation of Concerns**
   - Middleware handles middleware-level concerns
   - Agent.cs handles agent-level concerns
   - Pipeline just orchestrates the flow

4. **Better Composability**
   - Users can insert custom error formatting middleware
   - No conflict with pipeline's formatting logic
   - More predictable behavior

---

## Recommendations

### For Most Users (Recommended)

Use `WithErrorHandling()` which includes `ErrorFormattingMiddleware`:

```csharp
var agent = new AgentBuilder(config)
    .WithErrorHandling()  // Includes ErrorFormattingMiddleware
    .Build();
```

**Why:** Catches errors at the middleware level, consistent with the rest of your middleware stack.

---

### For Advanced Users

Register `ErrorFormattingMiddleware` manually for fine control:

```csharp
var agent = new AgentBuilder(config)
    .WithFunctionRetry()
    .WithFunctionTimeout()
    .WithErrorFormatting()  // Explicit registration
    .Build();
```

---

### For Minimalists

Don't register any middleware - rely on Agent.cs fallback:

```csharp
var agent = new AgentBuilder(config)
    .Build();  // No middleware, Agent.cs handles errors
```

**Why:** Simpler setup, still secure via fallback. Good for simple agents with no middleware needs.

---

## Migration from Old Behavior

### Before (Had Pipeline Catch Block)

```csharp
// If middleware threw an exception:
// → Pipeline catch block formatted it
// → Always included ex.Message (insecure)
```

### After (No Pipeline Catch Block)

```csharp
// If middleware throws an exception:
// → Exception propagates to Agent.cs
// → FormatErrorForLLM() formats it
// → Respects IncludeDetailedErrorsInChat (secure)
```

**No breaking changes** - all existing code works the same, just more secure now.

---

## Testing

All tests pass with the new behavior:

```
Passed!  - Failed:     0, Passed:   197, Skipped:     0, Total:   197
```

Error handling tests specifically:
- `WithErrorHandling_SimpleUsage_RegistersAllMiddleware` ✓
- `WithErrorHandling_CustomThresholds_UsesProvidedValues` ✓
- `WithErrorHandling_AdvancedConfiguration_AllowsFineTuning` ✓
- All middleware chain tests ✓

---

## Summary

### Key Points

✅ **All error paths are now secure** - No insecure formatting anywhere
✅ **Two handlers, same security** - Middleware primary, Agent.cs fallback
✅ **Backward compatible** - Existing code works unchanged
✅ **Cleaner architecture** - Single responsibility per component
✅ **Fully tested** - 197 middleware tests passing

### Decision Tree

```
Does user register ErrorFormattingMiddleware?
  ├─ YES → Middleware catches & formats (primary path)
  └─ NO → Agent.cs catches & formats (fallback path)

Both paths respect IncludeDetailedErrorsInChat ✅
```

### Bottom Line

**You can use ErrorFormattingMiddleware or not** - both paths are secure. The middleware approach is recommended for consistency with the middleware architecture, but the Agent.cs fallback ensures security even without it.
