# Filter Event System Usage Guide

This guide shows how to use the new bidirectional filter event system.

## Overview

The filter event system allows filters to:
- **Emit one-way events** (progress, errors, custom data)
- **Request/wait for responses** (permissions, approvals, user input)
- **Work with any protocol** (AGUI, Console, Web, etc.)

All events flow through a **shared channel** and are **streamed in real-time** to handlers.

---

## Quick Start

### 1. Create a Simple Progress Filter

```csharp
public class MyProgressFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        // Emit start event
        context.Emit(new InternalFilterProgressEvent(
            "MyProgressFilter",
            $"Starting {context.ToolCallRequest.FunctionName}",
            PercentComplete: 0));

        await next(context);

        // Emit completion event
        context.Emit(new InternalFilterProgressEvent(
            "MyProgressFilter",
            "Done!",
            PercentComplete: 100));
    }
}
```

### 2. Add Filter to Agent

```csharp
var agent = new AgentBuilder()
    .WithProvider("openai", "gpt-4", apiKey)
    .WithPlugin<FileSystemPlugin>()
    .WithFilter(new MyProgressFilter())  // Add your filter!
    .Build();
```

### 3. Handle Events

```csharp
await foreach (var evt in agent.RunStreamingAsync(thread, options))
{
    switch (evt)
    {
        case InternalFilterProgressEvent progress:
            Console.WriteLine($"[{progress.FilterName}] {progress.Message}");
            break;

        case InternalTextDeltaEvent text:
            Console.Write(text.Text);
            break;
    }
}
```

That's it! Events automatically flow from filters to handlers.

---

## Event Types

### One-Way Events (No Response Needed)

#### Progress Events
```csharp
context.Emit(new InternalFilterProgressEvent(
    "MyFilter",
    "Processing...",
    PercentComplete: 50));
```

#### Error Events
```csharp
context.Emit(new InternalFilterErrorEvent(
    "MyFilter",
    "Something went wrong",
    exception));
```

#### Custom Events
```csharp
context.Emit(new InternalCustomFilterEvent(
    "MyFilter",
    "MyEventType",
    new Dictionary<string, object?>
    {
        ["CustomData"] = "value",
        ["Count"] = 42
    }));
```

### Bidirectional Events (Request/Response)

#### Permission Requests
```csharp
var permissionId = Guid.NewGuid().ToString();

// 1. Emit request
context.Emit(new InternalPermissionRequestEvent(
    permissionId,
    functionName: "DeleteFile",
    description: "Delete important file",
    callId: "...",
    arguments: context.ToolCallRequest.Arguments));

// 2. Wait for response (blocks filter, but events still flow!)
var response = await context.WaitForResponseAsync<InternalPermissionResponseEvent>(
    permissionId,
    timeout: TimeSpan.FromMinutes(5));

// 3. Handle response
if (response.Approved)
{
    await next(context);  // Continue
}
else
{
    context.IsTerminated = true;  // Stop
}
```

---

## Complete Example: Permission Filter

### Filter Code

```csharp
public class SimplePermissionFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var permissionId = Guid.NewGuid().ToString();

        // Emit request
        context.Emit(new InternalPermissionRequestEvent(
            permissionId,
            context.ToolCallRequest.FunctionName,
            "Permission required",
            callId: "...",
            arguments: context.ToolCallRequest.Arguments));

        // Wait for user response
        try
        {
            var response = await context.WaitForResponseAsync<InternalPermissionResponseEvent>(
                permissionId);

            if (response.Approved)
            {
                context.Emit(new InternalPermissionApprovedEvent(permissionId));
                await next(context);
            }
            else
            {
                context.Emit(new InternalPermissionDeniedEvent(permissionId, "User denied"));
                context.Result = "Permission denied";
                context.IsTerminated = true;
            }
        }
        catch (TimeoutException)
        {
            context.Result = "Permission request timed out";
            context.IsTerminated = true;
        }
    }
}
```

### Handler Code (Console)

```csharp
public async Task RunWithPermissionsAsync(Agent agent)
{
    await foreach (var evt in agent.RunStreamingAsync(thread, options))
    {
        switch (evt)
        {
            case InternalPermissionRequestEvent permReq:
                // Prompt user in background thread
                _ = Task.Run(async () =>
                {
                    Console.WriteLine($"\nPermission required: {permReq.FunctionName}");
                    Console.Write("Allow? (y/n): ");
                    var input = Console.ReadLine();
                    var approved = input?.ToLower() == "y";

                    // Send response back to waiting filter
                    agent.SendFilterResponse(permReq.PermissionId,
                        new InternalPermissionResponseEvent(
                            permReq.PermissionId,
                            approved,
                            approved ? null : "User denied",
                            PermissionChoice.AllowOnce));
                });
                break;

            case InternalTextDeltaEvent text:
                Console.Write(text.Text);
                break;
        }
    }
}
```

---

## Custom Event Types

You can create your own event types:

```csharp
// 1. Define custom event
public record DatabaseQueryStartEvent(
    string QueryId,
    string Query,
    TimeSpan EstimatedDuration) : InternalAgentEvent;

// 2. Emit in filter
public class DatabaseFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var queryId = Guid.NewGuid().ToString();

        context.Emit(new DatabaseQueryStartEvent(
            queryId,
            query: "SELECT * FROM users",
            EstimatedDuration: TimeSpan.FromSeconds(2)));

        await next(context);
    }
}

// 3. Handle in event loop
await foreach (var evt in agent.RunStreamingAsync(...))
{
    switch (evt)
    {
        case DatabaseQueryStartEvent dbEvt:
            Console.WriteLine($"Query starting: {dbEvt.Query}");
            break;
    }
}
```

---

## Key Features

### ✅ Real-Time Streaming
Events are visible to handlers **WHILE** filters are executing (not after):

```
Timeline:
T0: Filter emits permission request → Shared channel
T1: Background drainer reads → Event queue
T2: Main loop yields → Handler receives event
T3: Filter still blocked waiting ← HANDLER CAN RESPOND!
T4: Handler sends response
T5: Filter receives response and unblocks
```

### ✅ Zero Dependencies
Filters don't need dependency injection:

```csharp
// No constructor parameters needed!
public class MyFilter : IAiFunctionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, ...)
    {
        context.Emit(event);  // Just works!
    }
}
```

### ✅ Protocol Agnostic
One filter works with **all** protocols:

```csharp
// Same filter used by:
// - AGUI (web UI)
// - Console app
// - Discord bot
// - Web API
// - Any future protocol!
```

### ✅ Thread-Safe
Multiple filters can emit events concurrently:

```csharp
// Filter pipeline:
Filter1.Emit(Event1)
  → calls Filter2
     → Filter2.Emit(Event2)  // Concurrent!

// Both events flow through shared channel safely
```

---

## Best Practices

### 1. Always Emit Observability Events
```csharp
context.Emit(new InternalFilterProgressEvent("MyFilter", "Starting"));
await next(context);
context.Emit(new InternalFilterProgressEvent("MyFilter", "Done"));
```

### 2. Handle Timeouts
```csharp
try
{
    var response = await context.WaitForResponseAsync<T>(id, TimeSpan.FromMinutes(5));
}
catch (TimeoutException)
{
    context.Result = "Timeout";
    context.IsTerminated = true;
}
```

### 3. Use Unique Request IDs
```csharp
var requestId = Guid.NewGuid().ToString();  // Always unique!
```

### 4. Emit Result Events
```csharp
if (response.Approved)
{
    context.Emit(new InternalPermissionApprovedEvent(permissionId));
}
else
{
    context.Emit(new InternalPermissionDeniedEvent(permissionId, reason));
}
```

---

## Performance

- **Memory**: ~16 bytes per function call (vs ~400 bytes with Task.Run)
- **CPU**: ~50ns per event (negligible vs 500ms-2s LLM calls)
- **Concurrency**: Background drainer handles all events efficiently

---

## Migration Guide

### Old Way (Custom Interfaces)
```csharp
public class OldFilter
{
    private readonly IPermissionEventEmitter _emitter;

    public OldFilter(IPermissionEventEmitter emitter)  // Requires DI!
    {
        _emitter = emitter;
    }
}
```

### New Way (Standardized)
```csharp
public class NewFilter : IAiFunctionFilter
{
    // No dependencies!

    public async Task InvokeAsync(AiFunctionContext context, ...)
    {
        context.Emit(new InternalPermissionRequestEvent(...));
        var response = await context.WaitForResponseAsync<T>(...);
    }
}
```

---

## Examples

See `ExampleFilters.cs` for:
- `ProgressLoggingFilter` - Simple one-way events
- `CostTrackingFilter` - Custom event types
- `SimplePermissionFilter` - Bidirectional request/response

Happy filtering! 🎉
