# Agent Context Extensibility Summary

## What We Have

HPD-Agent already has a comprehensive context system built-in via `Agent.CurrentFunctionContext` using AsyncLocal storage. No additional ConversationContext wrapper is needed.

## The Simple Architecture

```csharp
// Plugin code can directly access:
var ctx = Agent.CurrentFunctionContext;
if (ctx != null)
{
    var conversationId = ctx.RunContext?.ConversationId;
    var agentName = ctx.AgentName;
    var iteration = ctx.Iteration;
    var runContext = ctx.RunContext;  // Full AgentRunContext access
}
```

## What Plugins Get via Agent.CurrentFunctionContext

### Direct Properties
- `Function` - The AIFunction being invoked
- `FunctionName` - Name of the function
- `Arguments` - Function arguments
- `CallId` - Unique identifier for correlation
- `AgentName` - Name of the agent
- `Iteration` - Current iteration number
- `TotalFunctionCallsInRun` - Total function calls so far
- `Metadata` - Extensible metadata dictionary

### Via RunContext
- `RunId` - Unique identifier for this agent run
- `ConversationId` - **For Plan Mode and stateful features**
- `AgentName` - Name of the agent (also on AgentRunContext)
- `CurrentIteration` - Current iteration/function call number
- `MaxIterations` - Maximum allowed function calls
- `StartTime` - When this run started
- `ElapsedTime` - Time elapsed since start
- `CompletedFunctions` - List of function names completed
- `IsTerminated` + `TerminationReason` - Termination state
- `ConsecutiveErrorCount` - Error tracking
- `Metadata` - Additional metadata for the run
- `IsNearTimeout(threshold, maxDuration)` - Helper for timeout detection
- `IsNearIterationLimit(buffer)` - Helper for budget awareness
- `HasReachedMaxIterations` - Check if at limit
- `HasExceededErrorLimit(max)` - Check error threshold

## Example: Plan Mode Plugin

```csharp
public class AgentPlanPlugin
{
    [AIFunction]
    public async Task<string> CreatePlanAsync(string goal, string[] steps)
    {
        // Access conversation ID via Agent.CurrentFunctionContext
        var conversationId = Agent.CurrentFunctionContext?.RunContext?.ConversationId;

        if (string.IsNullOrEmpty(conversationId))
        {
            return "Error: No conversation context available.";
        }

        var plan = await _store.CreatePlanAsync(conversationId, goal, steps);
        return $"Created plan {plan.Id}";
    }
}
```

## Example: Adaptive Plugin

```csharp
public class SearchPlugin
{
    [AIFunction]
    public async Task<string> SearchCodebaseAsync(string pattern)
    {
        var ctx = Agent.CurrentFunctionContext;
        var runContext = ctx?.RunContext;

        // Check how many times we've searched already
        var searchCount = runContext?.CompletedFunctions
            .Count(f => f == "SearchCodebaseAsync") ?? 0;

        if (searchCount >= 3)
        {
            return "Searched 3 times already. Please refine your search pattern.";
        }

        // Check if we're running out of time
        if (runContext?.IsNearTimeout(TimeSpan.FromSeconds(30)) == true)
        {
            return await QuickSearchAsync(pattern);
        }

        return await DeepSearchAsync(pattern);
    }
}
```

## Example: Plugin Coordination via Metadata

```csharp
public class WebSearchPlugin
{
    [AIFunction]
    public async Task<string> SearchWebAsync(string query)
    {
        var results = await _searchService.SearchAsync(query);

        // Store in run metadata for other plugins
        var runContext = Agent.CurrentFunctionContext?.RunContext;
        if (runContext != null)
        {
            runContext.Metadata["lastWebSearchResults"] = results;
            runContext.Metadata["lastWebSearchQuery"] = query;
        }

        return FormatResults(results);
    }
}

public class SummarizePlugin
{
    [AIFunction]
    public Task<string> SummarizeLastSearchAsync()
    {
        var runContext = Agent.CurrentFunctionContext?.RunContext;

        if (runContext?.Metadata.TryGetValue("lastWebSearchResults", out var resultsObj) != true)
        {
            return Task.FromResult("No previous search results available.");
        }

        var results = resultsObj as List<SearchResult>;
        return Task.FromResult(GenerateSummary(results));
    }
}
```

## Example: Self-Terminating Tool

```csharp
public class IntensiveAnalysisPlugin
{
    [AIFunction]
    public async Task<string> DeepAnalysisAsync(string target)
    {
        var runContext = Agent.CurrentFunctionContext?.RunContext;

        var quickResult = await QuickCheckAsync(target);

        // Signal termination to prevent timeout
        if (runContext?.IsNearTimeout(TimeSpan.FromSeconds(45)) == true)
        {
            runContext.IsTerminated = true;
            runContext.TerminationReason = "Stopping early to avoid timeout";
            return $"Quick analysis only: {quickResult}";
        }

        var deepResult = await DeepCheckAsync(target);
        return $"Complete analysis:\nQuick: {quickResult}\nDeep: {deepResult}";
    }
}
```

## Why This is Better

### Before (Proposed ConversationContext wrapper)
- ❌ Extra wrapper class
- ❌ Duplicate properties
- ❌ Additional AsyncLocal storage
- ❌ Circular dependencies
- ❌ More code to maintain

### Now (Using existing infrastructure)
- ✅ Uses existing `Agent.CurrentFunctionContext`
- ✅ Already set for every function call
- ✅ No duplication
- ✅ Single source of truth
- ✅ Less code, simpler architecture

## Architecture Details

### How It Flows

1. **Agent.cs** creates `AgentRunContext` at the start of each turn
2. **Agent.cs** creates `FunctionInvocationContext` for each function call with reference to `AgentRunContext`
3. **Agent.cs** sets `Agent.CurrentFunctionContext` via AsyncLocal before invoking function
4. **Plugin** accesses `Agent.CurrentFunctionContext?.RunContext` to get conversation/run state
5. **Agent.cs** clears `Agent.CurrentFunctionContext` after function completes

### AsyncLocal Flow

```csharp
// In Agent.cs - per function call
Agent.CurrentFunctionContext = new FunctionInvocationContext
{
    Function = function,
    CallId = callId,
    AgentName = this.Name,
    Iteration = iteration,
    RunContext = agentRunContext,  // Reference to turn-level context
    // ...
};

try
{
    // Plugin function executes
    // Can access Agent.CurrentFunctionContext.RunContext.ConversationId
    var result = await function.InvokeAsync(args);
}
finally
{
    Agent.CurrentFunctionContext = null;
}
```

## Best Practices

### ✅ DO:
- Always null-check: `Agent.CurrentFunctionContext?.RunContext?.ConversationId`
- Use `RunContext.Metadata` for plugin coordination with namespaced keys: `"myPlugin.key"`
- Use helper methods: `IsNearTimeout()`, `IsNearIterationLimit()`
- Access `CompletedFunctions` to avoid duplicate work

### ❌ DON'T:
- Don't assume context is always available (it's only set during function execution)
- Don't store large objects in Metadata (use references/IDs)
- Don't modify core properties like `ConversationId`
- Don't use non-namespaced metadata keys

## Testing

```csharp
[Test]
public async Task TestPluginWithContext()
{
    // Arrange
    var agentRunContext = new AgentRunContext(
        runId: "test-run-123",
        conversationId: "test-conv-123",
        maxIterations: 10,
        agentName: "TestAgent"
    );

    Agent.CurrentFunctionContext = new FunctionInvocationContext
    {
        RunContext = agentRunContext,
        AgentName = "TestAgent",
        Iteration = 1
    };

    try
    {
        // Act
        var plugin = new MyPlugin();
        var result = await plugin.MyFunctionAsync("test");

        // Assert
        Assert.That(result, Is.Not.Null);
    }
    finally
    {
        // Cleanup
        Agent.CurrentFunctionContext = null;
    }
}
```

## Key Insight

We don't need ConversationContext because:
1. `Agent.CurrentFunctionContext` is already set via AsyncLocal for every function call
2. It already contains `RunContext` which has everything plugins need
3. Adding another AsyncLocal wrapper just duplicates functionality

**The architecture was already there - we just needed to use it!**

## Summary

HPD-Agent v0 has a clean, simple context architecture:
- **`Agent.CurrentFunctionContext`** - Per-function context (AsyncLocal)
  - **`.RunContext`** - Per-turn/run context with conversation ID, iteration state, metadata, etc.

Plugins access context via `Agent.CurrentFunctionContext.RunContext` - no wrapper needed!
