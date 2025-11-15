# Permission System

## Overview

The Permission System provides **human-in-the-loop approval** for sensitive or destructive operations through an event-driven architecture. Functions marked with `[RequiresPermission]` trigger permission requests before execution, allowing users to approve or deny actions.

**Architecture**: Event-driven, protocol-agnostic design that works with Console, Web, and Desktop UIs through internal agent events.

---

## Quick Start

### 1. Mark Functions Requiring Permission

```csharp
public class FileSystemPlugin
{
    [AIFunction]
    public string ReadFile(string path)
    {
        return File.ReadAllText(path);  // No permission needed
    }

    [AIFunction]
    [RequiresPermission]  // ← User approval required
    [AIDescription("Delete a file from the filesystem")]
    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    [AIFunction]
    [RequiresPermission]  // ← User approval required
    [AIDescription("Write content to a file")]
    public void WriteFile(string path, string content)
    {
        File.WriteAllText(path, content);
    }
}
```

### 2. Enable Permission System

```csharp
var agent = new AgentBuilder()
    .WithPlugin<FileSystemPlugin>()
    .WithPermissions()  // Enable permission handling
    .Build();
```

### 3. Handle Permission Events

The permission system emits events that your application handles:

```csharp
await foreach (var evt in agent.RunAgenticLoopAsync(messages))
{
    switch (evt)
    {
        case InternalPermissionRequestEvent permissionRequest:
            // Show UI prompt to user
            Console.WriteLine($"⚠️  Permission Request:");
            Console.WriteLine($"Function: {permissionRequest.FunctionName}");
            Console.WriteLine($"Arguments: {JsonSerializer.Serialize(permissionRequest.Arguments)}");
            Console.Write("Allow? (y/n): ");

            var approved = Console.ReadLine()?.ToLower() == "y";

            // Send response back to agent
            agent.EmitEvent(new InternalPermissionResponseEvent
            {
                PermissionId = permissionRequest.PermissionId,
                Approved = approved,
                Reason = approved ? null : "User denied permission"
            });
            break;

        case AgentMessageEvent message:
            Console.WriteLine($"Agent: {message.Message}");
            break;
    }
}
```

**That's it!** The agent will now request permission before executing `DeleteFile` or `WriteFile`.

---

## Core Architecture

### Event-Driven Design

The permission system uses **internal agent events** for communication instead of callbacks:

```
┌─────────────────────────────────────────────────────────────┐
│ Agent Loop                                                  │
│                                                             │
│  1. LLM calls DeleteFile("/important.txt")                 │
│  2. PermissionFilter intercepts                             │
│  3. Emit InternalPermissionRequestEvent ────────┐          │
│  4. Wait for response (blocking)                │          │
│                                                 │          │
└─────────────────────────────────────────────────┼──────────┘
                                                  │
                                                  ▼
┌─────────────────────────────────────────────────────────────┐
│ Application Event Loop                                      │
│                                                             │
│  1. Receive InternalPermissionRequestEvent                  │
│  2. Show UI prompt to user                                  │
│  3. Get user decision (approve/deny)                        │
│  4. Emit InternalPermissionResponseEvent ────────┐          │
│                                                  │          │
└──────────────────────────────────────────────────┼──────────┘
                                                   │
                                                   ▼
┌─────────────────────────────────────────────────────────────┐
│ Agent Loop (resumed)                                        │
│                                                             │
│  5. Receive response event                                  │
│  6. If approved → Execute DeleteFile                        │
│     If denied → Skip execution, continue                    │
│  7. Emit observability event                                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Benefits:**
- ✅ **Protocol-agnostic**: Works with any UI (Console, Web, Desktop)
- ✅ **Decoupled**: Permission logic separated from UI implementation
- ✅ **Async-friendly**: Supports async permission workflows
- ✅ **Observable**: All decisions tracked via events

---

## Permission Events

### InternalPermissionRequestEvent

Emitted when a function requires permission:

```csharp
public class InternalPermissionRequestEvent
{
    public string PermissionId { get; set; }       // Unique ID for correlation
    public string FunctionName { get; set; }       // e.g., "DeleteFile"
    public string? Description { get; set; }       // Function description
    public string? CallId { get; set; }            // Tool call ID
    public Dictionary<string, object> Arguments { get; set; }  // Function args
}
```

### InternalPermissionResponseEvent

Your application sends this to approve/deny:

```csharp
public class InternalPermissionResponseEvent
{
    public string PermissionId { get; set; }       // Matches request
    public bool Approved { get; set; }             // User's decision
    public string? Reason { get; set; }            // Optional denial reason
    public PermissionStorage? Storage { get; set; } // Optional: "remember this"
}
```

### Observability Events

```csharp
// Emitted after approval
public class InternalPermissionApprovedEvent
{
    public string FunctionName { get; set; }
    public Dictionary<string, object> Arguments { get; set; }
}

// Emitted after denial
public class InternalPermissionDeniedEvent
{
    public string FunctionName { get; set; }
    public Dictionary<string, object> Arguments { get; set; }
    public string? Reason { get; set; }
}
```

---

## Permission Storage & Scoping

### "Remember This Choice" Feature

Users can save permission preferences to avoid repeated prompts:

```csharp
agent.EmitEvent(new InternalPermissionResponseEvent
{
    PermissionId = permissionRequest.PermissionId,
    Approved = true,
    Storage = new PermissionStorage
    {
        Choice = PermissionChoice.AlwaysAllow,  // Or AlwaysDeny
        Scope = PermissionScope.Conversation     // Or Project, Global
    }
});
```

### Permission Scopes (Hierarchical)

```csharp
public enum PermissionScope
{
    Conversation,  // Only this conversation
    Project,       // All conversations in this project
    Global         // All conversations, all projects
}
```

**Lookup order**: Conversation → Project → Global

```
┌─────────────────────────────────────────────────────────────┐
│ Permission Storage Hierarchy                                │
│                                                             │
│  Global Scope                                               │
│  ├── DeleteFile: AlwaysAllow                                │
│  └── DropDatabase: AlwaysDeny                               │
│                                                             │
│  Project Scope (projectId: "my-app")                        │
│  ├── WriteFile: AlwaysAllow                                 │
│  └── DeleteFile: AlwaysDeny  ← Overrides global             │
│                                                             │
│  Conversation Scope (conversationId: "conv-123")            │
│  └── DeleteFile: AlwaysAllow  ← Overrides project           │
│                                                             │
│  Check order: Conversation first → Project → Global         │
└─────────────────────────────────────────────────────────────┘
```

### Permission Choices

```csharp
public enum PermissionChoice
{
    Ask,          // Prompt every time (default)
    AlwaysAllow,  // Auto-approve
    AlwaysDeny    // Auto-deny
}
```

### IPermissionStorage Interface

Implement custom storage backends:

```csharp
public interface IPermissionStorage
{
    Task<PermissionChoice?> GetStoredPermissionAsync(
        string functionName,
        string conversationId,
        string? projectId);

    Task SavePermissionAsync(
        string functionName,
        PermissionChoice choice,
        PermissionScope scope,
        string conversationId,
        string? projectId);
}
```

**Default implementation**: In-memory storage (non-persistent)

**Production**: Implement persistent storage (database, file system, etc.)

---

## Configuration

### Enable Permissions

```csharp
// With default in-memory storage
var agent = new AgentBuilder()
    .WithPermissions()
    .Build();

// With custom storage
var customStorage = new MyDatabasePermissionStorage();
var agent = new AgentBuilder()
    .WithPermissions(customStorage)
    .Build();
```

### Auto-Approve All (Testing Only)

```csharp
var agent = new AgentBuilder()
    .WithAutoApprovePermissions()  // ⚠️ Skips all permission checks
    .Build();
```

---

## MCP Integration

### Default Behavior

**All MCP tools require permission by default** (configurable per-server):

```json
{
  "servers": [
    {
      "name": "filesystem",
      "command": "npx",
      "arguments": ["-y", "@modelcontextprotocol/server-filesystem"],
      "requiresPermission": true  // Default
    }
  ]
}
```

**Every MCP function** from this server triggers permission requests.

### Disabling Permissions for Safe MCP Servers

```json
{
  "servers": [
    {
      "name": "context7",
      "command": "npx",
      "arguments": ["-y", "@upstash/context7-mcp"],
      "requiresPermission": false  // Read-only docs, no permission needed
    }
  ]
}
```

See [MCP-Integration.md](MCP-Integration.md#permission-control) for detailed MCP permission configuration.

---

## Container Expansion (No Permission)

**Important**: Expanding scoped containers **never** requires permission.

```csharp
// Container expansion function
[Scope("File operations")]
public class FileSystemPlugin { ... }

// Generated container function - NO permission required
public AIFunction ExpandFileSystemPlugin()
{
    RequiresPermission = false;  // ← Container expansion is free
    // Returns list of available functions
}

// Individual functions - Permission checked here
[RequiresPermission]
public void DeleteFile(string path) { ... }
```

**Workflow:**
```
1. Agent calls MCP_filesystem (container) → No permission needed
   Result: "Available functions: read_file, delete_file, ..."

2. Agent calls delete_file → Permission required
   User approves → File deleted
```

This applies to:
- Native C# plugins with `[Scope]`
- MCP servers with `enableScoping: true`
- Skills (always scoped)

---

## Continuation Permissions

The permission system also handles **continuation requests** when approaching max iterations:

```csharp
public class InternalContinuationRequestEvent
{
    public int CurrentIteration { get; set; }
    public int MaxIterations { get; set; }
}

public class InternalContinuationResponseEvent
{
    public bool Approved { get; set; }
}
```

**Configuration:**

```csharp
var config = new AgentConfig
{
    MaxAgenticIterations = 10,        // Request continuation at iteration 10
    ContinuationExtensionAmount = 3   // Add 3 more iterations if approved
};
```

---

## Complete Example: Console UI

```csharp
public class Program
{
    static async Task Main()
    {
        // Setup agent with permissions
        var agent = new AgentBuilder()
            .WithPlugin<FileSystemPlugin>()
            .WithMCP("MCP.json")
            .WithPermissions()
            .Build();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Delete the temp files in /tmp")
        };

        // Run agent and handle events
        await foreach (var evt in agent.RunAgenticLoopAsync(messages))
        {
            switch (evt)
            {
                case InternalPermissionRequestEvent permReq:
                    await HandlePermissionRequest(agent, permReq);
                    break;

                case InternalContinuationRequestEvent contReq:
                    await HandleContinuationRequest(agent, contReq);
                    break;

                case AgentMessageEvent msg:
                    Console.WriteLine($"Agent: {msg.Message}");
                    break;

                case InternalPermissionApprovedEvent approved:
                    Console.WriteLine($"✅ Approved: {approved.FunctionName}");
                    break;

                case InternalPermissionDeniedEvent denied:
                    Console.WriteLine($"❌ Denied: {denied.FunctionName} - {denied.Reason}");
                    break;
            }
        }
    }

    static async Task HandlePermissionRequest(Agent agent, InternalPermissionRequestEvent req)
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("⚠️  PERMISSION REQUEST");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"Function: {req.FunctionName}");
        Console.WriteLine($"Description: {req.Description}");
        Console.WriteLine("Arguments:");

        foreach (var (key, value) in req.Arguments)
        {
            Console.WriteLine($"  • {key}: {value}");
        }

        Console.WriteLine(new string('=', 60));
        Console.Write("Approve? [Y]es / [N]o / [A]lways / Ne[v]er: ");

        var response = Console.ReadLine()?.ToUpper();

        var (approved, storage) = response switch
        {
            "Y" => (true, null),
            "N" => (false, null),
            "A" => (true, new PermissionStorage
            {
                Choice = PermissionChoice.AlwaysAllow,
                Scope = PermissionScope.Conversation
            }),
            "V" => (false, new PermissionStorage
            {
                Choice = PermissionChoice.AlwaysDeny,
                Scope = PermissionScope.Conversation
            }),
            _ => (false, null)
        };

        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = approved,
            Reason = approved ? null : "User denied permission",
            Storage = storage
        });
    }

    static async Task HandleContinuationRequest(Agent agent, InternalContinuationRequestEvent req)
    {
        Console.WriteLine($"\n⚠️  Agent approaching max iterations ({req.CurrentIteration}/{req.MaxIterations})");
        Console.Write("Continue? (y/n): ");

        var approved = Console.ReadLine()?.ToLower() == "y";

        agent.EmitEvent(new InternalContinuationResponseEvent
        {
            Approved = approved
        });
    }
}
```

---

## Advanced Patterns

### Risk-Based Approval UI

```csharp
static async Task HandlePermissionRequest(Agent agent, InternalPermissionRequestEvent req)
{
    var risk = CalculateRisk(req.FunctionName, req.Arguments);

    var (icon, color) = risk switch
    {
        RiskLevel.Low => ("ℹ️", ConsoleColor.Green),
        RiskLevel.Medium => ("⚠️", ConsoleColor.Yellow),
        RiskLevel.High => ("🚨", ConsoleColor.Red),
        _ => ("❓", ConsoleColor.White)
    };

    Console.ForegroundColor = color;
    Console.WriteLine($"{icon} {risk} Risk: {req.FunctionName}");
    Console.ResetColor();

    // Auto-approve low risk, always prompt for high risk
    if (risk == RiskLevel.Low)
    {
        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = true
        });
        return;
    }

    // Show detailed prompt for medium/high risk
    // ... rest of approval logic
}

enum RiskLevel { Low, Medium, High }

RiskLevel CalculateRisk(string functionName, Dictionary<string, object> args)
{
    if (functionName.Contains("Delete") || functionName.Contains("Drop"))
        return RiskLevel.High;

    if (functionName.Contains("Write") || functionName.Contains("Update"))
        return RiskLevel.Medium;

    return RiskLevel.Low;
}
```

### Audit Trail

```csharp
static async Task HandlePermissionRequest(Agent agent, InternalPermissionRequestEvent req)
{
    var approved = await PromptUser(req);

    // Log the decision
    await _auditLog.LogAsync(new AuditEntry
    {
        Timestamp = DateTime.UtcNow,
        FunctionName = req.FunctionName,
        Arguments = req.Arguments,
        Approved = approved,
        UserId = _currentUser.Id,
        ConversationId = _conversationId
    });

    agent.EmitEvent(new InternalPermissionResponseEvent
    {
        PermissionId = req.PermissionId,
        Approved = approved
    });
}
```

### Timeout Handling

The permission filter has a built-in 5-minute timeout. Handle timeouts gracefully:

```csharp
static async Task HandlePermissionRequest(Agent agent, InternalPermissionRequestEvent req)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

    try
    {
        var approved = await PromptUserWithCancellation(req, cts.Token);

        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = approved
        });
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("⏱️ Permission request timed out - denying by default");

        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = false,
            Reason = "User did not respond within timeout period"
        });
    }
}
```

---

## Testing

### MockPermissionHandler

For testing, use the built-in mock handler:

```csharp
var agent = CreateAgentWithPermissions();
var eventStream = agent.RunAgenticLoopAsync(messages);

using var permissionHandler = new MockPermissionHandler(agent, eventStream)
    .AutoApproveAll();

await permissionHandler.WaitForCompletionAsync(TimeSpan.FromSeconds(10));

// Assert agent behavior
```

### Auto-Deny Pattern

```csharp
using var permissionHandler = new MockPermissionHandler(agent, eventStream)
    .AutoDenyAll("Permission denied for testing");

await permissionHandler.WaitForCompletionAsync(TimeSpan.FromSeconds(10));

// Assert agent handles denial gracefully
```

### Custom Test Logic

```csharp
var agent = new AgentBuilder()
    .WithPlugin<MyPlugin>()
    .WithPermissions()
    .Build();

var eventStream = agent.RunAgenticLoopAsync(messages);

await foreach (var evt in eventStream)
{
    if (evt is InternalPermissionRequestEvent permReq)
    {
        // Custom test logic
        var shouldApprove = permReq.FunctionName != "DangerousFunction";

        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = permReq.PermissionId,
            Approved = shouldApprove
        });
    }
}
```

---

## Best Practices

### ✅ DO: Use Permissions for Destructive Operations

```csharp
[RequiresPermission]
public void DeleteFile(string path) { ... }

[RequiresPermission]
public void DropTable(string tableName) { ... }

[RequiresPermission]
public async Task SendEmail(string to, string subject) { ... }
```

### ✅ DO: Provide Clear Descriptions

```csharp
[RequiresPermission]
[AIDescription("Delete a file from the filesystem. This action cannot be undone.")]
public void DeleteFile(string path) { ... }
```

### ✅ DO: Implement Defense in Depth

```csharp
[RequiresPermission]
public void DeleteFile(string path)
{
    // Layer 1: Permission system (user approval)
    // Layer 2: Path validation
    if (!IsPathSafe(path))
        throw new SecurityException("Invalid path");

    // Layer 3: File system permissions (OS level)
    File.Delete(path);
}
```

### ✅ DO: Use Scoped Storage for Workflows

```csharp
// Allow repeated operations in the same conversation
Storage = new PermissionStorage
{
    Choice = PermissionChoice.AlwaysAllow,
    Scope = PermissionScope.Conversation  // Only this conversation
}
```

### ❌ DON'T: Use Permissions for Read-Only Operations

```csharp
// ❌ Bad - ReadFile doesn't need permission
[RequiresPermission]
public string ReadFile(string path) { ... }

// ✅ Good - Only destructive operations need permission
public string ReadFile(string path) { ... }

[RequiresPermission]
public void WriteFile(string path, string content) { ... }
```

### ❌ DON'T: Auto-Approve Everything in Production

```csharp
// ❌ Bad - defeats the purpose
var agent = new AgentBuilder()
    .WithAutoApprovePermissions()  // Dangerous in production!
    .Build();

// ✅ Good - proper permission handling
var agent = new AgentBuilder()
    .WithPermissions(productionStorage)
    .Build();
```

### ❌ DON'T: Ignore Permission Events

```csharp
// ❌ Bad - permission requests will timeout
await foreach (var evt in agent.RunAgenticLoopAsync(messages))
{
    if (evt is AgentMessageEvent msg)
        Console.WriteLine(msg.Message);
    // Missing: InternalPermissionRequestEvent handling!
}

// ✅ Good - handle all event types
await foreach (var evt in agent.RunAgenticLoopAsync(messages))
{
    switch (evt)
    {
        case InternalPermissionRequestEvent permReq:
            await HandlePermissionRequest(agent, permReq);
            break;
        case AgentMessageEvent msg:
            Console.WriteLine(msg.Message);
            break;
    }
}
```

---

## Security Considerations

### 1. Validate Permission Decisions

```csharp
// Validate user is authorized to approve high-risk operations
if (CalculateRisk(req.FunctionName, req.Arguments) == RiskLevel.High)
{
    if (!await _authService.UserHasPermission(_currentUser, "approve-high-risk"))
    {
        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = false,
            Reason = "User not authorized to approve high-risk operations"
        });
        return;
    }
}
```

### 2. Rate Limiting

```csharp
private readonly Dictionary<string, List<DateTime>> _requestHistory = new();

static async Task HandlePermissionRequest(Agent agent, InternalPermissionRequestEvent req)
{
    var key = $"{_currentUser.Id}:{req.FunctionName}";

    // Track requests in last hour
    var recentRequests = _requestHistory.GetValueOrDefault(key, new())
        .Where(t => t > DateTime.UtcNow.AddHours(-1))
        .ToList();

    if (recentRequests.Count >= 10)
    {
        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = req.PermissionId,
            Approved = false,
            Reason = "Rate limit exceeded (10 requests per hour)"
        });
        return;
    }

    recentRequests.Add(DateTime.UtcNow);
    _requestHistory[key] = recentRequests;

    // Continue with normal approval flow
}
```

### 3. Audit All Decisions

Always log permission requests and decisions for security auditing and compliance.

---

## Troubleshooting

### Permission Requests Timing Out

**Problem**: Permission requests timeout after 5 minutes.

**Solution**: Ensure you're handling `InternalPermissionRequestEvent` and sending `InternalPermissionResponseEvent`:

```csharp
await foreach (var evt in agent.RunAgenticLoopAsync(messages))
{
    if (evt is InternalPermissionRequestEvent permReq)
    {
        // MUST send response
        agent.EmitEvent(new InternalPermissionResponseEvent
        {
            PermissionId = permReq.PermissionId,
            Approved = /* user decision */
        });
    }
}
```

### Functions Not Requiring Permission

**Problem**: Functions marked with `[RequiresPermission]` execute without prompts.

**Solutions:**

1. **Verify permission system is enabled:**
   ```csharp
   var agent = new AgentBuilder()
       .WithPermissions()  // ← Must call this
       .Build();
   ```

2. **Check for auto-approve:**
   ```csharp
   // Remove this in production:
   .WithAutoApprovePermissions()
   ```

3. **Verify attribute is applied:**
   ```csharp
   [RequiresPermission]  // ← Must be present
   [AIFunction]
   public void DeleteFile(string path) { ... }
   ```

### MCP Tools Not Respecting Permissions

**Problem**: MCP tools bypass permission checks.

**Solution**: Check `requiresPermission` setting in `MCP.json`:

```json
{
  "servers": [
    {
      "name": "filesystem",
      "requiresPermission": true  // ← Must be true (or omit for default)
    }
  ]
}
```

### Stored Permissions Not Working

**Problem**: Stored permissions not being applied.

**Solutions:**

1. **Verify storage is passed to agent:**
   ```csharp
   var storage = new MyPermissionStorage();
   var agent = new AgentBuilder()
       .WithPermissions(storage)  // ← Pass custom storage
       .Build();
   ```

2. **Check storage implementation:**
   - `GetStoredPermissionAsync` must check all scopes (Conversation → Project → Global)
   - `SavePermissionAsync` must persist the choice

---

## Summary

- **`[RequiresPermission]` attribute** marks functions requiring approval
- **Event-driven architecture** enables protocol-agnostic UI integration
- **Permission storage** supports "remember this choice" with hierarchical scoping
- **MCP integration** requires permission by default (configurable per-server)
- **Container expansion** never requires permission (scoped plugins, MCP, skills)
- **Continuation requests** handled through same event system
- **Testing helpers** available via `MockPermissionHandler` and `WithAutoApprovePermissions()`
- **Security**: Implement defense in depth, audit trails, and rate limiting

For MCP-specific permission configuration, see [MCP-Integration.md](MCP-Integration.md#permission-control).
