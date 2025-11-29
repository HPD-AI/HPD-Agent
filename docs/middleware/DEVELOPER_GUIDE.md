# Middleware Developer Guide

Guide for writing custom middleware in HPD-Agent.

## Minimal Middleware

```csharp
public class SimpleLoggingMiddleware : IAgentMiddleware
{
    private readonly ILogger _logger;

    public SimpleLoggingMiddleware(ILogger logger)
    {
        _logger = logger;
    }

    public Task BeforeFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        _logger.LogInformation("Calling function: {Name}", context.Function?.Name);
        return Task.CompletedTask;
    }

    public Task AfterFunctionAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        _logger.LogInformation("Result: {Result}",
            context.FunctionException?.Message ?? context.FunctionResult?.ToString());
        return Task.CompletedTask;
    }
}
```

**Usage:**
```csharp
builder.AddMiddleware(new SimpleLoggingMiddleware(logger));
```

## Choosing the Right Hook

| Goal | Hook |
|------|------|
| Inject context/docs ONCE | BeforeMessageTurn |
| Modify messages per LLM call | BeforeIteration |
| Cache/retry LLM calls | ExecuteLLMCallAsync |
| Block ALL tools | BeforeToolExecution |
| Check permissions | BeforeFunction |
| Transform results | AfterFunction |
| Track errors per iteration | AfterIteration |
| Extract memories | AfterMessageTurn |

## Patterns

### 1. Permission Middleware

```csharp
public class CustomPermissionMiddleware : IAgentMiddleware
{
    private readonly IPermissionService _service;

    public async Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        var allowed = await _service.CheckPermissionAsync(
            ctx.Function!.Name,
            ctx.ConversationId,
            ct
        );

        if (!allowed)
        {
            ctx.BlockFunctionExecution = true;
            ctx.FunctionResult = "Permission denied by policy";
        }
    }
}
```

### 2. Context Injection Middleware

```csharp
public class EnvironmentContextMiddleware : IAgentMiddleware
{
    public Task BeforeMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        var contextMsg = new ChatMessage(ChatRole.System,
            $"Current environment: {Environment.GetEnvironmentVariable("STAGE")}\n" +
            $"Current time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");

        ctx.ConversationHistory = ctx.ConversationHistory?.Prepend(contextMsg)
            ?? new[] { contextMsg };

        return Task.CompletedTask;
    }
}
```

### 3. Error Tracking Middleware

```csharp
public class ErrorBudgetMiddleware : IAgentMiddleware
{
    private record ErrorBudgetState(int ErrorCount, int Limit);

    public Task AfterFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        if (ctx.FunctionException != null)
        {
            ctx.UpdateState<ErrorBudgetState>(state =>
            {
                var newCount = state.ErrorCount + 1;
                return state with { ErrorCount = newCount };
            });
        }

        return Task.CompletedTask;
    }

    public Task BeforeIterationAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        var budget = ctx.State.GetState<ErrorBudgetState>();
        if (budget != null && budget.ErrorCount >= budget.Limit)
        {
            ctx.SkipLLMCall = true;
            ctx.Response = new ChatResponse {
                Contents = [new TextContent(
                    $"Error budget exhausted ({budget.ErrorCount}/{budget.Limit})"
                )]
            };
        }

        return Task.CompletedTask;
    }
}
```

**Initialize state:**
```csharp
public Task BeforeMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    ctx.UpdateState<ErrorBudgetState>(_ => new ErrorBudgetState(0, 5));
    return Task.CompletedTask;
}
```

### 4. Caching Middleware (Advanced)

```csharp
public class LLMCacheMiddleware : IAgentMiddleware
{
    private readonly IDistributedCache _cache;

    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(
        AgentMiddlewareContext ctx,
        Func<IAsyncEnumerable<ChatResponseUpdate>> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var cacheKey = ComputeCacheKey(ctx.Messages);
        var cached = await _cache.GetStringAsync(cacheKey, ct);

        if (cached != null)
        {
            var response = JsonSerializer.Deserialize<ChatResponseUpdate>(cached);
            yield return response!;
            yield break;
        }

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in next().WithCancellation(ct))
        {
            updates.Add(update);
            yield return update;
        }

        if (updates.Count > 0)
        {
            var json = JsonSerializer.Serialize(updates[^1]);
            await _cache.SetStringAsync(cacheKey, json, ct);
        }
    }

    private string ComputeCacheKey(IEnumerable<ChatMessage>? messages)
    {
        var json = JsonSerializer.Serialize(messages);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
```

### 5. Human-in-the-Loop Middleware

```csharp
public class ApprovalMiddleware : IAgentMiddleware
{
    public async Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        if (ctx.Function!.Metadata.Tags?.Contains("requires_approval") != true)
            return;

        var requestId = Guid.NewGuid().ToString();

        // Emit approval request to observers
        ctx.Emit(new FunctionApprovalRequest(
            RequestId: requestId,
            FunctionName: ctx.Function.Name,
            Arguments: ctx.FunctionArguments
        ));

        try
        {
            // Wait for user response
            var approval = await ctx.WaitForResponseAsync<FunctionApprovalResponse>(
                requestId,
                timeout: TimeSpan.FromMinutes(5),
                ct
            );

            if (!approval.Approved)
            {
                ctx.BlockFunctionExecution = true;
                ctx.FunctionResult = $"User denied: {approval.Reason}";
            }
        }
        catch (TimeoutException)
        {
            ctx.BlockFunctionExecution = true;
            ctx.FunctionResult = "Approval timeout - request denied";
        }
    }
}
```

## State Management

### State Must Be Immutable

Use records with `with` expressions:

```csharp
// ✅ GOOD - Immutable record
public record MyState(int Count, DateTimeOffset LastUpdate);

ctx.UpdateState<MyState>(state =>
    state with { Count = state.Count + 1, LastUpdate = DateTimeOffset.UtcNow }
);

// ❌ BAD - Mutable class
public class MyState { public int Count { get; set; } }

ctx.UpdateState<MyState>(state => {
    state.Count++;  // DON'T DO THIS - side effects!
    return state;
});
```

### State Lifecycle

```csharp
public class StatefulMiddleware : IAgentMiddleware
{
    private record State(int CallCount);

    // Initialize state once
    public Task BeforeMessageTurnAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        ctx.UpdateState<State>(_ => new State(0));
        return Task.CompletedTask;
    }

    // Update state during execution
    public Task AfterFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        ctx.UpdateState<State>(s => s with { CallCount = s.CallCount + 1 });
        return Task.CompletedTask;
    }

    // Read state in later hooks
    public Task BeforeIterationAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        var state = ctx.State.GetState<State>();
        if (state?.CallCount > 10)
        {
            // Do something
        }
        return Task.CompletedTask;
    }
}
```

### Shared State Between Middlewares

```csharp
// Define shared state
public record SharedErrorState(List<string> Errors);

// Middleware A adds errors
public class ErrorTracker : IAgentMiddleware
{
    public Task AfterFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        if (ctx.FunctionException != null)
        {
            ctx.UpdateState<SharedErrorState>(state =>
                state with {
                    Errors = state.Errors.Append(ctx.Function!.Name).ToList()
                }
            );
        }
        return Task.CompletedTask;
    }
}

// Middleware B reads errors
public class ErrorLimitEnforcer : IAgentMiddleware
{
    public Task BeforeToolExecutionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
    {
        var errors = ctx.State.GetState<SharedErrorState>();
        if (errors?.Errors.Count >= 5)
        {
            ctx.SkipToolExecution = true;
        }
        return Task.CompletedTask;
    }
}
```

## Scoping

Limit middleware to specific contexts:

```csharp
// Apply to everything
var global = new MyMiddleware().AsGlobal();

// Apply to plugin only
var pluginScoped = new MyMiddleware().ForPlugin("FileSystem");

// Apply to skill only
var skillScoped = new MyMiddleware().ForSkill("code_analysis");

// Apply to function only
var funcScoped = new MyMiddleware().ForFunction("read_file");

builder
    .AddMiddleware(global)
    .AddMiddleware(pluginScoped)
    .AddMiddleware(skillScoped)
    .AddMiddleware(funcScoped);
```

**Check scope in middleware:**
```csharp
public Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    // Middleware only executes if ShouldExecute returns true
    // based on its scope configuration
    return Task.CompletedTask;
}
```

## Testing

### Unit Test Middleware

```csharp
[Fact]
public async Task Middleware_Blocks_Dangerous_Functions()
{
    var middleware = new SafetyMiddleware();

    var context = new AgentMiddlewareContext
    {
        AgentName = "test",
        CancellationToken = CancellationToken.None,
        Function = new AIFunction("delete_all", "Deletes everything"),
        FunctionCallId = "123"
    };

    await middleware.BeforeFunctionAsync(context, CancellationToken.None);

    Assert.True(context.BlockFunctionExecution);
    Assert.Equal("Blocked for safety", context.FunctionResult);
}
```

### Integration Test with Agent

```csharp
[Fact]
public async Task Agent_With_Permission_Middleware_Blocks_Calls()
{
    var deniedFilter = new TestPermissionFilter(approved: false);
    var middleware = new PermissionMiddleware(deniedFilter);

    var agent = new AgentBuilder()
        .WithModel(mockModel)
        .WithTools(dangerousTool)
        .AddMiddleware(middleware)
        .Build();

    var events = await agent.RunAsync("Delete everything", thread)
        .ToListAsync();

    // Verify tool was blocked
    var toolResults = events.OfType<ToolResultEvent>();
    Assert.Contains(toolResults, e => e.Result.Contains("Permission denied"));
}
```

## Performance Tips

1. **Avoid expensive operations in Before*** - These run on critical path
2. **Use scoping** - Don't execute middleware when not needed
3. **Cache lookups** - Store frequently accessed data in state
4. **Async all the way** - Don't use `.Result` or `.Wait()`
5. **Use ExecuteLLMCallAsync sparingly** - Only when you need streaming control

## Common Mistakes

### ❌ Mutating Context Directly
```csharp
// BAD
ctx.Messages.Add(new ChatMessage(...));  // Mutates original list

// GOOD
ctx.Messages = ctx.Messages.Append(new ChatMessage(...));
```

### ❌ Blocking Async Code
```csharp
// BAD
_cache.GetAsync(key).Result;

// GOOD
await _cache.GetAsync(key, ct);
```

### ❌ Side Effects in State Updates
```csharp
// BAD
ctx.UpdateState<State>(s => {
    _logger.Log("Updating");  // Side effect!
    return s with { Count = s.Count + 1 };
});

// GOOD
var state = ctx.State.GetState<State>();
_logger.Log("Current count: {Count}", state?.Count);
ctx.UpdateState<State>(s => s with { Count = s.Count + 1 });
```

### ❌ Forgetting Cancellation Token
```csharp
// BAD
public Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    return _service.CheckAsync();  // Doesn't pass ct
}

// GOOD
public Task BeforeFunctionAsync(AgentMiddlewareContext ctx, CancellationToken ct)
{
    return _service.CheckAsync(ct);
}
```

### ❌ Not Yielding in ExecuteLLMCallAsync
```csharp
// BAD - Consumes stream without yielding
public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(...)
{
    await foreach (var update in next()) { }  // Stream consumed!
    yield return new ChatResponseUpdate(...);
}

// GOOD
public async IAsyncEnumerable<ChatResponseUpdate> ExecuteLLMCallAsync(...)
{
    await foreach (var update in next().WithCancellation(ct))
    {
        yield return update;  // Preserve streaming
    }
}
```

## See Also

- [User Guide](./USER_GUIDE.md) - Usage examples
- [API Reference](./API_REFERENCE.md) - Complete API
- [Architecture](../architecture/MIDDLEWARE_ARCHITECTURE.md) - Design details
