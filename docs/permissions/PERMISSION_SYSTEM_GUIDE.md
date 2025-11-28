# Permission System Guide

The HPD-Agent permission system provides fine-grained control over AI function execution, allowing you to protect sensitive operations and give users explicit control over what the agent can do.

---

## Table of Contents

1. [What is the Permission System?](#what-is-the-permission-system)
2. [Quick Start](#quick-start)
3. [Core Concepts](#core-concepts)
4. [Basic Usage](#basic-usage)
5. [Permission Scopes](#permission-scopes)
6. [Permission Overrides](#permission-overrides)
7. [Event Handling](#event-handling)
8. [Custom Permission Storage](#custom-permission-storage)
9. [Advanced: Custom Permission Middleware](#advanced-custom-permission-middleware)
10. [Best Practices](#best-practices)

---

## What is the Permission System?

The permission system adds a security layer that intercepts AI function calls and asks for user approval before executing sensitive operations. This prevents unauthorized actions like:

- Deleting files
- Making network requests
- Accessing databases
- Modifying system state

**Key Features:**
- ✅ Declarative permission requirements via `[RequiresPermission]` attribute
- ✅ Runtime permission overrides for third-party plugins
- ✅ Event-based architecture (works with any UI: console, web, desktop)
- ✅ Hierarchical permission scopes (conversation-specific or global)
- ✅ Persistent permission storage
- ✅ Auto-approve mode for testing

---

## Quick Start

### Step 1: Mark Functions That Need Permission

```csharp
public class FileSystemPlugin
{
    [AIFunction]
    [RequiresPermission]  // User must approve
    public string DeleteFile(string path)
    {
        File.Delete(path);
        return $"Deleted {path}";
    }

    [AIFunction]  // No permission needed
    public string ReadFile(string path)
    {
        return File.ReadAllText(path);
    }
}
```

### Step 2: Enable Permissions in Agent Builder

```csharp
var agent = new AgentBuilder()
    .WithPlugin<FileSystemPlugin>()
    .WithPermissions()  // Enable permission system
    .Build();
```

### Step 3: Handle Permission Events

```csharp
// Subscribe to permission request events
agent.OnEvent<PermissionRequestEvent>(async (e) =>
{
    // Show UI prompt to user
    Console.WriteLine($"⚠️  Agent wants to call: {e.FunctionName}");
    Console.WriteLine($"   Purpose: {e.FunctionDescription}");
    Console.Write("Allow? (y/n): ");

    var response = Console.ReadLine()?.ToLower() == "y";

    // Send response back
    await agent.RespondToEventAsync(e.RequestId, new PermissionResponseEvent(
        e.RequestId,
        Approved: response,
        Choice: response ? PermissionChoice.AlwaysAllow : PermissionChoice.Ask
    ));
});

// Run agent
await agent.RunAsync("Delete the temp.txt file");
```

---

## Core Concepts

### 1. Permission Requirement

Functions can require permission in two ways:

**Via Attribute** (recommended):
```csharp
[AIFunction]
[RequiresPermission]
public string DeleteDatabase() { ... }
```

**Via Runtime Override** (for third-party plugins):
```csharp
agent.RequirePermissionFor("DeleteDatabase");
```

### 2. Permission Flow

```
┌─────────────────────────────────────────────────────────┐
│ 1. Agent calls DeleteFile()                             │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│ 2. PermissionMiddleware checks:                         │
│    - Does function require permission?                  │
│    - Is there a stored permission (conversation/global)?│
└────────────────────┬────────────────────────────────────┘
                     │
         ┌───────────┴───────────┐
         │                       │
         ▼                       ▼
    [Allowed]              [Need Approval]
         │                       │
         │                       ▼
         │           ┌────────────────────────┐
         │           │ 3. Emit Permission     │
         │           │    Request Event       │
         │           └───────────┬────────────┘
         │                       │
         │                       ▼
         │           ┌────────────────────────┐
         │           │ 4. Your Event Handler  │
         │           │    Shows UI Prompt     │
         │           └───────────┬────────────┘
         │                       │
         │                       ▼
         │           ┌────────────────────────┐
         │           │ 5. User Approves/Denies│
         │           └───────────┬────────────┘
         │                       │
         └───────────┬───────────┘
                     │
                     ▼
         ┌──────────────────────┐
         │ 6. Execute or Block  │
         └──────────────────────┘
```

### 3. Permission Choices

When a user responds to a permission request, they can choose:

| Choice | Description | Scope |
|--------|-------------|-------|
| `Ask` | Ask again next time | One-time only |
| `AlwaysAllow` | Auto-approve future calls | Conversation or Global |
| `AlwaysDeny` | Auto-deny future calls | Conversation or Global |

---

## Basic Usage

### Enable Permissions with Default Storage

```csharp
var agent = new AgentBuilder()
    .WithPermissions()  // Uses in-memory storage
    .Build();
```

### Enable Permissions with Custom Storage

```csharp
var storage = new MyDatabasePermissionStorage();

var agent = new AgentBuilder()
    .WithPermissions(storage)  // Use persistent storage
    .Build();
```

### Auto-Approve Mode (Testing)

```csharp
var agent = new AgentBuilder()
    .WithAutoApprovePermissions()  // All permissions auto-approved
    .Build();
```

---

## Permission Scopes

The permission system supports two scopes:

### 1. Conversation-Scoped (Default)

Permissions are tied to a specific conversation. When the user approves `DeleteFile` in one conversation, it **only** applies to that conversation.

```csharp
// User approves in conversation "conv-123"
await storage.SavePermissionAsync(
    "DeleteFile",
    PermissionChoice.AlwaysAllow,
    conversationId: "conv-123"
);

// Only auto-approved for "conv-123", other conversations still prompt
```

### 2. Global Scope

Permissions apply to **all conversations**. When the user approves globally, they won't be prompted again in any conversation.

```csharp
// User approves globally (null conversationId)
await storage.SavePermissionAsync(
    "DeleteFile",
    PermissionChoice.AlwaysAllow,
    conversationId: null  // Global!
);

// Auto-approved for ALL conversations
```

### Hierarchical Lookup

The system checks permissions in this order:
1. **Conversation-specific** permission (if exists)
2. **Global** permission (fallback)
3. **Ask user** (if neither exists)

This allows users to have a global "always allow" but override it for specific conversations.

---

## Permission Overrides

Sometimes you need to override permission requirements at runtime:

### Force Permission on Third-Party Functions

If a library plugin has a dangerous function without `[RequiresPermission]`:

```csharp
var agent = new AgentBuilder()
    .WithPlugin<ThirdPartyDatabasePlugin>()
    .RequirePermissionFor("DeleteAllData")  // Force permission!
    .WithPermissions()
    .Build();
```

### Disable Permission on Trusted Functions

If you trust a function and don't want constant prompts:

```csharp
var agent = new AgentBuilder()
    .WithPlugin<FileSystemPlugin>()
    .DisablePermissionFor("ReadFile")  // No permission needed
    .WithPermissions()
    .Build();
```

### Clear Override

```csharp
agent.ClearPermissionOverride("ReadFile");  // Restore attribute behavior
```

### Override Multiple Functions

```csharp
var agent = new AgentBuilder()
    .WithPlugin<AdminPlugin>()
    .RequirePermissionFor("DeleteUser")
    .RequirePermissionFor("ModifyRoles")
    .RequirePermissionFor("AccessAuditLogs")
    .WithPermissions()
    .Build();
```

---

## Event Handling

The permission system uses an event-based architecture, making it UI-agnostic.

### Permission Request Event

When permission is needed, the agent emits:

```csharp
public record PermissionRequestEvent(
    string RequestId,           // Unique ID for this request
    string MiddlewareName,      // "PermissionMiddleware"
    string FunctionName,        // e.g., "DeleteFile"
    string FunctionDescription, // Function's description
    string CallId,              // Unique call ID
    IDictionary<string, object?> Arguments  // Function arguments
) : IAgentEvent;
```

### Permission Response Event

Your event handler must respond with:

```csharp
public record PermissionResponseEvent(
    string RequestId,      // Must match RequestId from request
    bool Approved,         // true = allow, false = deny
    PermissionChoice Choice,  // Ask/AlwaysAllow/AlwaysDeny
    string? Reason = null  // Optional denial reason
) : IAgentEvent;
```

### Complete Event Handler Example

```csharp
agent.OnEvent<PermissionRequestEvent>(async (e) =>
{
    // Build UI prompt
    var prompt = $@"
🔐 Permission Request
━━━━━━━━━━━━━━━━━━━━━━
Function: {e.FunctionName}
Purpose:  {e.FunctionDescription}
Arguments: {JsonSerializer.Serialize(e.Arguments)}

Options:
  [A] Allow Once
  [F] Allow Forever (this conversation)
  [G] Allow Globally (all conversations)
  [D] Deny
  [N] Deny Forever

Your choice: ";

    Console.Write(prompt);
    var input = Console.ReadLine()?.ToUpper();

    PermissionChoice choice = input switch
    {
        "A" => PermissionChoice.Ask,
        "F" => PermissionChoice.AlwaysAllow,  // Conversation-scoped
        "G" => PermissionChoice.AlwaysAllow,  // Global (handled below)
        "D" => PermissionChoice.Ask,
        "N" => PermissionChoice.AlwaysDeny,
        _ => PermissionChoice.Ask
    };

    bool approved = input is "A" or "F" or "G";

    // For global approval, save to storage manually
    if (input == "G" && approved)
    {
        await storage.SavePermissionAsync(
            e.FunctionName,
            PermissionChoice.AlwaysAllow,
            conversationId: null  // Global!
        );
    }

    await agent.RespondToEventAsync(e.RequestId,
        new PermissionResponseEvent(
            e.RequestId,
            Approved: approved,
            Choice: choice,
            Reason: approved ? null : "User denied permission"
        ));
});
```

### Observability Events

The system also emits events for monitoring:

```csharp
// Permission was approved
agent.OnEvent<PermissionApprovedEvent>((e) =>
{
    logger.LogInformation("Permission approved: {RequestId}", e.RequestId);
});

// Permission was denied
agent.OnEvent<PermissionDeniedEvent>((e) =>
{
    logger.LogWarning("Permission denied: {RequestId}, Reason: {Reason}",
        e.RequestId, e.Reason);
});
```

---

## Custom Permission Storage

Implement `IPermissionStorage` for persistent storage:

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

### Example: Database Storage

```csharp
public class DatabasePermissionStorage : IPermissionStorage
{
    private readonly DbContext _db;

    public async Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null)
    {
        var query = _db.Permissions
            .Where(p => p.FunctionName == functionName);

        // Hierarchical lookup: conversation → global
        if (!string.IsNullOrEmpty(conversationId))
        {
            var conversationPerm = await query
                .FirstOrDefaultAsync(p => p.ConversationId == conversationId);
            if (conversationPerm != null)
                return conversationPerm.Choice;
        }

        // Fallback to global
        var globalPerm = await query
            .FirstOrDefaultAsync(p => p.ConversationId == null);
        return globalPerm?.Choice;
    }

    public async Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null)
    {
        var permission = new PermissionRecord
        {
            FunctionName = functionName,
            ConversationId = conversationId,
            Choice = choice,
            CreatedAt = DateTime.UtcNow
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
    }
}
```

### Example: File-Based Storage

```csharp
public class FilePermissionStorage : IPermissionStorage
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, PermissionChoice> _cache = new();

    public FilePermissionStorage(string filePath)
    {
        _filePath = filePath;
        LoadFromFile();
    }

    public Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string? conversationId = null)
    {
        var key = BuildKey(functionName, conversationId);
        _cache.TryGetValue(key, out var choice);
        return Task.FromResult((PermissionChoice?)choice);
    }

    public async Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        string? conversationId = null)
    {
        var key = BuildKey(functionName, conversationId);
        _cache[key] = choice;
        await SaveToFileAsync();
    }

    private string BuildKey(string functionName, string? conversationId)
    {
        return string.IsNullOrEmpty(conversationId)
            ? functionName
            : $"{conversationId}:{functionName}";
    }

    private void LoadFromFile()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, PermissionChoice>>(json);
            foreach (var kvp in data ?? new())
            {
                _cache[kvp.Key] = kvp.Value;
            }
        }
    }

    private async Task SaveToFileAsync()
    {
        var json = JsonSerializer.Serialize(_cache);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
```

---

## Advanced: Custom Permission Middleware

For advanced scenarios, you can implement custom permission middleware:

```csharp
public interface IPermissionMiddleware
{
    Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next);
}
```

### Example: Time-Based Permissions

```csharp
public class TimeBasedPermissionMiddleware : IPermissionMiddleware
{
    public async Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var functionName = context.FunctionName;

        // Only allow admin functions during business hours
        if (functionName.StartsWith("Admin") && !IsBusinessHours())
        {
            context.Result = "Admin functions are only available 9 AM - 5 PM EST.";
            context.IsTerminated = true;
            return;
        }

        await next(context);
    }

    private bool IsBusinessHours()
    {
        var now = DateTime.Now;
        return now.Hour >= 9 && now.Hour < 17;
    }
}
```

### Example: Role-Based Permissions

```csharp
public class RoleBasedPermissionMiddleware : IPermissionMiddleware
{
    private readonly IUserContext _userContext;

    public async Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var functionName = context.FunctionName;
        var requiredRole = GetRequiredRole(functionName);

        if (requiredRole != null && !_userContext.HasRole(requiredRole))
        {
            // Emit permission request event
            var requestId = Guid.NewGuid().ToString();
            context.Emit(new PermissionRequestEvent(
                requestId,
                "RoleBasedPermissionMiddleware",
                functionName,
                $"Requires {requiredRole} role",
                context.Metadata["CallId"]?.ToString() ?? "",
                context.Arguments
            ));

            // Wait for admin approval
            var response = await context.WaitForResponseAsync<PermissionResponseEvent>(
                requestId,
                timeout: TimeSpan.FromMinutes(5)
            );

            if (!response.Approved)
            {
                context.Result = $"Permission denied: Requires {requiredRole} role.";
                context.IsTerminated = true;
                return;
            }
        }

        await next(context);
    }

    private string? GetRequiredRole(string functionName)
    {
        return functionName switch
        {
            "DeleteUser" => "Admin",
            "ModifyDatabase" => "DatabaseAdmin",
            _ => null
        };
    }
}
```

### Register Custom Middleware

```csharp
var agent = new AgentBuilder()
    .WithPlugin<AdminPlugin>()
    .WithMiddleware(new TimeBasedPermissionMiddleware())
    .WithMiddleware(new RoleBasedPermissionMiddleware(userContext))
    .WithPermissions()  // Can combine with standard permissions
    .Build();
```

### Event Architecture for Custom Middleware

Custom middleware can use the same event system:

1. **Emit Events**:
```csharp
context.Emit(new MyCustomPermissionEvent(...));
```

2. **Wait for Responses**:
```csharp
var response = await context.WaitForResponseAsync<MyCustomResponseEvent>(
    requestId,
    timeout: TimeSpan.FromMinutes(5)
);
```

3. **Handle Events**:
```csharp
agent.OnEvent<MyCustomPermissionEvent>(async (e) => {
    // Your custom UI logic
    await agent.RespondToEventAsync(e.RequestId, new MyCustomResponseEvent(...));
});
```

---

## Best Practices

### 1. Mark Sensitive Functions

Always use `[RequiresPermission]` on functions that:
- Modify state (delete, update, create)
- Access sensitive data
- Make external API calls
- Cost money

```csharp
[RequiresPermission] public void DeleteFile() { }      // ✅ Good
[RequiresPermission] public void ReadConfig() { }      // ❌ Too restrictive
[RequiresPermission] public void ChargeCard() { }      // ✅ Good
```

### 2. Use Descriptive Function Descriptions

Users see the description in permission prompts:

```csharp
[AIFunction(Description = "Permanently deletes a file from disk")]
[RequiresPermission]
public void DeleteFile(string path) { }
```

### 3. Implement Persistent Storage

In-memory storage loses permissions on restart. Use database or file storage for production:

```csharp
// Development
var agent = new AgentBuilder()
    .WithPermissions()  // In-memory OK
    .Build();

// Production
var storage = new DatabasePermissionStorage(dbContext);
var agent = new AgentBuilder()
    .WithPermissions(storage)  // Persistent
    .Build();
```

### 4. Handle Permission Events Gracefully

Always provide clear UI and handle timeouts:

```csharp
agent.OnEvent<PermissionRequestEvent>(async (e) =>
{
    try
    {
        // Show UI with timeout
        var approved = await ShowPermissionPromptAsync(e, timeout: TimeSpan.FromMinutes(2));

        await agent.RespondToEventAsync(e.RequestId,
            new PermissionResponseEvent(e.RequestId, approved, ...));
    }
    catch (TimeoutException)
    {
        // Auto-deny on timeout
        await agent.RespondToEventAsync(e.RequestId,
            new PermissionResponseEvent(e.RequestId, false, PermissionChoice.Ask));
    }
});
```

### 5. Use Permission Overrides for Third-Party Plugins

Don't trust all library functions:

```csharp
var agent = new AgentBuilder()
    .WithPlugin<CommunityPlugin>()  // Unknown safety
    .RequirePermissionFor("ExecuteShellCommand")  // Force permission
    .WithPermissions()
    .Build();
```

### 6. Test with Auto-Approve Mode

Speed up development:

```csharp
#if DEBUG
var agent = new AgentBuilder()
    .WithAutoApprovePermissions();
#else
var agent = new AgentBuilder()
    .WithPermissions(storage);
#endif
```

### 7. Log Permission Decisions

Track what users approve/deny:

```csharp
agent.OnEvent<PermissionApprovedEvent>((e) =>
{
    logger.LogInformation("User approved: {Function}", e.FunctionName);
});

agent.OnEvent<PermissionDeniedEvent>((e) =>
{
    logger.LogWarning("User denied: {Function}, Reason: {Reason}",
        e.FunctionName, e.Reason);
});
```

---

## Next Steps

- See [Permission System API Reference](./PERMISSION_SYSTEM_API.md) for complete API documentation
- See [Middleware Events Guide](../MIDDLEWARE_EVENTS_USAGE.md) for event system details
- See examples in `/examples/permissions/`
