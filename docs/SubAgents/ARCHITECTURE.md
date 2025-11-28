# SubAgent Architecture Documentation

Deep dive into the SubAgent architecture, implementation details, and design decisions.

## Table of Contents
- [Overview](#overview)
- [Architecture Diagram](#architecture-diagram)
- [Component Details](#component-details)
- [Source Generation](#source-generation)
- [Runtime Execution](#runtime-execution)
- [Event System](#event-system)
- [Thread Management](#thread-management)
- [Design Decisions](#design-decisions)
- [Performance Considerations](#performance-considerations)

---

## Overview

The SubAgent system enables hierarchical agent composition through a **compile-time source generation** approach combined with **runtime event bubbling** and **execution context tracking**.

### Key Design Principles

1. **Compile-Time Safety**: Source generation validates SubAgent definitions at build time
2. **Zero Reflection**: All code generation happens at compile time for performance
3. **Automatic Attribution**: Events automatically carry execution context
4. **Explicit Parent-Child**: Clear hierarchical relationships via `SetParent()`
5. **AsyncLocal Context Flow**: Context flows through nested async calls automatically

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ COMPILE TIME (Source Generation)                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Developer Code:                                                 │
│  ┌────────────────────────────────────────┐                     │
│  │ [SubAgent(Category = "Experts")]       │                     │
│  │ public SubAgent WeatherExpert()        │                     │
│  │ {                                       │                     │
│  │   return SubAgentFactory.Create(...);  │                     │
│  │ }                                       │                     │
│  └────────────────────────────────────────┘                     │
│                    ↓                                             │
│          SubAgentAnalyzer.cs                                     │
│          (Roslyn Source Analyzer)                                │
│                    ↓                                             │
│    ┌─────────────────────────────────┐                          │
│    │ Extract Metadata:                │                          │
│    │ - Method name                    │                          │
│    │ - SubAgent name                  │                          │
│    │ - Description                    │                          │
│    │ - Category, Priority             │                          │
│    │ - Thread mode                    │                          │
│    └─────────────────────────────────┘                          │
│                    ↓                                             │
│         SubAgentCodeGenerator.cs                                 │
│                    ↓                                             │
│    ┌─────────────────────────────────────────────────┐          │
│    │ Generated AIFunction Wrapper:                    │          │
│    │ - Calls method to get SubAgent def               │          │
│    │ - Creates AgentBuilder from config               │          │
│    │ - Registers plugins                              │          │
│    │ - Builds AgentCore                               │          │
│    │ - Links to parent (SetParent)                    │          │
│    │ - Creates ExecutionContext                       │          │
│    │ - Manages thread lifecycle                       │          │
│    │ - Invokes agent                                  │          │
│    │ - Returns response                               │          │
│    └─────────────────────────────────────────────────┘          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ RUNTIME (Execution)                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  User → Orchestrator.RunAsync("query")                          │
│           ↓                                                      │
│  ┌──────────────────────────────────────┐                       │
│  │ Orchestrator (Root)                  │                       │
│  │ - ExecutionContext (Depth 0)         │                       │
│  │ - AgentCore.RootAgent = this         │                       │
│  └──────────────────────────────────────┘                       │
│           ↓ (calls WeatherExpert)                               │
│  ┌──────────────────────────────────────┐                       │
│  │ Generated Wrapper                    │                       │
│  │ 1. Get SubAgent definition           │                       │
│  │ 2. Build new AgentCore               │                       │
│  │ 3. SetParent(orchestrator)           │ ← Event bubbling      │
│  │ 4. Create ExecutionContext           │ ← Attribution         │
│  │ 5. Get/Create thread                 │ ← Thread mode         │
│  │ 6. Run SubAgent                      │                       │
│  └──────────────────────────────────────┘                       │
│           ↓                                                      │
│  ┌──────────────────────────────────────┐                       │
│  │ WeatherExpert SubAgent               │                       │
│  │ - ExecutionContext (Depth 1)         │                       │
│  │ - EventCoordinator.Parent = orch     │                       │
│  │ - Emits events with context          │                       │
│  └──────────────────────────────────────┘                       │
│           ↓ (events bubble)                                     │
│  ┌──────────────────────────────────────┐                       │
│  │ Orchestrator receives events         │                       │
│  │ - evt.ExecutionContext.AgentName     │ = "WeatherExpert"    │
│  │ - evt.ExecutionContext.Depth         │ = 1                  │
│  │ - evt.ExecutionContext.AgentChain    │ = ["Orch", "Weather"]│
│  └──────────────────────────────────────┘                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Details

### File Structure

```
HPD-Agent/
├── SubAgents/
│   ├── SubAgent.cs                    # Core data class
│   ├── SubAgentAttribute.cs           # [SubAgent] attribute
│   ├── SubAgentFactory.cs             # Factory with validation
│   └── SubAgentThreadMode.cs          # Thread mode enum
├── Agent/
│   ├── AgentCore.cs                   # Core execution engine
│   │   ├── ExecutionContext property
│   │   ├── RootAgent (AsyncLocal)
│   │   ├── BidirectionalEventCoordinator
│   │   └── Event bubbling infrastructure
│   ├── AgentBuilder.cs                # Agent configuration builder
│   └── AgentConfig.cs                 # Agent configuration model

HPD-Agent.SourceGenerator/
└── SourceGeneration/
    ├── SubAgentAnalyzer.cs            # Compile-time analysis
    ├── SubAgentCodeGenerator.cs       # Code generation
    ├── HPDPluginSourceGenerator.cs    # Plugin integration
    └── PluginInfo.cs                  # Metadata structures
```

---

## Source Generation

### Phase 1: Discovery (SubAgentAnalyzer.cs)

**Entry Point:** `AnalyzeSubAgent(MethodDeclarationSyntax method)`

```csharp
public static SubAgentInfo? AnalyzeSubAgent(MethodDeclarationSyntax method)
{
    // 1. Validate method signature
    if (!IsSubAgentMethod(method))
        return null;

    // 2. Extract category and priority from attribute
    var (category, priority) = ExtractAttributeMetadata(method);

    // 3. Parse method body to extract factory call
    var (name, description, threadMode) = ParseFactoryCall(method);

    // 4. Build SubAgentInfo
    return new SubAgentInfo
    {
        MethodName = method.Identifier.Text,
        SubAgentName = name,
        Description = description,
        ThreadMode = threadMode,
        Category = category,
        Priority = priority,
        ClassName = parentClass.Identifier.Text,
        Namespace = GetNamespace(method),
        IsStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
    };
}
```

**Validation Rules:**
1. Method must be `public`
2. Method must return `SubAgent`
3. Method must have `[SubAgent]` attribute
4. Method body must contain `SubAgentFactory.Create*()` call

**Metadata Extraction:**
- **Name**: From first argument to `Create()` → `"WeatherExpert"`
- **Description**: From second argument → `"Weather forecast specialist"`
- **Thread Mode**: Inferred from factory method:
  - `Create()` → `"Stateless"`
  - `CreateStateful()` → `"SharedThread"`
  - `CreatePerSession()` → `"PerSession"`

---

### Phase 2: Code Generation (SubAgentCodeGenerator.cs)

**Entry Point:** `GenerateSubAgentFunction(SubAgentInfo subAgent, string pluginName)`

**Generated Code Structure:**

```csharp
// 1. Lambda function that will be wrapped in AIFunction
async Task<string> InvokeSubAgentAsync(string query, CancellationToken ct)
{
    // 2. Get SubAgent definition from user's method
    var subAgentDef = instance.WeatherExpert();

    // 3. Build agent from config
    var agentBuilder = new AgentBuilder(subAgentDef.AgentConfig);

    // 4. Register plugins if any
    if (subAgentDef.PluginTypes != null && subAgentDef.PluginTypes.Length > 0)
    {
        foreach (var pluginType in subAgentDef.PluginTypes)
            agentBuilder.PluginManager.RegisterPlugin(pluginType);
    }

    var agent = agentBuilder.BuildCoreAgent();

    // 5. SET UP EVENT BUBBLING (Parent-Child Linking)
    var currentAgent = AgentCore.RootAgent;
    if (currentAgent != null)
    {
        agent.EventCoordinator.SetParent(currentAgent.EventCoordinator);
    }

    // 6. BUILD EXECUTION CONTEXT (Event Attribution)
    var parentContext = currentAgent?.ExecutionContext;
    var randomId = Guid.NewGuid().ToString("N")[..8];
    var sanitizedAgentName = Regex.Replace("WeatherExpert", @"[^a-zA-Z0-9]", "_");

    var agentId = parentContext != null
        ? $"{parentContext.AgentId}-{sanitizedAgentName}-{randomId}"
        : $"{sanitizedAgentName}-{randomId}";

    var agentChain = parentContext != null
        ? new List<string>(parentContext.AgentChain) { "WeatherExpert" }
        : new List<string> { "WeatherExpert" };

    agent.ExecutionContext = new AgentExecutionContext
    {
        AgentName = "WeatherExpert",
        AgentId = agentId,
        ParentAgentId = parentContext?.AgentId,
        AgentChain = agentChain,
        Depth = (parentContext?.Depth ?? -1) + 1
    };

    // 7. Handle thread based on mode
    ConversationThread thread;
    switch (subAgentDef.ThreadMode)
    {
        case SubAgentThreadMode.SharedThread:
            thread = subAgentDef.SharedThread ?? new ConversationThread();
            break;
        case SubAgentThreadMode.PerSession:
            thread = subAgentDef.SharedThread ?? new ConversationThread();
            break;
        case SubAgentThreadMode.Stateless:
        default:
            thread = new ConversationThread();
            break;
    }

    // 8. Create user message and run agent
    var message = new ChatMessage(ChatMessageRole.User, query);
    var response = await agent.RunAsync(
        thread,
        new[] { message },
        cancellationToken: ct);

    // 9. Return last assistant message
    return response.Messages
        .LastOrDefault(m => m.Role == ChatMessageRole.Assistant)
        ?.Content ?? string.Empty;
}

// 10. Wrap in AIFunction with metadata
var subAgentFunction = HPDAIFunctionFactory.Create(
    invocation: InvokeSubAgentAsync,
    options: new HPDAIFunctionFactoryOptions
    {
        Name = "WeatherExpert",
        Description = "Weather forecast specialist",
        RequiresPermission = true,
        AdditionalProperties = new Dictionary<string, object>
        {
            ["IsSubAgent"] = true,
            ["SubAgentCategory"] = "Domain Experts",
            ["SubAgentPriority"] = 10,
            ["ThreadMode"] = "Stateless",
            ["PluginName"] = "WeatherPlugin"
        }
    });
```

---

### Phase 3: Plugin Integration (HPDPluginSourceGenerator.cs)

**Discovery:**
```csharp
// Scan for classes with [SubAgent] methods
foreach (var classDeclaration in compilation.SyntaxTrees
    .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()))
{
    if (SubAgentAnalyzer.HasSubAgentMethods(classDeclaration))
    {
        var plugin = AnalyzePlugin(classDeclaration);
        plugin.SubAgents = SubAgentAnalyzer.ExtractSubAgents(classDeclaration);
    }
}
```

**Plugin Types:**
- Pure function plugins (no SubAgents)
- Pure skill plugins (no SubAgents)
- SubAgent-only plugins
- **Mixed plugins** (functions + skills + SubAgents)

**Code Generation:**
All SubAgents from a plugin are added to the plugin's function list alongside regular functions and skills.

---

## Runtime Execution

### Orchestrator Initialization

**Step 1:** Create orchestrator agent

```csharp
var orchestrator = new AgentBuilder(config)
    .WithPlugin<WeatherExperts>()
    .Build();
```

**Step 2:** AgentCore initialization (AgentCore.cs:262)

```csharp
public AgentCore(AgentConfig config, ...)
{
    // ...
    _eventCoordinator = new BidirectionalEventCoordinator(this);  // ← Pass 'this'
    // ...
}
```

**Step 3:** RunAsync starts (AgentCore.cs:518)

```csharp
public async IAsyncEnumerable<AgentResponse> RunAsync(...)
{
    // Track root agent for event bubbling
    var previousRootAgent = RootAgent;
    RootAgent ??= this;  // ← AsyncLocal context

    // Initialize root orchestrator execution context
    if (RootAgent == this && ExecutionContext == null)
    {
        var randomId = Guid.NewGuid().ToString("N")[..8];
        ExecutionContext = new AgentExecutionContext
        {
            AgentName = _name,
            AgentId = $"{_name}-{randomId}",
            ParentAgentId = null,
            AgentChain = new[] { _name },
            Depth = 0
        };
    }

    // ... agent execution ...

    finally
    {
        RootAgent = previousRootAgent;  // Restore
    }
}
```

---

### SubAgent Invocation

**Step 1:** LLM decides to call SubAgent

```json
{
  "tool_calls": [
    {
      "function": {
        "name": "WeatherExpert",
        "arguments": "{\"query\": \"What's the weather in NYC?\"}"
      }
    }
  ]
}
```

**Step 2:** Generated wrapper executes

```csharp
// Remember: Generated code is executing here
var currentAgent = AgentCore.RootAgent;  // ← Gets orchestrator from AsyncLocal
```

**Step 3:** Build SubAgent

```csharp
var agent = agentBuilder.BuildCoreAgent();
```

**Step 4:** Link to parent

```csharp
if (currentAgent != null)
{
    agent.EventCoordinator.SetParent(currentAgent.EventCoordinator);
}
```

**Cycle Detection (BidirectionalEventCoordinator.cs:5722):**
```csharp
public void SetParent(BidirectionalEventCoordinator parent)
{
    // Check for self-reference
    if (parent == this)
        throw new InvalidOperationException("Cannot set coordinator as its own parent");

    // Walk up parent chain to detect cycles
    var current = parent;
    var visited = new HashSet<BidirectionalEventCoordinator> { this };
    while (current != null)
    {
        if (!visited.Add(current))
            throw new InvalidOperationException("Cycle detected in parent coordinator chain");
        current = current._parentCoordinator;
    }

    _parentCoordinator = parent;
}
```

**Step 5:** Create ExecutionContext

```csharp
agent.ExecutionContext = new AgentExecutionContext
{
    AgentName = "WeatherExpert",
    AgentId = "orchestrator-abc123-weatherExpert-def456",  // Hierarchical
    ParentAgentId = "orchestrator-abc123",
    AgentChain = ["Orchestrator", "WeatherExpert"],
    Depth = 1
};
```

**Step 6:** Run SubAgent

```csharp
var response = await agent.RunAsync(thread, messages, cancellationToken);
```

During execution, SubAgent emits events...

---

## Event System

### Event Emission and Auto-Attachment

**When SubAgent emits event (BidirectionalEventCoordinator.cs:5801):**

```csharp
public void Emit(AgentEvent evt)
{
    // Step 1: Auto-attach ExecutionContext if not already set
    var eventToEmit = evt;
    if (evt.ExecutionContext == null && _owningAgent?.ExecutionContext != null)
    {
        eventToEmit = evt with { ExecutionContext = _owningAgent.ExecutionContext };
    }

    // Step 2: Write to local channel (SubAgent's event loop sees it)
    _eventChannel.Writer.TryWrite(eventToEmit);

    // Step 3: Bubble to parent (Orchestrator's event loop sees it)
    _parentCoordinator?.Emit(eventToEmit);  // ← Recursive call
}
```

**Event Flow:**
```
WeatherExpert.Emit(event)
    ↓
[Auto-attach ExecutionContext]
    ↓
[Write to WeatherExpert._eventChannel]
    ↓
[Call parent.Emit(event)] → Orchestrator.Emit(event)
    ↓
[Write to Orchestrator._eventChannel]
```

---

### Event Observer Pattern

**Orchestrator subscribes:**
```csharp
orchestrator.OnEventAsync(async evt =>
{
    // Handler receives event with ExecutionContext
    var who = evt.ExecutionContext?.AgentName;
    var depth = evt.ExecutionContext?.Depth;

    Console.WriteLine($"[{who}] at depth {depth}: {evt}");
});
```

**Background Drainer (AgentCore.cs):**
```csharp
// Continuously reads from _eventChannel
while (await _eventCoordinator.EventReader.WaitToReadAsync(cancellationToken))
{
    while (_eventCoordinator.EventReader.TryRead(out var evt))
    {
        // Dispatch to all observers
        foreach (var observer in _observers)
        {
            await observer.OnEventAsync(evt, cancellationToken);
        }
    }
}
```

**Result:**
- Orchestrator sees events from itself (Depth 0)
- Orchestrator sees events from SubAgents (Depth 1+)
- All events have `ExecutionContext` for attribution

---

## Thread Management

### Three Lifecycle Patterns

#### **1. Stateless**

```csharp
// Every invocation
thread = new ConversationThread();  // Fresh thread
await agent.RunAsync(thread, messages);
// Thread discarded after use
```

**Memory:** None
**Concurrency:** Safe (no shared state)

---

#### **2. SharedThread**

```csharp
// Initialization (SubAgentFactory.CreateStateful)
subAgent.SharedThread = new ConversationThread();

// Every invocation
thread = subAgentDef.SharedThread;  // Same thread
await agent.RunAsync(thread, messages);
// Thread kept, messages accumulate
```

**Memory:** Persists across all invocations
**Concurrency:** **NOT SAFE** - sequential access only

**Memory Growth:**
```
Call 1: [User: "5+5"] [Assistant: "10"]
Call 2: [User: "5+5"] [Assistant: "10"] [User: "×2"] [Assistant: "20"]
Call 3: [User: "5+5"] [Assistant: "10"] [User: "×2"] [Assistant: "20"] [User: "÷4"] [Assistant: "5"]
```

**Mitigation:**
```csharp
new AgentConfig
{
    Memory = new MemoryConfig
    {
        DynamicMemory = new DynamicMemoryConfig
        {
            Enabled = true,
            MaxTokens = 4000  // Auto-summarize when exceeded
        }
    }
}
```

---

#### **3. PerSession**

```csharp
// User provides thread (externally managed)
thread = subAgentDef.SharedThread ?? new ConversationThread();
await agent.RunAsync(thread, messages);
```

**Memory:** User-controlled
**Concurrency:** User's responsibility

---

## Design Decisions

### Why Source Generation vs Reflection?

**Chosen:** Source Generation

**Rationale:**
1. **Performance**: Zero reflection overhead at runtime
2. **Compile-Time Validation**: Errors caught during build, not runtime
3. **Type Safety**: Fully typed generated code
4. **Debuggability**: Generated code is visible and debuggable
5. **AOT Compatibility**: Works with ahead-of-time compilation

**Tradeoff:**
- More complex implementation
- Requires Roslyn knowledge

**Rejected Alternative:** Reflection-based discovery
- Slower runtime performance
- Errors only at runtime
- Harder to optimize

---

### Why AsyncLocal for RootAgent?

**Chosen:** AsyncLocal<AgentCore?>

**Rationale:**
1. **Automatic Flow**: Context flows through nested async calls
2. **No Manual Passing**: Don't need to pass parent through call stack
3. **Isolation**: Each execution chain has its own root
4. **Thread-Safe**: AsyncLocal provides isolation per execution context

**Example:**
```csharp
// User request starts
RootAgent = orchestrator;

// Async call to SubAgent
await SubAgent.RunAsync(...);  // ← RootAgent still = orchestrator

    // Nested async call
    await NestedTool();  // ← RootAgent still = orchestrator
```

**Tradeoff:**
- Slightly harder to understand than explicit passing
- Magic behavior (context flows invisibly)

**Rejected Alternative:** Explicit parent parameter
- Clutters every function signature
- Easy to forget to pass
- Breaks existing APIs

---

### Why Explicit SetParent() vs Automatic?

**Chosen:** Explicit `SetParent()` call in generated code

**Rationale:**
1. **Control**: Clear when parent-child relationship is established
2. **Cycle Detection**: Can validate and prevent cycles
3. **Debugging**: Explicit call visible in generated code
4. **Flexibility**: Can change or remove parent if needed

**Tradeoff:**
- Requires generated code to call it
- Not automatic

**Rejected Alternative:** Automatic parent discovery
- How would SubAgent find its parent? (No clean mechanism)
- Cycle detection becomes harder
- Implicit relationships are harder to reason about

---

### Why Auto-Attach ExecutionContext vs Manual?

**Chosen:** Auto-attach in `Emit()` if missing

**Rationale:**
1. **Convenience**: Middleware/tools don't need to manually attach
2. **Backwards Compatible**: Events without context still work
3. **Correctness**: Hard to forget (automatic)
4. **Flexibility**: Can manually attach if needed (takes precedence)

**Algorithm:**
```csharp
if (evt.ExecutionContext == null && _owningAgent?.ExecutionContext != null)
{
    // Auto-attach
    eventToEmit = evt with { ExecutionContext = _owningAgent.ExecutionContext };
}
```

**Tradeoff:**
- Slightly slower (check + potential copy)
- Magic behavior

**Rejected Alternative:** Manual attachment required
- Easy to forget
- Inconsistent attribution
- Poor developer experience

---

## Performance Considerations

### Source Generation Overhead

**Compile-Time:**
- Adds ~2-3 seconds to full rebuild
- Incremental builds only regenerate changed plugins

**Runtime:**
- **Zero overhead** - all code generated at compile time
- No reflection, no dynamic dispatch

---

### Event Bubbling Performance

**Per Event:**
1. Auto-attach check: O(1) - simple null check
2. Channel write (local): O(1) - unbounded channel
3. Recursive bubble: O(depth) - typically 1-2 levels

**Unbounded Channels:**
```csharp
_eventChannel = Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions
{
    SingleWriter = false,  // Multiple Middlewares can emit concurrently
    SingleReader = true,   // Only background drainer reads
    AllowSynchronousContinuations = false  // Performance & safety
});
```

**Why Unbounded:**
- Prevents blocking during event emission
- Event emission is fire-and-forget
- Background drainer consumes at its own pace

**Memory:** Events kept until consumed (typically milliseconds)

---

### ExecutionContext Allocation

**Per SubAgent Invocation:**
- 1 `AgentExecutionContext` allocation
- 1 `List<string>` for AgentChain
- 1 GUID generation
- 1 string allocation for AgentId

**Optimization:**
- ExecutionContext is a `record` (struct-like semantics)
- `with` expression creates new instance efficiently
- AgentChain is `IReadOnlyList` (no copying on bubble)

**Typical Cost:** ~500 bytes per SubAgent invocation

---

### Thread Lifecycle Cost

| Mode | Per-Invocation Cost | Memory |
|------|---------------------|--------|
| Stateless | Create + GC thread | Low (short-lived) |
| SharedThread | No allocation | High (grows over time) |
| PerSession | User-managed | User-controlled |

**Recommendation:** Use Stateless unless conversation context is required

---

### AsyncLocal Performance

**Cost:**
- Read: ~5-10 ns (inline in JIT)
- Write: ~50-100 ns (allocates new context)

**Usage Pattern:**
```csharp
// Write once at orchestrator start
RootAgent = this;

// Read many times (fast)
var root = RootAgent;
```

**Optimization:** Minimize writes, maximize reads

---

## File References

| Component | File | Lines |
|-----------|------|-------|
| SubAgent Class | HPD-Agent/SubAgents/SubAgent.cs | 1-30 |
| SubAgentFactory | HPD-Agent/SubAgents/SubAgentFactory.cs | 1-60 |
| SubAgentAttribute | HPD-Agent/SubAgents/SubAgentAttribute.cs | 1-20 |
| AgentExecutionContext | HPD-Agent/Agent/AgentCore.cs | 6281-6314 |
| AgentEvent | HPD-Agent/Agent/AgentCore.cs | 6329-6336 |
| AgentCore.ExecutionContext | HPD-Agent/Agent/AgentCore.cs | 209-212 |
| Root Initialization | HPD-Agent/Agent/AgentCore.cs | 527-539 |
| BidirectionalEventCoordinator | HPD-Agent/Agent/AgentCore.cs | 5645-5900 |
| Emit with Auto-Attach | HPD-Agent/Agent/AgentCore.cs | 5801-5820 |
| SetParent with Cycle Detection | HPD-Agent/Agent/AgentCore.cs | 5722-5778 |
| SubAgentAnalyzer | HPD-Agent.SourceGenerator/SourceGeneration/SubAgentAnalyzer.cs | 1-200 |
| SubAgentCodeGenerator | HPD-Agent.SourceGenerator/SourceGeneration/SubAgentCodeGenerator.cs | 1-158 |
| ExecutionContext Generation | HPD-Agent.SourceGenerator/SourceGeneration/SubAgentCodeGenerator.cs | 64-92 |

---

## Testing

### Test Coverage

**Unit Tests (343 total, all passing):**
- `ExecutionContextTests.cs` (19 tests)
  - AgentExecutionContext creation and properties
  - IsSubAgent computation
  - Hierarchical IDs
  - Agent chain building
  - Event filtering by context

- `BidirectionalEventCoordinatorTests.cs` (23 tests)
  - Cycle detection (self, 2-node, 3-node, complex chains)
  - Event bubbling (single level, multi-level, multi-child)
  - ExecutionContext auto-attachment
  - Context preservation during bubbling

- `SubAgentIntegrationTests.cs` (5 tests)
  - Plugin registration
  - AIFunction metadata structure
  - Category and priority handling

- `SubAgentSourceGeneratorTests.cs`
  - Source generation validation
  - Analyzer correctness

---

## Summary

The SubAgent architecture provides a **production-ready hierarchical agent system** with:

✅ **Compile-Time Safety** - Roslyn source generation validates everything at build time
✅ **Zero Runtime Overhead** - No reflection, fully generated code
✅ **Automatic Attribution** - Events carry full execution context
✅ **Explicit Hierarchies** - Clear parent-child relationships via SetParent()
✅ **Flexible Threading** - Three modes for different use cases
✅ **Observable** - Full event bubbling with attribution
✅ **Type Safe** - Fully typed generated code
✅ **Testable** - 343 tests covering all components

The design balances **developer experience** (simple API, automatic context) with **performance** (compile-time generation, minimal allocations) and **correctness** (cycle detection, type safety).

---

## Next Steps

- **User Guide**: Learn how to use SubAgents → [USER_GUIDE.md](USER_GUIDE.md)
- **API Reference**: Detailed API documentation → [API_REFERENCE.md](API_REFERENCE.md)
- **Examples**: Working code examples → `examples/SubAgents/`
