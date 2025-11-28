# Filter Events - Quick Reference Card

## One-Way Events (Fire & Forget)

```csharp
// Progress
context.Emit(new FilterProgressEvent("FilterName", "message", percentComplete));

// Error
context.Emit(new FilterErrorEvent("FilterName", "error message", exception));

// Custom (define your own event type)
public record MyCustomEvent(string FilterName, string Data) : AgentEvent, IFilterEvent;
context.Emit(new MyCustomEvent("FilterName", "custom data"));
```

## Bidirectional Events (Request/Response)

```csharp
// 1. Generate ID
var requestId = Guid.NewGuid().ToString();

// 2. Emit request
context.Emit(new PermissionRequestEvent(requestId, functionName, description, callId, args));

// 3. Wait for response
try
{
    var response = await context.WaitForResponseAsync<PermissionResponseEvent>(
        requestId,
        timeout: TimeSpan.FromMinutes(5));

    if (response.Approved) { /* continue */ }
    else { context.IsTerminated = true; }
}
catch (TimeoutException) { /* handle timeout */ }
catch (OperationCanceledException) { /* handle cancellation */ }
```

## Event Handling

```csharp
await foreach (var evt in agent.RunStreamingAsync(thread, options))
{
    switch (evt)
    {
        case PermissionRequestEvent req:
            _ = Task.Run(() => {
                var approved = GetUserInput();
                agent.SendFilterResponse(req.PermissionId,
                    new PermissionResponseEvent(req.PermissionId, approved));
            });
            break;

        case FilterProgressEvent progress:
            Console.WriteLine($"[{progress.FilterName}] {progress.Message}");
            break;

        case TextDeltaEvent text:
            Console.Write(text.Text);
            break;
    }
}
```

## Custom Event Types

```csharp
// Define
public record MyCustomEvent(string Data) : AgentEvent;

// Emit
context.Emit(new MyCustomEvent("value"));

// Handle
case MyCustomEvent custom:
    ProcessCustom(custom.Data);
    break;
```

## Key APIs

| API | Purpose |
|-----|---------|
| `context.Emit(event)` | Emit one-way event |
| `context.EmitAsync(event, ct)` | Emit async (future-proof) |
| `context.WaitForResponseAsync<T>(id, timeout, ct)` | Wait for response |
| `agent.SendFilterResponse(id, response)` | Send response (handlers) |

## Event Types

| Event | Use Case | Bidirectional? |
|-------|----------|----------------|
| `FilterProgressEvent` | Progress tracking | No |
| `FilterErrorEvent` | Error reporting | No |
| Custom events (implement `IFilterEvent`) | User-defined data | No |
| `PermissionRequestEvent` | Permission request | Yes (request) |
| `PermissionResponseEvent` | Permission response | Yes (response) |
| `ContinuationRequestEvent` | Max iterations extension | Yes (request) |
| `ContinuationResponseEvent` | Continuation approval | Yes (response) |

## Error Handling

```csharp
public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
{
    try
    {
        context.Emit(new FilterProgressEvent("Filter", "Starting", 0));
        await next(context);
        context.Emit(new FilterProgressEvent("Filter", "Done", 100));
    }
    catch (Exception ex)
    {
        context.Emit(new FilterErrorEvent("Filter", ex.Message, ex));
        throw; // Re-throw to maintain error propagation
    }
}
```

## Best Practices

✅ **DO**:
- Use unique IDs (`Guid.NewGuid().ToString()`)
- Handle timeouts and cancellations
- Emit observability events (start/end)
- Emit result events (approved/denied)
- Re-throw exceptions after logging

❌ **DON'T**:
- Forget to handle `TimeoutException`
- Use hardcoded request IDs
- Swallow exceptions without emitting error events
- Block the main thread in handlers (use `Task.Run`)

## Performance

- Memory: ~16 bytes per function call
- CPU: ~50ns per event
- No Task.Run overhead
- Thread-safe concurrent emission

## See Also

- [FILTER_EVENTS_USAGE.md](HPD-Agent/Filters/FILTER_EVENTS_USAGE.md) - Full guide
- [ExampleFilters.cs](HPD-Agent/Filters/ExampleFilters.cs) - Example implementations
- [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) - Implementation details
