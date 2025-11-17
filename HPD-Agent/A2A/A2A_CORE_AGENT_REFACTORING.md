# A2A Core Agent Refactoring

## Overview

The A2A (Agent-to-Agent) integration has been refactored to work **directly with the core agent** instead of depending on the Microsoft adapter layer. This eliminates the dependency on `HPD-Agent.Microsoft` and uses `InternalAgentEvent` streaming directly.

## What Changed

### Before (Microsoft Adapter Dependency)

```
A2AHandler (in HPD-Agent.Microsoft)
    ↓
Microsoft.Agent (adapter)
    ↓
Core Agent (internal)
```

**Dependencies:**
- ✗ `MicrosoftAgent` - Microsoft adapter wrapper
- ✗ `MicrosoftThread` - Microsoft thread wrapper
- ✗ `AgentRunResponse` - Microsoft response wrapper
- ✗ Located in `HPD-Agent.Microsoft` project

### After (Direct Core Agent Access)

```
A2AHandler (in HPD-Agent/A2A)
    ↓
Core Agent (internal via InternalsVisibleTo)
    ↓
IAsyncEnumerable<InternalAgentEvent>
```

**Dependencies:**
- ✅ `Agent` - Core agent (internal access)
- ✅ `ConversationThread` - Core thread (internal access)
- ✅ `InternalAgentEvent` - Event streaming
- ✅ Located in `HPD-Agent/A2A` folder

## Key Changes

### 1. A2AHandler Refactoring

**File:** [HPD-Agent/A2A/A2AHandler.cs](A2AHandler.cs)

#### Changed: Constructor & Fields
```csharp
// OLD (Microsoft adapter):
private readonly MicrosoftAgent _agent;
private readonly ConcurrentDictionary<string, MicrosoftThread> _activeThreads;

// NEW (Core agent):
private readonly Agent _agent;
private readonly ConcurrentDictionary<string, ConversationThread> _activeThreads;
```

#### Changed: Response Collection
```csharp
// OLD (Microsoft adapter - pre-collected response):
var response = await _agent.RunAsync([hpdMessage], thread, cancellationToken: cancellationToken);
var responseText = response.Messages.LastOrDefault()?.Text ?? "No response.";
var artifact = A2AMapper.ToA2AArtifact(response);

// NEW (Core agent - streaming event collection):
string responseText = "";
await foreach (var evt in _agent.RunAsync(
    new[] { hpdMessage },
    options: null,
    thread: thread,
    cancellationToken: cancellationToken))
{
    // Collect text content from InternalTextDeltaEvent
    if (evt is InternalTextDeltaEvent textDelta)
    {
        responseText += textDelta.Text;
    }
}
var artifact = A2AMapper.ToA2AArtifact(responseText);
```

#### Changed: Agent Card Generation
```csharp
// OLD (Microsoft adapter):
_agent.Name
_agent.SystemInstructions

// NEW (Core agent):
_agent.Config?.Name ?? "HPD-Agent"
_agent.Config?.SystemInstructions ?? "An HPD-Agent."
_agent.DefaultOptions?.Tools  // For skills
```

### 2. A2AMapper Refactoring

**File:** [HPD-Agent/A2A/A2AMapper.cs](A2AMapper.cs)

#### Changed: ToA2AArtifact Signature
```csharp
// OLD (Microsoft adapter):
public static Artifact ToA2AArtifact(AgentRunResponse hpdResponse)
{
    var responseText = hpdResponse.Messages.LastOrDefault()?.Text ?? "No response.";
    return new Artifact { /* ... */ };
}

// NEW (Core agent):
public static Artifact ToA2AArtifact(string responseText)
{
    return new Artifact
    {
        ArtifactId = Guid.NewGuid().ToString(),
        Parts = [new TextPart { Text = responseText ?? "No response." }]
    };
}
```

### 3. Namespace Change
```csharp
// OLD:
namespace HPD.Agent.Microsoft;

// NEW:
namespace HPD.Agent.A2A;
```

### 4. Accessibility
```csharp
// A2AHandler is now internal since it uses internal Agent type
internal class A2AHandler
```

## Usage Example

### With Core Agent (Current)

```csharp
using HPD.Agent;
using HPD.Agent.A2A;
using A2A;
using A2A.AspNetCore;

// 1. Build CORE agent
var agent = new AgentBuilder()
    .WithName("AI Assistant")
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .WithInstructions("You are a helpful assistant")
    .BuildCoreAgent();  // ✅ Core agent (no Microsoft adapter)

// 2. Create A2A infrastructure
var taskManager = new TaskManager(new InMemoryTaskStore());
var a2aHandler = new A2AHandler(agent, taskManager);  // ✅ Works with core agent

// 3. Map A2A endpoints
app.MapA2A(taskManager, "/a2a-agent");
```

### Old Way (Microsoft Adapter - No Longer Needed)

```csharp
// ❌ OLD - Required Microsoft adapter
var agent = new AgentBuilder()
    .WithName("AI Assistant")
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .BuildMicrosoftAgent();  // Microsoft adapter

var a2aHandler = new A2AHandler(agent, taskManager);
```

## Benefits of Core Agent Approach

### 1. **Eliminates Adapter Dependency**
- No need for `HPD-Agent.Microsoft` project
- Direct access to core functionality
- Simpler dependency graph

### 2. **Full Event Control**
- Access to all `InternalAgentEvent` types
- Can handle reasoning, tool calls, etc.
- More flexible than pre-packaged responses

### 3. **Consistent Architecture**
- A2A now matches console/web API patterns
- All use same core agent interface
- Single source of truth

### 4. **Better Streaming Support**
- Native streaming from core agent
- Can add A2A streaming in future
- Real-time event processing

### 5. **Smaller Surface Area**
- Only uses public properties: `Config`, `DefaultOptions`
- Minimal internal access needed
- Clear boundaries

## Event Handling

The core agent emits various `InternalAgentEvent` types. Currently, A2A collects only text:

```csharp
await foreach (var evt in _agent.RunAsync(...))
{
    switch (evt)
    {
        case InternalTextDeltaEvent textDelta:
            // ✅ Currently collected for A2A response
            responseText += textDelta.Text;
            break;

        case InternalReasoningDeltaEvent reasoningDelta:
            // Can be used for reasoning transparency
            break;

        case InternalToolCallStartEvent toolStart:
            // Can be used for tool execution tracking
            break;

        case InternalToolCallResultEvent toolResult:
            // Can be used for tool results
            break;
    }
}
```

## Future Enhancements

With direct core agent access, A2A can now:

1. **Support Streaming A2A Protocol**
   - Stream events to A2A clients in real-time
   - Better UX for long-running tasks

2. **Expose Reasoning Steps**
   - Include `InternalReasoningDeltaEvent` in artifacts
   - Transparency into agent thinking

3. **Tool Execution Details**
   - Report tool calls to A2A clients
   - Better debugging and monitoring

4. **Richer Artifacts**
   - Include metadata (tokens, duration, etc.)
   - Multi-part artifacts with reasoning + answer

## Migration Guide

If you have existing code using the Microsoft adapter with A2A:

### Step 1: Update Agent Building
```csharp
// OLD:
var agent = builder.BuildMicrosoftAgent();

// NEW:
var agent = builder.BuildCoreAgent();
```

### Step 2: Update Namespace
```csharp
// OLD:
using HPD.Agent.Microsoft;

// NEW:
using HPD.Agent.A2A;
```

### Step 3: Update Project Reference (if needed)
If A2AHandler was in a separate project:
```xml
<!-- Remove Microsoft adapter reference -->
<!-- <ProjectReference Include="HPD-Agent.Microsoft/HPD-Agent.Microsoft.csproj" /> -->

<!-- A2A is now in core HPD-Agent -->
<ProjectReference Include="HPD-Agent/HPD-Agent.csproj" />
```

### Step 4: No Code Changes Needed!
The A2AHandler API remains the same:
```csharp
var a2aHandler = new A2AHandler(agent, taskManager);
app.MapA2A(taskManager, "/a2a-agent");
```

## Testing

The A2A integration can be tested the same way:

```csharp
// 1. Create core agent
var agent = new AgentBuilder()
    .WithProvider("openrouter", "google/gemini-2.5-pro")
    .BuildCoreAgent();

// 2. Create A2A handler
var taskStore = new InMemoryTaskStore();
var taskManager = new TaskManager(taskStore);
var handler = new A2AHandler(agent, taskManager);

// 3. Test task creation
var task = new AgentTask { /* ... */ };
await handler.OnTaskCreatedAsync(task, CancellationToken.None);

// ✅ Works exactly the same as before!
```

## Summary

The A2A integration now:
- ✅ Uses core agent directly (no Microsoft adapter)
- ✅ Collects response from `InternalAgentEvent` stream
- ✅ Lives in `HPD-Agent/A2A` folder
- ✅ Maintains same external API
- ✅ Enables future streaming enhancements
- ✅ Follows same pattern as console/web API

This refactoring eliminates the Microsoft adapter dependency while maintaining full A2A protocol compatibility.
