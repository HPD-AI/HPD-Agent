# Middleware Extraction & Enhancement Proposal

**Status:** Approved with Revisions
**Last Updated:** 2024-01-XX
**Evaluation Score:** 8.5/10

## Executive Summary

This proposal outlines a refactoring plan to extract hardcoded logic from `AgentCore` into proper middleware, and add new middleware capabilities inspired by LangChain's architecture while maintaining HPD-Agent's cleaner separation of concerns.

## Current State Analysis

### HPD-Agent Middleware Types (Existing)

| Interface | Layer | Purpose |
|-----------|-------|---------|
| `IPromptMiddleware` | Pre-Turn | Modify messages before any LLM call |
| `IIterationMiddleware` | Per-Iteration | Before/after each LLM call in agentic loop |
| `IAIFunctionMiddleware` | Tool Execution | Wrap individual function invocations |
| `IPermissionMiddleware` | Tool Authorization | Permission checks before tool execution |
| `IMessageTurnMiddleware` | Post-Turn | Observe completed turn (read-only) |
| `IChatClient` wrapping | Infrastructure | Transport-level (logging, caching, telemetry) |

### LangChain Middleware Capabilities (Reference)

| LangChain Middleware | Description |
|---------------------|-------------|
| `ModelRetryMiddleware` | Retry failed LLM calls with backoff |
| `ModelFallbackMiddleware` | Try alternative models on failure |
| `ModelCallLimitMiddleware` | Limit total LLM calls |
| `ToolCallLimitMiddleware` | Limit tool calls (global or per-tool) |
| `LLMToolSelectorMiddleware` | Use LLM to select relevant tools |
| `ContextEditingMiddleware` | Token-aware context clearing |
| `SummarizationMiddleware` | Summarize old messages |
| `PIIMiddleware` | Detect/redact PII |
| `HumanInTheLoopMiddleware` | Human review/approval |
| `TodoListMiddleware` | Task tracking |

---

## Phase 1: Extract Hardcoded Logic from AgentCore

### 1.1 CircuitBreakerIterationMiddleware

**Current Locations:**
- `AgentDecisionEngine.DecideNextAction()` - Decision logic (pure, checks state)
- `RunAgenticLoopInternal()` - Execution logic (BEFORE tool execution, prevents wasted calls)

**Current Code (in AgentDecisionEngine):**
```csharp
// AgentDecisionEngine.DecideNextAction() - lines 2750-2763
if (config.MaxConsecutiveFunctionCalls.HasValue)
{
    var toolRequests = ExtractToolRequestsFromResponse(lastResponse);

    foreach (var toolRequest in toolRequests)
    {
        var signature = ComputeFunctionSignature(toolRequest);

        if (state.ConsecutiveCountPerTool.TryGetValue(toolRequest.Name, out var count) &&
            count >= config.MaxConsecutiveFunctionCalls.Value)
        {
            return new AgentDecision.Terminate(
                $"Circuit breaker triggered: function '{toolRequest.Name}' called {count} consecutive times with identical arguments");
        }
    }
}
```

**Current Code (in RunAgenticLoopInternal):**
```csharp
// RunAgenticLoopInternal - lines 1314-1344
if (Config?.AgenticLoop?.MaxConsecutiveFunctionCalls is { } maxConsecutiveCalls)
{
    bool circuitBreakerTriggered = false;

    foreach (var toolRequest in toolRequests)
    {
        var signature = ComputeFunctionSignatureFromContent(toolRequest);
        var toolName = toolRequest.Name ?? "_unknown";
        var lastSig = state.LastSignaturePerTool.GetValueOrDefault(toolName);
        var isIdentical = !string.IsNullOrEmpty(lastSig) && signature == lastSig;
        var countAfterExecution = isIdentical
            ? state.ConsecutiveCountPerTool.GetValueOrDefault(toolName, 0) + 1
            : 1;

        if (countAfterExecution >= maxConsecutiveCalls)
        {
            // Emit error, terminate...
            circuitBreakerTriggered = true;
            break;
        }
    }

    if (circuitBreakerTriggered) break;
}
```

**Proposed Middleware:**
```csharp
/// <summary>
/// Prevents infinite loops by detecting repeated identical function calls.
/// Follows the pattern established by ContinuationPermissionIterationMiddleware.
/// </summary>
public class CircuitBreakerIterationMiddleware : IIterationMiddleware
{
    public int MaxConsecutiveCalls { get; set; } = 3;

    public Task BeforeIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // Check BEFORE LLM call if any function is about to exceed threshold
        // Access state.LastSignaturePerTool and state.ConsecutiveCountPerTool
        // If threshold would be exceeded:
        //   - Emit CircuitBreakerTriggeredEvent
        //   - Set context.SkipLLMCall = true
        //   - Provide termination message in context.Response
        return Task.CompletedTask;
    }

    public Task AfterIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // After tool calls complete, signal state updates via context.Properties
        // Properties["UpdateCircuitBreakerState"] = (toolName, signature) pairs
        return Task.CompletedTask;
    }
}
```

**Reference Pattern:** See `ContinuationPermissionIterationMiddleware` for the established pattern.

**Benefits:**
- Configurable per-agent
- Can be disabled entirely
- Testable in isolation
- Can emit custom events
- Follows proven `ContinuationPermissionIterationMiddleware` pattern

---

### 1.2 ErrorTrackingIterationMiddleware

> ⚠️ **Discovery Task Required:** The error tracking logic is distributed across multiple locations.
> A discovery task should be completed before implementing this middleware.

**Current Locations (Distributed):**
1. `AgentLoopState.WithFailure()` / `WithSuccess()` - State mutation methods
2. `AgentDecisionEngine.DecideNextAction()` - Decision check (line 2721-2723)
3. `RunAgenticLoopInternal()` - Error detection and termination (lines 1430-1447)

**Error Detection Logic (in RunAgenticLoopInternal):**
```csharp
// Lines 1430-1447
bool hasErrors = toolResultMessage.Contents
    .OfType<FunctionResultContent>()
    .Any(r =>
    {
        if (r.Exception != null) return true;
        var resultStr = r.Result?.ToString();
        if (string.IsNullOrEmpty(resultStr)) return false;
        return resultStr.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
               resultStr.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase) ||
               // ... more patterns
    });

if (hasErrors)
{
    state = state.WithFailure();
    var maxConsecutiveErrors = Config?.ErrorHandling?.MaxRetries ?? 3;
    if (state.ConsecutiveFailures >= maxConsecutiveErrors)
    {
        // Terminate with error message
    }
}
else
{
    state = state.WithSuccess();
}
```

**Decision Check (in AgentDecisionEngine):**
```csharp
// Lines 2721-2723
if (state.ConsecutiveFailures >= config.MaxConsecutiveFailures)
    return new AgentDecision.Terminate(
        $"Maximum consecutive failures ({config.MaxConsecutiveFailures}) exceeded");
```

**Discovery Tasks Before Implementation:**
1. Document the complete error tracking flow
2. Identify all state mutation points
3. Determine if state updates can be signaled via Properties
4. Design middleware based on actual flow, not assumptions

**Proposed Middleware (Pending Discovery):**
```csharp
public class ErrorTrackingIterationMiddleware : IIterationMiddleware
{
    public int MaxConsecutiveErrors { get; set; } = 3;
    public Func<FunctionResultContent, bool>? CustomErrorDetector { get; set; }

    public Task AfterIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // Analyze tool results for errors (context.ToolCalls results)
        // Signal state updates via context.Properties:
        //   Properties["HasErrors"] = true/false
        //   Properties["ShouldTerminate"] = true (if threshold exceeded)
        // Emit ErrorThresholdExceededEvent if terminating
    }
}
```

**Benefits:**
- Custom error detection logic (extensible)
- Configurable thresholds
- Observable error events
- Decoupled from core agent logic

---

### ~~1.3 ContainerExpansionPromptMiddleware~~ (DEFERRED)

> ⚠️ **Deferred:** Requires further investigation into actual container filtering mechanism.

**Actual Method Name:** `MiddlewareContainerResults` (not `FilterContainerResults`)

**Current Location:** `AgentCore.cs` line 1935

```csharp
private static List<AIContent> MiddlewareContainerResults(
    IList<AIContent> contents,
    IList<FunctionCallContent> toolRequests,
    ChatOptions? options)
```

**Status:** The container expansion filtering mechanism needs further investigation before designing the middleware extraction. This item is deferred until the actual mechanism is fully understood.

---

## Phase 2: New Middleware Capabilities

### 2.1 PIIPromptMiddleware (High Priority)

**Purpose:** Detect and handle PII in messages before sending to LLM

```csharp
public class PIIPromptMiddleware : IPromptMiddleware
{
    public enum PIIStrategy { Block, Redact, Mask, Hash }

    // Recommended defaults per evaluator feedback
    public PIIStrategy EmailStrategy { get; set; } = PIIStrategy.Redact;
    public PIIStrategy CreditCardStrategy { get; set; } = PIIStrategy.Block;  // High risk
    public PIIStrategy SSNStrategy { get; set; } = PIIStrategy.Block;         // High risk
    public PIIStrategy PhoneStrategy { get; set; } = PIIStrategy.Redact;
    public PIIStrategy IPAddressStrategy { get; set; } = PIIStrategy.Hash;

    // Custom patterns
    public List<(Regex Pattern, PIIStrategy Strategy, string Replacement)> CustomPatterns { get; set; }

    public Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next)
    {
        var sanitized = ScanAndProcess(context.Messages);
        context.Messages = sanitized;
        return next(context);
    }
}
```

**PII Types to Detect:**
- Email addresses
- Credit card numbers (with Luhn validation)
- Social Security Numbers
- Phone numbers
- IP addresses
- MAC addresses
- URLs with credentials
- API keys (common patterns)

---

### 2.2 ModelFallbackChatClient (IChatClient Wrapper)

**Purpose:** Automatically try fallback models on failure

```csharp
public class ModelFallbackChatClient : IChatClient
{
    private readonly IChatClient _primary;
    private readonly IReadOnlyList<IChatClient> _fallbacks;
    private readonly Func<Exception, bool>? _shouldFallback;

    public ModelFallbackChatClient(
        IChatClient primary,
        IEnumerable<IChatClient> fallbacks,
        Func<Exception, bool>? shouldFallback = null)
    {
        _primary = primary;
        _fallbacks = fallbacks.ToList();
        _shouldFallback = shouldFallback ?? DefaultShouldFallback;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var clients = new[] { _primary }.Concat(_fallbacks);
        Exception? lastException = null;

        foreach (var client in clients)
        {
            try
            {
                await foreach (var update in client.GetStreamingResponseAsync(messages, options, ct))
                {
                    yield return update;
                }
                yield break; // Success - exit
            }
            catch (Exception ex) when (_shouldFallback(ex))
            {
                lastException = ex;
                // Try next fallback
            }
        }

        throw new AggregateException("All models failed", lastException!);
    }
}
```

---

### 2.3 ContextEditingPromptMiddleware

**Purpose:** Token-aware clearing of old tool results

> **Token Counting Recommendation:** Use `ITokenCounter` injection with fallback estimate.

```csharp
public class ContextEditingPromptMiddleware : IPromptMiddleware
{
    public int TokenTrigger { get; set; } = 100_000;
    public int ClearAtLeast { get; set; } = 10_000;
    public int KeepRecentToolResults { get; set; } = 3;
    public HashSet<string> ExcludeTools { get; set; } = new();
    public bool ClearToolInputs { get; set; } = false;

    public ITokenCounter? TokenCounter { get; set; } // Injectable, optional

    public async Task<IEnumerable<ChatMessage>> InvokeAsync(
        PromptMiddlewareContext context,
        Func<PromptMiddlewareContext, Task<IEnumerable<ChatMessage>>> next)
    {
        var tokenCount = TokenCounter != null
            ? await TokenCounter.CountAsync(context.Messages)
            : EstimateTokens(context.Messages); // Fallback: 4 chars ≈ 1 token

        if (tokenCount > TokenTrigger)
        {
            context.Messages = ClearOldToolResults(context.Messages);
        }

        return await next(context);
    }
}
```

---

### 2.4 IAgentLifecycleMiddleware (New Interface)

**Purpose:** Before/after entire agent run (like LangChain's before_agent/after_agent)

> **Recommendation:** BeforeAgent should NOT modify input messages. Keep separation clean.
> Use `IPromptMiddleware` for message modification.

```csharp
public interface IAgentLifecycleMiddleware
{
    /// <summary>
    /// Called once before the agent run starts.
    /// Use for initialization, resource allocation, context setup.
    /// Should NOT modify input messages (use IPromptMiddleware for that).
    /// </summary>
    Task BeforeAgentAsync(
        AgentLifecycleContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called once after the agent run completes (success or failure).
    /// Use for cleanup, metrics, finalization.
    /// </summary>
    Task AfterAgentAsync(
        AgentLifecycleContext context,
        AgentLoopState finalState,
        Exception? exception,
        CancellationToken cancellationToken);
}

public class AgentLifecycleContext
{
    public required string AgentName { get; init; }
    public required string RunId { get; init; }
    public required string ConversationId { get; init; }
    public required IReadOnlyList<ChatMessage> InputMessages { get; init; }
    public required ChatOptions? Options { get; init; }
    public required ConversationThread? Thread { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
}
```

**Use Cases:**
- Resource allocation/cleanup
- Metrics collection (run duration, token usage)
- Audit logging
- Context initialization
- Memory/RAG preloading

---

### 2.5 ToolCallLimitIterationMiddleware

**Purpose:** Limit tool calls (global or per-tool)

```csharp
public class ToolCallLimitIterationMiddleware : IIterationMiddleware
{
    public int? GlobalThreadLimit { get; set; }
    public int? GlobalRunLimit { get; set; }
    public Dictionary<string, int> PerToolThreadLimits { get; set; } = new();
    public Dictionary<string, int> PerToolRunLimits { get; set; } = new();

    public enum ExitBehavior { Continue, Error, End }
    public ExitBehavior OnLimitExceeded { get; set; } = ExitBehavior.Continue;

    public Task AfterIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        // Track tool calls
        // Block/terminate if limits exceeded
        // Emit ToolCallLimitExceededEvent
    }
}
```

---

### 2.6 LLMToolSelectorIterationMiddleware

**Purpose:** Use LLM to select relevant tools from large toolsets

> ⚠️ **Lower Priority:** Extra LLM call overhead. Only valuable for very large toolsets (20+).

```csharp
public class LLMToolSelectorIterationMiddleware : IIterationMiddleware
{
    public required IChatClient SelectorModel { get; set; }
    public int MaxTools { get; set; } = 10;
    public HashSet<string> AlwaysInclude { get; set; } = new();
    public int MinToolsToTrigger { get; set; } = 20; // Only filter if > 20 tools

    public Task BeforeIterationAsync(IterationMiddlewareContext context, CancellationToken ct)
    {
        if (context.Options?.Tools?.Count > MinToolsToTrigger)
        {
            var relevantTools = await SelectRelevantTools(
                context.Messages,
                context.Options.Tools,
                ct);

            context.Options = context.Options with { Tools = relevantTools };
        }
    }
}
```

---

## Phase 3: AgentBuilder Integration

### New Builder Methods

```csharp
public class AgentBuilder
{
    // Existing
    public AgentBuilder WithPromptMiddleware(IPromptMiddleware middleware);
    public AgentBuilder WithIterationMiddleware(IIterationMiddleware middleware);
    public AgentBuilder WithFunctionMiddleware(IAIFunctionMiddleware middleware);
    public AgentBuilder WithPermissionMiddleware(IPermissionMiddleware middleware);

    // New - Phase 1 Extractions
    public AgentBuilder WithCircuitBreaker(int maxConsecutiveCalls = 3);
    public AgentBuilder WithErrorTracking(int maxConsecutiveErrors = 3);

    // New - Phase 2 Additions
    public AgentBuilder WithPIIProtection(Action<PIIPromptMiddleware>? configure = null);
    public AgentBuilder WithModelFallback(params IChatClient[] fallbacks);
    public AgentBuilder WithContextEditing(int tokenTrigger = 100_000);
    public AgentBuilder WithLifecycleMiddleware(IAgentLifecycleMiddleware middleware);
    public AgentBuilder WithToolCallLimits(int? globalLimit = null, Dictionary<string, int>? perToolLimits = null);
    public AgentBuilder WithToolSelector(IChatClient selectorModel, int maxTools = 10);
}
```

---

## Revised Implementation Priority

Based on evaluator feedback:

### Phase 1a: CircuitBreaker Extraction (2-3 days)
1. **CircuitBreakerIterationMiddleware** - Clear location, proven pattern exists
   - Follow `ContinuationPermissionIterationMiddleware` pattern
   - Add `CircuitBreakerTriggeredEvent` (already exists, ensure middleware emits it)
   - Add `AgentBuilder.WithCircuitBreaker()`
   - Write characterization tests first

### Phase 1b: Error Tracking Discovery & Extraction (3-5 days)
2. **Discovery Task:** Document actual error tracking flow
   - Map all state mutation points
   - Document decision points
   - Design middleware based on reality
3. **ErrorTrackingIterationMiddleware** - Implement based on discovery

### Phase 2: New Capabilities (Medium Priority)
4. **PIIPromptMiddleware** - Security/compliance (P1)
5. **IAgentLifecycleMiddleware** - New interface (P2)
6. **ModelFallbackChatClient** - Resilience (P2)

### Phase 3: Deferred / Lower Priority
7. **ContextEditingPromptMiddleware** - Token counting complexity (P3)
8. **ToolCallLimitIterationMiddleware** - Enhanced limiting (P3)
9. **ContainerExpansionPromptMiddleware** - Pending investigation
10. **LLMToolSelectorIterationMiddleware** - Extra LLM overhead (P4)

---

## Migration Strategy

### Step 1: Add Middleware Without Breaking Changes
- Add new middleware interfaces and implementations
- Keep existing hardcoded logic in AgentCore
- Add feature flags to switch between old/new behavior

### Step 2: Gradual Migration
- Default new agents to use middleware
- Existing agents continue with hardcoded behavior
- Add deprecation warnings

### Step 3: Remove Hardcoded Logic
- Remove hardcoded circuit breaker from AgentCore
- Remove hardcoded error tracking from AgentCore
- Middleware becomes the only implementation

---

## Events to Add

```csharp
// Circuit Breaker (already exists - ensure middleware emits it)
public record CircuitBreakerTriggeredEvent(
    string AgentName,
    string FunctionName,
    int ConsecutiveCount,
    int Iteration,
    DateTimeOffset Timestamp) : AgentEvent;

// Error Tracking
public record ErrorThresholdExceededEvent(
    string AgentName,
    int ConsecutiveErrors,
    int MaxAllowed,
    string LastErrorMessage,
    DateTimeOffset Timestamp) : AgentEvent;

// PII Detection
public record PIIDetectedEvent(
    string AgentName,
    string PIIType,
    PIIStrategy ActionTaken,
    int OccurrenceCount,
    DateTimeOffset Timestamp) : AgentEvent;

// Tool Limits
public record ToolCallLimitExceededEvent(
    string AgentName,
    string? ToolName,
    int CallCount,
    int Limit,
    ExitBehavior Action,
    DateTimeOffset Timestamp) : AgentEvent;

// Model Fallback
public record ModelFallbackEvent(
    string AgentName,
    string FailedModel,
    string FallbackModel,
    string FailureReason,
    DateTimeOffset Timestamp) : AgentEvent;

// Lifecycle
public record AgentRunStartedEvent(
    string AgentName,
    string RunId,
    string ConversationId,
    int InputMessageCount,
    DateTimeOffset Timestamp) : AgentEvent;

public record AgentRunCompletedEvent(
    string AgentName,
    string RunId,
    TimeSpan Duration,
    int TotalIterations,
    int TotalFunctionCalls,
    bool Success,
    string? TerminationReason,
    DateTimeOffset Timestamp) : AgentEvent;
```

---

## Testing Strategy

### Unit Tests (Per Middleware)
- CircuitBreakerIterationMiddleware: Test threshold detection, state tracking
- ErrorTrackingIterationMiddleware: Test error detection, consecutive counting
- PIIPromptMiddleware: Test each PII type, each strategy
- ModelFallbackChatClient: Test fallback chain, exception filtering

### Integration Tests
- Middleware composition (multiple middleware working together)
- Event emission and handling
- State transitions through middleware pipeline

### Characterization Tests
- **Run BEFORE extraction** to capture current behavior
- Verify behavior matches current hardcoded implementation
- Run after extraction to ensure no regression

---

## Resolved Questions

Based on evaluator recommendations:

1. **CircuitBreaker: IIterationMiddleware or IAIFunctionMiddleware?**
   - ✅ **IIterationMiddleware** - The existing logic runs before LLM calls to prevent wasted API calls. It checks tool call patterns from previous iterations, not individual function executions.

2. **PII: Block or Redact?**
   - ✅ **Configurable per-type** - Default to Redact for most, Block for SSN/credit cards.

3. **ContextEditing: How to count tokens without tokenizer?**
   - ✅ **ITokenCounter injection** - Make it optional with a fallback estimate (4 chars ≈ 1 token).

4. **Lifecycle: Should BeforeAgent modify input messages?**
   - ✅ **No** - Keep `IAgentLifecycleMiddleware` for setup/teardown only. Message modification belongs in `IPromptMiddleware`.

---

## Appendix: LangChain Reference Files

Located at: `/Users/einsteinessibu/Documents/HPD-Agent/Reference/langchain/libs/langchain_v1/langchain/agents/middleware/`

Key files reviewed:
- `types.py` - Core middleware architecture
- `model_retry.py` - Retry with backoff
- `model_fallback.py` - Fallback models
- `model_call_limit.py` - Call limiting
- `tool_call_limit.py` - Tool limiting
- `tool_selection.py` - LLM-based tool selection
- `context_editing.py` - Token-aware clearing
- `pii.py` - PII detection/redaction
- `human_in_the_loop.py` - Human review
- `summarization.py` - Message summarization

---

## Appendix: Evaluation Summary

| Criterion | Score |
|-----------|-------|
| Technical accuracy | 8/10 (minor inaccuracies corrected) |
| Architectural alignment | 9/10 (fits existing patterns well) |
| Completeness | 9/10 (covers most scenarios) |
| Feasibility | 8/10 (most items straightforward) |
| Prioritization | 9/10 (sensible ordering) |
| **Overall** | **8.5/10 - Approved with revisions** |
