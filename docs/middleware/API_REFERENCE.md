# Middleware API Reference

Complete API documentation for the HPD-Agent middleware system.

## Core Interfaces

### IAgentMiddleware

Unified interface for all agent lifecycle hooks.

```csharp
public interface IAgentMiddleware
{
    // Message Turn (once per user message)
    Task BeforeMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct);
    Task AfterMessageTurnAsync(AgentMiddlewareContext context, CancellationToken ct);

    // Iteration (once per LLM call)
    Task BeforeIterationAsync(AgentMiddlewareContext context, CancellationToken ct);
    IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
        AgentMiddlewareContext context,
        Func<IAsyncEnumerable<ChatResponseUpdate>> next,
        CancellationToken ct);
    Task BeforeToolExecutionAsync(AgentMiddlewareContext context, CancellationToken ct);
    Task AfterIterationAsync(AgentMiddlewareContext context, CancellationToken ct);

    // Function (once per tool call)
    Task BeforeSequentialFunctionAsync(AgentMiddlewareContext context, CancellationToken ct);
    Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct);
}
```

All methods have default no-op implementations - implement only what you need.

### AgentMiddlewareContext

Context provided to all middleware hooks.

#### Identity (Always Available)
```csharp
string AgentName                  // Agent executing this operation
string? ConversationId            // Conversation/session ID
CancellationToken CancellationToken
```

#### State (Always Available)
```csharp
AgentLoopState State              // Current loop state (reflects pending updates)
T GetState<T>()                   // Read middleware state
void UpdateState<T>(Func<T, T> transform)  // Schedule state update
```

#### Message Turn Properties
```csharp
ChatMessage? UserMessage          // Incoming user message
IEnumerable<ChatMessage>? ConversationHistory  // Prior conversation
ChatMessage? FinalResponse        // Final assistant response (AfterMessageTurn)
IReadOnlyList<FunctionCallContent>? TurnFunctionCalls  // All functions called (AfterMessageTurn)
```

#### Iteration Properties
```csharp
int Iteration                     // Current iteration number (0-based)
IEnumerable<ChatMessage>? Messages     // Messages to send to LLM (mutable)
ChatOptions? Options              // Chat options (mutable)
ChatResponse? Response            // LLM response (after ExecuteLLMCallAsync)
IReadOnlyList<FunctionCallContent>? ToolCalls  // Requested tool calls
IReadOnlyList<FunctionResultContent>? ToolResults  // Tool execution results
```

#### Function Properties
```csharp
AIFunction? Function              // Function being called
string? FunctionCallId            // Unique call ID
IDictionary<string, object?>? FunctionArguments  // Function arguments (mutable)
object? FunctionResult            // Function result (mutable)
Exception? FunctionException      // Exception if function failed
```

#### Control Flags
```csharp
bool SkipLLMCall { get; set; }              // Skip LLM in BeforeIteration
bool SkipToolExecution { get; set; }        // Skip ALL tools in BeforeToolExecution
bool BlockFunctionExecution { get; set; }   // Skip ONE function in BeforeFunction
```

#### Events
```csharp
void Emit(AgentEvent evt)         // Emit event to observers
Task<TResponse> WaitForResponseAsync<TResponse>(
    string requestId,
    TimeSpan timeout,
    CancellationToken ct)          // Wait for human-in-the-loop response
```

## Lifecycle Hooks

### BeforeMessageTurnAsync

Called once before processing a user message.

**Available context:**
- AgentName, ConversationId, State
- UserMessage, ConversationHistory

**Use for:**
- RAG injection
- Memory retrieval
- Context augmentation

**Example:**
```csharp
public Task BeforeMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    var memories = _store.Load(ctx.ConversationId);
    var contextMsg = new ChatMessage(ChatRole.System, memories);
    ctx.ConversationHistory = ctx.ConversationHistory.Prepend(contextMsg);
    return Task.CompletedTask;
}
```

### AfterMessageTurnAsync

Called once after all iterations complete.

**Available context:**
- All BeforeMessageTurn properties
- FinalResponse, TurnFunctionCalls

**Use for:**
- Memory extraction
- Turn-level analytics
- Cleanup

**Example:**
```csharp
public Task AfterMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    _analytics.RecordTurn(ctx.AgentName, ctx.TurnFunctionCalls?.Count ?? 0);
    return Task.CompletedTask;
}
```

### BeforeIterationAsync

Called before each LLM call within a turn.

**Available context:**
- All BeforeMessageTurn properties
- Iteration, Messages, Options

**Use for:**
- Dynamic instruction injection
- Iteration-aware prompting
- Token optimization

**Example:**
```csharp
public Task BeforeIterationAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    if (ctx.Iteration > 0)
    {
        var hint = new ChatMessage(ChatRole.System,
            "Previous iteration didn't complete. Try a different approach.");
        ctx.Messages = ctx.Messages.Append(hint);
    }
    return Task.CompletedTask;
}
```

**To skip LLM:**
```csharp
ctx.SkipLLMCall = true;
ctx.Response = new ChatResponse { Contents = [...] };
```

### ExecuteLLMCallAsync

Called to execute the LLM with full streaming control. Chains in REVERSE order (onion pattern).

**Available context:**
- All BeforeIteration properties

**Use for:**
- Caching
- Retry logic with backoff
- Response transformation
- Streaming interception

**Example (Caching):**
```csharp
public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
    AgentMiddlewareContext ctx,
    Func<IAsyncEnumerable<ChatResponseUpdate>> next,
    [EnumeratorCancellation] CancellationToken ct)
{
    var key = ComputeKey(ctx.Messages);

    if (_cache.TryGet(key, out var cached))
    {
        yield return cached;  // Return cached - NO LLM call
        yield break;
    }

    var response = new List<ChatResponseUpdate>();
    await foreach (var update in next().WithCancellation(ct))
    {
        response.Add(update);
        yield return update;
    }

    _cache.Set(key, response);
}
```

**Important:**
- MUST call `next()` unless completely skipping
- MUST yield all updates from `next()` to preserve streaming
- Use `[EnumeratorCancellation]` attribute on ct parameter
- Executes in REVERSE order (last registered = outermost)

### BeforeToolExecutionAsync

Called after LLM returns, before ANY tools execute.

**Available context:**
- All BeforeIteration properties
- Response, ToolCalls

**Use for:**
- Batch validation
- Circuit breaker
- Pre-execution guards

**Example:**
```csharp
public Task BeforeToolExecutionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    if (ctx.ToolCalls?.Count > 10)
    {
        ctx.SkipToolExecution = true;
        ctx.Response = new ChatResponse {
            Contents = [new TextContent("Too many tool calls requested")]
        };
    }
    return Task.CompletedTask;
}
```

### AfterIterationAsync

Called after all tools complete for this iteration.

**Available context:**
- All BeforeToolExecution properties
- ToolResults

**Use for:**
- Error tracking
- Result analysis
- State updates

**Example:**
```csharp
public Task AfterIterationAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    var errors = ctx.ToolResults?
        .Where(r => r.IsError)
        .Select(r => r.FunctionName)
        .ToList();

    if (errors?.Count > 0)
    {
        ctx.UpdateState<ErrorState>(s => s with {
            FailedFunctions = s.FailedFunctions.Concat(errors).ToList()
        });
    }

    return Task.CompletedTask;
}
```

### BeforeSequentialFunctionAsync

Called before a specific function executes.

**Available context:**
- All BeforeToolExecution properties
- Function, FunctionCallId, FunctionArguments

**Use for:**
- Permission checking
- Argument validation
- Per-function guards

**Example:**
```csharp
public Task BeforeSequentialFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    if (!_permissionFilter.IsApproved(ctx.Function!))
    {
        ctx.BlockFunctionExecution = true;
        ctx.FunctionResult = "Permission denied";
    }
    return Task.CompletedTask;
}
```

### AfterFunctionAsync

Called after a specific function completes.

**Available context:**
- All BeforeFunction properties
- FunctionResult, FunctionException

**Use for:**
- Result transformation
- Per-function logging
- Telemetry

**Example:**
```csharp
public Task AfterFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    _telemetry.RecordFunction(
        ctx.Function!.Name,
        success: ctx.FunctionException == null
    );
    return Task.CompletedTask;
}
```

## Extension Methods

### Scoping

```csharp
middleware.AsGlobal();                 // Apply to everything (default)
middleware.ForPlugin("PluginName");    // Apply to plugin only
middleware.ForSkill("skill_name");     // Apply to skill only
middleware.ForFunction("func_name");   // Apply to function only
```

### AgentBuilder

```csharp
builder.AddMiddleware(IAgentMiddleware middleware);
builder.AddMiddleware<TMiddleware>() where TMiddleware : IAgentMiddleware;
```

## Execution Order

### Before* Hooks
Execute in **registration order** (first registered = first executed):
```
Middleware1.BeforeIteration
Middleware2.BeforeIteration
Middleware3.BeforeIteration
```

### After* Hooks
Execute in **reverse order** (last registered = first executed):
```
Middleware3.AfterIteration
Middleware2.AfterIteration
Middleware1.AfterIteration
```

### ExecuteLLMCallAsync
Chains in **reverse order** (onion pattern):
```
Middleware3.ExecuteLLMCallAsync(next: () =>
  Middleware2.ExecuteLLMCallAsync(next: () =>
    Middleware1.ExecuteLLMCallAsync(next: () =>
      [Actual LLM Call]
    )
  )
)
```

## State Management

### Reading State

```csharp
var state = context.State.GetState<MyState>();
if (state != null)
{
    // Use state
}
```

### Updating State

```csharp
context.UpdateState<MyState>(current =>
    current with { Property = newValue }
);
```

**Important:**
- State updates are **scheduled**, not immediate
- Applied after middleware chain completes
- `context.State` reflects all pending updates from earlier middlewares
- State must be immutable (use records with `with` expressions)

### Initial State

```csharp
context.UpdateState<MyState>(_ =>
    new MyState { Property = initialValue }
);
```

## Event System

### Emitting Events

```csharp
context.Emit(new CustomEvent(
    AgentName: context.AgentName,
    Data: "something happened"
));
```

### Human-in-the-Loop

```csharp
// Send request
var requestId = Guid.NewGuid().ToString();
context.Emit(new UserApprovalRequest(requestId, context.Function!.Name));

// Wait for response
var approval = await context.WaitForResponseAsync<UserApprovalResponse>(
    requestId,
    timeout: TimeSpan.FromMinutes(5),
    context.CancellationToken
);

if (!approval.Approved)
{
    context.BlockFunctionExecution = true;
    context.FunctionResult = "User denied approval";
}
```

## Built-in Middleware

| Middleware | Purpose | Hook Used |
|------------|---------|-----------|
| `CircuitBreakerMiddleware` | Stop retry loops | BeforeFunction, AfterFunction |
| `ErrorTrackingMiddleware` | Track function failures | AfterFunction |
| `PermissionMiddleware` | Block unapproved calls | BeforeFunction |
| `AutoApprovePermissionMiddleware` | Rule-based approval | BeforeFunction |
| `LoggingMiddleware` | Structured logging | All hooks |
| `DynamicMemoryMiddleware` | User-editable memory | BeforeMessageTurn |
| `StaticMemoryMiddleware` | Read-only docs | BeforeMessageTurn |
| `HistoryReductionMiddleware` | Token optimization | BeforeIteration |
| `SkillInstructionMiddleware` | Skill context injection | BeforeIteration |
| `PIIMiddleware` | Redact sensitive data | BeforeIteration |

## See Also

- [User Guide](./USER_GUIDE.md) - Usage examples and patterns
- [Developer Guide](./DEVELOPER_GUIDE.md) - Writing custom middleware
- [Architecture](../architecture/MIDDLEWARE_ARCHITECTURE.md) - Design details
