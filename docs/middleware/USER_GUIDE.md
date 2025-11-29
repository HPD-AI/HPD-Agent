# Middleware User Guide

Middleware hooks into the agent execution lifecycle to add custom behavior like logging, permissions, memory, and caching.

## Quick Start

```csharp
// 1. Create middleware
var loggingMiddleware = new LoggingMiddleware(logger);
var permissionMiddleware = new PermissionMiddleware(myPermissionFilter);

// 2. Add to agent
var agent = new AgentBuilder()
    .WithModel(model)
    .AddMiddleware(loggingMiddleware)
    .AddMiddleware(permissionMiddleware)
    .Build();

// 3. Run - middleware executes automatically
await agent.RunAsync("Help me analyze this code", thread);
```

## Built-in Middleware

### Memory

**DynamicMemoryMiddleware** - Injects user-editable instructions and notes
```csharp
builder.WithDynamicMemory(new DynamicMemoryOptions
{
    RootDirectory = "./memories",
    AutoLoad = true
});
```

**StaticMemoryMiddleware** - Injects read-only documentation
```csharp
builder.WithStaticMemory(new StaticMemoryOptions
{
    RootDirectory = "./docs"
});
```

### Permissions

**PermissionMiddleware** - Blocks unapproved function calls
```csharp
builder.AddMiddleware(new PermissionMiddleware(
    new InteractivePermissionFilter()  // Prompts user for approval
));
```

**AutoApprovePermissionMiddleware** - Auto-approves based on rules
```csharp
builder.AddMiddleware(new AutoApprovePermissionMiddleware(
    autoApprove: func => func.Name.StartsWith("read_"),
    denyByDefault: false
));
```

### Error Handling

**CircuitBreakerMiddleware** - Stops retry loops
```csharp
builder.AddMiddleware(new CircuitBreakerMiddleware(
    consecutiveErrorThreshold: 3
));
```

**ErrorTrackingMiddleware** - Tracks function failures
```csharp
builder.AddMiddleware(new ErrorTrackingMiddleware());
```

### Optimization

**HistoryReductionMiddleware** - Reduces token usage
```csharp
builder.AddMiddleware(new HistoryReductionMiddleware(
    summarizerModel,
    maxMessagesBeforeReduction: 20
));
```

### Logging

**LoggingMiddleware** - Comprehensive structured logging
```csharp
builder.AddMiddleware(new LoggingMiddleware(
    loggerFactory,
    logLevel: LogLevel.Information
));
```

## Execution Order

Middleware executes in specific order:

```
BeforeMessageTurn (registration order)
  └─► BeforeIteration (registration order)
       └─► ExecuteLLMCallAsync (REVERSE order - onion)
            └─► BeforeToolExecution (registration order)
                 └─► BeforeFunction (registration order)
                      └─► [FUNCTION EXECUTION]
                      └─► AfterFunction (REVERSE order)
                 └─► AfterIteration (REVERSE order)
  └─► AfterMessageTurn (REVERSE order)
```

**Registration order**: First added = first executed
**Reverse order**: Last added = first executed (like stack unwinding)

## Scoping Middleware

Limit middleware to specific plugins, skills, or functions:

```csharp
// Global - applies to everything
middleware.AsGlobal();

// Plugin-scoped - only this plugin
middleware.ForPlugin("FileSystemPlugin");

// Skill-scoped - only this skill
middleware.ForSkill("code_analysis");

// Function-scoped - only this function
middleware.ForFunction("read_file");
```

Example:
```csharp
// Only log file operations
var fileLogger = new LoggingMiddleware(logger)
    .ForPlugin("FileSystemPlugin");

builder.AddMiddleware(fileLogger);
```

## Blocking Execution

Middleware can prevent operations:

```csharp
public class BlockDangerousMiddleware : IAgentMiddleware
{
    public Task BeforeFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        if (context.Function?.Name == "delete_all")
        {
            context.BlockFunctionExecution = true;
            context.FunctionResult = "Permission denied: Too dangerous";
        }
        return Task.CompletedTask;
    }
}
```

Blocking flags:
- `SkipLLMCall` - Skip LLM in BeforeIteration
- `SkipToolExecution` - Skip ALL tools in BeforeToolExecution
- `BlockFunctionExecution` - Skip ONE function in BeforeFunction

## State Management

Read middleware state:
```csharp
var myState = context.State.GetState<MyMiddlewareState>();
```

Update middleware state:
```csharp
context.UpdateState<MyMiddlewareState>(state =>
    state with { LastCallTime = DateTimeOffset.UtcNow }
);
```

State updates are applied **after** the middleware chain completes.

## Events

Emit events to observers:
```csharp
context.Emit(new CustomEvent("Something happened"));
```

Wait for responses (human-in-the-loop):
```csharp
var response = await context.WaitForResponseAsync<UserApprovalResponse>(
    requestId,
    timeout: TimeSpan.FromMinutes(5),
    ct
);
```

## Common Patterns

### Permission Check
```csharp
public Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    if (!_filter.IsApproved(ctx.Function!))
    {
        ctx.BlockFunctionExecution = true;
        ctx.FunctionResult = "Permission denied";
    }
    return Task.CompletedTask;
}
```

### Inject Context
```csharp
public Task BeforeIterationAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    var systemMsg = new ChatMessage(ChatRole.System, "Current time: " + DateTime.Now);
    ctx.Messages = ctx.Messages.Prepend(systemMsg);
    return Task.CompletedTask;
}
```

### Track Metrics
```csharp
public Task AfterFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    _metrics.RecordFunctionCall(
        ctx.Function!.Name,
        ctx.FunctionException == null
    );
    return Task.CompletedTask;
}
```

### Retry Logic (Advanced)
```csharp
public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
    AgentMiddlewareContext ctx,
    Func<IAsyncEnumerable<ChatResponseUpdate>> next,
    [EnumeratorCancellation] CancellationToken ct)
{
    for (int attempt = 0; attempt < 3; attempt++)
    {
        Exception? error = null;

        await foreach (var update in next().WithCancellation(ct))
        {
            try { yield return update; }
            catch (RateLimitException ex) { error = ex; break; }
        }

        if (error == null) break;
        if (attempt < 2) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        else throw error;
    }
}
```

## Best Practices

1. **Keep middleware focused** - One responsibility per middleware
2. **Use scoping** - Limit middleware to relevant functions
3. **Avoid side effects in Before*** - State updates are scheduled, not immediate
4. **Handle cancellation** - Check `ct.IsCancellationRequested` in long operations
5. **Use ExecuteLLMCallAsync sparingly** - Most use cases work with simpler hooks

## See Also

- [API Reference](./API_REFERENCE.md) - Complete API documentation
- [Developer Guide](./DEVELOPER_GUIDE.md) - Writing custom middleware
- [Middleware Architecture](../architecture/MIDDLEWARE_ARCHITECTURE.md) - Design details
