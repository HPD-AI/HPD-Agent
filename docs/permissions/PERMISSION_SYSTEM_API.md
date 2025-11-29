# Permission System API Reference

Complete API reference for the HPD-Agent permission system.

---

## Table of Contents

1. [Attributes](#attributes)
2. [Extension Methods](#extension-methods)
3. [Interfaces](#interfaces)
4. [Events](#events)
5. [Enums](#enums)
6. [Classes](#classes)

---

## Attributes

### RequiresPermissionAttribute

Marks a function as requiring user permission before execution.

**Namespace:** `HPD.Agent.Plugins`

**Usage:**
```csharp
[AIFunction]
[RequiresPermission]
public string DeleteFile(string path) { ... }
```

**Properties:** None

**Remarks:**
- Applied to methods decorated with `[AIFunction]`
- Can be overridden at runtime using `RequirePermissionFor()` or `DisablePermissionFor()`
- Functions without this attribute execute immediately without permission checks

---

## Extension Methods

### AgentBuilder Extensions

#### WithPermissions

Enables the permission system with optional custom storage.

```csharp
public static AgentBuilder WithPermissions(
    this AgentBuilder builder,
    IPermissionStorage? permissionStorage = null)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `builder` | `AgentBuilder` | The agent builder instance |
| `permissionStorage` | `IPermissionStorage?` | Optional custom permission storage. If `null`, uses `InMemoryPermissionStorage` |

**Returns:** `AgentBuilder` for method chaining

**Example:**
```csharp
// With default in-memory storage
var agent = new AgentBuilder()
    .WithPermissions()
    .Build();

// With custom storage
var storage = new DatabasePermissionStorage();
var agent = new AgentBuilder()
    .WithPermissions(storage)
    .Build();
```

---

#### WithAutoApprovePermissions

Enables auto-approve mode where all permissions are automatically granted. Useful for testing and automation.

```csharp
public static AgentBuilder WithAutoApprovePermissions(
    this AgentBuilder builder)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `builder` | `AgentBuilder` | The agent builder instance |

**Returns:** `AgentBuilder` for method chaining

**Example:**
```csharp
var agent = new AgentBuilder()
    .WithAutoApprovePermissions()
    .Build();
```

**Remarks:**
- Should only be used in testing/development
- Bypasses all permission checks
- Not recommended for production use

---

#### RequirePermissionFor

Forces a specific function to require permission, overriding its attribute.

```csharp
public static AgentBuilder RequirePermissionFor(
    this AgentBuilder builder,
    string functionName)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `builder` | `AgentBuilder` | The agent builder instance |
| `functionName` | `string` | Name of the function to require permission for |

**Returns:** `AgentBuilder` for method chaining

**Example:**
```csharp
var agent = new AgentBuilder()
    .WithPlugin<ThirdPartyPlugin>()
    .RequirePermissionFor("DangerousFunction")  // Force permission
    .WithPermissions()
    .Build();
```

**Remarks:**
- Override takes precedence over `[RequiresPermission]` attribute
- Useful for adding permissions to third-party plugins
- Can be used multiple times for different functions

---

#### DisablePermissionFor

Disables permission requirement for a specific function, overriding its attribute.

```csharp
public static AgentBuilder DisablePermissionFor(
    this AgentBuilder builder,
    string functionName)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `builder` | `AgentBuilder` | The agent builder instance |
| `functionName` | `string` | Name of the function to disable permission for |

**Returns:** `AgentBuilder` for method chaining

**Example:**
```csharp
var agent = new AgentBuilder()
    .WithPlugin<FileSystemPlugin>()
    .DisablePermissionFor("ReadFile")  // Remove permission requirement
    .WithPermissions()
    .Build();
```

**Remarks:**
- Override takes precedence over `[RequiresPermission]` attribute
- Useful for trusted functions where prompts are annoying
- Can be used multiple times for different functions

---

#### ClearPermissionOverride

Clears any permission override for a function, restoring attribute-based behavior.

```csharp
public static AgentBuilder ClearPermissionOverride(
    this AgentBuilder builder,
    string functionName)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `builder` | `AgentBuilder` | The agent builder instance |
| `functionName` | `string` | Name of the function to clear override for |

**Returns:** `AgentBuilder` for method chaining

**Example:**
```csharp
var agent = new AgentBuilder()
    .RequirePermissionFor("MyFunction")
    .ClearPermissionOverride("MyFunction")  // Restore attribute behavior
    .Build();
```

---

## Interfaces

### IPermissionStorage

Interface for storing and retrieving permission preferences.

**Namespace:** `HPD.Agent`

```csharp
public interface IPermissionStorage
{
    Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null);

    Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null);
}
```

#### GetStoredPermissionAsync

Retrieves a stored permission preference for a function.

| Parameter | Type | Description |
|-----------|------|-------------|
| `functionName` | `string` | Name of the function |
| `conversationId` | `string?` | Optional conversation ID. If `null`, retrieves global permission |

**Returns:** `Task<PermissionChoice?>` - The stored choice, or `null` if not found

**Remarks:**
- Implement hierarchical lookup: check conversation-scoped first, then global
- Return `null` if no permission is stored

---

#### SavePermissionAsync

Saves a permission preference for a function.

| Parameter | Type | Description |
|-----------|------|-------------|
| `functionName` | `string` | Name of the function |
| `choice` | `PermissionChoice` | The permission choice to store |
| `conversationId` | `string?` | Optional conversation ID. If `null`, saves as global permission |

**Returns:** `Task`

**Remarks:**
- If `conversationId` is provided, permission applies only to that conversation
- If `conversationId` is `null`, permission applies globally to all conversations
- Typically don't store `PermissionChoice.Ask` (transient choice)

---

### IPermissionMiddleware

Interface for custom permission middleware implementations.

**Namespace:** `HPD.Agent.MiddleWare`

```csharp
public interface IPermissionMiddleware
{
    Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next);
}
```

#### InvokeAsync

Called when a function is about to be invoked.

| Parameter | Type | Description |
|-----------|------|-------------|
| `context` | `FunctionInvocationContext` | Context containing function details and state |
| `next` | `Func<FunctionInvocationContext, Task>` | Next middleware in pipeline or actual function execution |

**Returns:** `Task`

**Remarks:**
- Call `await next(context)` to continue to next middleware/function
- Set `context.IsTerminated = true` to block execution
- Set `context.Result` to provide error message to LLM
- Use `context.Emit()` to emit permission events
- Use `await context.WaitForResponseAsync<T>()` to wait for event responses

**Example:**
```csharp
public async Task InvokeAsync(
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, Task> next)
{
    if (context.FunctionName == "DangerousFunction")
    {
        // Check permission...
        if (!approved)
        {
            context.Result = "Permission denied";
            context.IsTerminated = true;
            return;
        }
    }

    await next(context);  // Continue to function
}
```

---

## Events

### PermissionRequestEvent

Emitted when a function requires permission and no stored permission exists.

**Namespace:** `HPD.Agent`

```csharp
public record PermissionRequestEvent(
    string RequestId,
    string MiddlewareName,
    string FunctionName,
    string FunctionDescription,
    string CallId,
    IDictionary<string, object?> Arguments
) : IAgentEvent;
```

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | Unique identifier for this permission request. Must be included in response |
| `MiddlewareName` | `string` | Name of the middleware that emitted this event (typically `"PermissionMiddleware"`) |
| `FunctionName` | `string` | Name of the function requesting permission |
| `FunctionDescription` | `string` | Description of the function (from `[AIFunction(Description = "...")]`) |
| `CallId` | `string` | Unique identifier for this specific function invocation |
| `Arguments` | `IDictionary<string, object?>` | Arguments being passed to the function |

**Usage:**
```csharp
agent.OnEvent<PermissionRequestEvent>(async (e) =>
{
    Console.WriteLine($"Function {e.FunctionName} needs permission");
    Console.WriteLine($"Purpose: {e.FunctionDescription}");

    // Get user approval...
    bool approved = GetUserApproval();

    await agent.RespondToEventAsync(e.RequestId,
        new PermissionResponseEvent(
            e.RequestId,
            approved,
            PermissionChoice.Ask
        ));
});
```

---

### PermissionResponseEvent

Response to a permission request event.

**Namespace:** `HPD.Agent`

```csharp
public record PermissionResponseEvent(
    string RequestId,
    bool Approved,
    PermissionChoice Choice,
    string? Reason = null
) : IAgentEvent;
```

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | Must match the `RequestId` from the permission request |
| `Approved` | `bool` | `true` to allow execution, `false` to deny |
| `Choice` | `PermissionChoice` | Whether to remember this decision (`Ask`, `AlwaysAllow`, `AlwaysDeny`) |
| `Reason` | `string?` | Optional custom denial reason shown to the LLM (only used if `Approved = false`) |

**Usage:**
```csharp
// Allow once
await agent.RespondToEventAsync(requestId,
    new PermissionResponseEvent(
        requestId,
        Approved: true,
        Choice: PermissionChoice.Ask
    ));

// Allow forever for this conversation
await agent.RespondToEventAsync(requestId,
    new PermissionResponseEvent(
        requestId,
        Approved: true,
        Choice: PermissionChoice.AlwaysAllow
    ));

// Deny with custom message
await agent.RespondToEventAsync(requestId,
    new PermissionResponseEvent(
        requestId,
        Approved: false,
        Choice: PermissionChoice.Ask,
        Reason: "Insufficient privileges for this operation"
    ));
```

---

### PermissionApprovedEvent

Emitted when a permission request is approved (for observability).

**Namespace:** `HPD.Agent`

```csharp
public record PermissionApprovedEvent(
    string RequestId,
    string MiddlewareName
) : IAgentEvent;
```

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | The permission request ID that was approved |
| `MiddlewareName` | `string` | Name of the middleware |

**Usage:**
```csharp
agent.OnEvent<PermissionApprovedEvent>((e) =>
{
    logger.LogInformation("Permission approved: {RequestId}", e.RequestId);
});
```

---

### PermissionDeniedEvent

Emitted when a permission request is denied (for observability).

**Namespace:** `HPD.Agent`

```csharp
public record PermissionDeniedEvent(
    string RequestId,
    string MiddlewareName,
    string Reason
) : IAgentEvent;
```

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | The permission request ID that was denied |
| `MiddlewareName` | `string` | Name of the middleware |
| `Reason` | `string` | Reason for denial |

**Usage:**
```csharp
agent.OnEvent<PermissionDeniedEvent>((e) =>
{
    logger.LogWarning("Permission denied: {RequestId}, Reason: {Reason}",
        e.RequestId, e.Reason);
});
```

---

## Enums

### PermissionChoice

Represents the user's preference for how to handle permission requests.

**Namespace:** `HPD.Agent`

```csharp
public enum PermissionChoice
{
    Ask = 0,
    AlwaysAllow = 1,
    AlwaysDeny = 2
}
```

| Value | Description |
|-------|-------------|
| `Ask` | Ask for permission each time (default). Not stored in permission storage |
| `AlwaysAllow` | Always allow this function without asking. Stored for future invocations |
| `AlwaysDeny` | Always deny this function without asking. Stored for future invocations |

**Usage:**
```csharp
// User approves once
var response = new PermissionResponseEvent(
    requestId,
    Approved: true,
    Choice: PermissionChoice.Ask  // Don't remember
);

// User approves forever
var response = new PermissionResponseEvent(
    requestId,
    Approved: true,
    Choice: PermissionChoice.AlwaysAllow  // Remember as allowed
);
```

---

## Classes

### InMemoryPermissionStorage

Default in-memory implementation of `IPermissionStorage` for development and testing.

**Namespace:** `HPD.Agent`

```csharp
public class InMemoryPermissionStorage : IPermissionStorage
```

**Properties:** None (internal concurrent dictionary)

**Remarks:**
- Thread-safe for concurrent access
- Non-persistent (lost on restart)
- Supports both conversation-scoped and global permissions
- Automatically used when `WithPermissions()` is called without parameters

**Example:**
```csharp
// Implicit use
var agent = new AgentBuilder()
    .WithPermissions()  // Uses InMemoryPermissionStorage
    .Build();

// Explicit use
var storage = new InMemoryPermissionStorage();
var agent = new AgentBuilder()
    .WithPermissions(storage)
    .Build();
```

---

### PermissionMiddleware

The core middleware that implements permission checking.

**Namespace:** `HPD.Agent.Internal`

```csharp
internal class PermissionMiddleware : IPermissionMiddleware
```

**Constructor:**
```csharp
public PermissionMiddleware(
    IPermissionStorage? storage = null,
    AgentConfig? config = null,
    string? MiddlewareName = null,
    PermissionOverrideRegistry? overrideRegistry = null)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `storage` | `IPermissionStorage?` | Optional permission storage |
| `config` | `AgentConfig?` | Optional agent configuration |
| `MiddlewareName` | `string?` | Optional middleware name (defaults to `"PermissionMiddleware"`) |
| `overrideRegistry` | `PermissionOverrideRegistry?` | Optional registry for runtime overrides |

**Remarks:**
- Automatically instantiated by `WithPermissions()`
- Implements hierarchical permission lookup (conversation → global → ask)
- Emits permission events when user approval is needed
- Blocks execution if permission is denied

---

### PermissionOverrideRegistry

Internal registry for runtime permission overrides.

**Namespace:** `HPD_Agent.Permissions`

```csharp
internal class PermissionOverrideRegistry
```

**Methods:**

#### RequirePermission
```csharp
public void RequirePermission(string functionName)
```
Forces a function to require permission.

#### DisablePermission
```csharp
public void DisablePermission(string functionName)
```
Forces a function to NOT require permission.

#### GetEffectivePermissionRequirement
```csharp
public bool GetEffectivePermissionRequirement(
    string functionName,
    bool attributeValue)
```
Gets the effective permission requirement (override or attribute).

**Remarks:**
- Used internally by `RequirePermissionFor()` and `DisablePermissionFor()`
- Override takes precedence over `[RequiresPermission]` attribute
- Thread-safe for concurrent access

---

### FunctionInvocationContext

Context object passed to permission middleware containing function execution details.

**Namespace:** `HPD.Agent`

```csharp
public class FunctionInvocationContext
```

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Function` | `AIFunction?` | The function being invoked |
| `FunctionName` | `string` | Name of the function |
| `FunctionDescription` | `string?` | Description of the function |
| `Arguments` | `IDictionary<string, object?>` | Function arguments |
| `Result` | `object?` | Result to return (set by middleware) |
| `IsTerminated` | `bool` | Set to `true` to block execution |
| `State` | `AgentLoopState?` | Current agent state (includes `ConversationId`) |
| `Metadata` | `Dictionary<string, object>` | Extensible metadata dictionary |

**Key Methods:**

#### Emit
```csharp
public void Emit(IAgentEvent event)
```
Emits an event to the agent's event stream.

#### WaitForResponseAsync
```csharp
public async Task<TResponse> WaitForResponseAsync<TResponse>(
    string requestId,
    TimeSpan timeout)
    where TResponse : IAgentEvent
```
Waits for a response event matching the request ID.

**Example:**
```csharp
// Emit permission request
var requestId = Guid.NewGuid().ToString();
context.Emit(new PermissionRequestEvent(...));

// Wait for response
var response = await context.WaitForResponseAsync<PermissionResponseEvent>(
    requestId,
    timeout: TimeSpan.FromMinutes(5)
);

if (!response.Approved)
{
    context.Result = "Permission denied";
    context.IsTerminated = true;
}
```

---

## See Also

- [Permission System Guide](./PERMISSION_SYSTEM_GUIDE.md) - User guide and examples
- [Middleware Events Guide](../MIDDLEWARE_EVENTS_USAGE.md) - Event system documentation
- [Agent Builder API](../API_REFERENCE.md) - Complete agent builder API
