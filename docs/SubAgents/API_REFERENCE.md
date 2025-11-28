# SubAgent API Reference

Complete API documentation for the SubAgent system in HPD-Agent.

## Table of Contents
- [Core Classes](#core-classes)
- [Factory Methods](#factory-methods)
- [Attributes](#attributes)
- [Thread Modes](#thread-modes)
- [Event Attribution](#event-attribution)
- [Integration Points](#integration-points)

---

## Core Classes

### `SubAgent`

Data holder representing a callable sub-agent.

**Namespace:** `HPD.Agent`

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | AIFunction name shown to parent agent |
| `Description` | `string` | Tool description used by LLM for tool selection |
| `AgentConfig` | `AgentConfig` | Sub-agent configuration (model, instructions, etc.) |
| `ThreadMode` | `SubAgentThreadMode` | Thread handling strategy |
| `SharedThread` | `ConversationThread?` | Shared thread for stateful mode (internal) |
| `PluginTypes` | `Type[]` | Plugin types to register with sub-agent (internal) |

**Example:**
```csharp
var subAgent = SubAgentFactory.Create(
    "MathExpert",
    "Solves mathematical problems",
    new AgentConfig
    {
        Name = "Math Expert",
        SystemInstructions = "You are a mathematician...",
        Provider = new ProviderConfig { /* ... */ }
    });

Console.WriteLine(subAgent.Name);          // "MathExpert"
Console.WriteLine(subAgent.ThreadMode);    // SubAgentThreadMode.Stateless
```

---

## Factory Methods

### `SubAgentFactory.Create`

Creates a **stateless** SubAgent (default).

**Signature:**
```csharp
public static SubAgent Create(
    string name,
    string description,
    AgentConfig config,
    params Type[] pluginTypes)
```

**Parameters:**
- `name` (string, required): AIFunction name. Must be unique and descriptive.
- `description` (string, required): Tool description for LLM. Should clearly explain when to use this SubAgent.
- `config` (AgentConfig, required): Agent configuration including model, instructions, and settings.
- `pluginTypes` (params Type[]): Plugin types to register with this SubAgent.

**Returns:** `SubAgent` configured with `ThreadMode.Stateless`

**Throws:**
- `ArgumentException` if name or description is empty
- `ArgumentNullException` if config is null

**Example:**
```csharp
[SubAgent]
public SubAgent WeatherExpert()
{
    return SubAgentFactory.Create(
        "WeatherExpert",
        "Provides weather forecasts and current conditions",
        new AgentConfig
        {
            Name = "Weather Expert",
            SystemInstructions = "You are a meteorologist...",
            MaxAgenticIterations = 10,
            Provider = new ProviderConfig
            {
                ProviderKey = "openrouter",
                ModelName = "google/gemini-2.0-flash-exp:free"
            }
        },
        typeof(WeatherApiPlugin)  // Only this SubAgent has weather API access
    );
}
```

---

### `SubAgentFactory.CreateStateful`

Creates a **stateful** SubAgent with shared conversation thread.

**Signature:**
```csharp
public static SubAgent CreateStateful(
    string name,
    string description,
    AgentConfig config,
    params Type[] pluginTypes)
```

**Parameters:** Same as `Create()`

**Returns:** `SubAgent` configured with `ThreadMode.SharedThread` and a new `SharedThread`

**Example:**
```csharp
[SubAgent]
public SubAgent TutoringAgent()
{
    return SubAgentFactory.CreateStateful(
        "TutoringAgent",
        "Tutors students in mathematics with memory of previous lessons",
        new AgentConfig
        {
            SystemInstructions = "You are a patient math tutor. Remember previous lessons and build on them.",
            Provider = new ProviderConfig { ModelName = "gpt-4" }
        });
}
```

**Usage:**
```csharp
// First call
await orchestrator.RunAsync("What is 5 + 5?");
// SubAgent responds: "10"

// Second call - SubAgent remembers
await orchestrator.RunAsync("Now multiply that by 2");
// SubAgent responds: "20" (remembers previous answer)
```

---

### `SubAgentFactory.CreatePerSession`

Creates a SubAgent with **user-managed** thread lifecycle.

**Signature:**
```csharp
public static SubAgent CreatePerSession(
    string name,
    string description,
    AgentConfig config,
    params Type[] pluginTypes)
```

**Parameters:** Same as `Create()`

**Returns:** `SubAgent` configured with `ThreadMode.PerSession`

**Example:**
```csharp
[SubAgent]
public SubAgent PersonalAssistant()
{
    return SubAgentFactory.CreatePerSession(
        "PersonalAssistant",
        "Personalized assistant with per-user context",
        new AgentConfig
        {
            SystemInstructions = "You are a personal assistant...",
            Provider = new ProviderConfig { ModelName = "gpt-4" }
        });
}
```

---

## Attributes

### `SubAgentAttribute`

Marks a method as a SubAgent for source generator detection.

**Namespace:** `HPD.Agent`

**Target:** Methods only

**Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Category` | `string?` | null | Optional category for grouping (e.g., "Domain Experts") |
| `Priority` | `int` | 0 | Optional priority for ordering (higher = more prominent) |

**Example:**
```csharp
[SubAgent(Category = "Domain Experts", Priority = 10)]
public SubAgent SeniorArchitect()
{
    return SubAgentFactory.Create(/* ... */);
}

[SubAgent(Category = "Domain Experts", Priority = 5)]
public SubAgent JuniorDeveloper()
{
    return SubAgentFactory.Create(/* ... */);
}

[SubAgent(Category = "Tools")]  // Priority defaults to 0
public SubAgent Calculator()
{
    return SubAgentFactory.Create(/* ... */);
}
```

**Validation:**
- Method must be public
- Method must return `SubAgent`
- Method must have `[SubAgent]` attribute

**Code Generation:**
The source generator creates an AIFunction wrapper for each `[SubAgent]` method at compile-time.

---

## Thread Modes

### `SubAgentThreadMode` (Enum)

Controls how conversation threads are managed across SubAgent invocations.

**Namespace:** `HPD.Agent`

**Values:**

| Value | Description | Use Case |
|-------|-------------|----------|
| `Stateless` | New thread per invocation | Independent queries |
| `SharedThread` | Single shared thread | Multi-turn conversations |
| `PerSession` | User-managed threads | Custom scoping |

**Behavior:**

#### **Stateless**
```csharp
ConversationThread thread;
switch (subAgentDef.ThreadMode)
{
    case SubAgentThreadMode.Stateless:
        thread = new ConversationThread();  // ← Always new
        break;
}
```

**Lifecycle:**
```
Call 1: [Create Thread A] → Process → [Discard Thread A]
Call 2: [Create Thread B] → Process → [Discard Thread B]
Call 3: [Create Thread C] → Process → [Discard Thread C]
```

---

#### **SharedThread**
```csharp
case SubAgentThreadMode.SharedThread:
    thread = subAgentDef.SharedThread ?? new ConversationThread();
    break;
```

**Lifecycle:**
```
Initialize: [Create Shared Thread]
Call 1: [Shared Thread] → Process → [Keep + Add to history]
Call 2: [Shared Thread] → Process → [Keep + Add to history]
Call 3: [Shared Thread] → Process → [Keep + Add to history]
```

⚠️ **Not thread-safe!** Don't call concurrently.

---

#### **PerSession**
```csharp
case SubAgentThreadMode.PerSession:
    thread = subAgentDef.SharedThread ?? new ConversationThread();
    break;
```

**Lifecycle:** Managed externally (advanced usage)

---

## Event Attribution

### `AgentExecutionContext`

Provides hierarchical context about which agent emitted an event.

**Namespace:** `HPD.Agent`

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `AgentName` | `string` | Immediate agent that emitted event (e.g., "WeatherExpert") |
| `AgentId` | `string` | Hierarchical ID showing full execution path |
| `ParentAgentId` | `string?` | Parent agent ID (null if root orchestrator) |
| `AgentChain` | `IReadOnlyList<string>` | Full agent chain from root to current |
| `Depth` | `int` | Hierarchy depth (0 = root, 1 = direct SubAgent, etc.) |
| `IsSubAgent` | `bool` | True if Depth > 0 (computed property) |

**Example:**
```csharp
// Root orchestrator
var rootContext = new AgentExecutionContext
{
    AgentName = "Orchestrator",
    AgentId = "orchestrator-abc123",
    ParentAgentId = null,
    AgentChain = new[] { "Orchestrator" },
    Depth = 0
};

Console.WriteLine(rootContext.IsSubAgent);  // False

// Direct SubAgent
var subContext = new AgentExecutionContext
{
    AgentName = "WeatherExpert",
    AgentId = "orchestrator-abc123-weatherExpert-def456",
    ParentAgentId = "orchestrator-abc123",
    AgentChain = new[] { "Orchestrator", "WeatherExpert" },
    Depth = 1
};

Console.WriteLine(subContext.IsSubAgent);  // True
```

---

### `AgentEvent.ExecutionContext`

All internal agent events now include optional `ExecutionContext` property.

**Property:**
```csharp
public abstract record AgentEvent
{
    public AgentExecutionContext? ExecutionContext { get; init; }
}
```

**Usage:**
```csharp
orchestrator.OnEventAsync(evt =>
{
    if (evt.ExecutionContext != null)
    {
        var who = evt.ExecutionContext.AgentName;
        var depth = evt.ExecutionContext.Depth;
        var chain = string.Join(" → ", evt.ExecutionContext.AgentChain);

        Console.WriteLine($"[{chain}] Depth {depth}: {evt.GetType().Name}");
    }
});
```

**Filtering Examples:**
```csharp
// Filter by agent name
.Where(e => e.ExecutionContext?.AgentName == "WeatherExpert")

// Filter by depth
.Where(e => e.ExecutionContext?.Depth == 1)  // Only direct SubAgents

// Filter by hierarchy
.Where(e => e.ExecutionContext?.AgentChain.Contains("DomainExpert") == true)

// Filter only SubAgent events
.Where(e => e.ExecutionContext?.IsSubAgent == true)
```

---

### Auto-Attachment Behavior

**BidirectionalEventCoordinator** automatically attaches ExecutionContext to events:

```csharp
public void Emit(AgentEvent evt)
{
    // Auto-attach if not already set
    var eventToEmit = evt;
    if (evt.ExecutionContext == null && _owningAgent?.ExecutionContext != null)
    {
        eventToEmit = evt with { ExecutionContext = _owningAgent.ExecutionContext };
    }

    _eventChannel.Writer.TryWrite(eventToEmit);
    _parentCoordinator?.Emit(eventToEmit);  // Bubble with context
}
```

**Rules:**
1. If event already has `ExecutionContext` → **preserved** (not overwritten)
2. If event has no `ExecutionContext` and agent has one → **auto-attached**
3. If neither has context → remains `null`

---

## Integration Points

### AgentBuilder Integration

SubAgents integrate seamlessly with `AgentBuilder`:

```csharp
var orchestrator = new AgentBuilder(new AgentConfig { /* ... */ })
    .WithPlugin<MySubAgents>()      // Register SubAgent plugin
    .WithPlugin<RegularPlugin>()     // Mix with regular plugins
    .Build();
```

**Plugin Discovery:**
Source generator automatically creates AIFunctions for all `[SubAgent]` methods in registered plugins.

---

### Generated AIFunction Metadata

Each SubAgent becomes an AIFunction with additional metadata:

**Standard Properties:**
- `Name`: SubAgent name
- `Description`: SubAgent description

**Additional Properties (in `AdditionalProperties`):**
| Key | Type | Description |
|-----|------|-------------|
| `IsSubAgent` | `bool` | Always `true` for SubAgents |
| `SubAgentCategory` | `string` | Category from attribute (or "Uncategorized") |
| `SubAgentPriority` | `int` | Priority from attribute |
| `ThreadMode` | `string` | Thread mode ("Stateless", "SharedThread", "PerSession") |
| `PluginName` | `string` | Name of plugin containing this SubAgent |

**Example:**
```csharp
AIFunction func = /* generated SubAgent function */;

var isSubAgent = (bool)func.AdditionalProperties["IsSubAgent"];        // true
var category = (string)func.AdditionalProperties["SubAgentCategory"];  // "Domain Experts"
var priority = (int)func.AdditionalProperties["SubAgentPriority"];     // 10
var threadMode = (string)func.AdditionalProperties["ThreadMode"];      // "Stateless"
```

---

### Parent-Child Event Bubbling

**Setup (Generated Code):**
```csharp
// When SubAgent is invoked, generated code:
var currentAgent = AgentCore.RootAgent;
if (currentAgent != null)
{
    // 1. Link event coordinators
    agent.EventCoordinator.SetParent(currentAgent.EventCoordinator);

    // 2. Build execution context
    var parentContext = currentAgent.ExecutionContext;
    agent.ExecutionContext = new AgentExecutionContext
    {
        AgentName = "SubAgentName",
        AgentId = $"{parentContext.AgentId}-subagent-{randomId}",
        ParentAgentId = parentContext?.AgentId,
        AgentChain = new List<string>(parentContext.AgentChain) { "SubAgentName" },
        Depth = (parentContext?.Depth ?? -1) + 1
    };
}
```

**Result:**
Events emitted by SubAgent automatically:
1. Include `ExecutionContext` (auto-attached)
2. Appear in SubAgent's event channel
3. Bubble to parent's event channel
4. Preserve context during bubbling

---

### AsyncLocal Context Flow

**AgentCore provides AsyncLocal storage:**

```csharp
internal sealed class AgentCore
{
    // Current agent in execution chain (flows across async calls)
    public static AgentCore? RootAgent { get; internal set; }

    // Current conversation thread
    public static ConversationThread? CurrentThread { get; internal set; }

    // Current function invocation context
    public static FunctionInvocationContext? CurrentFunctionContext { get; internal set; }
}
```

**Usage in Middleware:**
```csharp
var currentAgent = AgentCore.RootAgent;
var thread = AgentCore.CurrentThread;
var funcContext = AgentCore.CurrentFunctionContext;
```

These flow automatically through nested async SubAgent calls.

---

## Complete Example

Putting it all together:

```csharp
using HPD.Agent;

// 1. Define SubAgents
public class EngineeringTeam
{
    [SubAgent(Category = "Engineering", Priority = 10)]
    public SubAgent SeniorEngineer()
    {
        return SubAgentFactory.Create(
            "SeniorEngineer",
            "Expert software architect for complex design decisions",
            new AgentConfig
            {
                Name = "Senior Engineer",
                SystemInstructions = "You are a senior software architect...",
                MaxAgenticIterations = 20,
                Provider = new ProviderConfig
                {
                    ProviderKey = "openrouter",
                    ModelName = "anthropic/claude-3.5-sonnet"
                }
            },
            typeof(CodeAnalysisPlugin), typeof(DesignPatternsPlugin)
        );
    }

    [SubAgent(Category = "Engineering", Priority = 5)]
    public SubAgent CodeReviewer()
    {
        return SubAgentFactory.CreateStateful(  // Remembers code context
            "CodeReviewer",
            "Reviews code for bugs, style, and best practices",
            new AgentConfig
            {
                Name = "Code Reviewer",
                SystemInstructions = "You review code methodically...",
                MaxAgenticIterations = 15,
                Provider = new ProviderConfig
                {
                    ProviderKey = "openrouter",
                    ModelName = "google/gemini-2.0-flash-exp:free"
                }
            },
            typeof(CodeAnalysisPlugin)
        );
    }
}

// 2. Create orchestrator
var orchestrator = new AgentBuilder(new AgentConfig
{
    Name = "EngineeringManager",
    SystemInstructions = @"
        You coordinate an engineering team.
        Delegate to SeniorEngineer for architecture decisions.
        Delegate to CodeReviewer for code reviews.",
    Provider = new ProviderConfig
    {
        ProviderKey = "openrouter",
        ModelName = "google/gemini-2.0-flash-exp:free"
    }
})
.WithPlugin<EngineeringTeam>()
.Build();

// 3. Subscribe to events with attribution
orchestrator.OnEventAsync(evt =>
{
    if (evt.ExecutionContext != null)
    {
        var agentName = evt.ExecutionContext.AgentName;
        var depth = evt.ExecutionContext.Depth;
        var chain = string.Join(" → ", evt.ExecutionContext.AgentChain);

        Console.WriteLine($"[{chain}] (Depth {depth}) {evt.GetType().Name}");

        // Filter specific SubAgent events
        if (agentName == "SeniorEngineer")
        {
            HandleArchitectureEvent(evt);
        }
    }
});

// 4. Run
var response = await orchestrator.RunAsync(
    "Review this authentication system design and check the implementation");

// Orchestrator will:
// 1. Call SeniorEngineer to review architecture
// 2. Call CodeReviewer to review implementation
// 3. All events bubble up with full ExecutionContext
```

---

## API Summary Table

| Class/Method | Purpose | Key Parameters |
|--------------|---------|----------------|
| `SubAgent` | Data holder for sub-agent definition | Properties: Name, Description, AgentConfig, ThreadMode |
| `SubAgentFactory.Create()` | Create stateless SubAgent | name, description, config, pluginTypes |
| `SubAgentFactory.CreateStateful()` | Create stateful SubAgent | name, description, config, pluginTypes |
| `SubAgentFactory.CreatePerSession()` | Create per-session SubAgent | name, description, config, pluginTypes |
| `[SubAgent]` | Mark method for source generation | Category, Priority |
| `SubAgentThreadMode` | Thread handling mode enum | Stateless, SharedThread, PerSession |
| `AgentExecutionContext` | Event attribution context | AgentName, AgentId, Depth, AgentChain |
| `AgentEvent.ExecutionContext` | Context property on events | ExecutionContext? (nullable) |

---

## Next Steps

- **User Guide**: Learn how to use SubAgents → [USER_GUIDE.md](USER_GUIDE.md)
- **Architecture**: Understand implementation details → [ARCHITECTURE.md](ARCHITECTURE.md)
