# Runtime Visualization

HPD-Agent provides built-in runtime visualization through the `MermaidVisualizationObserver`. Unlike static graph visualizations that show possible execution paths, this observer captures **what actually happened** during agent execution.

## Quick Start

```csharp
using HPD.Agent;

// Create the visualization observer
var vizObserver = new MermaidVisualizationObserver();

// Add it to your agent
var agent = new AgentBuilder()
    .WithModel(model)
    .WithTools(tools)
    .WithObserver(vizObserver)  // ← Add visualization observer
    .Build();

// Run your agent
await foreach (var evt in agent.RunAsync(messages, thread))
{
    // Process events...
}

// Generate visualizations
Console.WriteLine(vizObserver.GenerateTimeline());
File.WriteAllText("execution-flow.mmd", vizObserver.GenerateMermaid());
```

## Output Formats

### 1. ASCII Timeline

The timeline shows execution events in chronological order with precise timing:

```
Agent Execution Timeline
═══════════════════════════════════════════════════════════
[  0.00s] ▶ Message Turn Started: abc123
[  0.01s]   ├─ Iteration 0/10
[  0.01s]     ├─ Decision: CallLLM
[  0.02s]     ├─ LLM Call (5 messages)
[  1.25s]     ├─ Tools: 2 executed (350ms)
[  1.60s]   ├─ Iteration 1/10
[  1.61s]     ├─ Decision: CallLLM
[  1.62s]     ├─ LLM Call (7 messages)
[  2.45s]     ├─ Tools: 1 executed (120ms)
[  2.57s]   ├─ Iteration 2/10
[  2.57s]     ├─ Decision: Complete
[  2.58s] ✓ Completed: 3 iterations in 2.6s
═══════════════════════════════════════════════════════════
Total: 2.58s, 3 iterations
```

### 2. Mermaid Flowchart

The Mermaid diagram shows the execution flow as a graph (renders in GitHub, VS Code, etc.):

````markdown
```mermaid
graph TD
    START((Message Turn Start))
    ITER_0[Iteration 0<br/>(5 msgs)]
    DEC_0{Decision:<br/>CallLLM}
    LLM_0[LLM Call<br/>5 messages]
    TOOL_0[Tools: 2<br/>350ms]
    ITER_1[Iteration 1<br/>(7 msgs)]
    DEC_1{Decision:<br/>CallLLM}
    LLM_1[LLM Call<br/>7 messages]
    TOOL_1[Tools: 1<br/>120ms]
    ITER_2[Iteration 2<br/>(7 msgs)]
    DEC_2{Decision:<br/>Complete}
    END((Complete<br/>3 iterations<br/>2.6s))

    START --> ITER_0
    ITER_0 --> DEC_0
    DEC_0 --> LLM_0
    LLM_0 --> TOOL_0
    TOOL_0 --> ITER_1
    ITER_1 --> DEC_1
    DEC_1 --> LLM_1
    LLM_1 --> TOOL_1
    TOOL_1 --> ITER_2
    ITER_2 --> DEC_2
    DEC_2 --> END

    classDef error fill:#f96,stroke:#333,stroke-width:2px
    classDef warning fill:#fa3,stroke:#333,stroke-width:2px
    classDef success fill:#6f6,stroke:#333,stroke-width:2px
```
````

## What Gets Visualized

The observer captures:

| Event Type | Visualization |
|------------|---------------|
| **Iterations** | Shows each LLM call iteration with message count |
| **Decisions** | Diamond nodes showing agent decisions (CallLLM, Complete, etc.) |
| **LLM Calls** | Boxes showing messages sent to the LLM |
| **Tool Execution** | Boxes showing tool count, duration, and parallel execution |
| **Circuit Breakers** | Red error nodes when circuit breakers trigger |
| **Permission Denials** | Yellow warning nodes for denied function calls |
| **Completion** | Final completion node with total iterations and duration |

## Use Cases

### 1. Debugging

Quickly see what actually happened during execution:

```csharp
var viz = new MermaidVisualizationObserver();
await agent.RunAsync(messages, observers: [viz]);

// See execution path
Console.WriteLine(viz.GenerateTimeline());
// → "Oh, circuit breaker triggered at iteration 3 after 2 tool calls"
```

### 2. Testing

Verify agent behavior in tests:

```csharp
[Fact]
public async Task Agent_Should_Not_Loop_Infinitely()
{
    var viz = new MermaidVisualizationObserver();

    await agent.RunAsync(messages, observers: [viz]);

    Assert.True(viz.IterationCount < 5,
        $"Expected < 5 iterations, got {viz.IterationCount}:\n{viz.GenerateTimeline()}");
}
```

### 3. Documentation

Generate execution diagrams for documentation:

```csharp
var viz = new MermaidVisualizationObserver();
await agent.RunAsync(messages, observers: [viz]);

// Save diagram to docs
File.WriteAllText("docs/examples/execution-flow.mmd", viz.GenerateMermaid());
```

### 4. Production Monitoring

Export execution traces for analysis:

```csharp
var viz = new MermaidVisualizationObserver();
await agent.RunAsync(messages, observers: [viz]);

// Log execution summary
logger.LogInformation(
    "Agent execution: {Iterations} iterations in {Duration}s\n{Timeline}",
    viz.IterationCount,
    viz.TotalDuration.TotalSeconds,
    viz.GenerateTimeline());
```

## Properties

The observer exposes useful metrics:

```csharp
// Number of iterations executed
int iterationCount = viz.IterationCount;

// Total execution time
TimeSpan duration = viz.TotalDuration;
```

## Comparison to Other Frameworks

| Framework | Static Graphs | Runtime Traces |
|-----------|---------------|----------------|
| **LangGraph** | ✅ Shows possible paths | ✅ Via LangSmith |
| **Semantic Kernel** | ❌ No built-in visualization | ⚠️ Basic logging only |
| **HPD-Agent** | ❌ Runtime-only | ✅ Rich execution traces with timing, errors, parallel execution |

**Trade-off:** HPD-Agent focuses on runtime visualization with rich operational detail (timing, permissions, circuit breakers, parallelism) rather than static design diagrams.

## Advanced: Custom Observers

You can create custom observers for specific needs:

```csharp
/// <summary>
/// Exports execution data to JSON for web visualization
/// </summary>
public class JsonExportObserver : IAgentEventObserver
{
    private readonly List<object> _events = [];

    public Task OnEventAsync(AgentEvent evt, CancellationToken ct = default)
    {
        _events.Add(new
        {
            Type = evt.GetType().Name,
            Timestamp = GetTimestamp(evt),
            Data = evt
        });
        return Task.CompletedTask;
    }

    public string ExportJson()
    {
        return JsonSerializer.Serialize(_events, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
```

## Future Enhancements

Potential additions:

1. **Interactive Web UI** - Real-time visualization with SignalR
2. **Flamegraph View** - Performance profiling visualization
3. **State Inspector** - Browse `AgentLoopState` at each iteration
4. **Export Formats** - JSON, CSV, PlantUML, etc.

## See Also

- [Observer Pattern](../architecture/OBSERVER_PATTERN.md)
- [Telemetry](./TELEMETRY.md)
- [Logging](./LOGGING.md)
