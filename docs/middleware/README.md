# Middleware Documentation

Comprehensive documentation for the HPD-Agent middleware system.

## Overview

Middleware hooks into the agent execution lifecycle to add custom behavior:
- **Permissions** - Block unauthorized function calls
- **Memory** - Inject context and documentation
- **Logging** - Track execution and debug issues
- **Error Handling** - Circuit breakers and retry logic
- **Optimization** - Caching and token reduction
- **Custom Logic** - Any cross-cutting concern

## Documentation

| Guide | Description | Audience |
|-------|-------------|----------|
| **[User Guide](./USER_GUIDE.md)** | How to use built-in middleware and common patterns | End users |
| **[API Reference](./API_REFERENCE.md)** | Complete API documentation | Developers |
| **[Developer Guide](./DEVELOPER_GUIDE.md)** | Writing custom middleware | Middleware authors |

## Quick Start

```csharp
using HPD.Agent;

// Add built-in middleware
var agent = new AgentBuilder()
    .WithModel(model)
    .WithTools(tools)
    // Memory
    .WithDynamicMemory(new DynamicMemoryOptions { RootDirectory = "./memories" })
    // Permissions
    .AddMiddleware(new PermissionMiddleware(new InteractivePermissionFilter()))
    // Error handling
    .AddMiddleware(new CircuitBreakerMiddleware(consecutiveErrorThreshold: 3))
    // Logging
    .AddMiddleware(new LoggingMiddleware(loggerFactory))
    .Build();

await agent.RunAsync("Your task here", thread);
```

## Lifecycle Hooks

```
BeforeMessageTurn (once per user message)
  └─► BeforeIteration (once per LLM call)
       └─► ExecuteLLMCallAsync (REVERSE order - onion)
            └─► BeforeToolExecution (after LLM, before tools)
                 └─► BeforeFunction (per tool call)
                      └─► [FUNCTION EXECUTES]
                 └─► AfterFunction (REVERSE order)
            └─► AfterIteration (REVERSE order)
  └─► AfterMessageTurn (REVERSE order)
```

## Built-in Middleware

### Memory
- `DynamicMemoryMiddleware` - User-editable instructions and notes
- `StaticMemoryMiddleware` - Read-only documentation
- `SkillInstructionMiddleware` - Skill-specific context

### Permissions
- `PermissionMiddleware` - Human-in-the-loop approval
- `AutoApprovePermissionMiddleware` - Rule-based auto-approval

### Error Handling
- `CircuitBreakerMiddleware` - Stop retry loops
- `ErrorTrackingMiddleware` - Track function failures
- `TotalErrorThresholdMiddleware` - Error budget enforcement

### Optimization
- `HistoryReductionMiddleware` - Summarize old messages
- Caching via `ExecuteLLMCallAsync` (custom)

### Logging & Observability
- `LoggingMiddleware` - Structured logging
- Use with `TelemetryEventObserver`, `MermaidVisualizationObserver`

## Key Concepts

### Execution Order

**Before\* hooks**: Registration order (first added = first executed)
```csharp
builder
    .AddMiddleware(new A())  // Executes first
    .AddMiddleware(new B())  // Executes second
    .AddMiddleware(new C()); // Executes third
```

**After\* hooks**: Reverse order (last added = first executed)
```csharp
// AfterIteration order: C → B → A
```

**ExecuteLLMCallAsync**: Reverse order (onion pattern)
```csharp
// Execution: C wraps B wraps A wraps LLM
```

### Control Flags

Block operations with flags:
```csharp
ctx.SkipLLMCall = true;              // Skip LLM (BeforeIteration)
ctx.SkipToolExecution = true;        // Skip ALL tools (BeforeToolExecution)
ctx.BlockFunctionExecution = true;   // Skip ONE function (BeforeFunction)
```

### State Management

Immutable state with scheduled updates:
```csharp
// Read
var state = ctx.State.GetState<MyState>();

// Update (applied after middleware chain)
ctx.UpdateState<MyState>(s => s with { Count = s.Count + 1 });
```

### Scoping

Limit middleware to specific contexts:
```csharp
middleware.AsGlobal();                // Everything (default)
middleware.ForPlugin("FileSystem");   // One plugin
middleware.ForSkill("code_analysis"); // One skill
middleware.ForFunction("read_file");  // One function
```

## Examples

### Permission Check
```csharp
public Task BeforeSequentialFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    if (!IsApproved(ctx.Function!))
    {
        ctx.BlockFunctionExecution = true;
        ctx.FunctionResult = "Permission denied";
    }
    return Task.CompletedTask;
}
```

### Context Injection
```csharp
public Task BeforeMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    var docs = LoadDocs(ctx.ConversationId);
    var msg = new ChatMessage(ChatRole.System, docs);
    ctx.ConversationHistory = ctx.ConversationHistory?.Prepend(msg);
    return Task.CompletedTask;
}
```

### LLM Caching
```csharp
public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
    AgentMiddlewareContext ctx,
    Func<IAsyncEnumerable<ChatResponseUpdate>> next,
    [EnumeratorCancellation] CancellationToken ct)
{
    var key = ComputeKey(ctx.Messages);

    if (_cache.TryGet(key, out var cached))
    {
        yield return cached;
        yield break;
    }

    await foreach (var update in next().WithCancellation(ct))
    {
        yield return update;
    }
}
```

## Architecture

The middleware system is built on:
- **IAgentMiddleware** - Unified interface with 8 lifecycle hooks
- **AgentMiddlewareContext** - Rich context with state management
- **AgentMiddlewarePipeline** - Orchestrates execution order
- **ScopedMiddlewareSystem** - Filters by plugin/skill/function

See [Architecture Documentation](../architecture/MIDDLEWARE_ARCHITECTURE.md) for details.

## Migration

### From Old Filter System

Old iteration filters → New middleware:
```csharp
// OLD
builder.AddIterationFilter(new MyFilter());

// NEW
builder.AddMiddleware(new MyMiddleware());
```

### From IChatClient Middleware

Old chat client wrapping still supported for compatibility:
```csharp
// Still works (for WithTelemetry, WithLogging, WithCaching)
builder.UseChatClientMiddleware((client, services) =>
    new CustomChatClient(client));

// But prefer new middleware for full control
builder.AddMiddleware(new CustomMiddleware());
```

## See Also

- [Architecture Overview](../architecture/ARCHITECTURE_OVERVIEW.md)
- [Permissions System](../permissions/PERMISSION_SYSTEM_API.md)
- [Memory System](../memory/MEMORY_SYSTEM.md)
- [Observability](../observability/README.md)
