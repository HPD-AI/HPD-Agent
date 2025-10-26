# Filter Event System Usage Guide

This guide shows how to use the new bidirectional filter event system.

## Table of Contents

- [Overview](#overview)
- [Event Interfaces](#event-interfaces) - `IFilterEvent` and `IPermissionEvent`
- [Quick Start](#quick-start) - Get up and running in 5 minutes
- [Handling Events at Different Levels](#handling-events-at-different-levels) - Infrastructure, Domain, Specific
- [When to Create Custom Permission Filters](#when-to-create-custom-permission-filters) - **Start here if you need advanced permissions**
- [Custom Event Types](#custom-event-types) - Create your own filter and permission events
- [Key Features](#key-features) - Real-time streaming, protocol agnostic, thread-safe
- [Best Practices](#best-practices)
- [Examples](#examples)

## Overview

The filter event system allows filters to:
- **Emit one-way events** (progress, errors, custom data)
- **Request/wait for responses** (permissions, approvals, user input)
- **Work with any protocol** (AGUI, Console, Web, etc.)

All events flow through a **shared channel** and are **streamed in real-time** to handlers.

### Quick Decision Guide

**Use the default permission filter if:**
- ✅ You need simple approve/deny decisions
- ✅ You're building a console app or simple UI
- ✅ You don't need to modify function arguments
- ✅ Binary permissions are enough

**Create a custom permission filter if:**
- 🔧 You need richer decision states (approved with changes, deferred, requires preview)
- 🔧 You need to modify function arguments before execution
- 🔧 You have multi-stage approval workflows
- 🔧 You need enterprise metadata (cost, risk, compliance)
- 🔧 You want risk-based or cost-based auto-decisions

See [When to Create Custom Permission Filters](#when-to-create-custom-permission-filters) for details.

## Event Interfaces

The system provides two marker interfaces for categorizing filter events:

### `IBidirectionalEvent`
Marker interface for all events supporting bidirectional communication. Allows applications to handle events uniformly for monitoring, logging, and UI routing.

```csharp
public interface IBidirectionalEvent
{
    string SourceName { get; }
}
```

### `IPermissionEvent : IBidirectionalEvent`
Marker interface for permission-related events. A specialized subset of bidirectional events that require user interaction and approval workflows.

```csharp
public interface IPermissionEvent : IBidirectionalEvent
{
    string PermissionId { get; }
}
```

**Event Hierarchy:**
```
InternalAgentEvent (base)
    ↓
IBidirectionalEvent (all bidirectional events)
│   - SourceName: string
│
├── InternalFilterProgressEvent
├── InternalFilterErrorEvent
├── Custom events (user-defined, implement IBidirectionalEvent)
│
└── IPermissionEvent (permission-specific)
    │   - SourceName: string (inherited)
    │   - PermissionId: string
    │
    ├── InternalPermissionRequestEvent
    ├── InternalPermissionResponseEvent
    ├── InternalPermissionApprovedEvent
    ├── InternalPermissionDeniedEvent
    ├── InternalContinuationRequestEvent
    └── InternalContinuationResponseEvent
```

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
    // Option A: Handle specific event types
    switch (evt)
    {
        case InternalFilterProgressEvent progress:
            Console.WriteLine($"[{progress.SourceName}] {progress.Message}");
            break;

        case InternalTextDeltaEvent text:
            Console.Write(text.Text);
            break;
    }

    // Option B: Handle all filter events uniformly
    if (evt is IBidirectionalEvent bidirEvt)
    {
        _filterMonitor.Track(bidirEvt.SourceName);
    }

    // Option C: Handle permission events uniformly
    if (evt is IPermissionEvent permEvt)
    {
        await _permissionHandler.HandleAsync(permEvt);
    }
}
```

That's it! Events automatically flow from filters to handlers.

## Handling Events at Different Levels

The event system supports three levels of handling:

### Level 1: Infrastructure - All Filter Events (`IFilterEvent`)

Handle all filter events uniformly for monitoring, logging, or UI routing:

```csharp
await foreach (var evt in agent.RunStreamingAsync(...))
{
    if (evt is IFilterEvent filterEvt)
    {
        // Works for ALL filter events (progress, errors, permissions, custom)
        await _filterMonitor.TrackAsync(filterEvt.FilterName);
        await _filterLogger.LogAsync($"[{filterEvt.FilterName}] {evt.GetType().Name}");
    }
}
```

### Level 2: Domain - Permission Events (`IPermissionEvent`)

Handle all permission events for approval workflows:

```csharp
await foreach (var evt in agent.RunStreamingAsync(...))
{
    if (evt is IPermissionEvent permEvt)
    {
        // Works for ALL permission events (requests, responses, approvals, denials)
        await _auditLog.RecordAsync(permEvt.PermissionId, permEvt.FilterName, evt);
        await _permissionPipeline.ProcessAsync(permEvt);
    }
}
```

### Level 3: Specific - Individual Event Types

Handle specific event types for exact behavior:

```csharp
await foreach (var evt in agent.RunStreamingAsync(...))
{
    switch (evt)
    {
        case InternalPermissionRequestEvent req:
            // Specific handling for permission requests
            await PromptUserAsync(req);
            break;

        case InternalFilterProgressEvent progress:
            // Specific handling for progress
            UpdateProgressBar(progress.PercentComplete);
            break;
    }
}
```

### Combining All Three Levels

```csharp
await foreach (var evt in agent.RunStreamingAsync(...))
{
    // Level 1: Infrastructure (all filters)
    if (evt is IFilterEvent filterEvt)
    {
        await _filterMonitor.TrackAsync(filterEvt.FilterName);
    }

    // Level 2: Domain (permissions)
    if (evt is IPermissionEvent permEvt)
    {
        await _auditLog.RecordAsync(permEvt);
    }

    // Level 3: Specific (individual events)
    switch (evt)
    {
        case InternalPermissionRequestEvent req:
            await PromptUserAsync(req);
            break;
        case InternalTextDeltaEvent text:
            Console.Write(text.Text);
            break;
    }
}
```

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
// Define your own event type
public record MyCustomEvent(
    string SourceName,
    string CustomData,
    int Count
) : InternalAgentEvent, IBidirectionalEvent;

// Emit it
context.Emit(new MyCustomEvent(
    "MyFilter",
    "value",
    42));
```

### Bidirectional Events (Request/Response)

#### Permission Requests
```csharp
var permissionId = Guid.NewGuid().ToString();

// 1. Emit request
context.Emit(new InternalPermissionRequestEvent(
    permissionId,
    sourceName: "MyPermissionFilter",
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
    private readonly string _filterName;

    public SimplePermissionFilter(string filterName = "SimplePermissionFilter")
    {
        _filterName = filterName;
    }

    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var permissionId = Guid.NewGuid().ToString();

        // Emit request
        context.Emit(new InternalPermissionRequestEvent(
            permissionId,
            _filterName,
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
                context.Emit(new InternalPermissionApprovedEvent(permissionId, _filterName));
                await next(context);
            }
            else
            {
                context.Emit(new InternalPermissionDeniedEvent(permissionId, _filterName, "User denied"));
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
                            permReq.FilterName,
                            approved,
                            approved ? null : "User denied",
                            PermissionChoice.Ask));
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

You can create your own event types and implement the marker interfaces for automatic categorization:

### Custom Filter Events

```csharp
// 1. Define custom event that implements IFilterEvent
public record DatabaseQueryStartEvent(
    string SourceName,
    string QueryId,
    string Query,
    TimeSpan EstimatedDuration) : InternalAgentEvent, IFilterEvent;

// 2. Emit in filter
public class DatabaseFilter : IAiFunctionFilter
{
    private readonly string _filterName = "DatabaseFilter";

    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var queryId = Guid.NewGuid().ToString();

        context.Emit(new DatabaseQueryStartEvent(
            _filterName,
            queryId,
            query: "SELECT * FROM users",
            EstimatedDuration: TimeSpan.FromSeconds(2)));

        await next(context);
    }
}

// 3. Handle in event loop
await foreach (var evt in agent.RunStreamingAsync(...))
{
    // Option A: Handle generically as a filter event
    if (evt is IFilterEvent filterEvt)
    {
        _filterMonitor.Track(filterEvt.FilterName);  // Works automatically!
    }

    // Option B: Handle specifically
    switch (evt)
    {
        case DatabaseQueryStartEvent dbEvt:
            Console.WriteLine($"[{dbEvt.FilterName}] Query starting: {dbEvt.Query}");
            break;
    }
}
```

### Custom Permission Events

```csharp
// Define rich custom permission event
public record EnterprisePermissionRequestEvent(
    string PermissionId,
    string SourceName,
    string FunctionName,
    IDictionary<string, object?>? Arguments,

    // Custom enterprise fields
    decimal EstimatedCost,
    SecurityLevel SecurityLevel,
    string[] RequiredApprovers
) : InternalAgentEvent, IPermissionEvent;  // ← Implements IPermissionEvent

public record EnterprisePermissionResponseEvent(
    string PermissionId,
    string SourceName,
    bool Approved,

    // Custom enterprise fields
    Guid WorkflowInstanceId,
    string[] ApproverChain
) : InternalAgentEvent, IPermissionEvent;

// Use in custom filter
public class EnterprisePermissionFilter : IPermissionFilter
{
    private readonly string _filterName = "EnterprisePermissionFilter";

    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var permissionId = Guid.NewGuid().ToString();

        // Emit rich custom event
        context.Emit(new EnterprisePermissionRequestEvent(
            permissionId,
            _filterName,
            context.ToolCallRequest.FunctionName,
            context.ToolCallRequest.Arguments,
            EstimatedCost: CalculateCost(context),
            SecurityLevel: DetermineSecurityLevel(context),
            RequiredApprovers: new[] { "manager@company.com" }));

        var response = await context.WaitForResponseAsync<EnterprisePermissionResponseEvent>(
            permissionId);

        if (response.Approved)
            await next(context);
        else
            context.IsTerminated = true;
    }
}

// Handle in application
await foreach (var evt in agent.RunStreamingAsync(...))
{
    // Infrastructure: ALL permission events (built-in AND custom)
    if (evt is IPermissionEvent permEvt)
    {
        await _auditLog.RecordAsync(permEvt.PermissionId, permEvt.FilterName, evt);
    }

    // Specific: Handle your custom event
    switch (evt)
    {
        case EnterprisePermissionRequestEvent enterpriseReq:
            // Show rich UI with cost, security level, approvers
            await ShowEnterprisePermissionUI(enterpriseReq);
            break;
    }
}
```

---

## When to Create Custom Permission Filters

### Default Permission Filter: Good for Most Use Cases

The built-in `PermissionFilter` is perfect for 90% of applications:

```csharp
var agent = new AgentBuilder()
    .WithPermissions()  // Uses default PermissionFilter
    .Build();

// Simple binary decisions: Allow or Deny
case InternalPermissionRequestEvent req:
    var approved = Console.ReadLine()?.ToLower() == "y";
    agent.SendFilterResponse(req.PermissionId,
        new InternalPermissionResponseEvent(
            req.PermissionId,
            req.FilterName,
            approved));
```

**Use default when you need:**
- ✅ Simple approve/deny decisions
- ✅ Basic permission storage (always allow, always deny, ask)
- ✅ Console or simple UI prompts
- ✅ Low complexity permission logic

### Custom Permission Filters: For Advanced Scenarios

Create a custom permission filter when you need:

#### **1. Richer Decision States**

The default has binary `Approved` (true/false). You might need:

```csharp
public enum RichDecisionType
{
    Approved,              // Yes, execute as-is
    ApprovedWithChanges,   // Yes, but modify function arguments
    Denied,                // No, don't execute
    Deferred,              // Send to approval workflow, ask later
    RequiresPreview,       // Show preview first, then ask again
    PartiallyApproved      // Approve some operations, deny others
}
```

#### **2. Parameter Modification**

User sees: `DeleteFile("/important/data.txt")`
User wants: "Yes, but move to trash instead of permanent delete"

```csharp
// Custom response event with modified arguments
new RichPermissionResponseEvent(
    permissionId,
    filterName,
    Decision: RichDecisionType.ApprovedWithChanges,
    ModifiedArguments: new Dictionary<string, object?>
    {
        ["filePath"] = "/trash/data.txt",
        ["permanent"] = false  // Changed from true
    });

// Filter applies changes before execution
if (response.Decision == RichDecisionType.ApprovedWithChanges)
{
    foreach (var (key, value) in response.ModifiedArguments ?? new Dictionary<string, object?>())
    {
        context.ToolCallRequest.Arguments[key] = value;
    }
    await next(context);
}
```

#### **3. Multi-Stage Approval Workflows**

```csharp
// Stage 1: Request permission
context.Emit(new WorkflowPermissionRequestEvent(
    permissionId,
    filterName,
    functionName,
    RequiredApprovers: new[] { "manager@company.com" },
    WorkflowType: WorkflowType.ManagerApproval));

// Stage 2: Deferred to workflow
var response = await context.WaitForResponseAsync<WorkflowPermissionResponseEvent>(...);
if (response.Decision == WorkflowDecision.Deferred)
{
    // Workflow engine processes approval
    // User gets notification later when approved
    context.Result = $"Sent to approval workflow {response.WorkflowId}";
    context.IsTerminated = true;
}
```

#### **4. Rich Metadata**

Attach enterprise data to permission events:

```csharp
public record EnterprisePermissionRequestEvent(
    string PermissionId,
    string SourceName,
    string FunctionName,
    IDictionary<string, object?>? Arguments,

    // Enterprise-specific metadata
    decimal EstimatedCost,
    RiskLevel RiskLevel,
    string[] RequiredApprovers,
    ComplianceRequirement[] ComplianceFlags,
    string DepartmentId,
    string ProjectId
) : InternalAgentEvent, IPermissionEvent;
```

#### **5. Cost-Based or Risk-Based Auto-Decisions**

```csharp
public class RiskBasedPermissionFilter : IPermissionFilter
{
    public async Task InvokeAsync(AiFunctionContext context, Func<AiFunctionContext, Task> next)
    {
        var riskScore = CalculateRisk(
            context.ToolCallRequest.FunctionName,
            context.ToolCallRequest.Arguments);

        if (riskScore < 30)
        {
            // Low risk - auto-approve
            await next(context);
        }
        else if (riskScore < 70)
        {
            // Medium risk - ask user
            var approved = await AskUserAsync(context);
            if (approved) await next(context);
        }
        else
        {
            // High risk - auto-deny
            context.Result = "Operation blocked: too risky";
            context.IsTerminated = true;
        }
    }
}
```

### Benefits of `IPermissionEvent` for Custom Filters

When you create custom permission events that implement `IPermissionEvent`, you **automatically benefit** from application infrastructure:

```csharp
// Application handles ALL permission events uniformly
await foreach (var evt in agent.RunStreamingAsync(...))
{
    // Works for built-in AND custom permission events!
    if (evt is IPermissionEvent permEvt)
    {
        // Audit logging
        await _auditLog.RecordAsync(permEvt.PermissionId, permEvt.FilterName, evt);

        // Compliance validation
        await _complianceChecker.ValidateAsync(permEvt);

        // Metrics tracking
        await _metrics.TrackPermissionAsync(permEvt.FilterName);
    }

    // Then handle your specific custom event
    switch (evt)
    {
        case EnterprisePermissionRequestEvent enterpriseReq:
            await ShowEnterpriseUI(enterpriseReq);
            break;
    }
}
```

**No need to duplicate infrastructure for each custom filter!**

### Complete Custom Permission Filter Example

See the [Custom Permission Events](#custom-permission-events) section for a full working example.

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
ß